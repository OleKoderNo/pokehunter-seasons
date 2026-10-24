using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Reads completed redemption records from initialized storage.
    /// Saving results will be added alongside transaction checks.
    /// </summary>
    public sealed partial class RedemptionRepository
    {
        private readonly string databasePath;

        /// <summary>
        /// Records the database location without opening or creating it.
        /// Initialization is a separate setup step.
        /// </summary>
        public RedemptionRepository(string databasePath)
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
        /// Finds a completed redemption by its permanent ID.
        /// Returns null only when no matching record exists.
        ///
        /// This lookup alone does not reserve the ID. The future save
        /// transaction must repeat the lookup before inserting.
        /// </summary>
        public StoredRedemption FindById(string redemptionId)
        {
            RequireText(redemptionId, nameof(redemptionId));

            using (var connection = OpenConnection(true))
            {
                return ReadRedemption(connection, redemptionId);
            }
        }

        /// <summary>
        /// Uses an existing connection so the same lookup can later run
        /// inside the save transaction.
        /// </summary>
        private static StoredRedemption ReadRedemption(
            SQLiteConnection connection,
            string redemptionId
        )
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT redemption_id,
                           twitch_user_id,
                           season_id,
                           accepted_at_utc,
                           completed_at_utc,
                           event_id,
                           outcome,
                           previous_failure_streak,
                           available_attempts,
                           attempts_used,
                           next_failure_streak,
                           entry_id,
                           is_shiny,
                           category_id,
                           group_id,
                           entry_snapshot_json
                    FROM redemption_results
                    WHERE redemption_id = @redemptionId;";

                command.Parameters.AddWithValue(
                    "@redemptionId",
                    redemptionId
                );

                using (var reader = command.ExecuteReader())
                {
                    if (!reader.Read())
                    {
                        return null;
                    }

                    string id = ReadRequiredText(reader, 0);
                    string userId = ReadRequiredText(reader, 1);
                    string seasonId = ReadRequiredText(reader, 2);

                    DateTimeOffset acceptedAt = ParseUtcTimestamp(
                        ReadRequiredText(reader, 3),
                        "accepted_at_utc"
                    );

                    DateTimeOffset completedAt = ParseUtcTimestamp(
                        ReadRequiredText(reader, 4),
                        "completed_at_utc"
                    );

                    string eventId = ReadOptionalText(reader, 5);
                    string outcomeText = ReadRequiredText(reader, 6);

                    StoredRedemptionOutcome outcome;

                    if (outcomeText == "caught")
                    {
                        outcome = StoredRedemptionOutcome.Caught;
                    }
                    else if (outcomeText == "failed")
                    {
                        outcome = StoredRedemptionOutcome.Failed;
                    }
                    else
                    {
                        throw new InvalidDataException(
                            "Stored redemption has an unsupported outcome."
                        );
                    }

                    int previous = ReadNonnegativeInteger(reader, 7);
                    int available = ReadNonnegativeInteger(reader, 8);
                    int used = ReadNonnegativeInteger(reader, 9);
                    int next = ReadNonnegativeInteger(reader, 10);

                    string entryId = ReadOptionalText(reader, 11);
                    bool? isShiny = ReadOptionalBoolean(reader, 12);
                    string categoryId = ReadOptionalText(reader, 13);
                    string groupId = ReadOptionalText(reader, 14);

                    // Snapshot JSON may contain formatting whitespace.
                    // Do not apply the identifier text rules to this field.
                    string snapshotJson = reader.IsDBNull(15)
                        ? null
                        : reader.GetString(15);

                    RequireStored(
                        acceptedAt != default(DateTimeOffset) &&
                        completedAt >= acceptedAt,
                        "Stored redemption has invalid timestamps."
                    );

                    // Use long arithmetic so a damaged maximum-size
                    // previous streak cannot overflow during validation.
                    RequireStored(
                        (long)available == (long)previous + 1 &&
                        used >= 1 &&
                        used <= available,
                        "Stored redemption has an invalid attempt budget."
                    );

                    if (outcome == StoredRedemptionOutcome.Caught)
                    {
                        RequireStored(
                            next == 0 &&
                            entryId != null &&
                            isShiny.HasValue &&
                            categoryId != null &&
                            groupId != null &&
                            !string.IsNullOrWhiteSpace(snapshotJson),
                            "Stored catch is missing required data or " +
                            "has an invalid next failure streak."
                        );
                    }
                    else
                    {
                        RequireStored(
                            used == available &&
                            (long)next == (long)previous + 1 &&
                            entryId == null &&
                            !isShiny.HasValue &&
                            categoryId == null &&
                            groupId == null &&
                            snapshotJson == null,
                            "Stored failed redemption has inconsistent data."
                        );
                    }

                    return new StoredRedemption(
                        id,
                        userId,
                        seasonId,
                        acceptedAt,
                        completedAt,
                        eventId,
                        outcome,
                        previous,
                        available,
                        used,
                        next,
                        entryId,
                        isShiny,
                        categoryId,
                        groupId,
                        snapshotJson
                    );
                }
            }
        }

        /// <summary>
        /// Opens existing storage and verifies the expected application,
        /// schema version, and foreign-key enforcement.
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

                RequireStored(
                    ReadInteger(connection, "PRAGMA foreign_keys;") == 1,
                    "SQLite foreign-key enforcement could not be enabled."
                );

                RequireStored(
                    ReadInteger(connection, "PRAGMA application_id;") ==
                        0x50485331 &&
                    ReadInteger(connection, "PRAGMA user_version;") ==
                        DatabaseInitializer.CurrentSchemaVersion,
                    "Redemption storage requires an initialized " +
                    "PokéHunter Seasons database at schema version " +
                    DatabaseInitializer.CurrentSchemaVersion + "."
                );

                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Reads a required stored identifier or outcome string.
        /// Invalid stored values indicate a data problem, not bad input.
        /// </summary>
        private static string ReadRequiredText(
            SQLiteDataReader reader,
            int ordinal
        )
        {
            string value = ReadOptionalText(reader, ordinal);

            RequireStored(
                value != null,
                "Stored column '" + reader.GetName(ordinal) +
                "' must not be null."
            );

            return value;
        }

        private static string ReadOptionalText(
            SQLiteDataReader reader,
            int ordinal
        )
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            string value = reader.GetString(ordinal);

            RequireStored(
                IsValidText(value),
                "Stored column '" + reader.GetName(ordinal) +
                "' contains invalid text."
            );

            return value;
        }

        private static int ReadNonnegativeInteger(
            SQLiteDataReader reader,
            int ordinal
        )
        {
            RequireStored(
                !reader.IsDBNull(ordinal),
                "Stored column '" + reader.GetName(ordinal) +
                "' must not be null."
            );

            long value = reader.GetInt64(ordinal);

            RequireStored(
                value >= 0 && value <= int.MaxValue,
                "Stored column '" + reader.GetName(ordinal) +
                "' is outside the supported integer range."
            );

            return (int)value;
        }

        private static bool? ReadOptionalBoolean(
            SQLiteDataReader reader,
            int ordinal
        )
        {
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            long value = reader.GetInt64(ordinal);

            RequireStored(
                value == 0 || value == 1,
                "Stored shiny flag must be zero, one, or null."
            );

            return value == 1;
        }

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
                    "Redemption column '" + columnName +
                    "' must contain a UTC round-trip timestamp."
                );
            }

            return timestamp;
        }

        private static bool IsValidText(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value != value.Trim())
            {
                return false;
            }

            foreach (char character in value)
            {
                if (char.IsControl(character))
                {
                    return false;
                }
            }

            return true;
        }

        private static void RequireText(
            string value,
            string parameterName
        )
        {
            if (!IsValidText(value))
            {
                throw new ArgumentException(
                    "A nonempty value without surrounding whitespace " +
                    "or control characters is required.",
                    parameterName
                );
            }
        }

        private static void RequireStored(
            bool condition,
            string message
        )
        {
            if (!condition)
            {
                throw new InvalidDataException(message);
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

                RequireStored(
                    result != null && result != DBNull.Value,
                    "SQLite did not return the expected integer."
                );

                return Convert.ToInt32(
                    result,
                    CultureInfo.InvariantCulture
                );
            }
        }
    }
}