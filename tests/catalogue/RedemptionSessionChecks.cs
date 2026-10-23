using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class RedemptionSessionChecks
    {
        public static void Run(CatalogueDocument document)
        {
            var index = new CatalogueIndex(document);

            IReadOnlyList<EncounterCategory> normal =
                EncounterGroupSelectionChecks.CreateCategories(document);

            IReadOnlyList<EncounterCategory> shiny =
                EncounterGroupSelectionChecks.CreateCategories(document, true);

            var engine = new EncounterAttemptEngine(new ZeroRandomSource());

            EncounterAttemptResult newNormal = engine.Run(
                index, normal, shiny, new string[0], 0, false
            );

            EncounterAttemptResult newShiny = engine.Run(
                index, normal, shiny, new string[0], 1, false
            );

            Require(
                newNormal.Outcome == EncounterAttemptOutcome.NewEntry &&
                newShiny.Outcome == EncounterAttemptOutcome.NewEntry,
                "The fixture must produce new normal and shiny entries."
            );

            EncounterAttemptResult duplicateNormal = engine.Run(
                index,
                normal,
                shiny,
                new[] { newNormal.Entry.Id },
                0,
                false
            );

            EncounterAttemptResult duplicateShiny = engine.Run(
                index,
                normal,
                shiny,
                new[] { newShiny.Entry.Id },
                1,
                false
            );

            Require(
                duplicateNormal.Outcome == EncounterAttemptOutcome.Duplicate &&
                duplicateShiny.Outcome == EncounterAttemptOutcome.Duplicate,
                "The fixture must produce normal and shiny duplicates."
            );

            var empty = new List<EncounterCategory>();

            EncounterAttemptResult unavailable = engine.Run(
                index, empty, empty, new string[0], 0, false
            );

            CheckImmediateSuccess(newNormal);
            CheckEightAttemptExample(
                duplicateNormal,
                duplicateShiny,
                newShiny
            );
            CheckExhaustion(duplicateNormal, unavailable);
            CheckProtectionReset(duplicateShiny, unavailable);
            CheckTechnicalFailure(duplicateNormal);
            CheckInvalidStreak();

            Console.WriteLine(
                "All redemption session checks passed."
            );
        }

        private static void CheckImmediateSuccess(
            EncounterAttemptResult success
        )
        {
            int calls = 0;

            RedemptionSessionResult result = RedemptionSessionRunner.Run(
                7,
                protectedShiny =>
                {
                    Require(!protectedShiny, "A session must start unprotected.");
                    calls++;
                    return success;
                }
            );

            Require(
                result.Succeeded &&
                result.AvailableAttempts == 8 &&
                result.AttemptsUsed == 1 &&
                result.NextFailureStreak == 0 &&
                calls == 1 &&
                ReferenceEquals(result.SuccessfulAttempt, success),
                "Success must stop immediately and reset the streak."
            );

            Console.WriteLine(
                "PASS: Immediate success stopped and discarded unused attempts."
            );
        }

        private static void CheckEightAttemptExample(
            EncounterAttemptResult normalDuplicate,
            EncounterAttemptResult shinyDuplicate,
            EncounterAttemptResult shinySuccess
        )
        {
            var outcomes = new Queue<EncounterAttemptResult>(
                new[]
                {
                    normalDuplicate,
                    normalDuplicate,
                    shinyDuplicate,
                    shinyDuplicate,
                    shinySuccess
                }
            );

            var protectionFlags = new List<bool>();

            RedemptionSessionResult result = RedemptionSessionRunner.Run(
                7,
                protectedShiny =>
                {
                    protectionFlags.Add(protectedShiny);
                    return outcomes.Dequeue();
                }
            );

            Require(
                result.AvailableAttempts == 8 &&
                result.AttemptsUsed == 5 &&
                result.Succeeded &&
                result.SuccessfulAttempt.IsShiny &&
                result.NextFailureStreak == 0 &&
                outcomes.Count == 0,
                "The eight-attempt example must succeed on attempt five."
            );

            Require(
                protectionFlags.Count == 5 &&
                !protectionFlags[0] &&
                !protectionFlags[1] &&
                !protectionFlags[2] &&
                protectionFlags[3] &&
                protectionFlags[4],
                "A shiny on attempt three must protect attempts four onward."
            );

            Console.WriteLine(
                "PASS: Shiny on attempt three protected retries until success on five."
            );
        }

        private static void CheckExhaustion(
            EncounterAttemptResult duplicate,
            EncounterAttemptResult unavailable
        )
        {
            var outcomes = new Queue<EncounterAttemptResult>(
                new[] { duplicate, unavailable, duplicate }
            );

            RedemptionSessionResult result = RedemptionSessionRunner.Run(
                2,
                protectedShiny => outcomes.Dequeue()
            );

            Require(
                !result.Succeeded &&
                result.SuccessfulAttempt == null &&
                result.AvailableAttempts == 3 &&
                result.AttemptsUsed == 3 &&
                result.NextFailureStreak == 3,
                "Exhaustion must increment the streak once."
            );

            Console.WriteLine(
                "PASS: Exhausted attempts incremented the failure streak once."
            );
        }

        private static void CheckProtectionReset(
            EncounterAttemptResult shinyDuplicate,
            EncounterAttemptResult normalUnavailable
        )
        {
            RedemptionSessionResult first = RedemptionSessionRunner.Run(
                0,
                protectedShiny => shinyDuplicate
            );

            bool startedUnprotected = false;

            RedemptionSessionResult second = RedemptionSessionRunner.Run(
                first.NextFailureStreak,
                protectedShiny =>
                {
                    Require(
                        !protectedShiny,
                        "Protection must not carry into the next redemption."
                    );

                    startedUnprotected = true;
                    return normalUnavailable;
                }
            );

            Require(
                startedUnprotected &&
                second.AvailableAttempts == 2 &&
                second.AttemptsUsed == 2,
                "The next redemption must retain retries but reset protection."
            );

            Console.WriteLine(
                "PASS: Retry attempts carried forward without shiny protection."
            );
        }

        private static void CheckTechnicalFailure(
            EncounterAttemptResult duplicate
        )
        {
            var expected = new InvalidOperationException(
                "Deliberate technical failure."
            );

            int calls = 0;
            bool propagated = false;

            try
            {
                RedemptionSessionRunner.Run(
                    3,
                    protectedShiny =>
                    {
                        calls++;

                        if (calls == 2)
                        {
                            throw expected;
                        }

                        return duplicate;
                    }
                );
            }
            catch (InvalidOperationException exception)
            {
                Require(
                    ReferenceEquals(exception, expected),
                    "The original technical exception must propagate."
                );

                propagated = true;
            }

            Require(
                propagated && calls == 2,
                "Technical failure must stop without returning a session outcome."
            );

            Console.WriteLine(
                "PASS: A technical failure propagated without a gameplay outcome."
            );
        }

        private static void CheckInvalidStreak()
        {
            foreach (int streak in new[] { -1, int.MaxValue })
            {
                bool rejected = false;

                try
                {
                    RedemptionSessionRunner.Run(
                        streak,
                        protectedShiny =>
                        {
                            throw new InvalidOperationException(
                                "Invalid streaks must fail before any attempt."
                            );
                        }
                    );
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    Require(
                        exception.ParamName == "consecutiveFailedRedemptions",
                        "The error must identify the failure streak."
                    );

                    rejected = true;
                }

                Require(rejected, "An invalid streak was accepted.");
            }

            Console.WriteLine(
                "PASS: Invalid streaks were rejected before running attempts."
            );
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class ZeroRandomSource : IRandomSource
        {
            public double NextDouble()
            {
                return 0;
            }
        }
    }
}