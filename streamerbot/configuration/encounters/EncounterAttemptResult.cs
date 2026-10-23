using PokeHunter.Catalogue;

namespace PokeHunter.Encounters
{
    public enum EncounterAttemptOutcome
    {
        NewEntry,
        Duplicate,
        NoAvailableEntry
    }

    /// <summary>
    /// Describes one attempt, not a committed catch.
    /// IsShiny remains available even when the attempt fails.
    /// </summary>
    public sealed class EncounterAttemptResult
    {
        public EncounterAttemptOutcome Outcome { get; private set; }

        public bool IsShiny { get; private set; }

        // Null when no category/group could be selected.
        public string CategoryId { get; private set; }

        public string GroupId { get; private set; }

        // Null when no exact entry could be selected.
        public CatalogueEntry Entry { get; private set; }

        internal EncounterAttemptResult(
            EncounterAttemptOutcome outcome,
            bool isShiny,
            string categoryId,
            string groupId,
            CatalogueEntry entry
        )
        {
            Outcome = outcome;
            IsShiny = isShiny;
            CategoryId = categoryId;
            GroupId = groupId;
            Entry = entry;
        }
    }
}