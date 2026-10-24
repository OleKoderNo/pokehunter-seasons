using System;
using System.Data.SQLite;
using System.IO;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class DatabaseRedemptionSchemaCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
    // These are isolated SQL fixtures, not Twitch users or live catches.
    private const string Timestamp = "2026-10-23T12:00:00.0000000+00:00";

#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        string directory = Path.Combine(
            Path.GetTempPath(),
            "PokeHunter-RedemptionSchema-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "constraints.db");
            Require(DatabaseInitializer.Initialize(path) == 4,
                "The schema fixture must reach version 4.");

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
                Require(Scalar(connection, "PRAGMA foreign_keys;") == 1,
                    "Foreign keys must be enabled on the test connection.");

                SeedParents(connection);

                Insert(connection, new ResultRow { Id = "first" });
                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "first", Entry = "other:normal" }),
                    "UNIQUE constraint failed: redemption_results.redemption_id");
                CPH.LogInfo("[PokéHunter] PASS: Duplicate redemption IDs were rejected.");

                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "duplicate-entry" }),
                    "UNIQUE constraint failed: redemption_results.twitch_user_id, redemption_results.season_id, redemption_results.entry_id");
                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "case-duplicate", Season = "SEASON-A" }),
                    "UNIQUE constraint failed: redemption_results.twitch_user_id, redemption_results.season_id, redemption_results.entry_id");

                Insert(connection, new ResultRow { Id = "next-season", Season = "season-b" });
                Insert(connection, new ResultRow { Id = "shiny", Entry = "pikachu:male:shiny", Shiny = 1 });
                CPH.LogInfo("[PokéHunter] PASS: Seasonal duplicates were rejected; another season and shiny identity were accepted.");

                // A failure contains no catch fields, exhausts its budget,
                // and increments the previous streak exactly once.
                var failed = new ResultRow
                {
                    Id = "failure",
                    Outcome = "failed",
                    Previous = 2,
                    Available = 3,
                    Used = 3,
                    Next = 3,
                    Entry = null,
                    Shiny = null,
                    Category = null,
                    Group = null,
                    Snapshot = null
                };
                Insert(connection, failed);
                failed.Id = "another-failure";
                Insert(connection, failed);

                failed.Id = "failure-with-catch";
                failed.Entry = "unexpected";
                ExpectConstraint(connection, () => Insert(connection, failed), "CHECK constraint failed");
                failed.Entry = null;
                failed.Used = 2;
                ExpectConstraint(connection, () => Insert(connection, failed), "CHECK constraint failed");
                failed.Used = 3;
                failed.Next = 4;
                ExpectConstraint(connection, () => Insert(connection, failed), "CHECK constraint failed");

                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "missing-entry", Entry = null }),
                    "CHECK constraint failed");
                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "missing-shiny", Shiny = null }),
                    "CHECK constraint failed");
                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "missing-snapshot", Snapshot = null }),
                    "CHECK constraint failed");
                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "bad-shiny", Shiny = 2 }),
                    "CHECK constraint failed");
                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "bad-budget", Available = 2 }),
                    "CHECK constraint failed");
                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "bad-success-streak", Next = 1 }),
                    "CHECK constraint failed");
                CPH.LogInfo("[PokéHunter] PASS: Catch fields, attempt budgets, and result streak rules were enforced.");

                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "missing-trainer", User = "absent" }),
                    "FOREIGN KEY constraint failed");
                ExpectConstraint(connection,
                    () => Insert(connection, new ResultRow { Id = "missing-season", Season = "absent" }),
                    "FOREIGN KEY constraint failed");
                CPH.LogInfo("[PokéHunter] PASS: Redemption results required existing trainers and seasons.");

                ExecuteSql(connection, @"INSERT INTO trainer_season_progress
                    (twitch_user_id, season_id, consecutive_failed_redemptions, updated_at_utc)
                    VALUES ('fixture-trainer', 'season-a', 0, 'fixture-time');");
                ExpectConstraint(connection, () => ExecuteSql(connection,
                    @"INSERT INTO trainer_season_progress VALUES
                    ('fixture-trainer', 'SEASON-A', 0, 'fixture-time');"),
                    "UNIQUE constraint failed: trainer_season_progress.twitch_user_id, trainer_season_progress.season_id");
                ExpectConstraint(connection, () => ExecuteSql(connection,
                    "UPDATE trainer_season_progress SET consecutive_failed_redemptions = -1;"),
                    "CHECK constraint failed");
                ExpectConstraint(connection, () => ExecuteSql(connection,
                    @"INSERT INTO trainer_season_progress VALUES
                    ('absent', 'season-a', 0, 'fixture-time');"),
                    "FOREIGN KEY constraint failed");
                Require(Scalar(connection,
                    "SELECT consecutive_failed_redemptions FROM trainer_season_progress;") == 0,
                    "A rejected update changed the stored streak.");
                CPH.LogInfo("[PokéHunter] PASS: Seasonal progress enforced identity, parent references, and nonnegative streaks.");

                // Exercise SQLite transaction behavior. This is not yet
                // a test of the future repository's save implementation.
                long before = Scalar(connection, "SELECT COUNT(*) FROM redemption_results;");
                ExecuteSql(connection, "BEGIN IMMEDIATE;");
                try
                {
                    Insert(connection, new ResultRow { Id = "rolled-back", Entry = "other:normal" });
                    ExecuteSql(connection,
                        "UPDATE trainer_season_progress SET consecutive_failed_redemptions = 7;");
                }
                finally
                {
                    ExecuteSql(connection, "ROLLBACK;");
                }

                Require(Scalar(connection, "SELECT COUNT(*) FROM redemption_results;") == before &&
                    Scalar(connection, "SELECT consecutive_failed_redemptions FROM trainer_season_progress;") == 0,
                    "Rollback did not restore both result and progress.");
                CPH.LogInfo("[PokéHunter] PASS: Explicit rollback restored both result and progress changes.");
            }

            CPH.LogInfo("[PokéHunter] All redemption schema checks passed.");
            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError("[PokéHunter] Redemption schema check failed: " + exception);
            return false;
        }
        finally
        {
            try
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            catch (Exception exception)
            {
                CPH.LogWarn("[PokéHunter] Could not remove fixture folder '" + directory + "': " + exception.Message);
            }
        }
    }

    private static void SeedParents(SQLiteConnection connection)
    {
        ExecuteSql(connection, @"INSERT INTO trainers
            (twitch_user_id, login_name, display_name, created_at_utc, updated_at_utc)
            VALUES ('fixture-trainer', 'kapteinole', 'KapteinOle', 'fixture-time', 'fixture-time');
            INSERT INTO seasons
            (season_id, name, starts_at_utc, ends_at_exclusive_utc, created_at_utc)
            VALUES ('season-a', 'Fixture A', '2026-01-01T00:00:00Z', '2027-01-01T00:00:00Z', 'fixture-time');
            INSERT INTO seasons
            (season_id, name, starts_at_utc, ends_at_exclusive_utc, created_at_utc)
            VALUES ('season-b', 'Fixture B', '2027-01-01T00:00:00Z', '2028-01-01T00:00:00Z', 'fixture-time');");
    }

    // Minimal SQL fixtures, deliberately not complete catalogue snapshots.
    // JSON validity and identity agreement belong to repository checks.
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

    private static void ExpectConstraint(SQLiteConnection connection, Action action, string expectedMessage)
    {
        long resultsBefore = Scalar(connection, "SELECT COUNT(*) FROM redemption_results;");
        long progressBefore = Scalar(connection, "SELECT COUNT(*) FROM trainer_season_progress;");
        bool rejected = false;
        try { action(); }
        catch (SQLiteException exception)
        {
            Require(exception.Message.IndexOf(expectedMessage, StringComparison.OrdinalIgnoreCase) >= 0,
                "Unexpected SQLite failure: " + exception.Message);
            rejected = true;
        }
        Require(rejected, "Expected SQLite rejection: " + expectedMessage);
        Require(Scalar(connection, "SELECT COUNT(*) FROM redemption_results;") == resultsBefore &&
            Scalar(connection, "SELECT COUNT(*) FROM trainer_season_progress;") == progressBefore,
            "A rejected operation changed row counts.");
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
