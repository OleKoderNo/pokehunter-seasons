using System;
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
    // Change this when setting up the project on another computer.
    private const string ProjectFolder =
        @"C:\Users\ohfb9\Documents\Coding\Private\Streaming\pokehunter-seasons";

#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        try
        {
            // Use a dedicated test database, never the live game database.
            string databasePath = Path.Combine(
                ProjectFolder,
                "runtime",
                "database-initialization-test.db"
            );

            bool existedBefore = File.Exists(databasePath);

            // First call creates a fresh database or validates an existing one.
            int firstVersion = DatabaseInitializer.Initialize(databasePath);

            Require(
                firstVersion == DatabaseInitializer.CurrentSchemaVersion,
                "The first initialization returned an unexpected version."
            );

            string originalTimestamp = ReadMigrationTimestamp(databasePath);

            // Repeat initialization to verify it does not insert another
            // migration row or replace the original timestamp.
            int secondVersion = DatabaseInitializer.Initialize(databasePath);

            Require(
                secondVersion == firstVersion,
                "Repeated initialization changed the schema version."
            );

            string repeatedTimestamp = ReadMigrationTimestamp(databasePath);

            Require(
                string.Equals(
                    originalTimestamp,
                    repeatedTimestamp,
                    StringComparison.Ordinal
                ),
                "Repeated initialization changed the migration timestamp."
            );

            CPH.LogInfo(
                "[PokéHunter] Database initialization check passed."
            );

            CPH.LogInfo(
                "[PokéHunter] Database existed before this check: " +
                existedBefore
            );

            CPH.LogInfo(
                "[PokéHunter] Installed schema version: " + secondVersion
            );

            CPH.LogInfo(
                "[PokéHunter] Migration history: exactly one version-1 row."
            );

            CPH.LogInfo(
                "[PokéHunter] Original migration timestamp (UTC): " +
                originalTimestamp
            );

            CPH.LogInfo(
                "[PokéHunter] Repeated initialization preserved " +
                "the migration record."
            );

            CPH.LogInfo(
                "[PokéHunter] Test database: " + databasePath
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
    }

    /// <summary>
    /// Reads the saved migration through a separate, read-only connection.
    /// Checks that exactly one history row exists and belongs to version 1.
    /// </summary>
    private static string ReadMigrationTimestamp(string databasePath)
    {
        var connectionString = new SQLiteConnectionStringBuilder
        {
            DataSource = databasePath,
            Version = 3,
            ReadOnly = true,
            FailIfMissing = true,
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
                command.CommandText =
                    @"SELECT version, applied_at_utc
                      FROM schema_migrations
                      ORDER BY version;";

                using (var reader = command.ExecuteReader())
                {
                    Require(
                        reader.Read(),
                        "The migration history is empty."
                    );

                    Require(
                        reader.GetInt32(0) == 1,
                        "The saved migration version is not 1."
                    );

                    string timestamp = reader.GetString(1);

                    Require(
                        !string.IsNullOrWhiteSpace(timestamp),
                        "The migration timestamp is missing."
                    );

                    // A second row would mean initialization has produced
                    // unexpected history for this version-1 database.
                    Require(
                        !reader.Read(),
                        "The migration history contains unexpected extra rows."
                    );

                    return timestamp;
                }
            }
        }
    }

    /// <summary>
    /// Stops the check immediately when an expected condition is false.
    /// Execute catches and logs the failure.
    /// </summary>
    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}