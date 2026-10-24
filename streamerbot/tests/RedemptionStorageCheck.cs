using System;
using System.Data.SQLite;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PokeHunter.Catalogue;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class RedemptionStorageCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string UserId = "fixture-kapteinole";
    private const string OtherUserId = "fixture-isolation";

#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        string directory = null;

        try
        {
            string projectRoot;

            if (!CPH.TryGetArg("pokeHunterProjectRoot", out projectRoot) ||
                string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new ArgumentException(
                    "Set pokeHunterProjectRoot to your absolute repository path."
                );
            }

            string cataloguePath = Path.Combine(
                projectRoot,
                "data",
                "catalogue",
                "merged-entries.json"
            );

            var catalogue = new CatalogueIndex(
                CatalogueLoader.Load(cataloguePath)
            );

            CatalogueEntry pikachu = RequiredEntry(
                catalogue,
                "pikachu:male:normal"
            );

            CatalogueEntry shinyPikachu = RequiredEntry(
                catalogue,
                "pikachu:male:shiny"
            );

            CatalogueEntry bulbasaur = RequiredEntry(
                catalogue,
                "bulbasaur:male:normal"
            );

            CatalogueEntry charmander = RequiredEntry(
                catalogue,
                "charmander:male:normal"
            );

            directory = Path.Combine(
                Path.GetTempPath(),
                "PokeHunter-RedemptionStorage-" +
                Guid.NewGuid().ToString("N")
            );

            Directory.CreateDirectory(directory);

            string databasePath = Path.Combine(
                directory,
                "redemptions.db"
            );

            DatabaseInitializer.Initialize(databasePath);
            RegisterFixtures(databasePath);

            var collections = new CollectionRepository(databasePath);
            var redemptions = new RedemptionRepository(databasePath);

            var acceptedAt = new DateTimeOffset(
                2026, 10, 24, 12, 0, 0, TimeSpan.Zero
            );

            Require(
                redemptions.FindById("unknown") == null,
                "An unknown redemption did not return null."
            );

            // First failure: one available attempt, next streak one.
            CollectionSnapshot empty = collections.ReadSnapshot(
                UserId,
                "season-a"
            );

            RedemptionSaveRequest firstFailure =
                RedemptionSaveRequest.ForFailure(
                    "failure-1",
                    empty,
                    acceptedAt,
                    acceptedAt.AddSeconds(1),
                    null,
                    1
                );

            RedemptionSaveResult firstSaved = redemptions.Save(
                firstFailure,
                catalogue
            );

            Require(
                firstSaved.WasCreated &&
                !firstSaved.Redemption.Succeeded &&
                firstSaved.Redemption.AvailableAttempts == 1 &&
                firstSaved.Redemption.NextFailureStreak == 1,
                "The first failed redemption was saved incorrectly."
            );

            Require(
                collections.ReadSnapshot(
                    UserId,
                    "season-a"
                ).ConsecutiveFailedRedemptions == 1,
                "Failure did not update seasonal progress."
            );

            // Second failure: two attempts, next streak two.
            CollectionSnapshot afterFirstFailure =
                collections.ReadSnapshot(UserId, "season-a");

            redemptions.Save(
                RedemptionSaveRequest.ForFailure(
                    "failure-2",
                    afterFirstFailure,
                    acceptedAt.AddMinutes(1),
                    acceptedAt.AddMinutes(1).AddSeconds(1),
                    null,
                    2
                ),
                catalogue
            );

            CollectionSnapshot afterSecondFailure =
                collections.ReadSnapshot(UserId, "season-a");

            Require(
                afterSecondFailure.ConsecutiveFailedRedemptions == 2 &&
                afterSecondFailure.TotalUniqueEntries == 0,
                "Consecutive failures produced incorrect collection state."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Failed redemptions saved and " +
                "incremented the streak without awarding catches."
            );

            // Replay an older failure after progress has moved forward.
            // Returning the saved result must not restore its old streak.
            long beforeReplay = CountResults(databasePath);
            string progressBeforeReplay = ReadProgressTimestamp(
                databasePath
            );

            RedemptionSaveResult replayedFailure = redemptions.Save(
                firstFailure,
                catalogue
            );

            Require(
                !replayedFailure.WasCreated &&
                replayedFailure.Redemption.NextFailureStreak == 1 &&
                collections.ReadSnapshot(
                    UserId,
                    "season-a"
                ).ConsecutiveFailedRedemptions == 2 &&
                CountResults(databasePath) == beforeReplay &&
                ReadProgressTimestamp(databasePath) == progressBeforeReplay,
                "Replaying a failed redemption changed stored progress."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Repeated failure delivery returned " +
                "the original result without changing newer progress."
            );

            // Catch on attempt two of a three-attempt budget.
            RedemptionSaveRequest catchRequest = CreateCatch(
                "catch-pikachu",
                afterSecondFailure,
                pikachu,
                acceptedAt.AddMinutes(2),
                2
            );

            RedemptionSaveResult caught = redemptions.Save(
                catchRequest,
                catalogue
            );

            CollectionSnapshot afterCatch =
                collections.ReadSnapshot(UserId, "season-a");

            Require(
                caught.WasCreated &&
                caught.Redemption.Succeeded &&
                caught.Redemption.AvailableAttempts == 3 &&
                caught.Redemption.AttemptsUsed == 2 &&
                caught.Redemption.NextFailureStreak == 0 &&
                afterCatch.ConsecutiveFailedRedemptions == 0 &&
                afterCatch.SeasonalEntryIds.Count == 1 &&
                afterCatch.UniqueNormalEntries == 1,
                "The catch and streak reset were saved incorrectly."
            );

            StoredRedemption loaded =
                new RedemptionRepository(databasePath).FindById(
                    "catch-pikachu"
                );

            Require(
                loaded != null &&
                loaded.EntryId == pikachu.Id &&
                loaded.IsShiny == false &&
                loaded.AcceptedAtUtc == catchRequest.AcceptedAtUtc &&
                loaded.CompletedAtUtc == catchRequest.CompletedAtUtc &&
                JToken.DeepEquals(
                    JToken.Parse(loaded.EntrySnapshotJson),
                    JToken.FromObject(pikachu)
                ),
                "The saved catch did not round-trip correctly."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: A catch saved its metadata, " +
                "reset the streak, and was read through another repository."
            );

            // Advance progress again, making afterCatch stale.
            redemptions.Save(
                RedemptionSaveRequest.ForFailure(
                    "failure-after-catch",
                    afterCatch,
                    acceptedAt.AddMinutes(3),
                    acceptedAt.AddMinutes(3).AddSeconds(1),
                    null,
                    1
                ),
                catalogue
            );

            long beforeCatchReplay = CountResults(databasePath);
            string timestampBeforeCatchReplay =
                ReadProgressTimestamp(databasePath);

            RedemptionSaveResult replayedCatch = redemptions.Save(
                catchRequest,
                catalogue
            );

            Require(
                !replayedCatch.WasCreated &&
                replayedCatch.Redemption.EntryId == pikachu.Id &&
                collections.ReadSnapshot(
                    UserId,
                    "season-a"
                ).ConsecutiveFailedRedemptions == 1 &&
                CountResults(databasePath) == beforeCatchReplay &&
                ReadProgressTimestamp(databasePath) ==
                    timestampBeforeCatchReplay,
                "Replaying a catch changed newer progress."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Repeated catch delivery returned " +
                "the original catch without resetting newer progress."
            );

            RedemptionSaveRequest staleRequest = CreateCatch(
                "stale-catch",
                afterCatch,
                bulbasaur,
                acceptedAt.AddMinutes(4)
            );

            ExpectRejected<InvalidOperationException>(
                () => redemptions.Save(staleRequest, catalogue),
                "Collection state changed"
            );

            Require(
                redemptions.FindById("stale-catch") == null &&
                CountResults(databasePath) == beforeCatchReplay &&
                collections.ReadSnapshot(
                    UserId,
                    "season-a"
                ).ConsecutiveFailedRedemptions == 1,
                "A stale request changed stored data."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: A stale collection snapshot " +
                "was rejected without saving a result."
            );

            CheckRollback(
                databasePath,
                collections,
                redemptions,
                catalogue,
                bulbasaur,
                acceptedAt.AddMinutes(5)
            );

            CheckInvalidSnapshot(
                databasePath,
                collections,
                redemptions,
                catalogue,
                charmander,
                acceptedAt.AddMinutes(6)
            );

            // A fresh request cannot award an already-owned seasonal entry.
            CollectionSnapshot current = collections.ReadSnapshot(
                UserId,
                "season-a"
            );

            long beforeDuplicate = CountResults(databasePath);

            ExpectRejected<InvalidDataException>(
                () => redemptions.Save(
                    CreateCatch(
                        "duplicate-seasonal-entry",
                        current,
                        pikachu,
                        acceptedAt.AddMinutes(7)
                    ),
                    catalogue
                ),
                "already owned"
            );

            Require(
                CountResults(databasePath) == beforeDuplicate &&
                redemptions.FindById("duplicate-seasonal-entry") == null,
                "A duplicate seasonal catch was saved."
            );

            // An existing redemption ID cannot be reused by another trainer.
            CollectionSnapshot other = collections.ReadSnapshot(
                OtherUserId,
                "season-a"
            );

            ExpectRejected<InvalidDataException>(
                () => redemptions.Save(
                    RedemptionSaveRequest.ForFailure(
                        "catch-pikachu",
                        other,
                        acceptedAt,
                        acceptedAt.AddSeconds(1),
                        null,
                        1
                    ),
                    catalogue
                ),
                "different trainer or season"
            );

            Require(
                collections.ReadSnapshot(
                    OtherUserId,
                    "season-a"
                ).TotalUniqueEntries == 0 &&
                CountResults(databasePath) == beforeDuplicate,
                "A conflicting redemption identity changed stored data."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Seasonal duplicates and " +
                "redemption IDs reused by another trainer were rejected."
            );

            // Same entry in another season adds a catch, not a unique entry.
            CollectionSnapshot secondSeason = collections.ReadSnapshot(
                UserId,
                "season-b"
            );

            var nextSeasonTime = new DateTimeOffset(
                2027, 1, 2, 12, 0, 0, TimeSpan.Zero
            );

            redemptions.Save(
                CreateCatch(
                    "next-season-pikachu",
                    secondSeason,
                    pikachu,
                    nextSeasonTime
                ),
                catalogue
            );

            CollectionSnapshot afterRecatch = collections.ReadSnapshot(
                UserId,
                "season-b"
            );

            Require(
                afterRecatch.SeasonalEntryIds.Count == 1 &&
                afterRecatch.UniqueNormalEntries ==
                    secondSeason.UniqueNormalEntries &&
                afterRecatch.TotalUniqueEntries ==
                    secondSeason.TotalUniqueEntries,
                "A cross-season recatch inflated lifetime uniqueness."
            );

            redemptions.Save(
                CreateCatch(
                    "next-season-shiny",
                    afterRecatch,
                    shinyPikachu,
                    nextSeasonTime.AddMinutes(1)
                ),
                catalogue
            );

            CollectionSnapshot afterShiny = collections.ReadSnapshot(
                UserId,
                "season-b"
            );

            Require(
                afterShiny.SeasonalEntryIds.Count == 2 &&
                afterShiny.UniqueShinyEntries ==
                    afterRecatch.UniqueShinyEntries + 1 &&
                afterShiny.TotalUniqueEntries ==
                    afterRecatch.TotalUniqueEntries + 1,
                "A shiny catch did not create a separate unique entry."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Cross-season recatches and " +
                "separate shiny identities produced correct lifetime counts."
            );

            CPH.LogInfo(
                "[PokéHunter] All redemption storage checks passed."
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[PokéHunter] Redemption storage check failed: " +
                exception
            );

            return false;
        }
        finally
        {
            if (directory != null)
            {
                try
                {
                    if (Directory.Exists(directory))
                    {
                        Directory.Delete(directory, true);
                    }
                }
                catch (Exception exception)
                {
                    CPH.LogWarn(
                        "[PokéHunter] Could not remove fixture folder '" +
                        directory + "': " + exception.Message
                    );
                }
            }
        }
    }

    /// <summary>
    /// Deliberately blocks the progress update after the result insert.
    /// Both writes must roll back, and the same request must work afterward.
    /// </summary>
    private void CheckRollback(
        string databasePath,
        CollectionRepository collections,
        RedemptionRepository redemptions,
        CatalogueIndex catalogue,
        CatalogueEntry entry,
        DateTimeOffset acceptedAt
    )
    {
        CollectionSnapshot before = collections.ReadSnapshot(
            UserId,
            "season-a"
        );

        long countBefore = CountResults(databasePath);
        string timestampBefore = ReadProgressTimestamp(databasePath);

        RedemptionSaveRequest request = CreateCatch(
            "rollback-catch",
            before,
            entry,
            acceptedAt
        );

        using (var connection = OpenFixture(databasePath))
        {
            ExecuteSql(
                connection,
                @"CREATE TRIGGER reject_fixture_progress
                  BEFORE UPDATE ON trainer_season_progress
                  BEGIN
                      SELECT RAISE(
                          ABORT,
                          'deliberate progress failure'
                      );
                  END;"
            );
        }

        try
        {
            ExpectRejected<SQLiteException>(
                () => redemptions.Save(request, catalogue),
                "deliberate progress failure"
            );

            CollectionSnapshot after = collections.ReadSnapshot(
                UserId,
                "season-a"
            );

            Require(
                redemptions.FindById("rollback-catch") == null &&
                CountResults(databasePath) == countBefore &&
                after.SeasonalEntryIds.Count ==
                    before.SeasonalEntryIds.Count &&
                after.TotalUniqueEntries == before.TotalUniqueEntries &&
                after.ConsecutiveFailedRedemptions ==
                    before.ConsecutiveFailedRedemptions &&
                ReadProgressTimestamp(databasePath) == timestampBefore,
                "A failed progress update left partial changes."
            );
        }
        finally
        {
            using (var connection = OpenFixture(databasePath))
            {
                ExecuteSql(
                    connection,
                    "DROP TRIGGER IF EXISTS reject_fixture_progress;"
                );
            }
        }

        RedemptionSaveResult retry = redemptions.Save(
            request,
            catalogue
        );

        Require(
            retry.WasCreated &&
            retry.Redemption.EntryId == entry.Id &&
            collections.ReadSnapshot(
                UserId,
                "season-a"
            ).ConsecutiveFailedRedemptions == 0,
            "Saving did not recover after the deliberate failure."
        );

        CPH.LogInfo(
            "[PokéHunter] PASS: A progress-write failure rolled back " +
            "the catch and progress; retry succeeded after removing the failure."
        );
    }

    private void CheckInvalidSnapshot(
        string databasePath,
        CollectionRepository collections,
        RedemptionRepository redemptions,
        CatalogueIndex catalogue,
        CatalogueEntry entry,
        DateTimeOffset acceptedAt
    )
    {
        CollectionSnapshot current = collections.ReadSnapshot(
            UserId,
            "season-a"
        );

        // FindEntry gives an independent copy, so this deliberately changed
        // display name cannot modify the validated catalogue.
        CatalogueEntry changed = catalogue.FindEntry(entry.Id);
        changed.DisplayName = "Deliberately incorrect fixture name";

        RedemptionSaveRequest request =
            RedemptionSaveRequest.ForCatch(
                "invalid-snapshot",
                current,
                acceptedAt,
                acceptedAt.AddSeconds(1),
                null,
                1,
                entry.Id,
                entry.IsShiny,
                entry.Types[0],
                entry.EncounterGroupId,
                JsonConvert.SerializeObject(changed)
            );

        long before = CountResults(databasePath);
        string progressBefore = ReadProgressTimestamp(databasePath);

        ExpectRejected<InvalidDataException>(
            () => redemptions.Save(request, catalogue),
            "snapshot does not match"
        );

        Require(
            redemptions.FindById("invalid-snapshot") == null &&
            CountResults(databasePath) == before &&
            ReadProgressTimestamp(databasePath) == progressBefore,
            "An invalid entry snapshot changed stored data."
        );

        CPH.LogInfo(
            "[PokéHunter] PASS: Catch metadata that disagreed " +
            "with the catalogue was rejected without changes."
        );
    }

    private static void RegisterFixtures(string databasePath)
    {
        var trainers = new TrainerRepository(databasePath);

        // Synthetic identities in a disposable database.
        // These do not register real Twitch accounts or award live catches.
        trainers.SaveProfile(UserId, "kapteinole", "KapteinOle");
        trainers.SaveProfile(OtherUserId, "kapteinole", "KapteinOle");

        var seasons = new SeasonRepository(databasePath);

        var start = new DateTimeOffset(
            2026, 1, 1, 0, 0, 0, TimeSpan.Zero
        );

        seasons.Register(
            "season-a",
            "Fixture Season A",
            start,
            start.AddYears(1),
            new[] { 1 }
        );

        seasons.Register(
            "season-b",
            "Fixture Season B",
            start.AddYears(1),
            start.AddYears(2),
            new[] { 1, 2 }
        );
    }

    private static CatalogueEntry RequiredEntry(
        CatalogueIndex catalogue,
        string entryId
    )
    {
        CatalogueEntry entry = catalogue.FindEntry(entryId);

        Require(
            entry != null,
            "The test catalogue is missing entry: " + entryId
        );

        return entry;
    }

    private static RedemptionSaveRequest CreateCatch(
        string redemptionId,
        CollectionSnapshot collection,
        CatalogueEntry entry,
        DateTimeOffset acceptedAt,
        int attemptsUsed = 1
    )
    {
        return RedemptionSaveRequest.ForCatch(
            redemptionId,
            collection,
            acceptedAt,
            acceptedAt.AddSeconds(1),
            null,
            attemptsUsed,
            entry.Id,
            entry.IsShiny,
            entry.Types[0],
            entry.EncounterGroupId,
            JsonConvert.SerializeObject(entry)
        );
    }

    /// <summary>
    /// Opens only the temporary fixture database.
    /// Used to inspect rows and install/remove the deliberate failure.
    /// </summary>
    private static SQLiteConnection OpenFixture(string databasePath)
    {
        var settings = new SQLiteConnectionStringBuilder
        {
            DataSource = databasePath,
            Version = 3,
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
            ExecuteSql(connection, "PRAGMA foreign_keys = ON;");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static long CountResults(string databasePath)
    {
        using (var connection = OpenFixture(databasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT COUNT(*) FROM redemption_results;";

            return Convert.ToInt64(command.ExecuteScalar());
        }
    }

    private static string ReadProgressTimestamp(string databasePath)
    {
        using (var connection = OpenFixture(databasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = @"
                SELECT updated_at_utc
                FROM trainer_season_progress
                WHERE twitch_user_id = @user
                  AND season_id = @season;";

            command.Parameters.AddWithValue("@user", UserId);
            command.Parameters.AddWithValue("@season", "season-a");

            object value = command.ExecuteScalar();

            return value == null || value == DBNull.Value
                ? null
                : Convert.ToString(value);
        }
    }

    private static void ExecuteSql(
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
    /// An expected failure must have the intended exception type
    /// and message. Unrelated errors must fail the test.
    /// </summary>
    private static void ExpectRejected<TException>(
        Action action,
        string expectedMessage
    )
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (Exception exception)
        {
            if (!(exception is TException) ||
                exception.Message.IndexOf(
                    expectedMessage,
                    StringComparison.OrdinalIgnoreCase
                ) < 0)
            {
                throw new InvalidOperationException(
                    "Received an unexpected rejection.",
                    exception
                );
            }

            return;
        }

        throw new InvalidOperationException(
            "Expected rejection containing: " + expectedMessage
        );
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}