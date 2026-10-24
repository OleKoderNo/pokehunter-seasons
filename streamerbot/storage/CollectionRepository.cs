using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Globalization;
using System.IO;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Reads collection state from an initialized database.
    /// Does not roll, save catches, create trainers, or run migrations.
    /// </summary>
    public sealed class CollectionRepository
    {
        private readonly string databasePath;

        public CollectionRepository(string databasePath)
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
        /// Reads ownership, progress, and lifetime unique counts in one
        /// read transaction. Unknown trainers/seasons are setup errors.
        /// This snapshot does not reserve a redemption or lock future rolls.
        /// </summary>
        public CollectionSnapshot ReadSnapshot(string twitchUserId, string seasonId)
        {
            RequireText(twitchUserId, nameof(twitchUserId));
            RequireText(seasonId, nameof(seasonId));

            using (var connection = OpenConnection(true))
            {
                // A deferred read transaction keeps every SELECT on the
                // same database snapshot, without reserving a writer slot.
                Execute(connection, "BEGIN;");
                try
                {
                    RequireTrainer(connection, twitchUserId);
                    string canonicalSeason = ResolveSeasonId(connection, seasonId);
                    List<string> owned = ReadOwned(connection, twitchUserId, canonicalSeason);
                    int streak = ReadStreak(connection, twitchUserId, canonicalSeason);
                    int normal;
                    int shiny;
                    ReadLifetimeCounts(connection, twitchUserId, out normal, out shiny);

                    var snapshot = new CollectionSnapshot(
                        twitchUserId, canonicalSeason, owned, streak, normal, shiny
                    );
                    Execute(connection, "COMMIT;");
                    return snapshot;
                }
                catch (Exception readException)
                {
                    try { Execute(connection, "ROLLBACK;"); }
                    catch (Exception rollbackException)
                    {
                        throw new AggregateException(
                            "Reading collection state failed, and rollback also failed.",
                            readException, rollbackException
                        );
                    }
                    throw;
                }
            }
        }

        private static void RequireTrainer(SQLiteConnection connection, string userId)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT 1 FROM trainers WHERE twitch_user_id = @user;";
                command.Parameters.AddWithValue("@user", userId);
                if (command.ExecuteScalar() == null)
                {
                    throw new InvalidDataException("Register the trainer before reading collection state.");
                }
            }
        }

        /// <summary>
        /// Match SeasonRepository's Unicode-aware identity rules, then use
        /// the stored spelling in indexed catch/progress queries.
        /// Only the small season registry is scanned, not catch history.
        /// </summary>
        private static string ResolveSeasonId(SQLiteConnection connection, string requested)
        {
            string found = null;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "SELECT season_id FROM seasons;";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string candidate = reader.GetString(0);
                        if (!string.Equals(candidate, requested, StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (found != null)
                        {
                            throw new InvalidDataException("Multiple stored seasons match the requested ID.");
                        }
                        found = candidate;
                    }
                }
            }
            if (found == null)
            {
                throw new InvalidDataException("Register the season before reading collection state.");
            }
            return found;
        }

        private static List<string> ReadOwned(SQLiteConnection connection, string user, string season)
        {
            var entries = new List<string>();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT entry_id FROM redemption_results
                    WHERE twitch_user_id = @user AND season_id = @season AND outcome = 'caught'
                    ORDER BY entry_id COLLATE BINARY;";
                command.Parameters.AddWithValue("@user", user);
                command.Parameters.AddWithValue("@season", season);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read()) { entries.Add(reader.GetString(0)); }
                }
            }
            return entries;
        }

        private static int ReadStreak(SQLiteConnection connection, string user, string season)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT consecutive_failed_redemptions
                    FROM trainer_season_progress WHERE twitch_user_id = @user AND season_id = @season;";
                command.Parameters.AddWithValue("@user", user);
                command.Parameters.AddWithValue("@season", season);
                object value = command.ExecuteScalar();
                // A registered trainer can have no progress yet in this season.
                if (value == null) { return 0; }
                long streak = Convert.ToInt64(value, CultureInfo.InvariantCulture);
                if (streak < 0 || streak > int.MaxValue)
                {
                    throw new InvalidDataException("Stored failure streak is outside the supported range.");
                }
                return (int)streak;
            }
        }

        /// <summary>
        /// Group by permanent entry ID across every season. Recatches count
        /// once; shiny identities contribute to both total and shiny bonuses.
        /// Reject conflicting shiny flags rather than double-counting an ID.
        /// </summary>
        private static void ReadLifetimeCounts(
            SQLiteConnection connection, string user, out int normal, out int shiny)
        {
            normal = 0;
            shiny = 0;
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"SELECT entry_id, MIN(is_shiny), MAX(is_shiny)
                    FROM redemption_results WHERE twitch_user_id = @user AND outcome = 'caught'
                    GROUP BY entry_id;";
                command.Parameters.AddWithValue("@user", user);
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        long minimum = reader.GetInt64(1);
                        long maximum = reader.GetInt64(2);
                        if (minimum != maximum || (minimum != 0 && minimum != 1))
                        {
                            throw new InvalidDataException("Conflicting shiny flags for stored entry '" + reader.GetString(0) + "'.");
                        }
                        checked
                        {
                            if (minimum == 1) { shiny++; }
                            else { normal++; }
                        }
                    }
                }
            }
        }

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
                        "Collection storage requires an initialized " +
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