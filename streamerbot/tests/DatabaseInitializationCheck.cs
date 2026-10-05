using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class DatabaseInitializationCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string ProjectFolder =
        @"C:\Users\ohfb9\Documents\Coding\Private\Streaming\pokehunter-seasons";

#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "PokeHunter-Initialization-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            string databasePath = Path.Combine(
                ProjectFolder,
                "runtime",
                "database-initialization-test.db"
            );

            bool existedBefore = File.Exists(databasePath);

            // Read history before initialization so an upgrade must preserve
            // records written by the previous application version.
            List<string> originalHistory = existedBefore
                ? ReadHistory(databasePath)
                : new List<string>();

            int version = DatabaseInitializer.Initialize(databasePath);

            Require(
                version == 2,
                "Expected schema version 2."
            );

            List<string> installedHistory = ReadHistory(databasePath);

            Require(
                installedHistory.Count == 2,
                "Expected exactly two migration records."
            );

            RequirePreserved(originalHistory, installedHistory);
            CheckTrainerColumns(databasePath);

            // Repeating initialization must preserve both migration records.
            int repeatedVersion = DatabaseInitializer.Initialize(databasePath);
            List<string> repeatedHistory = ReadHistory(databasePath);

            Require(
                repeatedVersion == version &&
                repeatedHistory.Count == installedHistory.Count,
                "Repeated initialization changed the version or history count."
            );

            RequirePreserved(installedHistory, repeatedHistory);

            CPH.LogInfo(
                "[PokéHunter] PASS: Existing migration records were preserved."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Repeated initialization preserved " +
                "both migration records."
            );

            // A fresh database must run the complete migration sequence.
            Directory.CreateDirectory(temporaryDirectory);

            string freshPath = Path.Combine(
                temporaryDirectory,
                "fresh.db"
            );

            Require(
                DatabaseInitializer.Initialize(freshPath) == 2,
                "A fresh database did not reach version 2."
            );

            Require(
                ReadHistory(freshPath).Count == 2,
                "A fresh database did not record both migrations."
            );

            CheckTrainerColumns(freshPath);

            CPH.LogInfo(
                "[PokéHunter] PASS: A fresh database reached version 2 " +
                "with the trainer table."
            );

            CPH.LogInfo(
                "[PokéHunter] Database existed before this check: " +
                existedBefore +
                " | Previous migration count: " + originalHistory.Count
            );

            CPH.LogInfo(
                "[PokéHunter] Installed schema version: " + version
            );

            CPH.LogInfo(
                "[PokéHunter] Test database: " + databasePath
            );

            CPH.LogInfo(
                "[PokéHunter] All database initialization checks passed."
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[PokéHunter] Database initialization check failed: " +
                exception
            );

            return false;
        }
        finally
        {
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
                    "[PokéHunter] Could not remove temporary folder '" +
                    temporaryDirectory + "': " + exception.Message
                );
            }
        }
    }

    /// <summary>
    /// Reads each migration timestamp and verifies its ordered identity.
    /// Supports the version-1 fixture before its upgrade to version 2.
    /// </summary>
    private static List<string> ReadHistory(string databasePath)
    {
        string[] expectedNames =
        {
            "Create migration history",
            "Create trainers"
        };

        var timestamps = new List<string>();

        using (var connection = OpenReadOnly(databasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                @"SELECT version, name, applied_at_utc
                  FROM schema_migrations
                  ORDER BY version;";

            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    int index = timestamps.Count;

                    Require(
                        index < expectedNames.Length,
                        "Unexpected extra migration record."
                    );

                    Require(
                        reader.GetInt32(0) == index + 1 &&
                        reader.GetString(1) == expectedNames[index],
                        "Unexpected migration version or name."
                    );

                    string timestamp = reader.GetString(2);

                    Require(
                        !string.IsNullOrWhiteSpace(timestamp),
                        "A migration timestamp is missing."
                    );

                    timestamps.Add(timestamp);
                }
            }
        }

        Require(
            timestamps.Count > 0,
            "The migration history is empty."
        );

        return timestamps;
    }

    /// <summary>
    /// Earlier timestamps must survive unchanged, even when new rows appear.
    /// </summary>
    private static void RequirePreserved(
        List<string> before,
        List<string> after
    )
    {
        Require(
            after.Count >= before.Count,
            "Migration records were removed."
        );

        for (int index = 0; index < before.Count; index++)
        {
            Require(
                string.Equals(
                    before[index],
                    after[index],
                    StringComparison.Ordinal
                ),
                "Migration " + (index + 1) + " changed its timestamp."
            );
        }
    }

    /// <summary>
    /// Preparing this query verifies the table and named columns exist.
    /// LIMIT 0 avoids reading or creating any trainer records.
    /// </summary>
    private static void CheckTrainerColumns(string databasePath)
    {
        using (var connection = OpenReadOnly(databasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                @"SELECT twitch_user_id, login_name, display_name,
                         created_at_utc, updated_at_utc
                  FROM trainers
                  LIMIT 0;";

            using (var reader = command.ExecuteReader())
            {
                Require(
                    reader.FieldCount == 5,
                    "The trainer query returned an unexpected column count."
                );
            }
        }
    }

    private static SQLiteConnection OpenReadOnly(string databasePath)
    {
        var settings = new SQLiteConnectionStringBuilder
        {
            DataSource = databasePath,
            Version = 3,
            ReadOnly = true,
            FailIfMissing = true,
            Pooling = false,
            DefaultTimeout = 5
        };

        var connection = new SQLiteConnection(settings.ConnectionString);

        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}