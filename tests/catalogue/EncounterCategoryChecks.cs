using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class EncounterCategoryChecks
    {
        public static void Run(
            CatalogueIndex catalogue,
            EligibleCataloguePool pool
        )
        {
            var weights = new Dictionary<string, double>(
                StringComparer.Ordinal
            )
            {
                { "common", 10 },
                { "uncommon", 6 },
                { "rare", 3 },
                { "ultraRare", 1 }
            };

            IReadOnlyList<EncounterCategory> normal =
                EncounterCategoryBuilder.Build(
                    catalogue,
                    pool,
                    false,
                    1,
                    weights
                );

            IReadOnlyList<EncounterCategory> shiny =
                EncounterCategoryBuilder.Build(
                    catalogue,
                    pool,
                    true,
                    1,
                    weights
                );

            CheckStructure(catalogue, pool, normal, false, weights);
            CheckStructure(catalogue, pool, shiny, true, weights);

            Console.WriteLine(
                "PASS: Categories contain unique groups, correct weights, " +
                "and only their requested shiny state."
            );

            // This test event explicitly includes Libre. The ordinary
            // Pikachu variants and costume must still form one group.
            EncounterGroupCandidate electricPikachu = FindGroup(
                FindCategory(normal, "electric"),
                "pikachu"
            );

            Require(
                Contains(electricPikachu.EntryIds, "pikachu:male:normal") &&
                Contains(electricPikachu.EntryIds, "pikachu:female:normal") &&
                Contains(
                    electricPikachu.EntryIds,
                    "pikachu-libre:female:normal"
                ),
                "Electric Pikachu did not retain its eligible variants."
            );

            EncounterGroupCandidate eventPikachu = FindGroup(
                FindCategory(normal, "event"),
                "pikachu"
            );

            Require(
                eventPikachu.EntryIds.Count == 1 &&
                Contains(
                    eventPikachu.EntryIds,
                    "pikachu-libre:female:normal"
                ),
                "The Event category admitted non-event Pikachu variants."
            );

            Console.WriteLine(
                "PASS: Pikachu variants share one group while Event " +
                "membership remains restricted."
            );

            Require(
                FindGroup(
                    FindCategory(normal, "fire"),
                    "marowak-alola"
                ) != null &&
                FindGroup(
                    FindCategory(normal, "ghost"),
                    "marowak-alola"
                ) != null &&
                FindGroup(
                    FindCategory(normal, "event"),
                    "marowak-alola"
                ) != null,
                "An eligible dual-type event Pokémon lost a category."
            );

            Console.WriteLine(
                "PASS: Dual-type event Pokémon appear in both type " +
                "categories and the Event category."
            );

            bool invalidWeightRejected = false;

            try
            {
                EncounterCategoryBuilder.Build(
                    catalogue,
                    pool,
                    false,
                    0,
                    weights
                );
            }
            catch (ArgumentException exception)
            {
                if (!exception.Message.Contains("Type category weight"))
                {
                    throw;
                }

                invalidWeightRejected = true;
            }

            Require(
                invalidWeightRejected,
                "A zero category weight was accepted."
            );

            Console.WriteLine(
                "PASS: An invalid category weight was rejected."
            );
        }

        private static void CheckStructure(
            CatalogueIndex catalogue,
            EligibleCataloguePool pool,
            IReadOnlyList<EncounterCategory> categories,
            bool isShiny,
            Dictionary<string, double> weights
        )
        {
            var categoryIds = new HashSet<string>(StringComparer.Ordinal);
            var eventIds = new HashSet<string>(
                pool.EventEntryIds,
                StringComparer.Ordinal
            );

            var expectedIds = new HashSet<string>(StringComparer.Ordinal);
            var actualIds = new HashSet<string>(StringComparer.Ordinal);
            var expectedEventIds = new HashSet<string>(StringComparer.Ordinal);
            var actualEventIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (string id in pool.EntryIds)
            {
                CatalogueEntry entry = catalogue.FindEntry(id);

                if (entry.IsShiny == isShiny)
                {
                    expectedIds.Add(id);

                    if (eventIds.Contains(id))
                    {
                        expectedEventIds.Add(id);
                    }
                }
            }

            foreach (EncounterCategory category in categories)
            {
                Require(
                    categoryIds.Add(category.Id) && category.Groups.Count > 0,
                    "A category is duplicated or empty."
                );

                Require(
                    category.Weight ==
                    (category.Id == "event" ? pool.EventCategoryWeight : 1),
                    "Incorrect category weight."
                );

                var groupIds = new HashSet<string>(StringComparer.Ordinal);

                foreach (EncounterGroupCandidate group in category.Groups)
                {
                    Require(
                        groupIds.Add(group.GroupId) && group.EntryIds.Count > 0,
                        "A group is duplicated or empty within its category."
                    );

                    CatalogueEncounterGroup definition =
                        catalogue.FindGroup(group.GroupId);

                    Require(
                        group.Weight == weights[definition.RarityClass],
                        "Group weight does not match its rarity."
                    );

                    var memberIds = new HashSet<string>(StringComparer.Ordinal);

                    foreach (string id in group.EntryIds)
                    {
                        CatalogueEntry entry = catalogue.FindEntry(id);

                        Require(
                            memberIds.Add(id) &&
                            entry != null &&
                            entry.IsShiny == isShiny &&
                            entry.EncounterGroupId == group.GroupId &&
                            expectedIds.Contains(id),
                            "A category contains an invalid or duplicate entry."
                        );

                        if (category.Id == "event")
                        {
                            Require(
                                eventIds.Contains(id),
                                "An entry entered Event without eligibility."
                            );

                            actualEventIds.Add(id);
                        }
                        else
                        {
                            Require(
                                entry.Types.Contains(category.Id),
                                "An entry entered an incorrect type category."
                            );

                            actualIds.Add(id);
                        }
                    }
                }
            }

            Require(
                expectedIds.SetEquals(actualIds),
                "Type categories lost eligible entries."
            );

            Require(
                expectedEventIds.SetEquals(actualEventIds),
                "The Event category lost eligible entries."
            );

            // Every eligible entry must appear in every one of its types.
            foreach (string id in expectedIds)
            {
                CatalogueEntry entry = catalogue.FindEntry(id);

                foreach (string type in entry.Types)
                {
                    EncounterGroupCandidate group = FindGroup(
                        FindCategory(categories, type),
                        entry.EncounterGroupId
                    );

                    Require(
                        Contains(group.EntryIds, id),
                        "An eligible entry is missing from one of its types."
                    );
                }
            }
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

            throw new InvalidOperationException(
                "Expected category was not found: " + id + "."
            );
        }

        private static EncounterGroupCandidate FindGroup(
            EncounterCategory category,
            string id
        )
        {
            foreach (EncounterGroupCandidate group in category.Groups)
            {
                if (group.GroupId == id)
                {
                    return group;
                }
            }

            throw new InvalidOperationException(
                "Expected group " + id +
                " was not found in category " + category.Id + "."
            );
        }

        private static bool Contains(
            IReadOnlyList<string> values,
            string expected
        )
        {
            foreach (string value in values)
            {
                if (value == expected)
                {
                    return true;
                }
            }

            return false;
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