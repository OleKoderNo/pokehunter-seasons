using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Reads and saves trainer profiles in an initialized game database.
    /// Does not perform migrations or modify collection progress.
    /// </summary>
    public sealed class TrainerRepository
    {
        private readonly string databasePath;

        /// <summary>
        /// Stores the database location without opening or creating the file.
        /// Call DatabaseInitializer.Initialize separately during setup.
        /// </summary>
        public TrainerRepository(string databasePath)
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

            this.databasePath = fullPath;
        }

        /// <summary>
        /// Finds a trainer by their permanent Twitch user ID.
        /// Returns null when the trainer has not been saved.
        /// Database failures are reported as exceptions, not as missing users.
        /// </summary>
        public Trainer FindByTwitchUserId(string twitchUserId)
        {
            RequireText(twitchUserId, nameof(twitchUserId));

            using (var connection = OpenConnection(true))
            {
                return ReadTrainer(connection, twitchUserId);
            }
        }

        /// <summary>
        /// Creates a trainer or updates their login and display names.
        /// Preserves the original creation timestamp.
        /// Unchanged profiles preserve both timestamps.
        ///
        /// Returns a snapshot of the saved profile after committing.
        /// </summary>
        public Trainer SaveProfile(
            string twitchUserId,
            string loginName,
            string displayName
        )
        {
            RequireText(twitchUserId, nameof(twitchUserId));
            RequireText(loginName, nameof(loginName));
            RequireText(displayName, nameof(displayName));

            using (var connection = OpenConnection(false))
            {
                // Take the write transaction before checking whether the
                // trainer exists. Other writers cannot insert between
                // this lookup and our subsequent insert or update.
                Execute(connection, "BEGIN IMMEDIATE;");

                try
                {
                    Trainer existing = ReadTrainer(
                        connection,
                        twitchUserId
                    );

                    bool needsSave =
                        existing == null ||
                        !string.Equals(
                            existing.LoginName,
                            loginName,
                            StringComparison.Ordinal
                        ) ||
                        !string.Equals(
                            existing.DisplayName,
                            displayName,
                            StringComparison.Ordinal
                        );

                    if (needsSave)
                    {
                        WriteProfile(
                            connection,
                            twitchUserId,
                            loginName,
                            displayName,
                            existing == null
                        );
                    }

                    Trainer saved = needsSave
                        ? ReadTrainer(connection, twitchUserId)
                        : existing;

                    if (saved == null)
                    {
                        throw new InvalidDataException(
                            "The saved trainer could not be read."
                        );
                    }

                    Execute(connection, "COMMIT;");
                    return saved;
                }
                catch (Exception saveException)
                {
                    try
                    {
                        Execute(connection, "ROLLBACK;");
                    }
                    catch (Exception rollbackException)
                    {
                        throw new AggregateException(
                            "Saving the trainer failed, and rollback " +
                            "also reported an error.",
                            saveException,
                            rollbackException
                        );
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Opens an existing database and verifies its compatibility markers.
        /// Every caller owns and disposes its returned connection.
        /// </summary>
        private SQLiteConnection OpenConnection(bool readOnly)
        {
            var settings = new SQLiteConnectionStringBuilder
            {
                DataSource = databasePath,
                Version = 3,
                ReadOnly = readOnly,
                FailIfMissing = true,
                Pooling = false,
                DefaultTimeout = 5
            };

            var connection = new SQLiteConnection(
                settings.ConnectionString
            );

            try
            {
                connection.Open();

                Execute(connection, "PRAGMA foreign_keys = ON;");

                if (ReadInteger(connection, "PRAGMA foreign_keys;") != 1)
                {
                    throw new InvalidOperationException(
                        "SQLite foreign-key enforcement could not be enabled."
                    );
                }

                // This is a quick compatibility check, not migration-history
                // validation. Full initialization is a separate setup step.
                int applicationId = ReadInteger(
                    connection,
                    "PRAGMA application_id;"
                );

                int version = ReadInteger(
                    connection,
                    "PRAGMA user_version;"
                );

                if (applicationId != 0x50485331 ||
                    version != DatabaseInitializer.CurrentSchemaVersion)
                {
                    throw new InvalidDataException(
                        "Trainer storage requires an initialized " +
                        "PokéHunter Seasons database at schema version " +
                        DatabaseInitializer.CurrentSchemaVersion + "."
                    );
                }

                return connection;
            }
            catch
            {
                // If opening or checking fails, the caller never receives
                // the connection and cannot dispose it.
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Reads one trainer using an existing connection.
        /// Can also be used inside SaveProfile's transaction.
        /// </summary>
        private static Trainer ReadTrainer(
            SQLiteConnection connection,
            string twitchUserId
        )
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    @"SELECT twitch_user_id,
                             login_name,
                             display_name,
                             created_at_utc,
                             updated_at_utc
                      FROM trainers
                      WHERE twitch_user_id = @userId;";

                command.Parameters.AddWithValue("@userId", twitchUserId);

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    return new Trainer(
                        reader.GetString(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        ParseUtcTimestamp(
                            reader.GetString(3),
                            "created_at_utc"
                        ),
                        ParseUtcTimestamp(
                            reader.GetString(4),
                            "updated_at_utc"
                        )
                    );
                }
            }
        }

        /// <summary>
        /// Inserts or updates a profile inside the caller's transaction.
        /// Existing records are updated in place.
        /// </summary>
        private static void WriteProfile(
            SQLiteConnection connection,
            string twitchUserId,
            string loginName,
            string displayName,
            bool isNew
        )
        {
            string now = DateTimeOffset.UtcNow.ToString(
                "O",
                CultureInfo.InvariantCulture
            );

            using (var command = connection.CreateCommand())
            {
                if (isNew)
                {
                    command.CommandText =
                        @"INSERT INTO trainers (
                            twitch_user_id,
                            login_name,
                            display_name,
                            created_at_utc,
                            updated_at_utc
                        )
                        VALUES (
                            @userId,
                            @loginName,
                            @displayName,
                            @now,
                            @now
                        );";
                }
                else
                {
                    // Neither identity nor creation time is overwritten.
                    command.CommandText =
                        @"UPDATE trainers
                          SET login_name = @loginName,
                              display_name = @displayName,
                              updated_at_utc = @now
                          WHERE twitch_user_id = @userId;";
                }

                // Values are bound separately from the SQL statement.
                command.Parameters.AddWithValue("@userId", twitchUserId);
                command.Parameters.AddWithValue("@loginName", loginName);
                command.Parameters.AddWithValue("@displayName", displayName);
                command.Parameters.AddWithValue("@now", now);

                if (command.ExecuteNonQuery() != 1)
                {
                    throw new InvalidDataException(
                        "Saving a trainer did not affect exactly one row."
                    );
                }
            }
        }

        /// <summary>
        /// Reads the exact UTC timestamp format written by this repository.
        /// Invalid stored values are reported rather than silently replaced.
        /// </summary>
        private static DateTimeOffset ParseUtcTimestamp(
            string value,
            string columnName
        )
        {
            DateTimeOffset timestamp;

            if (!DateTimeOffset.TryParseExact(
                value,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out timestamp
            ) || timestamp.Offset != TimeSpan.Zero)
            {
                throw new InvalidDataException(
                    "Trainer column '" + columnName +
                    "' does not contain a valid UTC round-trip timestamp."
                );
            }

            return timestamp;
        }

        /// <summary>
        /// Rejects missing values, surrounding whitespace, and control
        /// characters. Preserves capitalization and international text.
        /// This does not attempt to reproduce Twitch's full naming rules.
        /// </summary>
        private static void RequireText(string value, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value != value.Trim())
            {
                throw new ArgumentException(
                    "A nonempty value without surrounding whitespace " +
                    "is required.",
                    parameterName
                );
            }

            foreach (char character in value)
            {
                if (char.IsControl(character))
                {
                    throw new ArgumentException(
                        "Control characters are not allowed.",
                        parameterName
                    );
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