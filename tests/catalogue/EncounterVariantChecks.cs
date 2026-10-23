using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class EncounterVariantChecks
    {
        public static void Run()
        {
            CheckEqualVariants();
            CheckRegularDuplicate();
            CheckCostumeProtection();
            CheckCompletedCostume();
            CheckSingleGenderAndCategoryScope();
            CheckShinySeparation();

            Console.WriteLine(
                "All encounter variant checks passed."
            );
        }

        private static void CheckEqualVariants()
        {
            List<CatalogueEntry> entries = CreatePair();
            int regular = 0;
            int costume = 0;

            // Sample the middle of ten equally spaced variant intervals.
            for (int index = 0; index < 10; index++)
            {
                CatalogueEntry result = Select(
                    entries,
                    new string[0],
                    0.1,
                    (index + 0.5) / 10.0
                );

                if (result.IsCostume)
                {
                    costume++;
                }
                else
                {
                    regular++;
                }
            }

            Require(
                regular == 5 && costume == 5,
                "Regular and costume variants must have equal weight."
            );

            Console.WriteLine(
                "PASS: Regular and costume variants received equal weight."
            );
        }

        private static void CheckRegularDuplicate()
        {
            List<CatalogueEntry> entries = CreatePair();

            CatalogueEntry result = Select(
                entries,
                new[] { "pikachu:male:normal" },
                0.1,
                0.1
            );

            Require(
                result.Id == "pikachu:male:normal",
                "An owned regular gender must remain a possible duplicate."
            );

            Console.WriteLine(
                "PASS: An owned regular entry remained selectable."
            );
        }

        private static void CheckCostumeProtection()
        {
            List<CatalogueEntry> entries = CreatePair();

            CatalogueEntry result = Select(
                entries,
                new[] { "pikachu-test:male:normal" },
                0.1,
                0.9
            );

            Require(
                result.Id == "pikachu-test:female:normal",
                "The costume must redirect to its missing female entry."
            );

            result = Select(
                entries,
                new[] { "pikachu-test:female:normal" },
                0.9,
                0.9
            );

            Require(
                result.Id == "pikachu-test:male:normal",
                "The costume must redirect to its missing male entry."
            );

            Console.WriteLine(
                "PASS: Costumes redirected to their only missing gender."
            );
        }

        private static void CheckCompletedCostume()
        {
            List<CatalogueEntry> entries = CreatePair();

            CatalogueEntry result = Select(
                entries,
                new[]
                {
                    "pikachu-test:male:normal",
                    "pikachu-test:female:normal"
                },
                0.9,
                0.9
            );

            Require(
                result.Id == "pikachu:female:normal",
                "A completed costume must be removed while regular remains."
            );

            Console.WriteLine(
                "PASS: Completed costumes were removed from variant options."
            );
        }

        private static void CheckSingleGenderAndCategoryScope()
        {
            // An Event candidate containing only Libre must never
            // introduce regular Pikachu or invent a male Libre.
            var entries = new List<CatalogueEntry>
            {
                Entry("pikachu-libre", "female", true, false)
            };

            CatalogueEntry result = Select(
                entries,
                new string[0],
                0.8
            );

            Require(
                result.Id == "pikachu-libre:female:normal",
                "The single eligible costume gender must be selected."
            );

            // No eligible variants remain, so no random draw is needed.
            result = Select(
                entries,
                new[] { "pikachu-libre:female:normal" }
            );

            Require(
                result == null,
                "A fully owned costume-only candidate must return null."
            );

            Console.WriteLine(
                "PASS: Costume-only candidates preserved category and gender limits."
            );
        }

        private static void CheckShinySeparation()
        {
            var entries = new List<CatalogueEntry>
            {
                Entry("pikachu-libre", "female", true, true)
            };

            CatalogueEntry result = Select(
                entries,
                new[] { "pikachu-libre:female:normal" },
                0.5
            );

            Require(
                result.Id == "pikachu-libre:female:shiny",
                "Normal ownership must not remove a shiny costume."
            );

            Console.WriteLine(
                "PASS: Normal costume ownership did not remove its shiny."
            );
        }

        private static List<CatalogueEntry> CreatePair()
        {
            return new List<CatalogueEntry>
            {
                Entry("pikachu", "male", false, false),
                Entry("pikachu", "female", false, false),
                Entry("pikachu-test", "male", true, false),
                Entry("pikachu-test", "female", true, false)
            };
        }

        /// <summary>
        /// Minimal fixture for this selector's inputs.
        /// These records are not complete importable catalogue definitions.
        /// </summary>
        private static CatalogueEntry Entry(
            string form,
            string gender,
            bool costume,
            bool shiny
        )
        {
            return new CatalogueEntry
            {
                Id = form + ":" + gender + ":" +
                    (shiny ? "shiny" : "normal"),
                EncounterGroupId = "pikachu",
                FormId = form,
                Gender = gender,
                IsCostume = costume,
                IsShiny = shiny
            };
        }

        private static CatalogueEntry Select(
            IReadOnlyList<CatalogueEntry> entries,
            IEnumerable<string> owned,
            params double[] draws
        )
        {
            var source = new SequenceRandomSource(draws);
            var selector = new EncounterVariantSelector(source);

            CatalogueEntry result = selector.Select(entries, owned);

            Require(
                source.Remaining == 0,
                "The selector consumed fewer draws than expected."
            );

            return result;
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

                return values.Dequeue();
            }
        }
    }
}