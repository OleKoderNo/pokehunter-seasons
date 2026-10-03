namespace PokeHunter.Configuration
{
    /// <summary>
    /// Describes the settings stored in config/game.json.
    /// Loading and validation are handled separately.
    /// </summary>
    public sealed class GameConfig
    {
        // Identifies the configuration format the loader must understand.
        public int SchemaVersion { get; set; }

        public RedemptionConfig Redemption { get; set; }

        public ShinyConfig Shiny { get; set; }

        public EncounterConfig Encounters { get; set; }
    }

    /// <summary>
    /// Settings for the channel-point reward and per-viewer cooldown.
    /// </summary>
    public sealed class RedemptionConfig
    {
        public int Cost { get; set; }

        public int PerUserCooldownSeconds { get; set; }
    }

    /// <summary>
    /// Base shiny probability and the bonuses applied to it.
    /// </summary>
    public sealed class ShinyConfig
    {
        // A value of 8192 represents a base probability of 1 / 8192.
        public int BaseOddsDenominator { get; set; }

        public CollectionBonusConfig CollectionBonus { get; set; }

        public SubscriberMultiplierConfig SubscriberMultipliers { get; set; }
    }

    /// <summary>
    /// Milestone bonuses based on unique lifetime collection entries.
    /// Repeat catches across seasons do not increase these counts.
    /// </summary>
    public sealed class CollectionBonusConfig
    {
        // This milestone counts unique normal AND shiny entries.
        public int UniqueEntriesPerMilestone { get; set; }

        // Stored as a fraction: 0.1 means a 10% bonus per milestone.
        public double BonusPerUniqueEntryMilestone { get; set; }

        // Unique shiny entries also earn this additional milestone bonus.
        public int UniqueShiniesPerMilestone { get; set; }

        // Stored as a fraction: 0.05 means a 5% bonus per milestone.
        public double BonusPerShinyMilestone { get; set; }
    }

    /// <summary>
    /// Shiny probability multipliers for each subscription status.
    /// </summary>
    public sealed class SubscriberMultiplierConfig
    {
        public double None { get; set; }

        public double Prime { get; set; }

        public double Tier1 { get; set; }

        public double Tier2 { get; set; }

        public double Tier3 { get; set; }
    }

    /// <summary>
    /// Relative weights used when choosing encounter categories
    /// and Pokémon within those categories.
    /// </summary>
    public sealed class EncounterConfig
    {
        public CategoryWeightConfig CategoryWeights { get; set; }

        public RarityWeightConfig RarityWeights { get; set; }
    }

    /// <summary>
    /// Default weights for an eligible ordinary type or Event category.
    /// </summary>
    public sealed class CategoryWeightConfig
    {
        public double Type { get; set; }

        public double Event { get; set; }
    }

    /// <summary>
    /// Relative selection weights for Pokémon rarity classes.
    /// These are weights, not percentages.
    /// </summary>
    public sealed class RarityWeightConfig
    {
        public double Common { get; set; }

        public double Uncommon { get; set; }

        public double Rare { get; set; }

        public double UltraRare { get; set; }
    }
}