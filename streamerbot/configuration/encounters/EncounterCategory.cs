using System.Collections.Generic;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// One selectable category, such as electric, ghost, or event.
    ///
    /// Category weight is used before selecting a Pokémon group.
    /// These records have not yet been filtered by viewer ownership.
    /// </summary>
    public sealed class EncounterCategory
    {
        public string Id { get; private set; }
        public double Weight { get; private set; }

        public IReadOnlyList<EncounterGroupCandidate> Groups
        {
            get;
            private set;
        }

        internal EncounterCategory(
            string id,
            double weight,
            List<EncounterGroupCandidate> groups
        )
        {
            Id = id;
            Weight = weight;
            Groups = new List<EncounterGroupCandidate>(groups).AsReadOnly();
        }
    }

    /// <summary>
    /// One Pokémon group's candidate entries within a specific category.
    ///
    /// The group receives one weight regardless of its variant count.
    /// Entry IDs contain only the requested shiny state and entries
    /// eligible for this particular category.
    /// </summary>
    public sealed class EncounterGroupCandidate
    {
        public string GroupId { get; private set; }
        public double Weight { get; private set; }

        public IReadOnlyList<string> EntryIds { get; private set; }

        internal EncounterGroupCandidate(
            string groupId,
            double weight,
            List<string> entryIds
        )
        {
            GroupId = groupId;
            Weight = weight;
            EntryIds = new List<string>(entryIds).AsReadOnly();
        }
    }
}