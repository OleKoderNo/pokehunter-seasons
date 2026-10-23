using System;
using PokeHunter.Configuration;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Calculates shiny probability from lifetime unique collection counts.
    /// Does not roll randomness, query storage, or modify configuration.
    /// </summary>
    public static class ShinyProbabilityCalculator
    {
        /// <summary>
        /// Returns a probability between zero and one.
        ///
        /// Normal and shiny counts are separate lifetime unique identities.
        /// Shiny entries contribute to both the combined collection milestone
        /// and the additional shiny milestone.
        /// </summary>
        public static double Calculate(
            ShinyConfig config,
            int uniqueNormalEntries,
            int uniqueShinyEntries,
            SubscriptionTier subscriptionTier
        )
        {
            ValidateSettings(config);

            if (uniqueNormalEntries < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(uniqueNormalEntries),
                    "Unique normal entries must not be negative."
                );
            }

            if (uniqueShinyEntries < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(uniqueShinyEntries),
                    "Unique shiny entries must not be negative."
                );
            }

            double subscriberMultiplier = GetSubscriberMultiplier(
                config.SubscriberMultipliers,
                subscriptionTier
            );

            CollectionBonusConfig bonuses = config.CollectionBonus;

            // Convert before adding so two valid int counts cannot overflow.
            long combinedEntries =
                (long)uniqueNormalEntries + uniqueShinyEntries;

            long collectionMilestones =
                combinedEntries / bonuses.UniqueEntriesPerMilestone;

            long shinyMilestones =
                uniqueShinyEntries / bonuses.UniqueShiniesPerMilestone;

            // Bonuses add together; they do not compound.
            double collectionMultiplier =
                1.0 +
                collectionMilestones *
                    bonuses.BonusPerUniqueEntryMilestone +
                shinyMilestones *
                    bonuses.BonusPerShinyMilestone;

            double probability =
                collectionMultiplier *
                subscriberMultiplier /
                config.BaseOddsDenominator;

            // Extremely large valid positive bonuses can overflow to
            // positive infinity. They still represent a capped 100% chance.
            return Math.Min(1.0, probability);
        }

        private static double GetSubscriberMultiplier(
            SubscriberMultiplierConfig config,
            SubscriptionTier tier
        )
        {
            switch (tier)
            {
                case SubscriptionTier.None:
                    return config.None;

                case SubscriptionTier.Prime:
                    return config.Prime;

                case SubscriptionTier.Tier1:
                    return config.Tier1;

                case SubscriptionTier.Tier2:
                    return config.Tier2;

                case SubscriptionTier.Tier3:
                    return config.Tier3;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(tier),
                        "Unknown subscription tier."
                    );
            }
        }

        private static void ValidateSettings(ShinyConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            if (config.BaseOddsDenominator <= 0)
            {
                throw new ArgumentException(
                    "BaseOddsDenominator must be greater than zero.",
                    nameof(config)
                );
            }

            CollectionBonusConfig bonuses = config.CollectionBonus;

            if (bonuses == null ||
                bonuses.UniqueEntriesPerMilestone <= 0 ||
                bonuses.UniqueShiniesPerMilestone <= 0)
            {
                throw new ArgumentException(
                    "Collection bonuses require positive milestone sizes.",
                    nameof(config)
                );
            }

            if (!IsFiniteNonnegative(bonuses.BonusPerUniqueEntryMilestone) ||
                !IsFiniteNonnegative(bonuses.BonusPerShinyMilestone))
            {
                throw new ArgumentException(
                    "Collection bonuses must be finite and nonnegative.",
                    nameof(config)
                );
            }

            SubscriberMultiplierConfig multipliers =
                config.SubscriberMultipliers;

            if (multipliers == null ||
                !IsFinitePositive(multipliers.None) ||
                !IsFinitePositive(multipliers.Prime) ||
                !IsFinitePositive(multipliers.Tier1) ||
                !IsFinitePositive(multipliers.Tier2) ||
                !IsFinitePositive(multipliers.Tier3))
            {
                throw new ArgumentException(
                    "Subscriber multipliers must be finite and positive.",
                    nameof(config)
                );
            }
        }

        private static bool IsFiniteNonnegative(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                value >= 0;
        }

        private static bool IsFinitePositive(double value)
        {
            return IsFiniteNonnegative(value) && value > 0;
        }
    }
}