using System;
using System.Collections.Generic;
using System.IO;
using PokeHunter.Catalogue;
using PokeHunter.Configuration;
using PokeHunter.Encounters;
using PokeHunter.Storage;
using Newtonsoft.Json;

namespace PokeHunter.Gameplay
{
    /// <summary>
    /// Connects collection reads, encounter sessions, and result storage.
    ///
    /// The caller supplies categories prepared for the accepted season
    /// and event. This class does not select schedules, register trainers,
    /// or send chat/overlay notifications.
    /// </summary>
    public sealed class RedemptionProcessor
    {
        private readonly CatalogueIndex catalogue;
        private readonly CollectionRepository collections;
        private readonly RedemptionRepository redemptions;
        private readonly EncounterAttemptEngine attemptEngine;
        private readonly Func<DateTimeOffset> utcNow;

        // Reuse one processor instance. Its short synchronous operations
        // are serialized so its own redemptions cannot interleave between
        // reading a collection and saving a result.
        //
        // Other instances/processes are still protected by the repository's
        // transaction, uniqueness constraints, and stale-state checks.
        private readonly object processingLock = new object();

        public RedemptionProcessor(
            string databasePath,
            CatalogueIndex catalogue,
            IRandomSource randomSource,
            Func<DateTimeOffset> utcNow = null
        )
        {
            if (catalogue == null)
            {
                throw new ArgumentNullException(nameof(catalogue));
            }

            if (randomSource == null)
            {
                throw new ArgumentNullException(nameof(randomSource));
            }

            this.catalogue = catalogue;
            this.utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

            collections = new CollectionRepository(databasePath);
            redemptions = new RedemptionRepository(databasePath);
            attemptEngine = new EncounterAttemptEngine(randomSource);
        }

        /// <summary>
        /// Processes one redemption and returns its committed result.
        ///
        /// Categories must come from the same catalogue and eligibility
        /// context identified by seasonId, eventId, and acceptedAt.
        /// Do not mutate the supplied configuration/categories during execution.
        ///
        /// Existing redemption IDs return their original result without
        /// recalculating odds, running attempts, or changing progress.
        /// </summary>
        public RedemptionSaveResult Process(
            string redemptionId,
            string twitchUserId,
            string seasonId,
            string eventId,
            DateTimeOffset acceptedAt,
            ShinyConfig shinyConfig,
            SubscriptionTier subscriptionTier,
            IReadOnlyList<EncounterCategory> normalCategories,
            IReadOnlyList<EncounterCategory> shinyCategories
        )
        {
            RequireText(redemptionId, nameof(redemptionId));
            RequireText(twitchUserId, nameof(twitchUserId));
            RequireText(seasonId, nameof(seasonId));

            lock (processingLock)
            {
                StoredRedemption existing = redemptions.FindById(
                    redemptionId
                );

                if (existing != null)
                {
                    RequireMatchingIdentity(
                        existing,
                        twitchUserId,
                        seasonId
                    );

                    // Return before any random draws or collection reads.
                    // The saved result's original context is authoritative.
                    return new RedemptionSaveResult(existing, false);
                }

                if (eventId != null)
                {
                    RequireText(eventId, nameof(eventId));
                }

                if (acceptedAt == default(DateTimeOffset))
                {
                    throw new ArgumentException(
                        "An acceptance timestamp is required.",
                        nameof(acceptedAt)
                    );
                }

                if (shinyConfig == null)
                {
                    throw new ArgumentNullException(nameof(shinyConfig));
                }

                if (normalCategories == null)
                {
                    throw new ArgumentNullException(
                        nameof(normalCategories)
                    );
                }

                if (shinyCategories == null)
                {
                    throw new ArgumentNullException(
                        nameof(shinyCategories)
                    );
                }

                CollectionSnapshot collection =
                    collections.ReadSnapshot(
                        twitchUserId,
                        seasonId
                    );

                // Calculate once for the entire redemption.
                // Unique shiny entries participate in both the combined
                // collection milestone and the additional shiny milestone.
                double shinyProbability =
                    ShinyProbabilityCalculator.Calculate(
                        shinyConfig,
                        collection.UniqueNormalEntries,
                        collection.UniqueShinyEntries,
                        subscriptionTier
                    );

                RedemptionSessionResult session =
                    RedemptionSessionRunner.Run(
                        collection.ConsecutiveFailedRedemptions,
                        shinyProtected => attemptEngine.Run(
                            catalogue,
                            normalCategories,
                            shinyCategories,
                            collection.SeasonalEntryIds,
                            shinyProbability,
                            shinyProtected
                        )
                    );

                DateTimeOffset completedAt = utcNow();

                RedemptionSaveRequest request;

                if (session.Succeeded)
                {
                    EncounterAttemptResult successfulAttempt =
                        session.SuccessfulAttempt;

                    request = RedemptionSaveRequest.ForCatch(
                        redemptionId,
                        collection,
                        acceptedAt,
                        completedAt,
                        eventId,
                        session.AttemptsUsed,
                        successfulAttempt.Entry.Id,
                        successfulAttempt.Entry.IsShiny,
                        successfulAttempt.CategoryId,
                        successfulAttempt.GroupId,
                        JsonConvert.SerializeObject(
                            successfulAttempt.Entry
                        )
                    );
                }
                else
                {
                    request = RedemptionSaveRequest.ForFailure(
                        redemptionId,
                        collection,
                        acceptedAt,
                        completedAt,
                        eventId,
                        session.AttemptsUsed
                    );
                }

                // Save rechecks redemption identity and collection state
                // inside its transaction. Another process may have written
                // since the reads above.
                //
                // Exceptions propagate: technical failures must not become
                // ordinary failed redemptions or trigger automatic rerolls.
                return redemptions.Save(request, catalogue);
            }
        }

        private static void RequireMatchingIdentity(
            StoredRedemption existing,
            string twitchUserId,
            string seasonId
        )
        {
            if (!string.Equals(
                existing.TwitchUserId,
                twitchUserId,
                StringComparison.Ordinal
            ) || !string.Equals(
                existing.SeasonId,
                seasonId,
                StringComparison.OrdinalIgnoreCase
            ))
            {
                throw new InvalidDataException(
                    "The redemption ID is already associated with " +
                    "a different trainer or season."
                );
            }
        }

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
    }
}