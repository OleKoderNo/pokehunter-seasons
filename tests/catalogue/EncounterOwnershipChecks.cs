using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class EncounterOwnershipChecks
    {
        public static void Run(CatalogueDocument catalogue)
        {
            IReadOnlyList<EncounterCategory> categories =
                EncounterGroupSelectionChecks.CreateCategories(catalogue);

            CheckNewTrainer(categories);
            CheckCompleteCollection(categories);
            CheckOneMissingEntry(categories);
            CheckSeparateShinyOwnership(categories);
            CheckEventReopensCategory(categories);

            Console.WriteLine(
                "All encounter ownership checks passed."
            );
        }

        private static void CheckNewTrainer(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            IReadOnlyList<EncounterCategory> result =
                EncounterOwnershipFilter.Filter(
                    categories,
                    new string[0]
                );

            Require(
                result.Count == categories.Count,
                "A new trainer must retain all eligible categories."
            );

            for (int index = 0; index < categories.Count; index++)
            {
                Require(
                    ReferenceEquals(result[index], categories[index]),
                    "Filtering changed category order or candidates."
                );
            }

            Console.WriteLine(
                "PASS: A trainer with no ownership retained every category."
            );
        }

        private static void CheckCompleteCollection(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            HashSet<string> owned = CollectEntryIds(categories);

            IReadOnlyList<EncounterCategory> result =
                EncounterOwnershipFilter.Filter(categories, owned);

            Require(
                result.Count == 0,
                "A completed collection must leave no categories for that state."
            );

            Require(
                categories.Count > 0,
                "Filtering modified the original category collection."
            );

            Console.WriteLine(
                "PASS: Complete ownership removed all categories for that state."
            );
        }

        private static void CheckOneMissingEntry(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            const string missingId = "pikachu:male:normal";

            HashSet<string> owned = CollectEntryIds(categories);

            Require(
                owned.Remove(missingId),
                "The fixture must include regular male Pikachu."
            );

            int originalOwnedCount = owned.Count;

            IReadOnlyList<EncounterCategory> result =
                EncounterOwnershipFilter.Filter(categories, owned);

            // This fixture includes Pikachu in Electric, while Event
            // includes only Libre. All entries except regular male Pikachu
            // are owned, so only Electric should remain.
            Require(
                result.Count == 1 && result[0].Id == "electric",
                "Only Electric should remain when regular Pikachu is missing."
            );

            EncounterCategory original = FindCategory(
                categories,
                "electric"
            );

            Require(
                ReferenceEquals(result[0], original),
                "An incomplete category must retain its original candidates."
            );

            bool retainedOwnedEntry = false;

            foreach (EncounterGroupCandidate group in result[0].Groups)
            {
                foreach (string entryId in group.EntryIds)
                {
                    if (owned.Contains(entryId))
                    {
                        retainedOwnedEntry = true;
                    }
                }
            }

            Require(
                retainedOwnedEntry,
                "Owned entries must remain possible duplicates."
            );

            Require(
                owned.Count == originalOwnedCount &&
                !owned.Contains(missingId),
                "Filtering changed the supplied ownership."
            );

            Console.WriteLine(
                "PASS: An incomplete category retained owned duplicate candidates."
            );
        }

        private static void CheckSeparateShinyOwnership(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            var shinyOwnership = new List<string>();

            foreach (string entryId in CollectEntryIds(categories))
            {
                Require(
                    entryId.EndsWith(":normal", StringComparison.Ordinal),
                    "This fixture must contain only normal entries."
                );

                shinyOwnership.Add(
                    entryId.Substring(
                        0,
                        entryId.Length - ":normal".Length
                    ) + ":shiny"
                );
            }

            IReadOnlyList<EncounterCategory> result =
                EncounterOwnershipFilter.Filter(
                    categories,
                    shinyOwnership
                );

            Require(
                result.Count == categories.Count,
                "Shiny ownership must not complete normal categories."
            );

            Console.WriteLine(
                "PASS: Shiny ownership did not complete normal categories."
            );
        }

        private static void CheckEventReopensCategory(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            const string costumeId = "pikachu-libre:female:normal";

            HashSet<string> owned = CollectEntryIds(categories);

            Require(
                owned.Remove(costumeId),
                "The fixture must include the event costume."
            );

            IReadOnlyList<EncounterCategory> result =
                EncounterOwnershipFilter.Filter(categories, owned);

            Require(
                result.Count == 2 &&
                FindCategory(result, "electric") != null &&
                FindCategory(result, "event") != null,
                "The missing costume must keep Electric and Event available."
            );

            // Once that newly available entry is owned, both complete.
            owned.Add(costumeId);

            Require(
                EncounterOwnershipFilter.Filter(categories, owned).Count == 0,
                "Catching the remaining costume must complete both categories."
            );

            Console.WriteLine(
                "PASS: An unowned event entry kept its type and Event available."
            );
        }

        private static HashSet<string> CollectEntryIds(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);

            foreach (EncounterCategory category in categories)
            {
                foreach (EncounterGroupCandidate group in category.Groups)
                {
                    foreach (string entryId in group.EntryIds)
                    {
                        ids.Add(entryId);
                    }
                }
            }

            return ids;
        }

        private static EncounterCategory FindCategory(
            IReadOnlyList<EncounterCategory> categories,
            string id
        )
        {
            foreach (EncounterCategory category in categories)
            {
                if (category.Id == id)
                {
                    return category;
                }
            }

            return null;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}