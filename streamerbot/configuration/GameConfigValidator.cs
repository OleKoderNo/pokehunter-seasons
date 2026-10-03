using System.IO;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Checks whether loaded game settings are usable by the game.
    /// Reports the first invalid setting without modifying its value.
    /// </summary>
    public static class GameConfigValidator
    {
        public static void Validate(GameConfig config)
        {
            // Also protect callers that construct configuration objects
            // directly instead of loading them through our JSON reader.
            Require(config != null, "The game configuration is required.");

            Require(
                config.SchemaVersion == 1,
                "schemaVersion must be 1. Other versions are not supported."
            );

            Require(config.Redemption != null, "redemption is required.");
            Require(config.Shiny != null, "shiny is required.");
            Require(config.Encounters != null, "encounters is required.");

            Require(
                config.Shiny.CollectionBonus != null,
                "shiny.collectionBonus is required."
            );

            Require(
                config.Shiny.SubscriberMultipliers != null,
                "shiny.subscriberMultipliers is required."
            );

            Require(
                config.Encounters.CategoryWeights != null,
                "encounters.categoryWeights is required."
            );

            Require(
                config.Encounters.RarityWeights != null,
                "encounters.rarityWeights is required."
            );

            // Local variables make the checks below easier to read.
            var redemption = config.Redemption;
            var shiny = config.Shiny;
            var bonus = shiny.CollectionBonus;
            var subscribers = shiny.SubscriberMultipliers;
            var categories = config.Encounters.CategoryWeights;
            var rarities = config.Encounters.RarityWeights;

            Require(
                redemption.Cost > 0,
                "redemption.cost must be greater than zero."
            );

            Require(
                redemption.PerUserCooldownSeconds >= 0,
                "redemption.perUserCooldownSeconds must be zero or greater."
            );

            // These settings will be used as divisors.
            Require(
                shiny.BaseOddsDenominator > 0,
                "shiny.baseOddsDenominator must be greater than zero."
            );

            Require(
                bonus.UniqueEntriesPerMilestone > 0,
                "shiny.collectionBonus.uniqueEntriesPerMilestone " +
                "must be greater than zero."
            );

            Require(
                bonus.UniqueShiniesPerMilestone > 0,
                "shiny.collectionBonus.uniqueShiniesPerMilestone " +
                "must be greater than zero."
            );

            // A zero bonus is allowed so creators can disable progression.
            RequireNonNegative(
                bonus.BonusPerUniqueEntryMilestone,
                "shiny.collectionBonus.bonusPerUniqueEntryMilestone"
            );

            RequireNonNegative(
                bonus.BonusPerShinyMilestone,
                "shiny.collectionBonus.bonusPerShinyMilestone"
            );

            RequirePositive(subscribers.None, "shiny.subscriberMultipliers.none");
            RequirePositive(subscribers.Prime, "shiny.subscriberMultipliers.prime");
            RequirePositive(subscribers.Tier1, "shiny.subscriberMultipliers.tier1");
            RequirePositive(subscribers.Tier2, "shiny.subscriberMultipliers.tier2");
            RequirePositive(subscribers.Tier3, "shiny.subscriberMultipliers.tier3");

            RequirePositive(categories.Type, "encounters.categoryWeights.type");
            RequirePositive(categories.Event, "encounters.categoryWeights.event");

            RequirePositive(rarities.Common, "encounters.rarityWeights.common");
            RequirePositive(rarities.Uncommon, "encounters.rarityWeights.uncommon");
            RequirePositive(rarities.Rare, "encounters.rarityWeights.rare");
            RequirePositive(rarities.UltraRare, "encounters.rarityWeights.ultraRare");
        }

        /// <summary>
        /// Throws a readable configuration error when a rule is broken.
        /// </summary>
        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(
                    "Configuration error: " + message
                );
            }
        }

        /// <summary>
        /// Rejects negative values, infinity, and NaN.
        /// </summary>
        private static void RequireNonNegative(double value, string path)
        {
            Require(
                IsFinite(value) && value >= 0,
                path + " must be a finite number, zero or greater."
            );
        }

        /// <summary>
        /// Rejects zero, negative values, infinity, and NaN.
        /// </summary>
        private static void RequirePositive(double value, string path)
        {
            Require(
                IsFinite(value) && value > 0,
                path + " must be a finite number greater than zero."
            );
        }

        private static bool IsFinite(double value)
        {
            // Double can represent special values that are unsuitable
            // for probabilities and weighted selection.
            return !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}