using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;
using PokeHunter.Configuration;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class EncounterGroupSelectionChecks
    {
        public static void Run(CatalogueDocument catalogue)
        {
            IReadOnlyList<EncounterCategory> categories =
                CreateCategories(catalogue);

            CheckFirstSelection(categories);
            CheckEventSelection(categories);
            CheckEmptyPool();

            Console.WriteLine(
                "All encounter group selection checks passed."
            );
        }

        private static void CheckFirstSelection(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            // Zero selects the first positive-weight interval at each stage.
            var source = new SequenceRandomSource(0.0, 0.0);
            var selector = new EncounterGroupSelector(source);

            EncounterGroupSelection result = selector.Select(categories);

            Require(
                result != null &&
                ReferenceEquals(result.Category, categories[0]) &&
                ReferenceEquals(result.Group, categories[0].Groups[0]),
                "Selection did not preserve the selected category and candidate."
            );

            Require(
                source.DrawCount == 2,
                "Category and group selection must consume two draws."
            );

            Console.WriteLine(
                "PASS: Category and group selection used separate draws."
            );
        }

        private static void CheckEventSelection(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            double totalWeight = 0;
            double precedingWeight = 0;
            EncounterCategory eventCategory = null;

            foreach (EncounterCategory category in categories)
            {
                totalWeight += category.Weight;
            }

            foreach (EncounterCategory category in categories)
            {
                if (category.Id == "event")
                {
                    eventCategory = category;
                    break;
                }

                precedingWeight += category.Weight;
            }

            Require(
                eventCategory != null,
                "The fixture must contain an Event category."
            );

            Require(
                eventCategory.Groups.Count == 1 &&
                eventCategory.Groups[0].GroupId == "pikachu",
                "The fixture Event category must contain only Pikachu."
            );

            // Select the middle of Event's interval, avoiding boundaries.
            double eventDraw =
                (precedingWeight + eventCategory.Weight / 2.0) /
                totalWeight;

            var source = new SequenceRandomSource(eventDraw, 0.0);
            var selector = new EncounterGroupSelector(source);

            EncounterGroupSelection result = selector.Select(categories);

            Require(
                result != null &&
                result.Category.Id == "event" &&
                result.Group.GroupId == "pikachu",
                "Expected Event followed by Pikachu."
            );

            Require(
                result.Group.EntryIds.Count == 1 &&
                result.Group.EntryIds[0] ==
                    "pikachu-libre:female:normal",
                "Event selection included an ineligible Pikachu variant."
            );

            Require(
                source.DrawCount == 2,
                "Event selection must consume exactly two draws."
            );

            Console.WriteLine(
                "PASS: Event selection retained only the eligible costume."
            );
        }

        private static void CheckEmptyPool()
        {
            var source = new SequenceRandomSource();
            var selector = new EncounterGroupSelector(source);

            EncounterGroupSelection result = selector.Select(
                new List<EncounterCategory>()
            );

            Require(
                result == null && source.DrawCount == 0,
                "An empty pool must return null without drawing randomness."
            );

            Console.WriteLine(
                "PASS: An empty category pool returned no selection."
            );
        }

        /// <summary>
        /// Builds normal candidates for Generation 1 plus one event costume.
        /// Fixed dates keep the test independent of today's date.
        /// </summary>
        private static IReadOnlyList<EncounterCategory> CreateCategories(
            CatalogueDocument catalogue
        )
        {
            var start = new DateTimeOffset(
                2026, 1, 1, 0, 0, 0, TimeSpan.Zero
            );

            var end = start.AddYears(1);

            var seasons = new SeasonsConfig
            {
                SchemaVersion = 1,
                TimeZone = "Europe/Oslo",
                Seasons = new List<SeasonDefinition>
                {
                    new SeasonDefinition
                    {
                        Id = "selection-test-season",
                        Name = "Selection Test Season",
                        StartsAt = start,
                        EndsAtExclusive = end,
                        UnlockedGenerations = new List<int> { 1 }
                    }
                }
            };

            var events = new EventsConfig
            {
                SchemaVersion = 1,
                TimeZone = "Europe/Oslo",
                Events = new List<EventDefinition>
                {
                    new EventDefinition
                    {
                        Id = "selection-test-event",
                        Name = "Selection Test Event",
                        Enabled = true,
                        StartsAt = start,
                        EndsAtExclusive = end,
                        CategoryWeight = 4,
                        AllowLockedGenerations = true,
                        RequireUnlockedGenerationForLegendary = true,
                        RequireUnlockedGenerationForMythical = true,
                        Include = new EventInclusionConfig
                        {
                            Types = new List<string>(),
                            EvolutionFamilies = new List<string>(),
                            Forms = new List<string>(),
                            Costumes = new List<string>
                            {
                                "pikachu-libre"
                            }
                        }
                    }
                }
            };

            var index = new CatalogueIndex(catalogue);

            EligibleCataloguePool pool = EligibilityPoolBuilder.Build(
                index,
                seasons,
                events,
                start.AddDays(1)
            );

            var rarityWeights = new Dictionary<string, double>
            {
                { "common", 10 },
                { "uncommon", 6 },
                { "rare", 3 },
                { "ultraRare", 1 }
            };

            return EncounterCategoryBuilder.Build(
                index,
                pool,
                false,
                1.0,
                rarityWeights
            );
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Supplies exact test values and rejects unexpected extra draws.
        /// </summary>
        private sealed class SequenceRandomSource : IRandomSource
        {
            private readonly Queue<double> values;

            public int DrawCount { get; private set; }

            public SequenceRandomSource(params double[] values)
            {
                this.values = new Queue<double>(values);
            }

            public double NextDouble()
            {
                if (values.Count == 0)
                {
                    throw new InvalidOperationException(
                        "An unexpected random draw was requested."
                    );
                }

                DrawCount++;
                return values.Dequeue();
            }
        }
    }
}