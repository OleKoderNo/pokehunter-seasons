using System;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Completed gameplay outcomes stored in redemption_results.
    /// Technical errors are not ordinary failed redemptions.
    /// </summary>
    public enum StoredRedemptionOutcome
    {
        Caught,
        Failed
    }

    /// <summary>
    /// Immutable snapshot of a completed redemption read from SQLite.
    ///
    /// A caught result is also its catch record. Failed results have
    /// no entry, shiny flag, category, group, or entry snapshot.
    ///
    /// This object does not execute SQL or announce a catch.
    /// </summary>
    public sealed class StoredRedemption
    {
        /// <summary>
        /// Permanent redemption identity.
        /// Repeated delivery must resolve to the same saved result.
        /// </summary>
        public string RedemptionId { get; private set; }

        /// <summary>
        /// Permanent Twitch user ID, not a changeable display name.
        /// </summary>
        public string TwitchUserId { get; private set; }

        /// <summary>
        /// Stored season ID associated with this redemption.
        /// </summary>
        public string SeasonId { get; private set; }

        /// <summary>
        /// UTC instant at which the game accepted the redemption.
        /// </summary>
        public DateTimeOffset AcceptedAtUtc { get; private set; }

        /// <summary>
        /// UTC completion timestamp recorded for this result.
        /// This is not an exact measurement of the database commit time.
        /// </summary>
        public DateTimeOffset CompletedAtUtc { get; private set; }

        /// <summary>
        /// Active event associated with the redemption, or null.
        /// An active event does not mean the Event category was selected.
        /// </summary>
        public string EventId { get; private set; }

        public StoredRedemptionOutcome Outcome { get; private set; }

        /// <summary>
        /// Consecutive failed redemptions before this session.
        /// </summary>
        public int PreviousFailureStreak { get; private set; }

        /// <summary>
        /// Attempt budget: previous failure streak plus one.
        /// </summary>
        public int AvailableAttempts { get; private set; }

        /// <summary>
        /// Attempts actually used before success or budget exhaustion.
        /// </summary>
        public int AttemptsUsed { get; private set; }

        /// <summary>
        /// Zero after success; previous streak plus one after full failure.
        /// </summary>
        public int NextFailureStreak { get; private set; }

        /// <summary>
        /// Permanent caught collection identity, or null after failure.
        /// </summary>
        public string EntryId { get; private set; }

        /// <summary>
        /// True for a shiny catch, false for a normal catch,
        /// and null when no Pokémon was caught.
        /// </summary>
        public bool? IsShiny { get; private set; }

        /// <summary>
        /// Category that produced the successful catch, or null.
        /// </summary>
        public string CategoryId { get; private set; }

        /// <summary>
        /// Encounter group that produced the successful catch, or null.
        /// </summary>
        public string GroupId { get; private set; }

        /// <summary>
        /// Serialized caught-entry metadata preserved with the result.
        /// Null after failure.
        ///
        /// Keeping this snapshot lets historical catches retain their
        /// recorded metadata when the generated catalogue changes.
        /// Validation belongs to the save/read implementation.
        /// </summary>
        public string EntrySnapshotJson { get; private set; }

        /// <summary>
        /// Convenience property derived from the stored outcome.
        /// It is not an independently stored success flag.
        /// </summary>
        public bool Succeeded
        {
            get { return Outcome == StoredRedemptionOutcome.Caught; }
        }

        /// <summary>
        /// Used by storage code after validating a database record.
        /// Calling code receives snapshots through the repository.
        /// </summary>
        internal StoredRedemption(
            string redemptionId,
            string twitchUserId,
            string seasonId,
            DateTimeOffset acceptedAtUtc,
            DateTimeOffset completedAtUtc,
            string eventId,
            StoredRedemptionOutcome outcome,
            int previousFailureStreak,
            int availableAttempts,
            int attemptsUsed,
            int nextFailureStreak,
            string entryId,
            bool? isShiny,
            string categoryId,
            string groupId,
            string entrySnapshotJson
        )
        {
            RedemptionId = redemptionId;
            TwitchUserId = twitchUserId;
            SeasonId = seasonId;
            AcceptedAtUtc = acceptedAtUtc;
            CompletedAtUtc = completedAtUtc;
            EventId = eventId;
            Outcome = outcome;
            PreviousFailureStreak = previousFailureStreak;
            AvailableAttempts = availableAttempts;
            AttemptsUsed = attemptsUsed;
            NextFailureStreak = nextFailureStreak;
            EntryId = entryId;
            IsShiny = isShiny;
            CategoryId = categoryId;
            GroupId = groupId;
            EntrySnapshotJson = entrySnapshotJson;
        }
    }
}