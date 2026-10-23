using System;
using System.Collections.Generic;
using System.IO;
using PokeHunter.Catalogue;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Builds type and Event categories for one shiny state.
    ///
    /// Does not roll randomness, inspect ownership, remove completed
    /// categories, or award catches.
    /// </summary>
    public static class EncounterCategoryBuilder
    {
        /// <summary>
        /// Creates categories from an already-resolved eligibility pool.
        ///
        /// typeWeight applies to each populated ordinary type category.
        /// The active event supplies its own category weight.
        /// rarityWeights maps rarity classes to Pokémon-group weights.
        /// </summary>
        public static IReadOnlyList<EncounterCategory> Build(
            CatalogueIndex catalogue,
            EligibleCataloguePool pool,
            bool isShiny,
            double typeWeight,
            IReadOnlyDictionary<string, double> rarityWeights
        )
        {
            if (catalogue == null)
            {
                throw new ArgumentNullException(nameof(catalogue));
            }

            if (pool == null)
            {
                throw new ArgumentNullException(nameof(pool));
            }

            ValidateWeight(typeWeight, "Type category weight");
            ValidateRarityWeights(rarityWeights);

            var eligibleIds = new HashSet<string>(
                pool.EntryIds,
                StringComparer.Ordinal
            );

            var eventIds = new HashSet<string>(
                pool.EventEntryIds,
                StringComparer.Ordinal
            );

            if (
                eligibleIds.Count != pool.EntryIds.Count ||
                eventIds.Count != pool.EventEntryIds.Count ||
                !eventIds.IsSubsetOf(eligibleIds)
            )
            {
                throw new InvalidDataException(
                    "Eligibility pool contains duplicate IDs or event " +
                    "entries outside the general pool."
                );
            }

            if (pool.EventId == null)
            {
                if (eventIds.Count != 0 || pool.EventCategoryWeight != 0)
                {
                    throw new InvalidDataException(
                        "An inactive event cannot supply entries or weight."
                    );
                }
            }
            else
            {
                ValidateWeight(
                    pool.EventCategoryWeight,
                    "Event category weight"
                );
            }

            // category ID -> group ID -> eligible collection entry IDs.
            //
            // Grouping here prevents variant count from multiplying
            // a Pokémon group's weight.
            var buckets =
                new Dictionary<string, Dictionary<string, List<string>>>(
                    StringComparer.Ordinal
                );

            foreach (string entryId in pool.EntryIds)
            {
                CatalogueEntry entry = catalogue.FindEntry(entryId);

                if (entry == null)
                {
                    throw new InvalidDataException(
                        "Eligibility pool references unknown entry " +
                        entryId + "."
                    );
                }

                if (entry.IsShiny != isShiny)
                {
                    continue;
                }

                foreach (string type in entry.Types)
                {
                    AddEntry(buckets, type, entry);
                }

                if (eventIds.Contains(entryId))
                {
                    AddEntry(buckets, "event", entry);
                }
            }

            var categoryIds = new List<string>(buckets.Keys);
            categoryIds.Sort(StringComparer.Ordinal);

            var result = new List<EncounterCategory>();

            foreach (string categoryId in categoryIds)
            {
                Dictionary<string, List<string>> groups =
                    buckets[categoryId];

                var groupIds = new List<string>(groups.Keys);
                groupIds.Sort(StringComparer.Ordinal);

                var candidates = new List<EncounterGroupCandidate>();

                foreach (string groupId in groupIds)
                {
                    CatalogueEncounterGroup group =
                        catalogue.FindGroup(groupId);

                    if (group == null)
                    {
                        throw new InvalidDataException(
                            "Unknown encounter group " + groupId + "."
                        );
                    }

                    double groupWeight;

                    if (!rarityWeights.TryGetValue(
                        group.RarityClass,
                        out groupWeight
                    ))
                    {
                        throw new InvalidDataException(
                            "No weight configured for rarity class " +
                            group.RarityClass + "."
                        );
                    }

                    List<string> entryIds = groups[groupId];
                    entryIds.Sort(StringComparer.Ordinal);

                    candidates.Add(
                        new EncounterGroupCandidate(
                            groupId,
                            groupWeight,
                            entryIds
                        )
                    );
                }

                double categoryWeight = categoryId == "event"
                    ? pool.EventCategoryWeight
                    : typeWeight;

                result.Add(
                    new EncounterCategory(
                        categoryId,
                        categoryWeight,
                        candidates
                    )
                );
            }

            // Empty categories are never added. An empty result is valid:
            // the caller must handle it without forcing a shiny outcome.
            return result.AsReadOnly();
        }

        private static void AddEntry(
            Dictionary<string, Dictionary<string, List<string>>> buckets,
            string categoryId,
            CatalogueEntry entry
        )
        {
            Dictionary<string, List<string>> groups;

            if (!buckets.TryGetValue(categoryId, out groups))
            {
                groups = new Dictionary<string, List<string>>(
                    StringComparer.Ordinal
                );

                buckets.Add(categoryId, groups);
            }

            List<string> entryIds;

            if (!groups.TryGetValue(entry.EncounterGroupId, out entryIds))
            {
                entryIds = new List<string>();
                groups.Add(entry.EncounterGroupId, entryIds);
            }

            entryIds.Add(entry.Id);
        }

        private static void ValidateRarityWeights(
            IReadOnlyDictionary<string, double> weights
        )
        {
            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            var requiredClasses = new HashSet<string>(
                StringComparer.Ordinal
            )
            {
                "common",
                "uncommon",
                "rare",
                "ultraRare"
            };

            if (weights.Count != requiredClasses.Count)
            {
                throw new ArgumentException(
                    "Provide exactly the four supported rarity weights.",
                    nameof(weights)
                );
            }

            foreach (KeyValuePair<string, double> pair in weights)
            {
                if (!requiredClasses.Contains(pair.Key))
                {
                    throw new ArgumentException(
                        "Unsupported rarity weight: " + pair.Key + ".",
                        nameof(weights)
                    );
                }

                ValidateWeight(
                    pair.Value,
                    "Rarity weight " + pair.Key
                );
            }
        }

        private static void ValidateWeight(double value, string name)
        {
            if (
                double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value <= 0
            )
            {
                throw new ArgumentException(
                    name + " must be a finite number greater than zero."
                );
            }
        }
    }
}