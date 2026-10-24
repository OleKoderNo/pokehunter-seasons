using System;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Converts Twitch subscription information into the game's tier.
    /// Does not calculate odds or call Twitch APIs.
    /// </summary>
    public static class TwitchSubscriptionMapper
    {
        public static SubscriptionTier Map(
            bool isSubscribed,
            string subscriptionTier
        )
        {
            // Non-subscribers receive the configured non-subscriber rate.
            // An unused or stale tier value must not grant a bonus.
            if (!isSubscribed)
            {
                return SubscriptionTier.None;
            }

            if (string.IsNullOrWhiteSpace(subscriptionTier))
            {
                throw new ArgumentException(
                    "A subscribed viewer must have a subscription tier.",
                    nameof(subscriptionTier)
                );
            }

            switch (subscriptionTier)
            {
                case "1000":
                    return SubscriptionTier.Tier1;

                case "2000":
                    return SubscriptionTier.Tier2;

                case "3000":
                    return SubscriptionTier.Tier3;
            }

            // Support an explicit Prime label when the source supplies it.
            // A numeric 1000 alone cannot distinguish Prime from paid Tier 1.
            if (string.Equals(
                subscriptionTier,
                "Prime",
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return SubscriptionTier.Prime;
            }

            // Unknown data is a technical problem. Do not silently assign
            // a different bonus or record an ordinary failed encounter.
            throw new ArgumentException(
                "Unsupported Twitch subscription tier: " +
                subscriptionTier + ".",
                nameof(subscriptionTier)
            );
        }
    }
}