using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Creates the initial database structure and checks schema compatibility.
    /// Does not create trainers, award catches, or modify configuration files.
    /// </summary>
    public static class DatabaseInitializer
    {
        // Increase this only when adding a corresponding migration.
        public const int CurrentSchemaVersion = 1;

        // A stable project-specific marker: hexadecimal ASCII for "PHS1".
        // This identifies the database family, not its changing schema version.
        private const int ApplicationId = 0x50485331;

        /// <summary>
        /// Initializes a database at an absolute file path.
        /// Returns the installed schema version.
        ///
        /// Repeated calls preserve an already-initialized database.
        /// Unsupported versions and unrelated databases are rejected.
        /// </summary>
        public static int Initialize(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException(
                    "A database file path is required.",
                    nameof(databasePath)
                );
            }

            // Reject relative and drive-relative paths rather than allowing
            // Streamer.bot's working directory to choose the database location.
            string fullPath = Path.GetFullPath(databasePath);

            if (!string.Equals(
                databasePath,
                fullPath,
                StringComparison.OrdinalIgnoreCase
            ))
            {
                throw new ArgumentException(
                    "Use a full, normalized absolute database file path.",
                    nameof(databasePath)
                );
            }

            string directory = Path.GetDirectoryName(fullPath);

            if (string.IsNullOrEmpty(directory))
            {
                throw new ArgumentException(
                    "The database path must include a parent directory.",
                    nameof(databasePath)
                );
            }

            // Creates missing directories, but preserves existing ones.
            Directory.CreateDirectory(directory);

            // A builder safely handles special characters in file paths.
            var connectionString = new SQLiteConnectionStringBuilder
            {
                DataSource = fullPath,
                Version = 3,
                Pooling = false,
                DefaultTimeout = 5
            };

            using (var connection = new SQLiteConnection(
                connectionString.ConnectionString
            ))
            {
                connection.Open();

                // Foreign-key enforcement belongs to each connection.
                // Enable it before starting a transaction.
                Execute(connection, "PRAGMA foreign_keys = ON;");

                if (ReadInteger(connection, "PRAGMA foreign_keys;") != 1)
                {
                    throw new InvalidOperationException(
                        "SQLite foreign-key enforcement could not be enabled."
                    );
                }

                // Obtain the write transaction before reading the version.
                // Concurrent initializers must not both decide to migrate
                // the same original database state.
                Execute(connection, "BEGIN IMMEDIATE;");

                try
                {
                    int version = ReadInteger(
                        connection,
                        "PRAGMA user_version;"
                    );

                    int applicationId = ReadInteger(
                        connection,
                        "PRAGMA application_id;"
                    );

                    if (version < 0 || version > CurrentSchemaVersion)
                    {
                        throw new InvalidDataException(
                            "Unsupported database schema version " +
                            version + ". This build supports versions " +
                            "0 through " + CurrentSchemaVersion + "."
                        );
                    }

                    if (version == 0)
                    {
                        // Only an unmarked, empty database may receive
                        // the first migration.
                        RequireEmptyDatabase(connection, applicationId);
                        ApplyFirstMigration(connection);
                        version = 1;
                    }
                    else
                    {
                        if (applicationId != ApplicationId)
                        {
                            throw new InvalidDataException(
                                "This file is not a recognized " +
                                "PokéHunter Seasons database."
                            );
                        }

                        // Check that the version marker agrees with the
                        // migration history we expect for this first version.
                        ValidateFirstMigration(connection);
                    }

                    Execute(connection, "COMMIT;");
                    return version;
                }
                catch (Exception initializationException)
                {
                    try
                    {
                        Execute(connection, "ROLLBACK;");
                    }
                    catch (Exception rollbackException)
                    {
                        // Preserve both errors if rollback also fails.
                        throw new AggregateException(
                            "Database initialization failed, and rollback " +
                            "also reported an error.",
                            initializationException,
                            rollbackException
                        );
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Prevents initialization from adopting an unrelated database.
        /// </summary>
        private static void RequireEmptyDatabase(
            SQLiteConnection connection,
            int applicationId
        )
        {
            int objectCount = ReadInteger(
                connection,
                @"SELECT COUNT(*)
                  FROM sqlite_master
                  WHERE name NOT GLOB 'sqlite_*';"
            );

            if (applicationId != 0 || objectCount != 0)
            {
                throw new InvalidDataException(
                    "Schema version 0 was found, but the database is " +
                    "not empty or is marked for another application. " +
                    "Initialization has been refused."
                );
            }
        }

        /// <summary>
        /// Installs schema version 1 inside the caller's transaction.
        /// Future migrations must preserve this migration's history.
        /// </summary>
        private static void ApplyFirstMigration(
            SQLiteConnection connection
        )
        {
            // PRIMARY KEY prevents duplicate migration versions.
            // NOT NULL requires values for every history field.
            Execute(
                connection,
                @"CREATE TABLE schema_migrations (
                    version INTEGER NOT NULL PRIMARY KEY
                        CHECK (version > 0),
                    name TEXT NOT NULL,
                    applied_at_utc TEXT NOT NULL
                );"
            );

            using (var command = connection.CreateCommand())
            {
                // Parameters pass values separately from the SQL itself.
                command.CommandText =
                    @"INSERT INTO schema_migrations (
                        version,
                        name,
                        applied_at_utc
                    )
                    VALUES (
                        @version,
                        @name,
                        @appliedAtUtc
                    );";

                command.Parameters.AddWithValue("@version", 1);
                command.Parameters.AddWithValue(
                    "@name",
                    "Create migration history"
                );
                command.Parameters.AddWithValue(
                    "@appliedAtUtc",
                    DateTimeOffset.UtcNow.ToString(
                        "O",
                        CultureInfo.InvariantCulture
                    )
                );

                command.ExecuteNonQuery();
            }

            // Fixed SQL constants, not values supplied by a viewer.
            // These markers commit together with the table and history row.
            Execute(connection, "PRAGMA application_id = 0x50485331;");
            Execute(connection, "PRAGMA user_version = 1;");
        }

        /// <summary>
        /// Checks the expected history for an existing version-1 database.
        /// This is a consistency check, not a full database integrity audit.
        /// </summary>
        private static void ValidateFirstMigration(
            SQLiteConnection connection
        )
        {
            int totalRows = ReadInteger(
                connection,
                "SELECT COUNT(*) FROM schema_migrations;"
            );

            int matchingRows = ReadInteger(
                connection,
                @"SELECT COUNT(*)
                  FROM schema_migrations
                  WHERE version = 1
                    AND name = 'Create migration history';"
            );

            if (totalRows != 1 || matchingRows != 1)
            {
                throw new InvalidDataException(
                    "The database migration history does not match " +
                    "schema version 1."
                );
            }
        }

        /// <summary>
        /// Executes SQL that does not return a result we need to read.
        /// </summary>
        private static void Execute(
            SQLiteConnection connection,
            string sql
        )
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Reads one integer from the first column of the first result row.
        /// </summary>
        private static int ReadInteger(
            SQLiteConnection connection,
            string sql
        )
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;

                object result = command.ExecuteScalar();

                if (result == null || result == DBNull.Value)
                {
                    throw new InvalidDataException(
                        "SQLite did not return the expected integer."
                    );
                }

                return Convert.ToInt32(
                    result,
                    CultureInfo.InvariantCulture
                );
            }
        }
    }
}