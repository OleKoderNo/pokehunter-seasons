using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class EncounterAttemptChecks
    {
        public static void Run(CatalogueDocument document)
        {
            var index = new CatalogueIndex(document);

            IReadOnlyList<EncounterCategory> normal =
                EncounterGroupSelectionChecks.CreateCategories(document);

            IReadOnlyList<EncounterCategory> shiny =
                EncounterGroupSelectionChecks.CreateCategories(document, true);

            // Zero probability guarantees a normal attempt, even with draw 0.
            var source = new ZeroRandomSource();
            var engine = new EncounterAttemptEngine(source);

            EncounterAttemptResult first = engine.Run(
                index, normal, shiny, new string[0], 0, false
            );

            Require(
                first.Outcome == EncounterAttemptOutcome.NewEntry &&
                first.Entry != null &&
                !first.IsShiny &&
                !first.Entry.IsShiny &&
                !first.Entry.IsCostume &&
                first.GroupId == first.Entry.EncounterGroupId,
                "An empty collection should produce a new normal entry."
            );

            Console.WriteLine(
                "PASS: A complete attempt selected a new normal entry."
            );

            // Keep the selected category incomplete so its owned regular
            // entry remains a possible duplicate.
            var owned = new HashSet<string>(StringComparer.Ordinal)
            {
                first.Entry.Id
            };

            EncounterCategory selectedCategory = null;

            foreach (EncounterCategory category in normal)
            {
                if (category.Id == first.CategoryId)
                {
                    selectedCategory = category;
                }
            }

            bool anotherEntryExists = false;

            if (selectedCategory != null)
            {
                foreach (EncounterGroupCandidate group in selectedCategory.Groups)
                {
                    foreach (string id in group.EntryIds)
                    {
                        if (id != first.Entry.Id)
                        {
                            anotherEntryExists = true;
                        }
                    }
                }
            }

            Require(
                anotherEntryExists,
                "The duplicate fixture requires an incomplete category."
            );

            EncounterAttemptResult duplicate = engine.Run(
                index, normal, shiny, owned, 0, false
            );

            Require(
                duplicate.Outcome == EncounterAttemptOutcome.Duplicate &&
                duplicate.Entry.Id == first.Entry.Id &&
                owned.Count == 1,
                "An owned regular entry should return a duplicate without mutations."
            );

            Console.WriteLine(
                "PASS: An owned regular entry produced a duplicate result."
            );

            HashSet<string> allNormal = CollectIds(normal);
            source = new ZeroRandomSource();
            engine = new EncounterAttemptEngine(source);

            EncounterAttemptResult completedNormal = engine.Run(
                index, normal, shiny, allNormal, 0, false
            );

            Require(
                completedNormal.Outcome ==
                    EncounterAttemptOutcome.NoAvailableEntry &&
                !completedNormal.IsShiny &&
                completedNormal.Entry == null &&
                source.DrawCount == 1,
                "Completing normal entries must not force a shiny."
            );

            Console.WriteLine(
                "PASS: A completed normal collection failed without forcing shiny."
            );

            // A freshly rolled shiny with no available entries still
            // records IsShiny so the session can protect later attempts.
            source = new ZeroRandomSource();
            engine = new EncounterAttemptEngine(source);

            EncounterAttemptResult completedShiny = engine.Run(
                index, normal, shiny, CollectIds(shiny), 1, false
            );

            Require(
                completedShiny.Outcome ==
                    EncounterAttemptOutcome.NoAvailableEntry &&
                completedShiny.IsShiny &&
                source.DrawCount == 1,
                "An unavailable shiny attempt must retain its shiny state."
            );

            Console.WriteLine(
                "PASS: An unavailable shiny attempt retained its shiny state."
            );

            source = new ZeroRandomSource();
            engine = new EncounterAttemptEngine(source);

            EncounterAttemptResult rolled = engine.Run(
                index, normal, shiny, new string[0], 1, false
            );

            int ordinaryDraws = source.DrawCount;

            source = new ZeroRandomSource();
            engine = new EncounterAttemptEngine(source);

            EncounterAttemptResult protectedResult = engine.Run(
                index, normal, shiny, new string[0], 0, true
            );

            Require(
                rolled.Outcome == EncounterAttemptOutcome.NewEntry &&
                protectedResult.Outcome == EncounterAttemptOutcome.NewEntry &&
                protectedResult.IsShiny &&
                protectedResult.Entry.IsShiny &&
                protectedResult.Entry.Id == rolled.Entry.Id &&
                source.DrawCount == ordinaryDraws - 1,
                "Protection must select shiny and skip exactly the shiny draw."
            );

            Console.WriteLine(
                "PASS: Shiny protection skipped the shiny draw."
            );

            Console.WriteLine("All encounter attempt checks passed.");
        }

        private static HashSet<string> CollectIds(
            IReadOnlyList<EncounterCategory> categories
        )
        {
            var result = new HashSet<string>(StringComparer.Ordinal);

            foreach (EncounterCategory category in categories)
            {
                foreach (EncounterGroupCandidate group in category.Groups)
                {
                    foreach (string id in group.EntryIds)
                    {
                        result.Add(id);
                    }
                }
            }

            return result;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Selects the start of each interval and counts requested draws.
        /// Intended only for deterministic integration checks.
        /// </summary>
        private sealed class ZeroRandomSource : IRandomSource
        {
            public int DrawCount { get; private set; }

            public double NextDouble()
            {
                DrawCount++;
                return 0;
            }
        }
    }
}