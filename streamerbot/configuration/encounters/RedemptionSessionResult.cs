using System.Collections.Generic;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Proposed outcome of one redemption.
    /// Nothing in this result has been committed to storage yet.
    /// </summary>
    public sealed class RedemptionSessionResult
    {
        public int AvailableAttempts { get; private set; }

        public int AttemptsUsed
        {
            get { return Attempts.Count; }
        }

        public int NextFailureStreak { get; private set; }

        public IReadOnlyList<EncounterAttemptResult> Attempts
        {
            get;
            private set;
        }

        /// <summary>
        /// The successful attempt, or null when every attempt failed.
        /// A successful selection still needs to be saved transactionally.
        /// </summary>
        public EncounterAttemptResult SuccessfulAttempt { get; private set; }

        public bool Succeeded
        {
            get { return SuccessfulAttempt != null; }
        }

        internal RedemptionSessionResult(
            int availableAttempts,
            int nextFailureStreak,
            List<EncounterAttemptResult> attempts,
            EncounterAttemptResult successfulAttempt
        )
        {
            AvailableAttempts = availableAttempts;
            NextFailureStreak = nextFailureStreak;
            Attempts = new List<EncounterAttemptResult>(attempts).AsReadOnly();
            SuccessfulAttempt = successfulAttempt;
        }
    }
}