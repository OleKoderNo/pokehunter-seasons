using System.Collections.Generic;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Available collection identities for one season/event evaluation.
    ///
    /// Contains both normal and shiny entries. Ownership, shiny rolls,
    /// completed categories, and variant selection are handled later.
    /// </summary>
    public sealed class EligibleCataloguePool
    {
        public string SeasonId { get; private set; }

        /// <summary>
        /// Null when no event is active.
        /// </summary>
        public string EventId { get; private set; }

        /// <summary>
        /// Configured Event category weight, or zero without an event.
        /// An empty Event pool must not become a selectable category.
        /// </summary>
        public double EventCategoryWeight { get; private set; }

        /// <summary>
        /// All eligible entries, including eligible event additions.
        /// These entries will supply the ordinary type categories.
        /// </summary>
        public IReadOnlyList<string> EntryIds { get; private set; }

        /// <summary>
        /// Eligible entries matching the active event.
        /// Every ID here also appears in EntryIds.
        /// </summary>
        public IReadOnlyList<string> EventEntryIds { get; private set; }

        internal EligibleCataloguePool(
            string seasonId,
            string eventId,
            double eventCategoryWeight,
            List<string> entryIds,
            List<string> eventEntryIds
        )
        {
            SeasonId = seasonId;
            EventId = eventId;
            EventCategoryWeight = eventCategoryWeight;

            EntryIds = new List<string>(entryIds).AsReadOnly();
            EventEntryIds = new List<string>(eventEntryIds).AsReadOnly();
        }
    }
}