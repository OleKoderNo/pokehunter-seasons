using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Registers and reads permanent season records.
    /// Does not select active seasons, run migrations, or modify catches.
    /// Existing season definitions cannot be silently overwritten.
    /// </summary>
    public sealed class SeasonRepository
    {
        private readonly string databasePath;

        /// <summary>
        /// Stores the database location without opening or creating it.
        /// Initialize the database separately before using this repository.
        /// </summary>
        public SeasonRepository(string databasePath)
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
        /// Finds a season using a case-insensitive identifier.
        /// Returns null when no matching season has been registered.
        /// Database failures are reported as exceptions.
        /// </summary>
        public StoredSeason FindById(string seasonId)
        {
            RequireText(seasonId, nameof(seasonId));

            using (var connection = OpenConnection(true))
            {
                return ReadSeason(connection, seasonId);
            }
        }

        /// <summary>
        /// Saves a new season and its unlocked generations together.
        ///
        /// An identical registration returns the existing record without
        /// changing its creation timestamp or original ID capitalization.
        ///
        /// Conflicting settings throw InvalidDataException and leave the
        /// existing season unchanged.
        ///
        /// Schedule continuity is validated by the configuration layer.
        /// This method validates only the individual season being saved.
        /// </summary>
        public StoredSeason Register(
            string seasonId,
            string name,
            DateTimeOffset startsAt,
            DateTimeOffset endsAtExclusive,
            IEnumerable<int> unlockedGenerations
        )
        {
            RequireText(seasonId, nameof(seasonId));
            RequireText(name, nameof(name));

            if (startsAt == default(DateTimeOffset))
            {
                throw new ArgumentException(
                    "A starting timestamp is required.",
                    nameof(startsAt)
                );
            }

            if (endsAtExclusive <= startsAt)
            {
                throw new ArgumentException(
                    "The exclusive end must be later than the start.",
                    nameof(endsAtExclusive)
                );
            }

            // Copy and validate before opening the database.
            // Sorting makes comparison independent of input order.
            List<int> generations = PrepareGenerations(
                unlockedGenerations
            );

            DateTimeOffset startsAtUtc = startsAt.ToUniversalTime();
            DateTimeOffset endsAtUtc =
                endsAtExclusive.ToUniversalTime();

            using (var connection = OpenConnection(false))
            {
                // Reserve the write transaction before checking for an
                // existing season. Another writer cannot insert a matching
                // record between our lookup and insert.
                Execute(connection, "BEGIN IMMEDIATE;");

                try
                {
                    StoredSeason existing = ReadSeason(
                        connection,
                        seasonId
                    );

                    StoredSeason saved;

                    if (existing != null)
                    {
                        RequireMatchingDefinition(
                            existing,
                            name,
                            startsAtUtc,
                            endsAtUtc,
                            generations
                        );

                        saved = existing;
                    }
                    else
                    {
                        WriteSeason(
                            connection,
                            seasonId,
                            name,
                            startsAtUtc,
                            endsAtUtc,
                            generations
                        );

                        saved = ReadSeason(connection, seasonId);

                        if (saved == null)
                        {
                            throw new InvalidDataException(
                                "The registered season could not be read."
                            );
                        }
                    }

                    Execute(connection, "COMMIT;");
                    return saved;
                }
                catch (Exception registrationException)
                {
                    try
                    {
                        Execute(connection, "ROLLBACK;");
                    }
                    catch (Exception rollbackException)
                    {
                        throw new AggregateException(
                            "Registering the season failed, and rollback " +
                            "also reported an error.",
                            registrationException,
                            rollbackException
                        );
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Opens an existing database and checks its compatibility markers.
        /// Matches the connection policy used by TrainerRepository.
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

                // Initialization performs the full migration-history check.
                // Repositories only check the expected application and version.
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
                        "Season storage requires an initialized " +
                        "PokéHunter Seasons database at schema version " +
                        DatabaseInitializer.CurrentSchemaVersion + "."
                    );
                }

                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Reads season metadata and generations in one SQL statement.
        ///
        /// Comparing IDs in C# preserves the configuration validator's
        /// OrdinalIgnoreCase behaviour, including non-ASCII identifiers.
        /// SQLite's built-in NOCASE comparison only handles ASCII casing.
        ///
        /// The season registry is small, so scanning these records avoids
        /// introducing a separate normalized-ID column or custom collation.
        /// This approach is not intended for a large catch-history table.
        /// </summary>
        private static StoredSeason ReadSeason(
            SQLiteConnection connection,
            string seasonId
        )
        {
            string savedId = null;
            string savedName = null;
            DateTimeOffset startsAtUtc = default(DateTimeOffset);
            DateTimeOffset endsAtUtc = default(DateTimeOffset);
            DateTimeOffset createdAtUtc = default(DateTimeOffset);
            var generations = new List<int>();

            using (var command = connection.CreateCommand())
            {
                // LEFT JOIN also exposes a damaged season that has no
                // generation records, allowing us to report the problem.
                //
                // A single statement reads both tables consistently.
                command.CommandText =
                    @"SELECT s.season_id,
                             s.name,
                             s.starts_at_utc,
                             s.ends_at_exclusive_utc,
                             s.created_at_utc,
                             g.generation
                      FROM seasons AS s
                      LEFT JOIN season_generations AS g
                        ON g.season_id = s.season_id
                      ORDER BY s.season_id COLLATE BINARY,
                               g.generation;";

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string candidateId = reader.GetString(0);

                        if (!string.Equals(
                            candidateId,
                            seasonId,
                            StringComparison.OrdinalIgnoreCase
                        ))
                        {
                            continue;
                        }

                        if (savedId == null)
                        {
                            savedId = candidateId;
                            savedName = reader.GetString(1);

                            startsAtUtc = ParseUtcTimestamp(
                                reader.GetString(2),
                                "starts_at_utc"
                            );

                            endsAtUtc = ParseUtcTimestamp(
                                reader.GetString(3),
                                "ends_at_exclusive_utc"
                            );

                            createdAtUtc = ParseUtcTimestamp(
                                reader.GetString(4),
                                "created_at_utc"
                            );
                        }
                        else if (!string.Equals(
                            savedId,
                            candidateId,
                            StringComparison.Ordinal
                        ))
                        {
                            // A database modified outside this repository
                            // could contain Unicode IDs that SQLite considers
                            // different but our configuration considers equal.
                            throw new InvalidDataException(
                                "Multiple stored seasons match ID '" +
                                seasonId + "' ignoring capitalization."
                            );
                        }

                        if (reader.IsDBNull(5))
                        {
                            throw new InvalidDataException(
                                "Stored season '" + savedId +
                                "' has no unlocked generations."
                            );
                        }

                        long generation = reader.GetInt64(5);

                        if (generation <= 0 || generation > int.MaxValue)
                        {
                            throw new InvalidDataException(
                                "Stored season '" + savedId +
                                "' contains an invalid generation number."
                            );
                        }

                        int value = (int)generation;

                        if (generations.Contains(value))
                        {
                            throw new InvalidDataException(
                                "Stored season '" + savedId +
                                "' contains duplicate generations."
                            );
                        }

                        generations.Add(value);
                    }
                }
            }

            if (savedId == null)
            {
                return null;
            }

            if (startsAtUtc == default(DateTimeOffset) ||
                endsAtUtc <= startsAtUtc)
            {
                throw new InvalidDataException(
                    "Stored season '" + savedId +
                    "' has invalid time boundaries."
                );
            }

            return new StoredSeason(
                savedId,
                savedName,
                startsAtUtc,
                endsAtUtc,
                generations,
                createdAtUtc
            );
        }

        /// <summary>
        /// Inserts the parent season and all generation rows.
        /// The caller owns the transaction covering every insert.
        /// </summary>
        private static void WriteSeason(
            SQLiteConnection connection,
            string seasonId,
            string name,
            DateTimeOffset startsAtUtc,
            DateTimeOffset endsAtUtc,
            List<int> generations
        )
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    @"INSERT INTO seasons (
                        season_id,
                        name,
                        starts_at_utc,
                        ends_at_exclusive_utc,
                        created_at_utc
                      )
                      VALUES (
                        @seasonId,
                        @name,
                        @startsAt,
                        @endsAt,
                        @createdAt
                      );";

                command.Parameters.AddWithValue("@seasonId", seasonId);
                command.Parameters.AddWithValue("@name", name);
                command.Parameters.AddWithValue(
                    "@startsAt",
                    FormatUtcTimestamp(startsAtUtc)
                );
                command.Parameters.AddWithValue(
                    "@endsAt",
                    FormatUtcTimestamp(endsAtUtc)
                );
                command.Parameters.AddWithValue(
                    "@createdAt",
                    FormatUtcTimestamp(DateTimeOffset.UtcNow)
                );

                if (command.ExecuteNonQuery() != 1)
                {
                    throw new InvalidDataException(
                        "Registering a season did not insert exactly one row."
                    );
                }
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    @"INSERT INTO season_generations (
                        season_id,
                        generation
                      )
                      VALUES (@seasonId, @generation);";

                command.Parameters.AddWithValue("@seasonId", seasonId);

                var generationParameter =
                    command.Parameters.AddWithValue("@generation", 0);

                foreach (int generation in generations)
                {
                    // Reuse the parameterized command for each generation.
                    generationParameter.Value = generation;

                    if (command.ExecuteNonQuery() != 1)
                    {
                        throw new InvalidDataException(
                            "Saving an unlocked generation did not " +
                            "insert exactly one row."
                        );
                    }
                }
            }
        }

        /// <summary>
        /// Compares the requested definition with the permanent record.
        /// Display-name differences are significant; ID casing is not.
        /// DateTimeOffset comparisons compare actual instants.
        /// </summary>
        private static void RequireMatchingDefinition(
            StoredSeason existing,
            string name,
            DateTimeOffset startsAtUtc,
            DateTimeOffset endsAtUtc,
            List<int> generations
        )
        {
            bool matches =
                string.Equals(
                    existing.Name,
                    name,
                    StringComparison.Ordinal
                ) &&
                existing.StartsAtUtc == startsAtUtc &&
                existing.EndsAtExclusiveUtc == endsAtUtc &&
                existing.UnlockedGenerations.Count == generations.Count;

            if (matches)
            {
                for (int index = 0; index < generations.Count; index++)
                {
                    if (existing.UnlockedGenerations[index] !=
                        generations[index])
                    {
                        matches = false;
                        break;
                    }
                }
            }

            if (!matches)
            {
                throw new InvalidDataException(
                    "Season '" + existing.Id +
                    "' is already registered with different settings. " +
                    "Its name, boundaries, and unlocked generations " +
                    "cannot be overwritten by registration."
                );
            }
        }

        /// <summary>
        /// Makes a sorted copy of distinct, positive generation numbers.
        /// Invalid input is rejected rather than silently repaired.
        /// </summary>
        private static List<int> PrepareGenerations(
            IEnumerable<int> unlockedGenerations
        )
        {
            if (unlockedGenerations == null)
            {
                throw new ArgumentNullException(
                    nameof(unlockedGenerations)
                );
            }

            var result = new List<int>();
            var seen = new HashSet<int>();

            foreach (int generation in unlockedGenerations)
            {
                if (generation <= 0 || !seen.Add(generation))
                {
                    throw new ArgumentException(
                        "Unlocked generations must contain distinct " +
                        "positive integers.",
                        nameof(unlockedGenerations)
                    );
                }

                result.Add(generation);
            }

            if (result.Count == 0)
            {
                throw new ArgumentException(
                    "At least one unlocked generation is required.",
                    nameof(unlockedGenerations)
                );
            }

            result.Sort();
            return result;
        }

        /// <summary>
        /// Uses one invariant UTC representation for stored timestamps.
        /// </summary>
        private static string FormatUtcTimestamp(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture
            );
        }

        /// <summary>
        /// Rejects stored timestamps that do not use our UTC format.
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
                    "Season column '" + columnName +
                    "' does not contain a valid UTC round-trip timestamp."
                );
            }

            return timestamp;
        }

        /// <summary>
        /// Preserves capitalization and international text while rejecting
        /// missing values, surrounding whitespace, and control characters.
        /// </summary>
        private static void RequireText(
            string value,
            string parameterName
        )
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