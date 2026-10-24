using System;
using System.Collections.Generic;
using System.IO;
using PokeHunter.Catalogue;
using PokeHunter.Configuration;
using PokeHunter.Encounters;
using PokeHunter.Gameplay;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class TwitchRedemptionCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
    // Serialize this action's work from acceptance through storage.
    // This is execution ordering, not a per-viewer cooldown.
    private static readonly object ActionLock = new object();

    private CatalogueIndex catalogue;
    private RedemptionProcessor processor;
    private string loadedDatabasePath;

#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        lock (ActionLock)
        {
            try
            {
                return ProcessRedemption();
            }
            catch (Exception exception)
            {
                // Technical failures must not trigger an automatic reroll.
                // A saved result can be recovered using its redemption ID.
                CPH.LogError(
                    "[PokéHunter] Twitch redemption processing error: " +
                    exception
                );

                return false;
            }
        }
    }

    private bool ProcessRedemption()
    {
        // These setup arguments belong to the Streamer.bot action.
        // Machine paths and account restrictions stay outside source code.
        string projectRoot = RequireStringArgument(
            "pokeHunterProjectRoot"
        );

        string allowedUserId = RequireStringArgument(
            "pokeHunterTestUserId"
        );

        string expectedRewardId = RequireStringArgument(
            "pokeHunterRewardId"
        );

        // These arguments come from the actual reward redemption trigger.
        // Never generate a replacement redemption ID for a missing argument.
        string userId = RequireStringArgument("userId");
        string rewardId = RequireStringArgument("rewardId");
        string redemptionId = RequireStringArgument("redemptionId");

        if (!string.Equals(
            rewardId,
            expectedRewardId,
            StringComparison.Ordinal
        ))
        {
            throw new InvalidDataException(
                "This action received a different reward than configured."
            );
        }

        if (!string.Equals(
            userId,
            allowedUserId,
            StringComparison.Ordinal
        ))
        {
            CPH.LogWarn(
                "[PokéHunter] Redemption ignored: this test action " +
                "is restricted to its configured Twitch user ID."
            );

            return false;
        }

        if (!Path.IsPathRooted(projectRoot))
        {
            throw new InvalidDataException(
                "pokeHunterProjectRoot must be an absolute folder path."
            );
        }

        projectRoot = Path.GetFullPath(projectRoot);

        if (!Directory.Exists(projectRoot))
        {
            throw new DirectoryNotFoundException(
                "The configured project folder does not exist."
            );
        }

        string databasePath = Path.Combine(
            projectRoot,
            "runtime",
            "twitch-redemption-test.db"
        );

        // Creates or upgrades this dedicated database using our migrations.
        DatabaseInitializer.Initialize(databasePath);

        var redemptions = new RedemptionRepository(databasePath);

        // Replay detection comes before current schedule/configuration reads.
        // An old delivery still belongs to its original saved season,
        // even if a different season or event is active now.
        StoredRedemption existing = redemptions.FindById(redemptionId);

        if (existing != null)
        {
            if (!string.Equals(
                existing.TwitchUserId,
                userId,
                StringComparison.Ordinal
            ))
            {
                throw new InvalidDataException(
                    "This redemption ID belongs to another trainer."
                );
            }

            LogResult(existing, false, databasePath);
            return true;
        }

        string loginName = RequireStringArgument("userName");
        string displayName = RequireStringArgument("user");

        bool isSubscribed;

        if (!CPH.TryGetArg<bool>("isSubscribed", out isSubscribed))
        {
            throw new InvalidDataException(
                "The redemption did not supply isSubscribed."
            );
        }

        string subscriptionTier = null;

        if (isSubscribed)
        {
            subscriptionTier = RequireStringArgument(
                "subscriptionTier"
            );
        }

        SubscriptionTier tier = TwitchSubscriptionMapper.Map(
            isSubscribed,
            subscriptionTier
        );

        GameConfig game = GameConfigLoader.Load(
            Path.Combine(projectRoot, "config", "game.json")
        );

        SeasonsConfig seasons = SeasonsConfigLoader.Load(
            Path.Combine(projectRoot, "config", "seasons.json")
        );

        EventsConfig events = EventsConfigLoader.Load(
            Path.Combine(projectRoot, "config", "events.json")
        );

        EnsureProcessor(projectRoot, databasePath);

        // This is our acceptance time, not Twitch's original event time.
        // Use the same instant for season, event, eligibility, and storage.
        DateTimeOffset acceptedAt = DateTimeOffset.UtcNow;

        SeasonDefinition season = SeasonSelector.FindActive(
            seasons,
            acceptedAt
        );

        if (season == null)
        {
            throw new InvalidDataException(
                "No season is active at the acceptance time."
            );
        }

        EventDefinition activeEvent = EventSelector.FindActive(
            events,
            acceptedAt
        );

        var pool = EligibilityPoolBuilder.Build(
            catalogue,
            seasons,
            events,
            acceptedAt
        );

        var rarityWeights = new Dictionary<string, double>(
            StringComparer.Ordinal
        )
        {
            { "common", game.Encounters.RarityWeights.Common },
            { "uncommon", game.Encounters.RarityWeights.Uncommon },
            { "rare", game.Encounters.RarityWeights.Rare },
            { "ultraRare", game.Encounters.RarityWeights.UltraRare }
        };

        IReadOnlyList<EncounterCategory> normalCategories =
            EncounterCategoryBuilder.Build(
                catalogue,
                pool,
                false,
                game.Encounters.CategoryWeights.Type,
                rarityWeights
            );

        IReadOnlyList<EncounterCategory> shinyCategories =
            EncounterCategoryBuilder.Build(
                catalogue,
                pool,
                true,
                game.Encounters.CategoryWeights.Type,
                rarityWeights
            );

        // Prepare permanent parent records before processing the redemption.
        // These registrations commit separately from the result transaction.
        var seasonRepository = new SeasonRepository(databasePath);

        seasonRepository.Register(
            season.Id,
            season.Name,
            season.StartsAt,
            season.EndsAtExclusive,
            season.UnlockedGenerations
        );

        var trainers = new TrainerRepository(databasePath);

        trainers.SaveProfile(userId, loginName, displayName);

        RedemptionSaveResult result = processor.Process(
            redemptionId,
            userId,
            season.Id,
            activeEvent == null ? null : activeEvent.Id,
            acceptedAt,
            game.Shiny,
            tier,
            normalCategories,
            shinyCategories
        );

        LogResult(
            result.Redemption,
            result.WasCreated,
            databasePath
        );

        return true;
    }

    /// <summary>
    /// Retains the validated catalogue and processor between executions.
    /// Recompile the action or restart Streamer.bot after catalogue changes.
    /// Configuration files are read separately for each new redemption.
    /// </summary>
    private void EnsureProcessor(
        string projectRoot,
        string databasePath
    )
    {
        if (processor != null &&
            string.Equals(
                loadedDatabasePath,
                databasePath,
                StringComparison.OrdinalIgnoreCase
            ))
        {
            return;
        }

        var loadedCatalogue = new CatalogueIndex(
            CatalogueLoader.Load(
                Path.Combine(
                    projectRoot,
                    "data",
                    "catalogue",
                    "merged-entries.json"
                )
            )
        );

        var loadedProcessor = new RedemptionProcessor(
            databasePath,
            loadedCatalogue,
            SystemRandomSource.Shared
        );

        // Publish the new fields only after construction succeeds.
        catalogue = loadedCatalogue;
        processor = loadedProcessor;
        loadedDatabasePath = databasePath;
    }

    private void LogResult(
        StoredRedemption result,
        bool wasCreated,
        string databasePath
    )
    {
        CPH.LogInfo(
            "[PokéHunter] " +
            (wasCreated
                ? "New redemption result saved."
                : "Repeated delivery: original result returned.")
        );

        CPH.LogInfo(
            "[PokéHunter] Redemption: " + result.RedemptionId +
            " | Twitch user ID: " + result.TwitchUserId +
            " | Season: " + result.SeasonId +
            " | Event: " + (result.EventId ?? "none")
        );

        CPH.LogInfo(
            "[PokéHunter] Outcome: " + result.Outcome +
            " | Attempts: " + result.AttemptsUsed +
            "/" + result.AvailableAttempts +
            " | Next failure streak: " + result.NextFailureStreak
        );

        if (result.Succeeded)
        {
            CPH.LogInfo(
                "[PokéHunter] Saved catch: " + result.EntryId +
                " | Category: " + result.CategoryId +
                " | Group: " + result.GroupId
            );
        }

        CPH.LogInfo(
            "[PokéHunter] Test database: " + databasePath
        );
    }

    /// <summary>
    /// Rejects missing or malformed arguments instead of substituting
    /// another viewer, reward, redemption ID, or machine path.
    /// </summary>
    private string RequireStringArgument(string name)
    {
        string value;

        if (!CPH.TryGetArg<string>(name, out value) ||
            string.IsNullOrWhiteSpace(value) ||
            value != value.Trim())
        {
            throw new InvalidDataException(
                "Missing or invalid action argument: " + name + "."
            );
        }

        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                throw new InvalidDataException(
                    "Action argument contains control characters: " +
                    name + "."
                );
            }
        }

        return value;
    }
}