using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class CollectionStorageCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string Timestamp = "2026-10-24T00:00:00.0000000+00:00";

#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        string directory = Path.Combine(Path.GetTempPath(),
            "PokeHunter-Collection-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "collections.db");
            DatabaseInitializer.Initialize(path);
            var trainers = new TrainerRepository(path);
            // Synthetic IDs in a disposable database; neither is a Twitch account.
            trainers.SaveProfile("fixture-trainer", "kapteinole", "KapteinOle");
            trainers.SaveProfile("fixture-isolation", "kapteinole", "KapteinOle");
            var seasons = new SeasonRepository(path);
            var start = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
            seasons.Register("season-a", "Fixture A", start, start.AddYears(1), new[] { 1 });
            seasons.Register("season-b", "Fixture B", start.AddYears(1), start.AddYears(2), new[] { 1, 2 });
            seasons.Register("sæson-c", "Fixture C", start.AddYears(2), start.AddYears(3), new[] { 1, 2, 3 });

            var repository = new CollectionRepository(path);
            CollectionSnapshot empty = repository.ReadSnapshot("fixture-trainer", "SEASON-A");
            Require(empty.SeasonId == "season-a" && empty.SeasonalEntryIds.Count == 0 &&
                empty.TotalUniqueEntries == 0 && empty.ConsecutiveFailedRedemptions == 0,
                "A registered trainer's empty collection was incorrect.");
            CPH.LogInfo("[PokéHunter] PASS: Empty collections returned zero counts and a canonical season ID.");

            ExpectInvalid(() => repository.ReadSnapshot("absent", "season-a"), "Register the trainer");
            ExpectInvalid(() => repository.ReadSnapshot("fixture-trainer", "absent"), "Register the season");
            CPH.LogInfo("[PokéHunter] PASS: Unknown trainers and seasons were rejected.");

            var settings = new SQLiteConnectionStringBuilder
            {
                DataSource = path,
                Version = 3,
                FailIfMissing = true,
                Pooling = false,
                DefaultTimeout = 5
            };
            using (var connection = new SQLiteConnection(settings.ConnectionString))
            {
                connection.Open();
                ExecuteSql(connection, "PRAGMA foreign_keys = ON;");
                Require(Scalar(connection, "PRAGMA foreign_keys;") == 1, "Foreign keys are disabled.");
                // Fixture writes intentionally bypass the future save repository.
                // These tests exercise reading, not result validation or atomic saving.
                Insert(connection, new ResultRow { Id = "a-normal" });
                Insert(connection, new ResultRow { Id = "a-shiny", Entry = "pikachu:male:shiny", Shiny = 1 });
                Insert(connection, new ResultRow { Id = "b-repeat", Season = "season-b" });
                Insert(connection, new ResultRow { Id = "b-new", Season = "season-b", Entry = "bulbasaur:unspecified:normal", Group = "bulbasaur", Category = "grass" });
                Insert(connection, new ResultRow
                {
                    Id = "a-failure",
                    Outcome = "failed",
                    Next = 1,
                    Entry = null,
                    Shiny = null,
                    Category = null,
                    Group = null,
                    Snapshot = null
                });
                ExecuteSql(connection, @"INSERT INTO trainer_season_progress
                    (twitch_user_id, season_id, consecutive_failed_redemptions, updated_at_utc)
                    VALUES ('fixture-trainer', 'season-a', 1, '2026-10-24T00:00:00.0000000+00:00');");
                Insert(connection, new ResultRow
                {
                    Id = "isolation",
                    User = "fixture-isolation",
                    Entry = "other:normal"
                });

                CollectionSnapshot first = new CollectionRepository(path).ReadSnapshot("fixture-trainer", "season-a");
                Require(first.UniqueNormalEntries == 2 && first.UniqueShinyEntries == 1 &&
                    first.TotalUniqueEntries == 3 && first.SeasonalEntryIds.Count == 2 &&
                    first.ConsecutiveFailedRedemptions == 1,
                    "Seasonal ownership or lifetime unique counts were incorrect.");
                Require(first.SeasonalEntryIds[0] == "pikachu:male:normal" &&
                    first.SeasonalEntryIds[1] == "pikachu:male:shiny", "Unexpected seasonal identities.");
                CollectionSnapshot second = repository.ReadSnapshot("fixture-trainer", "SEASON-B");
                Require(second.SeasonalEntryIds.Count == 2 && second.ConsecutiveFailedRedemptions == 0 &&
                    second.TotalUniqueEntries == 3, "Season isolation failed.");
                CPH.LogInfo("[PokéHunter] PASS: Seasonal ownership, streaks, and lifetime unique normal/shiny counts remained separate.");
                CPH.LogInfo("[PokéHunter] PASS: Recatches across seasons and failed redemptions did not inflate unique counts.");

                CollectionSnapshot other = repository.ReadSnapshot("fixture-isolation", "season-a");
                Require(other.TotalUniqueEntries == 1 && other.SeasonalEntryIds.Count == 1,
                    "Trainer isolation failed.");
                CollectionSnapshot international = repository.ReadSnapshot("fixture-trainer", "SÆSON-C");
                Require(international.SeasonId == "sæson-c" && international.SeasonalEntryIds.Count == 0 &&
                    international.TotalUniqueEntries == 3, "International season identity failed.");
                CPH.LogInfo("[PokéHunter] PASS: Different trainers and international season IDs were handled correctly.");

                var list = first.SeasonalEntryIds as IList<string>;
                Require(list != null && list.IsReadOnly, "Snapshot entry IDs are mutable.");
                bool blocked = false;
                try { list.Add("unexpected"); }
                catch (NotSupportedException) { blocked = true; }
                Require(blocked, "Snapshot entries accepted mutation.");
                ExecuteSql(connection, "UPDATE trainer_season_progress SET consecutive_failed_redemptions = 2;");
                Require(first.ConsecutiveFailedRedemptions == 1 &&
                    repository.ReadSnapshot("fixture-trainer", "season-a").ConsecutiveFailedRedemptions == 2,
                    "A later write changed a previously returned snapshot.");
                CPH.LogInfo("[PokéHunter] PASS: Returned snapshots were read-only and independent of later database changes.");

                // SQL permits this metadata conflict across seasons. The reader must
                // reject it rather than count one permanent identity in both totals.
                Insert(connection, new ResultRow
                {
                    Id = "conflicting-shiny",
                    Season = "sæson-c",
                    Shiny = 1
                });
                ExpectInvalid(() => repository.ReadSnapshot("fixture-trainer", "season-a"), "Conflicting shiny flags");
                ExecuteSql(connection, "DELETE FROM redemption_results WHERE redemption_id = 'conflicting-shiny';");
                Require(repository.ReadSnapshot("fixture-trainer", "season-a").TotalUniqueEntries == 3,
                    "Reads did not recover after the deliberately conflicting fixture was removed.");
                CPH.LogInfo("[PokéHunter] PASS: Conflicting shiny metadata was rejected and subsequent valid reads succeeded.");
            }
            CPH.LogInfo("[PokéHunter] All collection storage checks passed.");
            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError("[PokéHunter] Collection storage check failed: " + exception);
            return false;
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            catch (Exception exception)
            {
                CPH.LogWarn("[PokéHunter] Could not remove fixture folder '" + directory + "': " + exception.Message);
            }
        }
    }

    private static void ExpectInvalid(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException exception)
        {
            Require(exception.Message.IndexOf(message, StringComparison.Ordinal) >= 0,
                "Unexpected rejection: " + exception.Message);
            return;
        }
        throw new InvalidOperationException("Expected rejection: " + message);
    }

    // Minimal raw SQL fixture records, not production catalogue snapshots.
    private sealed class ResultRow
    {
        public string Id;
        public string User = "fixture-trainer";
        public string Season = "season-a";
        public string Outcome = "caught";
        public int Previous = 0;
        public int Available = 1;
        public int Used = 1;
        public int Next = 0;
        public string Entry = "pikachu:male:normal";
        public int? Shiny = 0;
        public string Category = "electric";
        public string Group = "pikachu";
        public string Snapshot = "{}";
    }

    private static void Insert(SQLiteConnection connection, ResultRow row)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"INSERT INTO redemption_results (
                redemption_id, twitch_user_id, season_id, accepted_at_utc,
                completed_at_utc, event_id, outcome, previous_failure_streak,
                available_attempts, attempts_used, next_failure_streak,
                entry_id, is_shiny, category_id, group_id, entry_snapshot_json
            ) VALUES (
                @id, @user, @season, @time, @time, NULL, @outcome, @previous,
                @available, @used, @next, @entry, @shiny, @category, @group, @snapshot
            );";
            command.Parameters.AddWithValue("@id", row.Id);
            command.Parameters.AddWithValue("@user", row.User);
            command.Parameters.AddWithValue("@season", row.Season);
            command.Parameters.AddWithValue("@time", Timestamp);
            command.Parameters.AddWithValue("@outcome", row.Outcome);
            command.Parameters.AddWithValue("@previous", row.Previous);
            command.Parameters.AddWithValue("@available", row.Available);
            command.Parameters.AddWithValue("@used", row.Used);
            command.Parameters.AddWithValue("@next", row.Next);
            command.Parameters.AddWithValue("@entry", (object)row.Entry ?? DBNull.Value);
            command.Parameters.AddWithValue("@shiny", (object)row.Shiny ?? DBNull.Value);
            command.Parameters.AddWithValue("@category", (object)row.Category ?? DBNull.Value);
            command.Parameters.AddWithValue("@group", (object)row.Group ?? DBNull.Value);
            command.Parameters.AddWithValue("@snapshot", (object)row.Snapshot ?? DBNull.Value);
            command.ExecuteNonQuery();
        }
    }

    private static void ExecuteSql(SQLiteConnection connection, string sql)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    private static long Scalar(SQLiteConnection connection, string sql)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;
            return Convert.ToInt64(command.ExecuteScalar());
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}