using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using PokeHunter.Catalogue;
using PokeHunter.Configuration;
using PokeHunter.Encounters;
using PokeHunter.Gameplay;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class RedemptionProcessingCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
    private const string UserId = "fixture-kapteinole";
    private const string SeasonId = "processing-season";

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

            CatalogueIndex catalogue = CreateTestCatalogue(
                cataloguePath
            );

            var acceptedAt = new DateTimeOffset(
                2026, 10, 24, 12, 0, 0, TimeSpan.Zero
            );

            SeasonsConfig seasons = CreateSeasons();

            var events = new EventsConfig
            {
                SchemaVersion = 1,
                TimeZone = "Europe/Oslo",
                Events = new List<EventDefinition>()
            };

            EligibleCataloguePool pool = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                acceptedAt
            );

            var rarityWeights = new Dictionary<string, double>
            {
                { "common", 10 },
                { "uncommon", 6 },
                { "rare", 3 },
                { "ultraRare", 1 }
            };

            IReadOnlyList<EncounterCategory> normalCategories =
                EncounterCategoryBuilder.Build(
                    catalogue,
                    pool,
                    false,
                    1,
                    rarityWeights
                );

            IReadOnlyList<EncounterCategory> shinyCategories =
                EncounterCategoryBuilder.Build(
                    catalogue,
                    pool,
                    true,
                    1,
                    rarityWeights
                );

            Require(
                normalCategories.Count == 1 &&
                shinyCategories.Count == 1 &&
                normalCategories[0].Id == "electric" &&
                shinyCategories[0].Id == "electric",
                "The Pikachu fixture should have one category per shiny state."
            );

            directory = Path.Combine(
                Path.GetTempPath(),
                "PokeHunter-Processing-" +
                Guid.NewGuid().ToString("N")
            );

            Directory.CreateDirectory(directory);

            string databasePath = Path.Combine(
                directory,
                "processing.db"
            );

            DatabaseInitializer.Initialize(databasePath);

            // Synthetic identity in this temporary database only.
            new TrainerRepository(databasePath).SaveProfile(
                UserId,
                "kapteinole",
                "KapteinOle"
            );

            SeasonDefinition season = seasons.Seasons[0];

            new SeasonRepository(databasePath).Register(
                season.Id,
                season.Name,
                season.StartsAt,
                season.EndsAtExclusive,
                season.UnlockedGenerations
            );

            var random = new ControlledRandomSource
            {
                Value = 0.75
            };

            var processor = new RedemptionProcessor(
                databasePath,
                catalogue,
                random,
                () => acceptedAt.AddSeconds(1)
            );

            var collections = new CollectionRepository(databasePath);
            var redemptions = new RedemptionRepository(databasePath);

            ShinyConfig shinyConfig = CreateShinyConfig();

            // Reuse one processor and one prepared eligibility context.
            // Every fixture uses the same fixed acceptance time.
            // Cooldowns are deliberately outside this test's scope.
            Func<string, RedemptionSaveResult> process =
                redemptionId => processor.Process(
                    redemptionId,
                    UserId,
                    SeasonId,
                    null,
                    acceptedAt,
                    shinyConfig,
                    SubscriptionTier.None,
                    normalCategories,
                    shinyCategories
                );

            // With no ownership and a high shiny draw, the first encounter
            // must be a new normal Pikachu.
            RedemptionSaveResult first = process("processing-first");

            CollectionSnapshot afterFirst =
                collections.ReadSnapshot(UserId, SeasonId);

            Require(
                first.WasCreated &&
                first.Redemption.Succeeded &&
                first.Redemption.IsShiny == false &&
                first.Redemption.AttemptsUsed == 1 &&
                afterFirst.UniqueNormalEntries == 1 &&
                afterFirst.UniqueShinyEntries == 0 &&
                afterFirst.ConsecutiveFailedRedemptions == 0,
                "The first processed redemption did not save a normal catch."
            );

            Require(
                random.Calls > 0 &&
                CountResults(databasePath) == 1,
                "The first redemption did not roll and save exactly one result."
            );

            StoredRedemption reread =
                new RedemptionRepository(databasePath).FindById(
                    "processing-first"
                );

            Require(
                reread != null &&
                reread.EntryId == first.Redemption.EntryId,
                "The processed catch could not be read through another repository."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Collection loading, encounter rolling, " +
                "and saving produced a persistent normal catch."
            );

            // Replaying the same identity must return before random draws.
            int callsBeforeReplay = random.Calls;

            RedemptionSaveResult replay = process("processing-first");

            Require(
                !replay.WasCreated &&
                replay.Redemption.EntryId == first.Redemption.EntryId &&
                random.Calls == callsBeforeReplay &&
                CountResults(databasePath) == 1,
                "Repeated delivery rolled again or inserted another result."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Repeated delivery returned the saved " +
                "catch without consuming randomness."
            );

            // The same controlled draws select the same regular gender.
            // The other gender is still missing, so the category stays open,
            // and the owned gender can produce a duplicate.
            RedemptionSaveResult failedOnce = process(
                "processing-failure-1"
            );

            Require(
                failedOnce.WasCreated &&
                !failedOnce.Redemption.Succeeded &&
                failedOnce.Redemption.AvailableAttempts == 1 &&
                failedOnce.Redemption.AttemptsUsed == 1 &&
                failedOnce.Redemption.NextFailureStreak == 1,
                "The first duplicate redemption did not produce streak one."
            );

            RedemptionSaveResult failedTwice = process(
                "processing-failure-2"
            );

            CollectionSnapshot afterFailures =
                collections.ReadSnapshot(UserId, SeasonId);

            Require(
                failedTwice.WasCreated &&
                !failedTwice.Redemption.Succeeded &&
                failedTwice.Redemption.AvailableAttempts == 2 &&
                failedTwice.Redemption.AttemptsUsed == 2 &&
                afterFailures.ConsecutiveFailedRedemptions == 2 &&
                afterFailures.UniqueNormalEntries == 1 &&
                afterFailures.UniqueShinyEntries == 0 &&
                CountResults(databasePath) == 3,
                "Saved failure progress did not control the next attempt budget."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Duplicate encounters saved failures, " +
                "and the next redemption used the increased attempt budget."
            );

            // Replay the original catch after progress has advanced.
            // Its old successful outcome must not reset the current streak.
            callsBeforeReplay = random.Calls;

            RedemptionSaveResult oldCatchReplay = process(
                "processing-first"
            );

            Require(
                !oldCatchReplay.WasCreated &&
                random.Calls == callsBeforeReplay &&
                collections.ReadSnapshot(
                    UserId,
                    SeasonId
                ).ConsecutiveFailedRedemptions == 2 &&
                CountResults(databasePath) == 3,
                "Replaying an older catch changed newer progress."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Replaying an old catch preserved " +
                "the current failure streak and consumed no randomness."
            );

            // A technical error must propagate without being saved
            // as an ordinary failed redemption.
            int callsBeforeError = random.Calls;
            random.ThrowNext = true;

            ExpectRandomFailure(
                () => process("processing-technical-error")
            );

            Require(
                random.Calls == callsBeforeError + 1 &&
                redemptions.FindById("processing-technical-error") == null &&
                CountResults(databasePath) == 3 &&
                collections.ReadSnapshot(
                    UserId,
                    SeasonId
                ).ConsecutiveFailedRedemptions == 2,
                "A technical failure saved a result, changed progress, " +
                "or triggered an automatic retry."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: A technical error propagated without " +
                "saving failure, changing progress, or automatically rerolling."
            );

            // Zero is below our positive shiny probability.
            // This is a deliberate test rerun after removing the injected
            // error, not an automatic production recovery policy.
            random.Value = 0;

            RedemptionSaveResult recovered = process(
                "processing-technical-error"
            );

            CollectionSnapshot afterShiny =
                collections.ReadSnapshot(UserId, SeasonId);

            Require(
                recovered.WasCreated &&
                recovered.Redemption.Succeeded &&
                recovered.Redemption.IsShiny == true &&
                recovered.Redemption.AvailableAttempts == 3 &&
                recovered.Redemption.AttemptsUsed == 1 &&
                afterShiny.ConsecutiveFailedRedemptions == 0 &&
                afterShiny.UniqueNormalEntries == 1 &&
                afterShiny.UniqueShinyEntries == 1 &&
                afterShiny.TotalUniqueEntries == 2 &&
                CountResults(databasePath) == 4,
                "The subsequent shiny catch did not save or reset progress correctly."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: After removing the injected error, " +
                "a shiny catch saved, stopped the session early, and reset the streak."
            );

            // A failed redemption replay must also leave newer success intact.
            callsBeforeReplay = random.Calls;

            RedemptionSaveResult oldFailureReplay = process(
                "processing-failure-1"
            );

            Require(
                !oldFailureReplay.WasCreated &&
                !oldFailureReplay.Redemption.Succeeded &&
                random.Calls == callsBeforeReplay &&
                collections.ReadSnapshot(
                    UserId,
                    SeasonId
                ).ConsecutiveFailedRedemptions == 0 &&
                CountResults(databasePath) == 4,
                "Replaying an older failure changed newer successful progress."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Replaying an old failure preserved " +
                "newer successful progress without consuming randomness."
            );

            CPH.LogInfo(
                "[PokéHunter] All redemption processing checks passed."
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[PokéHunter] Redemption processing check failed: " +
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
    /// Builds a small validated catalogue using real generated entries.
    /// The production catalogue file is never modified.
    /// </summary>
    private static CatalogueIndex CreateTestCatalogue(string path)
    {
        var source = new CatalogueIndex(
            CatalogueLoader.Load(path)
        );

        var ids = new List<string>
        {
            "pikachu:male:normal",
            "pikachu:female:normal",
            "pikachu:male:shiny",
            "pikachu:female:shiny"
        };

        var entries = new List<CatalogueEntry>();

        foreach (string id in ids)
        {
            CatalogueEntry entry = source.FindEntry(id);

            Require(
                entry != null,
                "The test catalogue is missing " + id + "."
            );

            entries.Add(entry);
        }

        var document = new CatalogueDocument
        {
            SchemaVersion = 1,
            SampleOnly = true,
            Scope = "merged-collection-entries",
            Sources = new List<string>
            {
                "redemption-processing-fixture"
            },
            Entries = entries,
            EncounterGroups = new List<CatalogueEncounterGroup>
            {
                new CatalogueEncounterGroup
                {
                    Id = "pikachu",
                    SpeciesId = "pikachu",
                    NationalDexNumber = 25,
                    RarityClass = "common",
                    EntryIds = ids
                }
            }
        };

        return new CatalogueIndex(document);
    }

    private static SeasonsConfig CreateSeasons()
    {
        return new SeasonsConfig
        {
            SchemaVersion = 1,
            TimeZone = "Europe/Oslo",
            Seasons = new List<SeasonDefinition>
            {
                new SeasonDefinition
                {
                    Id = SeasonId,
                    Name = "Processing Fixture Season",
                    StartsAt = new DateTimeOffset(
                        2026, 1, 1, 0, 0, 0, TimeSpan.Zero
                    ),
                    EndsAtExclusive = new DateTimeOffset(
                        2027, 1, 1, 0, 0, 0, TimeSpan.Zero
                    ),
                    UnlockedGenerations = new List<int> { 1 }
                }
            }
        };
    }

    /// <summary>
    /// Fixed settings keep the test independent of creator customization.
    /// The actual probability calculation still uses the production code.
    /// </summary>
    private static ShinyConfig CreateShinyConfig()
    {
        return new ShinyConfig
        {
            BaseOddsDenominator = 8192,
            CollectionBonus = new CollectionBonusConfig
            {
                UniqueEntriesPerMilestone = 50,
                BonusPerUniqueEntryMilestone = 0.1,
                UniqueShiniesPerMilestone = 10,
                BonusPerShinyMilestone = 0.05
            },
            SubscriberMultipliers = new SubscriberMultiplierConfig
            {
                None = 1,
                Prime = 2,
                Tier1 = 2,
                Tier2 = 2.5,
                Tier3 = 3.5
            }
        };
    }

    /// <summary>
    /// Predictable test-only randomness with observable call counts.
    /// Never use this source for real channel-point encounters.
    /// </summary>
    private sealed class ControlledRandomSource : IRandomSource
    {
        public double Value { get; set; }
        public bool ThrowNext { get; set; }
        public int Calls { get; private set; }

        public double NextDouble()
        {
            Calls++;

            if (ThrowNext)
            {
                ThrowNext = false;

                throw new InvalidOperationException(
                    "Deliberate processing random failure."
                );
            }

            return Value;
        }
    }

    private static void ExpectRandomFailure(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            if (exception.Message !=
                "Deliberate processing random failure.")
            {
                throw;
            }

            return;
        }

        throw new InvalidOperationException(
            "The deliberate random-source failure was not propagated."
        );
    }

    private static long CountResults(string databasePath)
    {
        var settings = new SQLiteConnectionStringBuilder
        {
            DataSource = databasePath,
            Version = 3,
            ReadOnly = true,
            FailIfMissing = true,
            Pooling = false,
            DefaultTimeout = 5
        };

        using (var connection = new SQLiteConnection(
            settings.ConnectionString
        ))
        {
            connection.Open();

            using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    "SELECT COUNT(*) FROM redemption_results;";

                return Convert.ToInt64(command.ExecuteScalar());
            }
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}