using System;
using System.Collections.Generic;

namespace PokeHunter.Storage
{
    /// <summary>
    /// An immutable snapshot of a season saved in the database.
    /// Active-season selection still belongs to the configuration code.
    /// </summary>
    public sealed class StoredSeason
    {
        /// <summary>
        /// Stable season identifier, such as "season-1".
        /// Future catches will reference this identifier.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Saved display name for collection history.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Inclusive starting instant, normalized to UTC.
        /// </summary>
        public DateTimeOffset StartsAtUtc { get; }

        /// <summary>
        /// Exclusive ending instant, normalized to UTC.
        /// The season is no longer active at this exact instant.
        /// </summary>
        public DateTimeOffset EndsAtExclusiveUtc { get; }

        /// <summary>
        /// Generation numbers associated with this saved season.
        /// The returned collection cannot be modified.
        /// </summary>
        public IReadOnlyList<int> UnlockedGenerations { get; }

        /// <summary>
        /// When the season was first registered in this database.
        /// This is separate from the season's starting instant.
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; }

        /// <summary>
        /// Storage code creates snapshots from saved values.
        /// Copies the generation list so callers cannot mutate the snapshot
        /// by changing the original collection.
        /// </summary>
        internal StoredSeason(
            string id,
            string name,
            DateTimeOffset startsAtUtc,
            DateTimeOffset endsAtExclusiveUtc,
            IEnumerable<int> unlockedGenerations,
            DateTimeOffset createdAtUtc
        )
        {
            if (unlockedGenerations == null)
            {
                throw new ArgumentNullException(
                    nameof(unlockedGenerations)
                );
            }

            Id = id;
            Name = name;
            StartsAtUtc = startsAtUtc;
            EndsAtExclusiveUtc = endsAtExclusiveUtc;
            CreatedAtUtc = createdAtUtc;

            UnlockedGenerations =
                new List<int>(unlockedGenerations).AsReadOnly();
        }
    }
}