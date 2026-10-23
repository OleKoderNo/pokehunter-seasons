using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Executes one encounter attempt without database or presentation work.
    /// The caller supplies a consistent catalogue, category, and ownership snapshot.
    /// </summary>
    public sealed class EncounterAttemptEngine
    {
        private readonly ShinyRoller shinyRoller;
        private readonly EncounterGroupSelector groupSelector;
        private readonly EncounterVariantSelector variantSelector;

        public EncounterAttemptEngine(IRandomSource randomSource)
        {
            if (randomSource == null)
            {
                throw new ArgumentNullException(nameof(randomSource));
            }

            // All decisions consume values from the same source in order.
            shinyRoller = new ShinyRoller(randomSource);
            groupSelector = new EncounterGroupSelector(randomSource);
            variantSelector = new EncounterVariantSelector(randomSource);
        }

        /// <summary>
        /// Runs one attempt.
        ///
        /// shinyProtected means an earlier attempt in this redemption
        /// already rolled shiny. It skips the shiny draw.
        ///
        /// NewEntry means eligible to save, not already saved.
        /// Technical failures propagate instead of becoming gameplay failures.
        /// </summary>
        public EncounterAttemptResult Run(
            CatalogueIndex catalogue,
            IReadOnlyList<EncounterCategory> normalCategories,
            IReadOnlyList<EncounterCategory> shinyCategories,
            IEnumerable<string> seasonalOwnedEntryIds,
            double shinyProbability,
            bool shinyProtected
        )
        {
            if (catalogue == null)
            {
                throw new ArgumentNullException(nameof(catalogue));
            }

            if (normalCategories == null)
            {
                throw new ArgumentNullException(nameof(normalCategories));
            }

            if (shinyCategories == null)
            {
                throw new ArgumentNullException(nameof(shinyCategories));
            }

            if (seasonalOwnedEntryIds == null)
            {
                throw new ArgumentNullException(nameof(seasonalOwnedEntryIds));
            }

            // Validate even when protection skips the shiny roll.
            if (double.IsNaN(shinyProbability) ||
                double.IsInfinity(shinyProbability) ||
                shinyProbability < 0 ||
                shinyProbability > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(shinyProbability),
                    "Shiny probability must be between zero and one."
                );
            }

            // Enumerate ownership once so every stage sees the same snapshot.
            var owned = new HashSet<string>(StringComparer.Ordinal);

            foreach (string id in seasonalOwnedEntryIds)
            {
                if (string.IsNullOrWhiteSpace(id) || id != id.Trim())
                {
                    throw new ArgumentException(
                        "Seasonal ownership IDs must be nonempty and trimmed.",
                        nameof(seasonalOwnedEntryIds)
                    );
                }

                owned.Add(id);
            }

            bool isShiny = shinyProtected ||
                shinyRoller.Roll(shinyProbability);

            IReadOnlyList<EncounterCategory> categories =
                isShiny ? shinyCategories : normalCategories;

            IReadOnlyList<EncounterCategory> incompleteCategories =
                EncounterOwnershipFilter.Filter(categories, owned);

            EncounterGroupSelection selection =
                groupSelector.Select(incompleteCategories);

            if (selection == null)
            {
                return new EncounterAttemptResult(
                    EncounterAttemptOutcome.NoAvailableEntry,
                    isShiny,
                    null,
                    null,
                    null
                );
            }

            var entries = new List<CatalogueEntry>();

            // Resolve only the selected category's candidate IDs.
            // Do not expand to the whole catalogue group.
            foreach (string id in selection.Group.EntryIds)
            {
                CatalogueEntry entry = catalogue.FindEntry(id);

                if (entry == null ||
                    entry.EncounterGroupId != selection.Group.GroupId ||
                    entry.IsShiny != isShiny)
                {
                    throw new InvalidOperationException(
                        "Selected candidates disagree with the catalogue: " +
                        id + "."
                    );
                }

                entries.Add(entry);
            }

            CatalogueEntry selectedEntry =
                variantSelector.Select(entries, owned);

            if (selectedEntry == null)
            {
                return new EncounterAttemptResult(
                    EncounterAttemptOutcome.NoAvailableEntry,
                    isShiny,
                    selection.Category.Id,
                    selection.Group.GroupId,
                    null
                );
            }

            EncounterAttemptOutcome outcome = owned.Contains(selectedEntry.Id)
                ? EncounterAttemptOutcome.Duplicate
                : EncounterAttemptOutcome.NewEntry;

            return new EncounterAttemptResult(
                outcome,
                isShiny,
                selection.Category.Id,
                selection.Group.GroupId,
                selectedEntry
            );
        }
    }
}