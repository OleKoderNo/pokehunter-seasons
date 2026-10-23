namespace PokeHunter.Encounters
{
    /// <summary>
    /// Subscription status used when calculating shiny probability.
    /// Twitch-specific subscription data will be mapped to this separately.
    /// </summary>
    public enum SubscriptionTier
    {
        None,
        Prime,
        Tier1,
        Tier2,
        Tier3
    }
}