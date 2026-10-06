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

    // Keep test expectations explicit and independent of the initializer.
    // A missing migration should fail the test, not redefine its expectation.
    private const int ExpectedSchemaVersion = 3;

    private static readonly string[] ExpectedMigrationNames =
    {
        "Create migration history",
        "Create trainers",
        "Create seasons"
    };

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

            // Capture existing history before initialization so upgrades
            // must preserve timestamps written by earlier versions.
            List<string> originalHistory = existedBefore
                ? ReadHistory(databasePath)
                : new List<string>();

            int version = DatabaseInitializer.Initialize(databasePath);

            Require(
                version == ExpectedSchemaVersion,
                "Initialization returned an unexpected schema version."
            );

            List<string> installedHistory = ReadHistory(databasePath);

            Require(
                installedHistory.Count == ExpectedSchemaVersion,
                "The installed migration count is incorrect."
            );

            RequirePreserved(originalHistory, installedHistory);
            CheckExpectedColumns(databasePath);

            // Running initialization again must preserve the complete history.
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
                "all three migration records."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Trainer and season tables contain " +
                "the expected columns."
            );

            // A separate fresh database checks the complete migration sequence.
            Directory.CreateDirectory(temporaryDirectory);

            string freshPath = Path.Combine(
                temporaryDirectory,
                "fresh.db"
            );

            Require(
                DatabaseInitializer.Initialize(freshPath) ==
                    ExpectedSchemaVersion,
                "A fresh database did not reach the expected version."
            );

            Require(
                ReadHistory(freshPath).Count == ExpectedSchemaVersion,
                "A fresh database did not record every migration."
            );

            CheckExpectedColumns(freshPath);

            CPH.LogInfo(
                "[PokéHunter] PASS: A fresh database reached version 3 " +
                "with trainer and season tables."
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
    /// Reads timestamps while verifying ordered migration identities.
    /// Accepts earlier complete histories before initialization upgrades them.
    /// </summary>
    private static List<string> ReadHistory(string databasePath)
    {
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
                        index < ExpectedMigrationNames.Length,
                        "Unexpected extra migration record."
                    );

                    Require(
                        reader.GetInt32(0) == index + 1 &&
                        reader.GetString(1) == ExpectedMigrationNames[index],
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
    /// Existing history must remain unchanged when new migrations are added.
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
    /// Checks named columns without reading or inserting application records.
    /// Constraint behaviour is checked separately from column existence.
    /// </summary>
    private static void CheckExpectedColumns(string databasePath)
    {
        using (var connection = OpenReadOnly(databasePath))
        {
            CheckColumns(
                connection,
                @"SELECT twitch_user_id, login_name, display_name,
                         created_at_utc, updated_at_utc
                  FROM trainers
                  LIMIT 0;",
                5,
                "trainers"
            );

            CheckColumns(
                connection,
                @"SELECT season_id, name, starts_at_utc,
                         ends_at_exclusive_utc, created_at_utc
                  FROM seasons
                  LIMIT 0;",
                5,
                "seasons"
            );

            CheckColumns(
                connection,
                @"SELECT season_id, generation
                  FROM season_generations
                  LIMIT 0;",
                2,
                "season_generations"
            );
        }
    }

    private static void CheckColumns(
        SQLiteConnection connection,
        string sql,
        int expectedCount,
        string tableName
    )
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;

            using (var reader = command.ExecuteReader())
            {
                Require(
                    reader.FieldCount == expectedCount,
                    "Unexpected column count for " + tableName + "."
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