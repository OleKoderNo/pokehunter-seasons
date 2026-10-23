using System;
using System.Collections.Generic;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Removes categories completed in the viewer's current Seasonal Dex.
    /// Retains all candidates inside incomplete categories.
    /// </summary>
    public static class EncounterOwnershipFilter
    {
        /// <summary>
        /// Categories must already represent one shiny state and the
        /// currently eligible season/event entries.
        ///
        /// ownedEntryIds must contain ownership for the current season,
        /// not the viewer's lifetime National Dex.
        /// </summary>
        public static IReadOnlyList<EncounterCategory> Filter(
            IReadOnlyList<EncounterCategory> categories,
            IEnumerable<string> ownedEntryIds
        )
        {
            if (categories == null)
            {
                throw new ArgumentNullException(nameof(categories));
            }

            if (ownedEntryIds == null)
            {
                throw new ArgumentNullException(nameof(ownedEntryIds));
            }

            // Collection identities use exact, case-sensitive matching.
            var owned = new HashSet<string>(StringComparer.Ordinal);

            foreach (string entryId in ownedEntryIds)
            {
                if (string.IsNullOrWhiteSpace(entryId) ||
                    entryId != entryId.Trim())
                {
                    throw new ArgumentException(
                        "Owned entry IDs must be nonempty and have " +
                        "no surrounding whitespace.",
                        nameof(ownedEntryIds)
                    );
                }

                owned.Add(entryId);
            }

            var remaining = new List<EncounterCategory>();

            foreach (EncounterCategory category in categories)
            {
                if (category == null ||
                    category.Groups == null ||
                    category.Groups.Count == 0)
                {
                    throw new ArgumentException(
                        "Every category must contain candidate groups.",
                        nameof(categories)
                    );
                }

                bool hasMissingEntry = false;

                foreach (EncounterGroupCandidate group in category.Groups)
                {
                    if (group == null ||
                        group.EntryIds == null ||
                        group.EntryIds.Count == 0)
                    {
                        throw new ArgumentException(
                            "Every candidate group must contain entry IDs.",
                            nameof(categories)
                        );
                    }

                    foreach (string entryId in group.EntryIds)
                    {
                        if (string.IsNullOrWhiteSpace(entryId))
                        {
                            throw new ArgumentException(
                                "Candidate entry IDs must not be empty.",
                                nameof(categories)
                            );
                        }

                        if (!owned.Contains(entryId))
                        {
                            hasMissingEntry = true;
                        }
                    }
                }

                if (hasMissingEntry)
                {
                    // Preserve every candidate, including owned entries.
                    // Removing individual owned entries here would prevent
                    // the duplicate encounters required by the game rules.
                    remaining.Add(category);
                }
            }

            return remaining.AsReadOnly();
        }
    }
}