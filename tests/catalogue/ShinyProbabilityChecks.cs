using System;
using PokeHunter.Configuration;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class ShinyProbabilityChecks
    {
        public static void Run()
        {
            CheckCollectionMilestones();
            CheckShinyMilestones();
            CheckSubscriptionTiers();
            CheckCustomSettings();
            CheckProbabilityCap();
            CheckLargeCounts();
            CheckInvalidInputs();

            Console.WriteLine(
                "All shiny probability checks passed."
            );
        }

        private static void CheckCollectionMilestones()
        {
            ShinyConfig config = CreateSettings();

            Expect(config, 0, 0, SubscriptionTier.None, 1.0 / 8192);
            Expect(config, 49, 0, SubscriptionTier.None, 1.0 / 8192);
            Expect(config, 50, 0, SubscriptionTier.None, 1.1 / 8192);
            Expect(config, 99, 0, SubscriptionTier.None, 1.1 / 8192);
            Expect(config, 100, 0, SubscriptionTier.None, 1.2 / 8192);

            // The shiny entry completes the combined milestone,
            // without completing an additional ten-shiny milestone.
            Expect(config, 49, 1, SubscriptionTier.None, 1.1 / 8192);

            Console.WriteLine(
                "PASS: Combined collection bonuses start at full milestones."
            );
        }

        private static void CheckShinyMilestones()
        {
            ShinyConfig config = CreateSettings();

            Expect(config, 0, 9, SubscriptionTier.None, 1.0 / 8192);
            Expect(config, 0, 10, SubscriptionTier.None, 1.05 / 8192);
            Expect(config, 0, 19, SubscriptionTier.None, 1.05 / 8192);
            Expect(config, 0, 20, SubscriptionTier.None, 1.10 / 8192);

            // Fifty shiny entries earn one combined milestone (+10%)
            // and five shiny milestones (+25%).
            Expect(config, 0, 50, SubscriptionTier.None, 1.35 / 8192);

            // 200 combined entries: +40%. 100 shinies: an extra +50%.
            Expect(config, 100, 100, SubscriptionTier.None, 1.9 / 8192);

            Console.WriteLine(
                "PASS: Shiny entries earn both combined and extra bonuses."
            );
        }

        private static void CheckSubscriptionTiers()
        {
            ShinyConfig config = CreateSettings();

            Expect(config, 0, 0, SubscriptionTier.None, 1.0 / 8192);
            Expect(config, 0, 0, SubscriptionTier.Prime, 2.0 / 8192);
            Expect(config, 0, 0, SubscriptionTier.Tier1, 2.0 / 8192);
            Expect(config, 0, 0, SubscriptionTier.Tier2, 2.5 / 8192);
            Expect(config, 0, 0, SubscriptionTier.Tier3, 3.5 / 8192);

            Expect(config, 100, 100, SubscriptionTier.Tier1, 3.8 / 8192);
            Expect(config, 100, 100, SubscriptionTier.Tier3, 6.65 / 8192);

            Console.WriteLine(
                "PASS: Subscription multipliers stack with collection bonuses."
            );
        }

        private static void CheckCustomSettings()
        {
            ShinyConfig config = CreateSettings();

            config.BaseOddsDenominator = 1000;
            config.CollectionBonus.UniqueEntriesPerMilestone = 20;
            config.CollectionBonus.BonusPerUniqueEntryMilestone = 0.2;
            config.CollectionBonus.UniqueShiniesPerMilestone = 5;
            config.CollectionBonus.BonusPerShinyMilestone = 0.1;
            config.SubscriberMultipliers.Tier2 = 3;

            // 40 combined: +40%. 10 shiny: +20%. Then multiply by 3.
            Expect(config, 30, 10, SubscriptionTier.Tier2, 4.8 / 1000);

            config.CollectionBonus.BonusPerUniqueEntryMilestone = 0;
            config.CollectionBonus.BonusPerShinyMilestone = 0;

            Expect(config, 30, 10, SubscriptionTier.Tier2, 3.0 / 1000);

            Console.WriteLine(
                "PASS: Custom settings and disabled bonuses were respected."
            );
        }

        private static void CheckProbabilityCap()
        {
            ShinyConfig config = CreateSettings();
            config.BaseOddsDenominator = 1;

            Expect(config, 0, 0, SubscriptionTier.None, 1.0);
            Expect(config, 100, 100, SubscriptionTier.Tier3, 1.0);

            Console.WriteLine(
                "PASS: Shiny probability never exceeds 100%."
            );
        }

        private static void CheckLargeCounts()
        {
            ShinyConfig config = CreateSettings();

            // The sum exceeds int.MaxValue. Zero bonuses isolate the
            // count addition from the probability cap.
            config.CollectionBonus.BonusPerUniqueEntryMilestone = 0;
            config.CollectionBonus.BonusPerShinyMilestone = 0;

            Expect(
                config,
                int.MaxValue,
                int.MaxValue,
                SubscriptionTier.None,
                1.0 / 8192
            );

            Console.WriteLine(
                "PASS: Adding large unique counts did not overflow."
            );
        }

        private static void CheckInvalidInputs()
        {
            ExpectRejected(
                () => ShinyProbabilityCalculator.Calculate(
                    null, 0, 0, SubscriptionTier.None
                ),
                "config"
            );

            ExpectRejected(
                () => ShinyProbabilityCalculator.Calculate(
                    CreateSettings(), -1, 0, SubscriptionTier.None
                ),
                "uniqueNormalEntries"
            );

            ExpectRejected(
                () => ShinyProbabilityCalculator.Calculate(
                    CreateSettings(), 0, -1, SubscriptionTier.None
                ),
                "uniqueShinyEntries"
            );

            ExpectRejected(
                () => ShinyProbabilityCalculator.Calculate(
                    CreateSettings(), 0, 0, (SubscriptionTier)999
                ),
                "tier"
            );

            ShinyConfig invalidMilestone = CreateSettings();
            invalidMilestone.CollectionBonus.UniqueEntriesPerMilestone = 0;

            ExpectRejected(
                () => ShinyProbabilityCalculator.Calculate(
                    invalidMilestone, 0, 0, SubscriptionTier.None
                ),
                "config"
            );

            ShinyConfig invalidBonus = CreateSettings();
            invalidBonus.CollectionBonus.BonusPerShinyMilestone = double.NaN;

            ExpectRejected(
                () => ShinyProbabilityCalculator.Calculate(
                    invalidBonus, 0, 0, SubscriptionTier.None
                ),
                "config"
            );

            ShinyConfig invalidMultiplier = CreateSettings();
            invalidMultiplier.SubscriberMultipliers.Tier3 =
                double.PositiveInfinity;

            ExpectRejected(
                () => ShinyProbabilityCalculator.Calculate(
                    invalidMultiplier, 0, 0, SubscriptionTier.Tier3
                ),
                "config"
            );

            Console.WriteLine(
                "PASS: Invalid counts, tiers, and settings were rejected."
            );
        }

        private static void Expect(
            ShinyConfig config,
            int normalCount,
            int shinyCount,
            SubscriptionTier tier,
            double expected
        )
        {
            double actual = ShinyProbabilityCalculator.Calculate(
                config,
                normalCount,
                shinyCount,
                tier
            );

            // Floating-point arithmetic can differ by a tiny rounding amount.
            if (double.IsNaN(actual) ||
                double.IsInfinity(actual) ||
                Math.Abs(actual - expected) > 1e-12)
            {
                throw new InvalidOperationException(
                    "Unexpected shiny probability for normal=" +
                    normalCount + ", shiny=" + shinyCount +
                    ", tier=" + tier +
                    ". Expected " + expected +
                    ", received " + actual + "."
                );
            }
        }

        private static void ExpectRejected(
            Action action,
            string expectedParameter
        )
        {
            try
            {
                action();
            }
            catch (ArgumentException exception)
            {
                if (exception.ParamName != expectedParameter)
                {
                    throw new InvalidOperationException(
                        "Expected rejection of '" + expectedParameter +
                        "', received '" + exception.ParamName + "'.",
                        exception
                    );
                }

                return;
            }

            throw new InvalidOperationException(
                "Expected rejection of '" + expectedParameter + "'."
            );
        }

        private static ShinyConfig CreateSettings()
        {
            return new ShinyConfig
            {
                BaseOddsDenominator = 8192,
                CollectionBonus = new CollectionBonusConfig
                {
                    UniqueEntriesPerMilestone = 50,
                    BonusPerUniqueEntryMilestone = 0.10,
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
    }
}