namespace PokeHunter.Encounters
{
    /// <summary>
    /// The category and group selected for one encounter attempt.
    /// Gender, costume selection, and ownership checks happen later.
    /// </summary>
    public sealed class EncounterGroupSelection
    {
        public EncounterCategory Category { get; private set; }

        public EncounterGroupCandidate Group { get; private set; }

        internal EncounterGroupSelection(
            EncounterCategory category,
            EncounterGroupCandidate group
        )
        {
            Category = category;
            Group = group;
        }
    }
}