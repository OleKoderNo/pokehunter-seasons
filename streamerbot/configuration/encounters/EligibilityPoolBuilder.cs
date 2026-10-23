using System;
using System.Collections.Generic;
using System.IO;
using PokeHunter.Catalogue;
using PokeHunter.Configuration;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Resolves season and event availability against a validated catalogue.
    ///
    /// Does not modify the catalogue or configuration, inspect ownership,
    /// roll randomness, or write catches.
    /// </summary>
    public static class EligibilityPoolBuilder
    {
        public static EligibleCataloguePool Build(
            CatalogueIndex catalogue,
            SeasonsConfig seasons,
            EventsConfig events,
            DateTimeOffset instant
        )
        {
            if (catalogue == null)
            {
                throw new ArgumentNullException(nameof(catalogue));
            }

            // Existing selectors validate their configurations.
            // Evaluate both schedules using exactly the same instant.
            SeasonDefinition season = SeasonSelector.FindActive(
                seasons,
                instant
            );

            EventDefinition activeEvent = EventSelector.FindActive(
                events,
                instant
            );

            if (season == null)
            {
                throw new InvalidOperationException(
                    "Cannot build an encounter pool without an active season."
                );
            }

            var unlockedGenerations = new HashSet<int>(
                season.UnlockedGenerations
            );

            // Read independent entry copies once for this build.
            var entries = new List<CatalogueEntry>();

            var familyBySpecies = new Dictionary<string, int>(
                StringComparer.Ordinal
            );

            var ordinaryForms = new HashSet<string>(StringComparer.Ordinal);
            var costumeForms = new HashSet<string>(StringComparer.Ordinal);

            foreach (string groupId in catalogue.GroupIds)
            {
                foreach (CatalogueEntry entry in catalogue.GetGroupEntries(groupId))
                {
                    entries.Add(entry);

                    int existingFamily;

                    if (familyBySpecies.TryGetValue(
                        entry.SpeciesId,
                        out existingFamily
                    ))
                    {
                        if (existingFamily != entry.EvolutionChainId)
                        {
                            throw new InvalidDataException(
                                "Conflicting evolution families for species " +
                                entry.SpeciesId + "."
                            );
                        }
                    }
                    else
                    {
                        familyBySpecies.Add(
                            entry.SpeciesId,
                            entry.EvolutionChainId
                        );
                    }

                    if (entry.IsCostume)
                    {
                        costumeForms.Add(entry.FormId);
                    }
                    else
                    {
                        ordinaryForms.Add(entry.FormId);
                    }
                }
            }

            var selectedTypes = new HashSet<string>(StringComparer.Ordinal);
            var selectedFamilies = new HashSet<int>();
            var selectedForms = new HashSet<string>(StringComparer.Ordinal);
            var selectedCostumes = new HashSet<string>(StringComparer.Ordinal);

            if (activeEvent != null)
            {
                selectedTypes.UnionWith(activeEvent.Include.Types);

                foreach (string speciesId in activeEvent.Include.EvolutionFamilies)
                {
                    int familyId;

                    if (!familyBySpecies.TryGetValue(speciesId, out familyId))
                    {
                        throw new InvalidDataException(
                            "Event '" + activeEvent.Id +
                            "' references an evolution-family species absent " +
                            "from the catalogue: " + speciesId + "."
                        );
                    }

                    selectedFamilies.Add(familyId);
                }

                foreach (string formId in activeEvent.Include.Forms)
                {
                    RequireSelector(
                        ordinaryForms.Contains(formId),
                        activeEvent.Id,
                        "form",
                        formId
                    );

                    selectedForms.Add(formId);
                }

                foreach (string costumeId in activeEvent.Include.Costumes)
                {
                    RequireSelector(
                        costumeForms.Contains(costumeId),
                        activeEvent.Id,
                        "costume",
                        costumeId
                    );

                    selectedCostumes.Add(costumeId);
                }
            }

            var eligibleIds = new List<string>();
            var eventIds = new List<string>();

            foreach (CatalogueEntry entry in entries)
            {
                bool generationUnlocked =
                    unlockedGenerations.Contains(entry.IntroducedGeneration);

                bool matchesEvent = false;

                if (activeEvent != null)
                {
                    // Costumes must be named explicitly. Broad type or
                    // family selectors must not accidentally grant them.
                    matchesEvent = entry.IsCostume
                        ? selectedCostumes.Contains(entry.FormId)
                        : selectedForms.Contains(entry.FormId) ||
                          selectedFamilies.Contains(entry.EvolutionChainId) ||
                          selectedTypes.Overlaps(entry.Types);
                }

                bool eventAllowsEntry =
                    matchesEvent &&
                    (
                        generationUnlocked ||
                        (
                            activeEvent.AllowLockedGenerations &&
                            !(
                                entry.IsLegendary &&
                                activeEvent.RequireUnlockedGenerationForLegendary
                            ) &&
                            !(
                                entry.IsMythical &&
                                activeEvent.RequireUnlockedGenerationForMythical
                            )
                        )
                    );

                // Event-only entries never become normally available just
                // because their introduction generation is unlocked.
                bool normallyAvailable =
                    generationUnlocked &&
                    !entry.EventOnly &&
                    !entry.IsCostume;

                if (normallyAvailable || eventAllowsEntry)
                {
                    eligibleIds.Add(entry.Id);
                }

                if (eventAllowsEntry)
                {
                    eventIds.Add(entry.Id);
                }
            }

            // Stable output makes checks and comparisons easier.
            eligibleIds.Sort(StringComparer.Ordinal);
            eventIds.Sort(StringComparer.Ordinal);

            return new EligibleCataloguePool(
                season.Id,
                activeEvent == null ? null : activeEvent.Id,
                activeEvent == null ? 0 : activeEvent.CategoryWeight,
                eligibleIds,
                eventIds
            );
        }

        private static void RequireSelector(
            bool exists,
            string eventId,
            string kind,
            string id
        )
        {
            if (!exists)
            {
                throw new InvalidDataException(
                    "Event '" + eventId + "' references an unknown " +
                    kind + ": " + id + "."
                );
            }
        }
    }
}