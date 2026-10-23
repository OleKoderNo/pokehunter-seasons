using System;
using System.Collections.Generic;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Coordinates attempts for one redemption.
    /// Does not write storage, send messages, or change viewer state.
    /// </summary>
    public static class RedemptionSessionRunner
    {
        public static RedemptionSessionResult Run(
            int consecutiveFailedRedemptions,
            Func<bool, EncounterAttemptResult> runAttempt
        )
        {
            if (consecutiveFailedRedemptions < 0 ||
                consecutiveFailedRedemptions == int.MaxValue)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(consecutiveFailedRedemptions),
                    "The failure streak must be nonnegative and allow " +
                    "one additional attempt within the integer range."
                );
            }

            if (runAttempt == null)
            {
                throw new ArgumentNullException(nameof(runAttempt));
            }

            int availableAttempts = consecutiveFailedRedemptions + 1;

            // Local to this invocation: never carry protection into
            // the next redemption.
            bool shinyProtected = false;

            var attempts = new List<EncounterAttemptResult>();

            for (int index = 0; index < availableAttempts; index++)
            {
                // Exceptions deliberately propagate. A technical failure
                // must not become a failed redemption or update its streak.
                EncounterAttemptResult attempt = runAttempt(shinyProtected);

                ValidateAttempt(attempt, shinyProtected);
                attempts.Add(attempt);

                if (attempt.IsShiny)
                {
                    shinyProtected = true;
                }

                if (attempt.Outcome == EncounterAttemptOutcome.NewEntry)
                {
                    return new RedemptionSessionResult(
                        availableAttempts,
                        0,
                        attempts,
                        attempt
                    );
                }
            }

            // Increment once for the whole failed redemption,
            // not once for each unsuccessful attempt.
            return new RedemptionSessionResult(
                availableAttempts,
                consecutiveFailedRedemptions + 1,
                attempts,
                null
            );
        }

        private static void ValidateAttempt(
            EncounterAttemptResult attempt,
            bool shinyProtected
        )
        {
            if (attempt == null)
            {
                throw new InvalidOperationException(
                    "The attempt function returned no result."
                );
            }

            if (shinyProtected && !attempt.IsShiny)
            {
                throw new InvalidOperationException(
                    "An attempt ignored active shiny protection."
                );
            }

            switch (attempt.Outcome)
            {
                case EncounterAttemptOutcome.NewEntry:
                case EncounterAttemptOutcome.Duplicate:
                    if (attempt.Entry == null ||
                        attempt.Entry.IsShiny != attempt.IsShiny)
                    {
                        throw new InvalidOperationException(
                            "The attempt entry disagrees with its outcome."
                        );
                    }

                    break;

                case EncounterAttemptOutcome.NoAvailableEntry:
                    if (attempt.Entry != null)
                    {
                        throw new InvalidOperationException(
                            "An unavailable attempt must not contain an entry."
                        );
                    }

                    break;

                default:
                    throw new InvalidOperationException(
                        "The attempt returned an unknown outcome."
                    );
            }
        }
    }
}