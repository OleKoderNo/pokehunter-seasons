using System;
using System.Collections.Generic;

namespace PokeHunter.Catalogue
{
    /// <summary>
    /// An independently owned, validated catalogue with direct ID lookups.
    ///
    /// Construct once when loading the catalogue, then retain this instance
    /// for repeated lookups. This class does not read files during lookups.
    ///
    /// Internal records are never returned directly. Callers receive
    /// independent copies so their changes cannot alter future results.
    /// </summary>
    public sealed class CatalogueIndex
    {
        private readonly Dictionary<string, CatalogueEntry> entriesById;
        private readonly Dictionary<string, CatalogueEncounterGroup> groupsById;
        private readonly IReadOnlyList<string> groupIds;

        public int EntryCount
        {
            get { return entriesById.Count; }
        }

        public int GroupCount
        {
            get { return groupsById.Count; }
        }

        /// <summary>
        /// Group IDs in the original validated catalogue order.
        /// The collection cannot be modified by callers.
        /// </summary>
        public IReadOnlyList<string> GroupIds
        {
            get { return groupIds; }
        }

        /// <summary>
        /// Validates the document and takes independent copies of its data.
        ///
        /// Later changes to the original document do not affect this index.
        /// Do not modify the document concurrently during construction.
        /// </summary>
        public CatalogueIndex(CatalogueDocument document)
        {
            CatalogueValidator.Validate(document);

            entriesById = new Dictionary<string, CatalogueEntry>(
                StringComparer.Ordinal
            );

            groupsById = new Dictionary<string, CatalogueEncounterGroup>(
                StringComparer.Ordinal
            );

            foreach (CatalogueEntry entry in document.Entries)
            {
                entriesById.Add(entry.Id, CopyEntry(entry));
            }

            var orderedGroupIds = new List<string>();

            foreach (CatalogueEncounterGroup group in document.EncounterGroups)
            {
                groupsById.Add(group.Id, CopyGroup(group));
                orderedGroupIds.Add(group.Id);
            }

            groupIds = orderedGroupIds.AsReadOnly();
        }

        /// <summary>
        /// Returns an independent entry copy, or null for an unknown ID.
        /// Matching is exact and case-sensitive.
        /// </summary>
        public CatalogueEntry FindEntry(string id)
        {
            RequireId(id);

            CatalogueEntry entry;

            return entriesById.TryGetValue(id, out entry)
                ? CopyEntry(entry)
                : null;
        }

        /// <summary>
        /// Returns an independent group copy, or null for an unknown ID.
        /// Matching is exact and case-sensitive.
        /// </summary>
        public CatalogueEncounterGroup FindGroup(string id)
        {
            RequireId(id);

            CatalogueEncounterGroup group;

            return groupsById.TryGetValue(id, out group)
                ? CopyGroup(group)
                : null;
        }

        /// <summary>
        /// Returns independent copies of every entry belonging to a group.
        ///
        /// This includes locked, event-only, normal, and shiny entries.
        /// The encounter system must apply eligibility rules separately.
        ///
        /// An unknown group is an error rather than an empty valid pool.
        /// </summary>
        public IReadOnlyList<CatalogueEntry> GetGroupEntries(string groupId)
        {
            RequireId(groupId);

            CatalogueEncounterGroup group;

            if (!groupsById.TryGetValue(groupId, out group))
            {
                throw new KeyNotFoundException(
                    "Unknown encounter group: " + groupId + "."
                );
            }

            var entries = new List<CatalogueEntry>(group.EntryIds.Count);

            foreach (string entryId in group.EntryIds)
            {
                // Validation guarantees that every reference exists.
                entries.Add(CopyEntry(entriesById[entryId]));
            }

            return entries.AsReadOnly();
        }

        /// <summary>
        /// Copies nested collections and media objects as well as
        /// scalar fields. A shallow copy would still share mutable data.
        /// </summary>
        private static CatalogueEntry CopyEntry(CatalogueEntry source)
        {
            return new CatalogueEntry
            {
                Id = source.Id,
                SpeciesId = source.SpeciesId,
                NationalDexNumber = source.NationalDexNumber,
                EvolutionChainId = source.EvolutionChainId,
                IsLegendary = source.IsLegendary,
                IsMythical = source.IsMythical,
                EncounterGroupId = source.EncounterGroupId,
                FormId = source.FormId,
                DisplayName = source.DisplayName,
                FormName = source.FormName,
                RegionalForm = source.RegionalForm,
                Gender = source.Gender,
                HasGenderDifference = source.HasGenderDifference,
                IsShiny = source.IsShiny,
                IsCostume = source.IsCostume,
                EventOnly = source.EventOnly,
                IntroducedGeneration = source.IntroducedGeneration,
                Types = new List<string>(source.Types),
                Sprite = new CatalogueSpriteReference
                {
                    Url = source.Sprite.Url,
                    FallbackUrl = source.Sprite.FallbackUrl
                },
                Cries = new CatalogueCryReferences
                {
                    Latest = source.Cries.Latest,
                    Legacy = source.Cries.Legacy
                }
            };
        }

        private static CatalogueEncounterGroup CopyGroup(
            CatalogueEncounterGroup source
        )
        {
            return new CatalogueEncounterGroup
            {
                Id = source.Id,
                SpeciesId = source.SpeciesId,
                NationalDexNumber = source.NationalDexNumber,
                RarityClass = source.RarityClass,
                EntryIds = new List<string>(source.EntryIds)
            };
        }

        private static void RequireId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id != id.Trim())
            {
                throw new ArgumentException(
                    "A catalogue ID without surrounding whitespace is required.",
                    nameof(id)
                );
            }
        }
    }
}