using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;

namespace PokeHunter.Catalogue
{
    /// <summary>
    /// Validates catalogue values and relationships.
    ///
    /// Does not modify the supplied document, download media,
    /// or decide which Pokémon are currently obtainable.
    /// </summary>
    public static class CatalogueValidator
    {
        private static readonly HashSet<string> SupportedTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "normal",
                "fire",
                "water",
                "electric",
                "grass",
                "ice",
                "fighting",
                "poison",
                "ground",
                "flying",
                "psychic",
                "bug",
                "rock",
                "ghost",
                "dragon",
                "dark",
                "steel",
                "fairy"
            };

        private static readonly HashSet<string> SupportedGenders =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "male",
                "female",
                "genderless",
                "unspecified"
            };

        private static readonly HashSet<string> SupportedRarities =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "common",
                "uncommon",
                "rare",
                "ultraRare"
            };

        /// <summary>
        /// Checks the current intermediate catalogue format.
        /// Reports the first problem as InvalidDataException.
        ///
        /// Accepting an intermediate document here does not authorize
        /// its use by a production catching action.
        /// </summary>
        public static void Validate(CatalogueDocument document)
        {
            Require(document != null, "The document must not be null.");

            Require(
                document.SchemaVersion == 1,
                "schemaVersion must be 1."
            );

            Require(
                document.SampleOnly,
                "sampleOnly must be true for the current intermediate format."
            );

            Require(
                document.Scope == "merged-collection-entries",
                "scope must be merged-collection-entries."
            );

            ValidateSources(document.Sources);

            Require(
                document.Entries != null && document.Entries.Count > 0,
                "entries must contain at least one collection entry."
            );

            Require(
                document.EncounterGroups != null &&
                document.EncounterGroups.Count > 0,
                "encounterGroups must contain at least one group."
            );

            // Catalogue IDs are exact, case-sensitive identifiers.
            // Do not apply trainer-name or season-ID matching rules here.
            var entriesById = new Dictionary<string, CatalogueEntry>(
                StringComparer.Ordinal
            );

            foreach (CatalogueEntry entry in document.Entries)
            {
                ValidateEntry(entry);

                Require(
                    !entriesById.ContainsKey(entry.Id),
                    "Duplicate collection entry ID: " + entry.Id + "."
                );

                entriesById.Add(entry.Id, entry);
            }

            ValidateCounterparts(entriesById);
            ValidateGroups(document.EncounterGroups, entriesById);
        }

        private static void ValidateSources(List<string> sources)
        {
            Require(
                sources != null && sources.Count > 0,
                "sources must contain at least one source name."
            );

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string source in sources)
            {
                RequireText(source, "Source name");

                Require(
                    seen.Add(source),
                    "Duplicate source name: " + source + "."
                );
            }
        }

        /// <summary>
        /// Checks one collection entry independently of group membership.
        /// Missing media is allowed; malformed media references are not.
        /// </summary>
        private static void ValidateEntry(CatalogueEntry entry)
        {
            Require(entry != null, "Collection entries must not be null.");

            RequireText(entry.Id, "Entry ID");
            RequireSlug(entry.SpeciesId, entry.Id + ".speciesId");
            RequireSlug(entry.FormId, entry.Id + ".formId");
            RequireSlug(
                entry.EncounterGroupId,
                entry.Id + ".encounterGroupId"
            );

            Require(
                entry.NationalDexNumber > 0,
                entry.Id + ".nationalDexNumber must be positive."
            );

            Require(
                entry.EvolutionChainId > 0,
                entry.Id + ".evolutionChainId must be positive."
            );

            Require(
                entry.IntroducedGeneration >= 1 &&
                entry.IntroducedGeneration <= 9,
                entry.Id + ".introducedGeneration must be between 1 and 9."
            );

            RequireText(entry.DisplayName, entry.Id + ".displayName");

            if (entry.FormName != null)
            {
                RequireText(entry.FormName, entry.Id + ".formName");
            }

            if (entry.RegionalForm != null)
            {
                RequireSlug(entry.RegionalForm, entry.Id + ".regionalForm");
            }

            Require(
                entry.Gender != null &&
                SupportedGenders.Contains(entry.Gender),
                entry.Id + " has an unsupported gender."
            );

            Require(
                !entry.HasGenderDifference ||
                entry.Gender == "male" ||
                entry.Gender == "female",
                entry.Id + " has a visible gender difference but no " +
                "male or female collection identity."
            );

            string expectedId =
                entry.FormId + ":" + entry.Gender + ":" +
                (entry.IsShiny ? "shiny" : "normal");

            Require(
                entry.Id == expectedId,
                entry.Id + " does not match its form, gender, and shiny state."
            );

            // Costumes share the species group. Other reviewed forms
            // use their canonical form ID as their encounter group.
            string expectedGroupId =
                entry.IsCostume ? entry.SpeciesId : entry.FormId;

            Require(
                entry.EncounterGroupId == expectedGroupId,
                entry.Id + " has an unexpected encounter group."
            );

            Require(
                !entry.IsCostume || entry.EventOnly,
                entry.Id + " is a costume but is not marked eventOnly."
            );

            ValidateTypes(entry);

            Require(
                entry.Sprite != null,
                entry.Id + ".sprite must not be null."
            );

            Require(
                entry.Cries != null,
                entry.Id + ".cries must not be null."
            );

            ValidateAsset(entry.Sprite.Url, entry.Id + ".sprite.url");
            ValidateAsset(
                entry.Sprite.FallbackUrl,
                entry.Id + ".sprite.fallbackUrl"
            );
            ValidateAsset(entry.Cries.Latest, entry.Id + ".cries.latest");
            ValidateAsset(entry.Cries.Legacy, entry.Id + ".cries.legacy");
        }

        private static void ValidateTypes(CatalogueEntry entry)
        {
            Require(
                entry.Types != null &&
                entry.Types.Count >= 1 &&
                entry.Types.Count <= 2,
                entry.Id + " must have one or two types."
            );

            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string type in entry.Types)
            {
                Require(
                    type != null && SupportedTypes.Contains(type),
                    entry.Id + " has an unsupported type."
                );

                Require(
                    seen.Add(type),
                    entry.Id + " contains duplicate type " + type + "."
                );
            }
        }

        /// <summary>
        /// Requires normal and shiny counterparts with matching metadata.
        ///
        /// Artwork may differ or be missing. Identity and shiny state
        /// necessarily differ. All other entry fields must match.
        /// </summary>
        private static void ValidateCounterparts(
            Dictionary<string, CatalogueEntry> entriesById
        )
        {
            foreach (CatalogueEntry entry in entriesById.Values)
            {
                string counterpartId =
                    entry.FormId + ":" + entry.Gender + ":" +
                    (entry.IsShiny ? "normal" : "shiny");

                CatalogueEntry counterpart;

                Require(
                    entriesById.TryGetValue(counterpartId, out counterpart),
                    entry.Id + " is missing counterpart " +
                    counterpartId + "."
                );

                // Both directions have now been checked for existence.
                // Compare each pair only once.
                if (entry.IsShiny)
                {
                    continue;
                }

                JObject normalMetadata = CreateSharedMetadata(entry);
                JObject shinyMetadata = CreateSharedMetadata(counterpart);

                Require(
                    JToken.DeepEquals(normalMetadata, shinyMetadata),
                    entry.Id + " and " + counterpartId +
                    " have conflicting shared metadata."
                );
            }
        }

        /// <summary>
        /// Creates a temporary comparison object without changing
        /// the original entry or its sprite references.
        /// </summary>
        private static JObject CreateSharedMetadata(CatalogueEntry entry)
        {
            JObject metadata = JObject.FromObject(entry);

            metadata.Remove("id");
            metadata.Remove("isShiny");
            metadata.Remove("sprite");

            return metadata;
        }

        /// <summary>
        /// Verifies every group reference and requires each collection
        /// entry to belong to exactly one matching group.
        /// </summary>
        private static void ValidateGroups(
            List<CatalogueEncounterGroup> groups,
            Dictionary<string, CatalogueEntry> entriesById
        )
        {
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            var assignedEntryIds = new HashSet<string>(
                StringComparer.Ordinal
            );

            foreach (CatalogueEncounterGroup group in groups)
            {
                Require(group != null, "Encounter groups must not be null.");

                RequireSlug(group.Id, "Encounter group ID");
                RequireSlug(group.SpeciesId, group.Id + ".speciesId");

                Require(
                    groupIds.Add(group.Id),
                    "Duplicate encounter group ID: " + group.Id + "."
                );

                Require(
                    group.NationalDexNumber > 0,
                    group.Id + ".nationalDexNumber must be positive."
                );

                Require(
                    group.RarityClass != null &&
                    SupportedRarities.Contains(group.RarityClass),
                    group.Id + " has an unsupported rarity class."
                );

                Require(
                    group.EntryIds != null && group.EntryIds.Count > 0,
                    group.Id + " must reference at least one entry."
                );

                foreach (string entryId in group.EntryIds)
                {
                    RequireText(entryId, group.Id + ".entryIds value");

                    CatalogueEntry entry;

                    Require(
                        entriesById.TryGetValue(entryId, out entry),
                        group.Id + " references unknown entry " +
                        entryId + "."
                    );

                    Require(
                        assignedEntryIds.Add(entryId),
                        entryId + " is referenced more than once by groups."
                    );

                    Require(
                        entry.EncounterGroupId == group.Id,
                        entryId + " does not point back to group " +
                        group.Id + "."
                    );

                    Require(
                        entry.SpeciesId == group.SpeciesId &&
                        entry.NationalDexNumber == group.NationalDexNumber,
                        entryId + " conflicts with the species or " +
                        "National Pokédex number of group " + group.Id + "."
                    );
                }
            }

            foreach (string entryId in entriesById.Keys)
            {
                Require(
                    assignedEntryIds.Contains(entryId),
                    entryId + " is not referenced by an encounter group."
                );
            }
        }

        /// <summary>
        /// Permits missing media but requires supplied references
        /// to be absolute HTTP or HTTPS URLs.
        ///
        /// This does not contact the server or verify the media file.
        /// </summary>
        private static void ValidateAsset(string value, string path)
        {
            if (value == null)
            {
                return;
            }

            RequireText(value, path);

            Uri uri;

            Require(
                Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == Uri.UriSchemeHttp ||
                 uri.Scheme == Uri.UriSchemeHttps) &&
                !string.IsNullOrEmpty(uri.Host),
                path + " must be an absolute HTTP or HTTPS URL, or null."
            );
        }

        /// <summary>
        /// Matches the lowercase slug convention used by the importer.
        /// Rejects leading, trailing, and repeated hyphens.
        /// </summary>
        private static void RequireSlug(string value, string path)
        {
            RequireText(value, path);

            bool previousWasHyphen = true;

            foreach (char character in value)
            {
                if (character == '-')
                {
                    Require(
                        !previousWasHyphen,
                        path + " must be a lowercase slug."
                    );

                    previousWasHyphen = true;
                    continue;
                }

                Require(
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9'),
                    path + " must be a lowercase slug."
                );

                previousWasHyphen = false;
            }

            Require(
                !previousWasHyphen,
                path + " must not end with a hyphen."
            );
        }

        private static void RequireText(string value, string path)
        {
            Require(
                !string.IsNullOrWhiteSpace(value),
                path + " must not be empty."
            );

            Require(
                value == value.Trim(),
                path + " must not contain surrounding whitespace."
            );
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(
                    "Catalogue validation error: " + message
                );
            }
        }
    }
}