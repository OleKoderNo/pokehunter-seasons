using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;
using PokeHunter.Configuration;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    /// <summary>
    /// Checks separate gender identities using real catalogue entries.
    /// Does not write player data or depend on today's date.
    /// </summary>
    internal static class GenderCollectionChecks
    {
        public static void Run(CatalogueIndex catalogue)
        {
            var seasons = new SeasonsConfig
            {
                SchemaVersion = 1,
                TimeZone = "Europe/Oslo",
                Seasons = new List<SeasonDefinition>
                {
                    new SeasonDefinition
                    {
                        Id = "gender-test-season",
                        Name = "Gender Test Season",
                        StartsAt = new DateTimeOffset(
                            2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)
                        ),
                        EndsAtExclusive = new DateTimeOffset(
                            2027, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)
                        ),
                        UnlockedGenerations = new List<int> { 1 }
                    }
                }
            };

            var eventStart = new DateTimeOffset(
                2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(2)
            );

            var eventEnd = new DateTimeOffset(
                2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(1)
            );

            var events = new EventsConfig
            {
                SchemaVersion = 1,
                TimeZone = "Europe/Oslo",
                Events = new List<EventDefinition>
                {
                    new EventDefinition
                    {
                        Id = "gender-test-event",
                        Name = "Gender Test Event",
                        Enabled = true,
                        StartsAt = eventStart,
                        EndsAtExclusive = eventEnd,
                        CategoryWeight = 4,
                        AllowLockedGenerations = true,
                        RequireUnlockedGenerationForLegendary = true,
                        RequireUnlockedGenerationForMythical = true,
                        Include = new EventInclusionConfig
                        {
                            Types = new List<string>(),
                            EvolutionFamilies =
                                new List<string> { "delphox" },
                            Forms = new List<string>(),
                            Costumes = new List<string>()
                        }
                    }
                }
            };

            EligibleCataloguePool before = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                eventStart.AddTicks(-1)
            );

            EligibleCataloguePool active = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                eventStart
            );

            EligibleCataloguePool ended = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                eventEnd
            );

            var activeGeneral = new HashSet<string>(
                active.EntryIds,
                StringComparer.Ordinal
            );

            var activeEvent = new HashSet<string>(
                active.EventEntryIds,
                StringComparer.Ordinal
            );

            var beforeIds = new HashSet<string>(
                before.EntryIds,
                StringComparer.Ordinal
            );

            var endedIds = new HashSet<string>(
                ended.EntryIds,
                StringComparer.Ordinal
            );

            foreach (string gender in new[] { "male", "female" })
            {
                foreach (string state in new[] { "normal", "shiny" })
                {
                    string id = "braixen:" + gender + ":" + state;
                    CatalogueEntry entry = catalogue.FindEntry(id);

                    Require(
                        entry != null &&
                        entry.Gender == gender &&
                        entry.IsShiny == (state == "shiny") &&
                        !entry.HasGenderDifference,
                        "Braixen must have separate gender identities " +
                        "without a visible gender difference: " + id + "."
                    );

                    Require(
                        activeGeneral.Contains(id) &&
                        activeEvent.Contains(id),
                        "The active family event did not include " + id + "."
                    );

                    Require(
                        !beforeIds.Contains(id) &&
                        !endedIds.Contains(id),
                        "Locked Braixen remained available outside " +
                        "the event: " + id + "."
                    );
                }
            }

            Console.WriteLine(
                "PASS: Both Braixen genders and shiny states followed " +
                "event eligibility and exact schedule boundaries."
            );

            var rarityWeights = new Dictionary<string, double>(
                StringComparer.Ordinal
            )
            {
                { "common", 10 },
                { "uncommon", 6 },
                { "rare", 3 },
                { "ultraRare", 1 }
            };

            IReadOnlyList<EncounterCategory> normalCategories =
                EncounterCategoryBuilder.Build(
                    catalogue,
                    active,
                    false,
                    1,
                    rarityWeights
                );

            IReadOnlyList<EncounterCategory> shinyCategories =
                EncounterCategoryBuilder.Build(
                    catalogue,
                    active,
                    true,
                    1,
                    rarityWeights
                );

            var ownedMale = new[] { "braixen:male:normal" };

            IReadOnlyList<EncounterCategory> filteredNormal =
                EncounterOwnershipFilter.Filter(
                    normalCategories,
                    ownedMale
                );

            List<CatalogueEntry> normalEntries = GetBraixenEventEntries(
                catalogue,
                filteredNormal,
                false
            );

            // Owning the male must not remove the female.
            // Regular male entries also remain possible duplicates.
            Require(
                Select(normalEntries, ownedMale, 0.499999).Id ==
                    "braixen:male:normal",
                "An owned male Braixen must remain a possible duplicate."
            );

            Require(
                Select(normalEntries, ownedMale, 0.5).Id ==
                    "braixen:female:normal",
                "Owning male Braixen must not prevent selecting the female."
            );

            var ownedFemale = new[] { "braixen:female:normal" };

            Require(
                Select(normalEntries, ownedFemale, 0.1).Id ==
                    "braixen:male:normal",
                "Owning female Braixen must not prevent selecting the male."
            );

            Console.WriteLine(
                "PASS: Braixen selection respected the 50/50 boundary; " +
                "owning one gender did not complete the other."
            );

            var ownedNormalPair = new[]
            {
                "braixen:male:normal",
                "braixen:female:normal"
            };

            IReadOnlyList<EncounterCategory> filteredShiny =
                EncounterOwnershipFilter.Filter(
                    shinyCategories,
                    ownedNormalPair
                );

            List<CatalogueEntry> shinyEntries = GetBraixenEventEntries(
                catalogue,
                filteredShiny,
                true
            );

            Require(
                Select(shinyEntries, ownedNormalPair, 0.1).Id ==
                    "braixen:male:shiny",
                "Normal ownership must not prevent a male shiny catch."
            );

            Require(
                Select(shinyEntries, ownedNormalPair, 0.9).Id ==
                    "braixen:female:shiny",
                "Normal ownership must not prevent a female shiny catch."
            );

            Console.WriteLine(
                "PASS: Owning both normal Braixen genders left " +
                "both shiny genders selectable."
            );

            Console.WriteLine(
                "All gender collection checks passed."
            );
        }

        /// <summary>
        /// Reads only the selected Event category's Braixen candidates.
        /// Also verifies that both genders share exactly one group.
        /// </summary>
        private static List<CatalogueEntry> GetBraixenEventEntries(
            CatalogueIndex catalogue,
            IReadOnlyList<EncounterCategory> categories,
            bool isShiny
        )
        {
            EncounterGroupCandidate matchingGroup = null;
            int matches = 0;

            foreach (EncounterCategory category in categories)
            {
                if (category.Id != "event")
                {
                    continue;
                }

                foreach (EncounterGroupCandidate group in category.Groups)
                {
                    if (group.GroupId == "braixen")
                    {
                        matchingGroup = group;
                        matches++;
                    }
                }
            }

            Require(
                matches == 1 && matchingGroup != null,
                "The Event category must contain exactly one Braixen group."
            );

            string state = isShiny ? "shiny" : "normal";

            var expectedIds = new HashSet<string>(
                StringComparer.Ordinal
            )
            {
                "braixen:male:" + state,
                "braixen:female:" + state
            };

            Require(
                matchingGroup.EntryIds.Count == 2 &&
                expectedIds.SetEquals(matchingGroup.EntryIds),
                "Braixen candidates must contain exactly the male " +
                "and female entries for the requested shiny state."
            );

            var entries = new List<CatalogueEntry>();

            foreach (string id in matchingGroup.EntryIds)
            {
                CatalogueEntry entry = catalogue.FindEntry(id);

                Require(
                    entry != null &&
                    entry.EncounterGroupId == "braixen" &&
                    entry.IsShiny == isShiny,
                    "Braixen candidates disagree with the catalogue."
                );

                entries.Add(entry);
            }

            return entries;
        }

        /// <summary>
        /// Supplies exactly two draws: gender, then form.
        /// Unexpected additional or missing draws fail the check.
        /// </summary>
        private static CatalogueEntry Select(
            IReadOnlyList<CatalogueEntry> entries,
            IEnumerable<string> owned,
            double genderDraw
        )
        {
            var source = new SequenceRandomSource(genderDraw, 0.25);
            var selector = new EncounterVariantSelector(source);

            CatalogueEntry selected = selector.Select(entries, owned);

            Require(selected != null, "Expected a selectable Braixen entry.");

            Require(
                source.Remaining == 0,
                "Gender and form selection must consume both supplied draws."
            );

            return selected;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class SequenceRandomSource : IRandomSource
        {
            private readonly Queue<double> values;

            public int Remaining
            {
                get { return values.Count; }
            }

            public SequenceRandomSource(params double[] draws)
            {
                values = new Queue<double>(draws);
            }

            public double NextDouble()
            {
                if (values.Count == 0)
                {
                    throw new InvalidOperationException(
                        "An unexpected random draw was requested."
                    );
                }

                return values.Dequeue();
            }
        }
    }
}