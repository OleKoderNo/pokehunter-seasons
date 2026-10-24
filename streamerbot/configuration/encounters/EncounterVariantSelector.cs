using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Selects an exact entry from one group's category-specific candidates.
    /// Does not award catches or modify seasonal ownership.
    /// </summary>
    public sealed class EncounterVariantSelector
    {
        private readonly IRandomSource randomSource;

        public EncounterVariantSelector(IRandomSource randomSource)
        {
            if (randomSource == null)
            {
                throw new ArgumentNullException(nameof(randomSource));
            }

            this.randomSource = randomSource;
        }

        /// <summary>
        /// Entries must belong to one group and one shiny state.
        /// Only pass entries eligible for the selected category.
        ///
        /// Returns null when no selectable variant remains.
        /// Regular entries remain selectable even when already owned.
        /// </summary>
        public CatalogueEntry Select(
            IReadOnlyList<CatalogueEntry> entries,
            IEnumerable<string> ownedEntryIds
        )
        {
            if (entries == null)
            {
                throw new ArgumentNullException(nameof(entries));
            }

            if (ownedEntryIds == null)
            {
                throw new ArgumentNullException(nameof(ownedEntryIds));
            }

            var owned = new HashSet<string>(StringComparer.Ordinal);

            foreach (string id in ownedEntryIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim())
                {
                    throw new ArgumentException(
                        "Owned entry IDs must be nonempty and trimmed.",
                        nameof(ownedEntryIds)
                    );
                }

                owned.Add(id);
            }

            var forms = new SortedDictionary<
                string,
                List<CatalogueEntry>
            >(StringComparer.Ordinal);

            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            CatalogueEntry first = null;

            foreach (CatalogueEntry entry in entries)
            {
                if (entry == null ||
                    string.IsNullOrWhiteSpace(entry.Id) ||
                    string.IsNullOrWhiteSpace(entry.FormId) ||
                    string.IsNullOrWhiteSpace(entry.EncounterGroupId) ||
                    !IsSupportedGender(entry.Gender))
                {
                    throw new ArgumentException(
                        "Candidate identity metadata is invalid.",
                        nameof(entries)
                    );
                }

                if (!seenIds.Add(entry.Id))
                {
                    throw new ArgumentException(
                        "Candidate entry IDs must be unique.",
                        nameof(entries)
                    );
                }

                if (first == null)
                {
                    first = entry;
                }
                else if (
                    entry.EncounterGroupId != first.EncounterGroupId ||
                    entry.IsShiny != first.IsShiny)
                {
                    throw new ArgumentException(
                        "Candidates must share a group and shiny state.",
                        nameof(entries)
                    );
                }

                List<CatalogueEntry> formEntries;

                if (!forms.TryGetValue(entry.FormId, out formEntries))
                {
                    formEntries = new List<CatalogueEntry>();
                    forms.Add(entry.FormId, formEntries);
                }

                formEntries.Add(entry);
            }

            var variants = new List<List<CatalogueEntry>>();
            bool needsGenderRoll = false;

            foreach (List<CatalogueEntry> formEntries in forms.Values)
            {
                ValidateForm(formEntries);

                bool isCostume = formEntries[0].IsCostume;
                bool hasMissingEntry = false;

                foreach (CatalogueEntry entry in formEntries)
                {
                    if (!owned.Contains(entry.Id))
                    {
                        hasMissingEntry = true;
                    }
                }

                // Completion removes costumes, never regular variants.
                if (isCostume && !hasMissingEntry)
                {
                    continue;
                }

                variants.Add(formEntries);

                if (formEntries.Count == 2)
                {
                    needsGenderRoll = true;
                }
            }

            if (variants.Count == 0)
            {
                return null;
            }

            // Gender is rolled before variant selection.
            // Single-gender forms will ignore an incompatible coin result.
            string rolledGender = null;

            if (needsGenderRoll)
            {
                rolledGender = Draw() < 0.5 ? "male" : "female";
            }

            // Each form gets one option regardless of its gender count.
            int variantIndex = (int)(Draw() * variants.Count);

            // Protect the upper boundary against floating-point rounding.
            variantIndex = Math.Min(variantIndex, variants.Count - 1);

            List<CatalogueEntry> selected = variants[variantIndex];

            if (selected[0].IsCostume)
            {
                var missing = new List<CatalogueEntry>();

                foreach (CatalogueEntry entry in selected)
                {
                    if (!owned.Contains(entry.Id))
                    {
                        missing.Add(entry);
                    }
                }

                // This also handles costumes supporting only one gender.
                if (missing.Count == 1)
                {
                    return missing[0];
                }
            }

            if (selected.Count == 1)
            {
                return selected[0];
            }

            foreach (CatalogueEntry entry in selected)
            {
                if (entry.Gender == rolledGender)
                {
                    return entry;
                }
            }

            throw new InvalidOperationException(
                "The selected variant has no entry for the gender roll."
            );
        }

        private double Draw()
        {
            double value = randomSource.NextDouble();

            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value < 0 ||
                value >= 1)
            {
                throw new InvalidOperationException(
                    "The random source must return a finite value in [0, 1)."
                );
            }

            return value;
        }

        private static bool IsSupportedGender(string gender)
        {
            return gender == "male" ||
                gender == "female" ||
                gender == "genderless";
        }

        /// <summary>
        /// A form has one collection gender or a male/female pair.
        /// Full catalogue validation remains the loader's responsibility.
        /// </summary>
        private static void ValidateForm(List<CatalogueEntry> entries)
        {
            bool isCostume = entries[0].IsCostume;

            var genders = new HashSet<string>(StringComparer.Ordinal);

            foreach (CatalogueEntry entry in entries)
            {
                if (entry.IsCostume != isCostume ||
                    !genders.Add(entry.Gender))
                {
                    throw new ArgumentException(
                        "A form has inconsistent costume or gender metadata.",
                        "entries"
                    );
                }
            }

            if (entries.Count > 2 ||
                (entries.Count == 2 &&
                 (!genders.Contains("male") ||
                  !genders.Contains("female"))))
            {
                throw new ArgumentException(
                    "A form must contain one gender or a male/female pair.",
                    "entries"
                );
            }
        }
    }
}