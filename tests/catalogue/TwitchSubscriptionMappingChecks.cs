using System;
using PokeHunter.Encounters;

/// <summary>
/// Checks how Twitch subscription values map to game tiers.
/// Does not contact Twitch or modify player data.
/// </summary>
internal static class TwitchSubscriptionMappingChecks
{
    public static void Run()
    {
        ExpectTier(false, null, SubscriptionTier.None);
        ExpectTier(false, "", SubscriptionTier.None);

        // A stale tier value must not grant a non-subscriber a bonus.
        ExpectTier(false, "3000", SubscriptionTier.None);

        Console.WriteLine(
            "PASS: Non-subscribers map to None, including stale tier values."
        );

        ExpectTier(true, "1000", SubscriptionTier.Tier1);
        ExpectTier(true, "2000", SubscriptionTier.Tier2);
        ExpectTier(true, "3000", SubscriptionTier.Tier3);

        Console.WriteLine(
            "PASS: Twitch tier codes map to Tier 1, Tier 2, and Tier 3."
        );

        ExpectTier(true, "Prime", SubscriptionTier.Prime);
        ExpectTier(true, "prime", SubscriptionTier.Prime);
        ExpectTier(true, "PRIME", SubscriptionTier.Prime);

        Console.WriteLine(
            "PASS: Explicit Prime labels are recognized without case sensitivity."
        );

        ExpectRejected(null);
        ExpectRejected("");
        ExpectRejected(" ");
        ExpectRejected("4000");
        ExpectRejected("unknown");
        ExpectRejected(" 1000 ");

        Console.WriteLine(
            "PASS: Missing and unsupported subscriber tiers are rejected."
        );
    }

    private static void ExpectTier(
        bool isSubscribed,
        string suppliedTier,
        SubscriptionTier expected
    )
    {
        SubscriptionTier actual = TwitchSubscriptionMapper.Map(
            isSubscribed,
            suppliedTier
        );

        if (actual != expected)
        {
            throw new InvalidOperationException(
                "Expected " + expected +
                " for subscribed=" + isSubscribed +
                ", tier=" + (suppliedTier ?? "<null>") +
                ", but received " + actual + "."
            );
        }
    }

    /// <summary>
    /// Verify the mapper rejects the tier argument specifically.
    /// An unrelated exception must not make this check pass.
    /// </summary>
    private static void ExpectRejected(string suppliedTier)
    {
        try
        {
            TwitchSubscriptionMapper.Map(true, suppliedTier);
        }
        catch (ArgumentException exception)
        {
            if (exception.ParamName != "subscriptionTier")
            {
                throw new InvalidOperationException(
                    "Subscription mapping rejected an unexpected argument.",
                    exception
                );
            }

            return;
        }

        throw new InvalidOperationException(
            "Expected rejection for subscription tier: " +
            (suppliedTier ?? "<null>") + "."
        );
    }
}