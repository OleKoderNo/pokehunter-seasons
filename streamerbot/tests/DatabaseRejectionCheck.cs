using System;
using System.Data.SQLite;
using System.IO;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class DatabaseRejectionCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        // Each execution owns a unique temporary directory.
        // No existing development or live database is used.
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "PokeHunter-DatabaseRejection-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            Directory.CreateDirectory(temporaryDirectory);

            CheckNewerVersion(temporaryDirectory);
            CheckUnrelatedDatabase(temporaryDirectory);

            CPH.LogInfo(
                "[PokéHunter] All database rejection checks passed."
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[PokéHunter] Database rejection check failed: " +
                exception
            );

            return false;
        }
        finally
        {
            // Only delete this execution's temporary directory.
            // Cleanup errors should not hide the actual test result.
            try
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, true);
                }
            }
            catch (Exception exception)
            {
                CPH.LogWarn(
                    "[PokéHunter] Could not remove temporary test folder '" +
                    temporaryDirectory + "': " + exception.Message
                );
            }
        }
    }

    /// <summary>
    /// Simulates a schema version newer than this build supports.
    /// </summary>
    private void CheckNewerVersion(string temporaryDirectory)
    {
        string databasePath = Path.Combine(
            temporaryDirectory,
            "newer-version.db"
        );

        DatabaseInitializer.Initialize(databasePath);

        // Stay one version ahead of the initializer.
        int futureVersion = DatabaseInitializer.CurrentSchemaVersion + 1;

        string versionText = futureVersion.ToString(
            System.Globalization.CultureInfo.InvariantCulture
        );

        ExecuteFixtureSql(
            databasePath,
            "PRAGMA user_version = " + versionText + ";"
        );

        ExpectRejectedWithoutChanges(
            databasePath,
            "Unsupported database schema version " + versionText
        );

        CPH.LogInfo(
            "[PokéHunter] PASS: A newer database version was rejected " +
            "without changing its file."
        );
    }
    /// <summary>
    /// Creates an ordinary SQLite database with unrelated content.
    /// A zero schema version alone must not make it eligible for migration.
    /// </summary>
    private void CheckUnrelatedDatabase(string temporaryDirectory)
    {
        string databasePath = Path.Combine(
            temporaryDirectory,
            "unrelated.db"
        );

        ExecuteFixtureSql(
            databasePath,
            @"CREATE TABLE unrelated_notes (
                id INTEGER PRIMARY KEY,
                message TEXT NOT NULL
            );"
        );

        ExecuteFixtureSql(
            databasePath,
            @"INSERT INTO unrelated_notes (id, message)
              VALUES (1, 'Keep this existing content.');"
        );

        ExpectRejectedWithoutChanges(
            databasePath,
            "not empty or is marked for another application"
        );

        CPH.LogInfo(
            "[PokéHunter] PASS: An unrelated database was rejected " +
            "without changing its file."
        );
    }

    /// <summary>
    /// Requires the expected rejection and verifies that the database file
    /// remains byte-for-byte identical after the connection has closed.
    /// </summary>
    private static void ExpectRejectedWithoutChanges(
        string databasePath,
        string expectedMessage
    )
    {
        // These are small, isolated fixtures with no other connections.
        // Reading their complete files is appropriate for this test.
        byte[] before = File.ReadAllBytes(databasePath);
        bool rejected = false;

        try
        {
            DatabaseInitializer.Initialize(databasePath);
        }
        catch (InvalidDataException exception)
        {
            if (exception.Message.IndexOf(
                expectedMessage,
                StringComparison.OrdinalIgnoreCase
            ) < 0)
            {
                throw new InvalidOperationException(
                    "The database was rejected for an unexpected reason: " +
                    exception.Message,
                    exception
                );
            }

            rejected = true;
        }

        if (!rejected)
        {
            throw new InvalidOperationException(
                "The initializer accepted a database it should reject."
            );
        }

        byte[] after = File.ReadAllBytes(databasePath);

        if (before.Length != after.Length)
        {
            throw new InvalidOperationException(
                "The rejected database file changed size."
            );
        }

        for (int index = 0; index < before.Length; index++)
        {
            if (before[index] != after[index])
            {
                throw new InvalidOperationException(
                    "The rejected database file changed at byte " +
                    index + "."
                );
            }
        }
    }

    /// <summary>
    /// Creates or changes a temporary fixture using fixed test SQL.
    /// This helper is not part of production database initialization.
    /// </summary>
    private static void ExecuteFixtureSql(
        string databasePath,
        string sql
    )
    {
        var connectionString = new SQLiteConnectionStringBuilder
        {
            DataSource = databasePath,
            Version = 3,
            Pooling = false,
            DefaultTimeout = 5
        };

        using (var connection = new SQLiteConnection(
            connectionString.ConnectionString
        ))
        {
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }
    }
}