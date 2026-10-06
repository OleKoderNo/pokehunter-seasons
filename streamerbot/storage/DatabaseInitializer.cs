using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Creates or upgrades the database using ordered migrations.
    /// Existing migration history is preserved.
    /// </summary>
    public static class DatabaseInitializer
    {
        public const int CurrentSchemaVersion = 3;

        // Stable database-family identifier: ASCII "PHS1".
        // This does not change when the schema version increases.
        private const int ApplicationId = 0x50485331;

        // Array position + 1 is the migration version.
        // Preserve existing names and ordering.
        private static readonly string[] MigrationNames =
        {
            "Create migration history",
            "Create trainers",
            "Create seasons"
        };

        private static readonly string[] MigrationResources =
        {
            "PokeHunter.Storage.Migrations.001_CreateMigrationHistory.sql",
            "PokeHunter.Storage.Migrations.002_CreateTrainers.sql",
            "PokeHunter.Storage.Migrations.003_CreateSeasons.sql"
        };
        /// <summary>
        /// Initializes or upgrades a database at a normalized absolute path.
        /// Returns the installed schema version.
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

            // Catch mistakes in the migration registry before opening a file.
            if (MigrationNames.Length != CurrentSchemaVersion ||
                MigrationResources.Length != CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    "The migration registry does not match " +
                    "CurrentSchemaVersion."
                );
            }

            Directory.CreateDirectory(directory);

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

                Execute(connection, "PRAGMA foreign_keys = ON;");

                if (ReadInteger(connection, "PRAGMA foreign_keys;") != 1)
                {
                    throw new InvalidOperationException(
                        "SQLite foreign-key enforcement could not be enabled."
                    );
                }

                // Acquire the write transaction before inspecting history.
                // All pending migrations commit together.
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
                        RequireEmptyDatabase(connection, applicationId);
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

                        // Validate existing history before modifying anything.
                        ValidateMigrationHistory(connection, version);
                    }

                    // Apply only migrations newer than the installed version.
                    // Fresh databases receive the complete sequence.
                    // Current databases skip this loop.
                    while (version < CurrentSchemaVersion)
                    {
                        int nextVersion = version + 1;
                        ApplyMigration(connection, nextVersion);
                        version = nextVersion;
                    }

                    ValidateMigrationHistory(connection, version);

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
        /// Only an unmarked, empty database can start at migration 1.
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
        /// Runs one migration and records it within the existing transaction.
        /// Never updates or replaces earlier migration records.
        /// </summary>
        private static void ApplyMigration(
            SQLiteConnection connection,
            int version
        )
        {
            int index = version - 1;

            string sql = ReadMigrationSql(MigrationResources[index]);
            Execute(connection, sql);

            using (var command = connection.CreateCommand())
            {
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

                command.Parameters.AddWithValue("@version", version);
                command.Parameters.AddWithValue(
                    "@name",
                    MigrationNames[index]
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

            // Set the database-family marker only during first initialization.
            if (version == 1)
            {
                Execute(connection, "PRAGMA application_id = 0x50485331;");
            }

            // PRAGMA assignment uses a trusted integer from our own registry.
            // No viewer-provided text is inserted into this SQL.
            Execute(
                connection,
                "PRAGMA user_version = " +
                version.ToString(CultureInfo.InvariantCulture) + ";"
            );
        }

        /// <summary>
        /// Requires exactly the expected ordered history for this version.
        /// This is not a complete database integrity audit.
        /// </summary>
        private static void ValidateMigrationHistory(
            SQLiteConnection connection,
            int version
        )
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    @"SELECT version, name, applied_at_utc
                      FROM schema_migrations
                      ORDER BY version;";

                using (var reader = command.ExecuteReader())
                {
                    for (int expected = 1; expected <= version; expected++)
                    {
                        if (!reader.Read() ||
                            reader.GetInt32(0) != expected ||
                            reader.IsDBNull(1) ||
                            !string.Equals(
                                reader.GetString(1),
                                MigrationNames[expected - 1],
                                StringComparison.Ordinal
                            ) ||
                            reader.IsDBNull(2) ||
                            string.IsNullOrWhiteSpace(reader.GetString(2)))
                        {
                            throw new InvalidDataException(
                                "The database migration history does not " +
                                "match schema version " + version + "."
                            );
                        }
                    }

                    if (reader.Read())
                    {
                        throw new InvalidDataException(
                            "The database migration history contains " +
                            "unexpected extra records."
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Reads migration SQL embedded in the storage library.
        /// </summary>
        private static string ReadMigrationSql(string resourceName)
        {
            var assembly = typeof(DatabaseInitializer).Assembly;

            using (Stream stream =
                assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException(
                        "Embedded migration SQL was not found: '" +
                        resourceName + "'. Check the EmbeddedResource " +
                        "and LogicalName entries in PokeHunter.Storage.csproj."
                    );
                }

                using (var reader = new StreamReader(stream))
                {
                    string sql = reader.ReadToEnd();

                    if (string.IsNullOrWhiteSpace(sql))
                    {
                        throw new InvalidDataException(
                            "Embedded migration SQL is empty: '" +
                            resourceName + "'."
                        );
                    }

                    return sql;
                }
            }
        }

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