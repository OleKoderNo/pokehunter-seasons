using System;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Immutable input for saving one completed redemption.
    ///
    /// ExpectedCollection is the snapshot used when rolling.
    /// The repository must check it against current database state
    /// inside the write transaction.
    ///
    /// Constructing this object does not save anything.
    /// </summary>
    public sealed class RedemptionSaveRequest
    {
        public string RedemptionId { get; private set; }

        public CollectionSnapshot ExpectedCollection { get; private set; }

        public DateTimeOffset AcceptedAtUtc { get; private set; }

        public DateTimeOffset CompletedAtUtc { get; private set; }

        public string EventId { get; private set; }

        public StoredRedemptionOutcome Outcome { get; private set; }

        public int AttemptsUsed { get; private set; }

        public string EntryId { get; private set; }

        public bool? IsShiny { get; private set; }

        public string CategoryId { get; private set; }

        public string GroupId { get; private set; }

        /// <summary>
        /// Complete serialized caught-entry metadata.
        /// The repository must validate its JSON and identity agreement.
        /// Null for failed redemptions.
        /// </summary>
        public string EntrySnapshotJson { get; private set; }

        public int PreviousFailureStreak
        {
            get
            {
                return ExpectedCollection.ConsecutiveFailedRedemptions;
            }
        }

        public int AvailableAttempts
        {
            get { return PreviousFailureStreak + 1; }
        }

        public int NextFailureStreak
        {
            get
            {
                return Outcome == StoredRedemptionOutcome.Caught
                    ? 0
                    : PreviousFailureStreak + 1;
            }
        }

        /// <summary>
        /// Creates a request for a successful catch.
        /// Success can occur before the attempt budget is exhausted.
        /// </summary>
        public static RedemptionSaveRequest ForCatch(
            string redemptionId,
            CollectionSnapshot expectedCollection,
            DateTimeOffset acceptedAt,
            DateTimeOffset completedAt,
            string eventId,
            int attemptsUsed,
            string entryId,
            bool isShiny,
            string categoryId,
            string groupId,
            string entrySnapshotJson
        )
        {
            RequireText(entryId, nameof(entryId));
            RequireText(categoryId, nameof(categoryId));
            RequireText(groupId, nameof(groupId));

            // JSON can legitimately contain whitespace and line breaks.
            // Its structure will be checked by the storage implementation.
            if (string.IsNullOrWhiteSpace(entrySnapshotJson))
            {
                throw new ArgumentException(
                    "A caught-entry JSON snapshot is required.",
                    nameof(entrySnapshotJson)
                );
            }

            return new RedemptionSaveRequest(
                redemptionId,
                expectedCollection,
                acceptedAt,
                completedAt,
                eventId,
                StoredRedemptionOutcome.Caught,
                attemptsUsed,
                entryId,
                isShiny,
                categoryId,
                groupId,
                entrySnapshotJson
            );
        }

        /// <summary>
        /// Creates a request for an ordinary gameplay failure.
        /// Every available attempt must have been used.
        /// Do not use this for database errors or interrupted execution.
        /// </summary>
        public static RedemptionSaveRequest ForFailure(
            string redemptionId,
            CollectionSnapshot expectedCollection,
            DateTimeOffset acceptedAt,
            DateTimeOffset completedAt,
            string eventId,
            int attemptsUsed
        )
        {
            return new RedemptionSaveRequest(
                redemptionId,
                expectedCollection,
                acceptedAt,
                completedAt,
                eventId,
                StoredRedemptionOutcome.Failed,
                attemptsUsed,
                null,
                null,
                null,
                null,
                null
            );
        }

        private RedemptionSaveRequest(
            string redemptionId,
            CollectionSnapshot expectedCollection,
            DateTimeOffset acceptedAt,
            DateTimeOffset completedAt,
            string eventId,
            StoredRedemptionOutcome outcome,
            int attemptsUsed,
            string entryId,
            bool? isShiny,
            string categoryId,
            string groupId,
            string entrySnapshotJson
        )
        {
            RequireText(redemptionId, nameof(redemptionId));

            if (expectedCollection == null)
            {
                throw new ArgumentNullException(
                    nameof(expectedCollection)
                );
            }

            if (eventId != null)
            {
                RequireText(eventId, nameof(eventId));
            }

            if (acceptedAt == default(DateTimeOffset))
            {
                throw new ArgumentException(
                    "An acceptance timestamp is required.",
                    nameof(acceptedAt)
                );
            }

            if (completedAt < acceptedAt)
            {
                throw new ArgumentException(
                    "Completion cannot be earlier than acceptance.",
                    nameof(completedAt)
                );
            }

            int previous =
                expectedCollection.ConsecutiveFailedRedemptions;

            // Both the attempt budget and a failed result need previous + 1.
            // Reject the limit before performing that addition.
            if (previous < 0 || previous == int.MaxValue)
            {
                throw new ArgumentException(
                    "The failure streak cannot produce a supported " +
                    "attempt budget.",
                    nameof(expectedCollection)
                );
            }

            int available = previous + 1;

            if (attemptsUsed < 1 || attemptsUsed > available)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(attemptsUsed),
                    "Used attempts must be between one and the " +
                    "available attempt budget."
                );
            }

            if (outcome == StoredRedemptionOutcome.Failed &&
                attemptsUsed != available)
            {
                throw new ArgumentException(
                    "A failed redemption must use every available attempt.",
                    nameof(attemptsUsed)
                );
            }

            RedemptionId = redemptionId;
            ExpectedCollection = expectedCollection;
            AcceptedAtUtc = acceptedAt.ToUniversalTime();
            CompletedAtUtc = completedAt.ToUniversalTime();
            EventId = eventId;
            Outcome = outcome;
            AttemptsUsed = attemptsUsed;
            EntryId = entryId;
            IsShiny = isShiny;
            CategoryId = categoryId;
            GroupId = groupId;
            EntrySnapshotJson = entrySnapshotJson;
        }

        private static void RequireText(
            string value,
            string parameterName
        )
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value != value.Trim())
            {
                throw new ArgumentException(
                    "A nonempty value without surrounding whitespace " +
                    "is required.",
                    parameterName
                );
            }

            foreach (char character in value)
            {
                if (char.IsControl(character))
                {
                    throw new ArgumentException(
                        "Control characters are not allowed.",
                        parameterName
                    );
                }
            }
        }
    }
}