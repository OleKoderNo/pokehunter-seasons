using System;
using System.Collections.Generic;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Selects a category and then one of that category's Pokémon groups.
    /// Does not select a collection entry or save a catch.
    /// </summary>
    public sealed class EncounterGroupSelector
    {
        private readonly WeightedRoller roller;

        public EncounterGroupSelector(IRandomSource randomSource)
        {
            roller = new WeightedRoller(randomSource);
        }

        /// <summary>
        /// Returns null for an empty pool without consuming randomness.
        /// The encounter engine will treat that as a failed attempt.
        /// Invalid populated pools throw instead of becoming gameplay failures.
        /// </summary>
        public EncounterGroupSelection Select(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            if (categories == null)
            {
                throw new ArgumentNullException(nameof(categories));
            }

            if (categories.Count == 0)
            {
                return null;
            }

            // Builders produce populated categories. Check that contract
            // before consuming randomness.
            foreach (EncounterCategory candidate in categories)
            {
                if (candidate == null ||
                    candidate.Groups == null ||
                    candidate.Groups.Count == 0)
                {
                    throw new ArgumentException(
                        "Every category must contain at least one group.",
                        nameof(categories)
                    );
                }

                foreach (EncounterGroupCandidate group in candidate.Groups)
                {
                    if (group == null ||
                        group.EntryIds == null ||
                        group.EntryIds.Count == 0)
                    {
                        throw new ArgumentException(
                            "Every group must contain eligible entry IDs.",
                            nameof(categories)
                        );
                    }
                }
            }

            EncounterCategory selectedCategory = roller.Roll(
                categories,
                category => category.Weight
            );

            EncounterGroupCandidate selectedGroup = roller.Roll(
                selectedCategory.Groups,
                group => group.Weight
            );

            // Keep this exact candidate. A catalogue-wide group lookup
            // could reintroduce entries excluded from the chosen category.
            return new EncounterGroupSelection(
                selectedCategory,
                selectedGroup
            );
        }
    }
}