using System.Collections.Generic;
using Newtonsoft.Json;

namespace PokeHunter.Catalogue
{
    /// <summary>
    /// Describes the generated catalogue document.
    ///
    /// This contains Pokémon definitions, not viewer ownership.
    /// Loading this document does not make every entry obtainable:
    /// season, event, and shiny eligibility are evaluated separately.
    /// </summary>
    public sealed class CatalogueDocument
    {
        /// <summary>
        /// Identifies the supported catalogue structure.
        /// The validator must reject unsupported versions.
        /// </summary>
        [JsonProperty("schemaVersion", Required = Required.Always)]
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Marks the current output as an intermediate catalogue.
        ///
        /// Development checks may inspect this document. Production
        /// readiness must not be inferred merely from successful loading.
        /// </summary>
        [JsonProperty("sampleOnly", Required = Required.Always)]
        public bool SampleOnly { get; set; }

        /// <summary>
        /// Identifies the kind of generated document.
        /// Currently expected to be "merged-collection-entries".
        /// </summary>
        [JsonProperty("scope", Required = Required.Always)]
        public string Scope { get; set; }

        /// <summary>
        /// Names of the source catalogues used during generation.
        /// These are provenance labels, not paths to load at runtime.
        /// </summary>
        [JsonProperty("sources", Required = Required.Always)]
        public List<string> Sources { get; set; }

        /// <summary>
        /// Pokémon groups selected before their collection variants.
        /// Each group references entries by permanent collection ID.
        /// </summary>
        [JsonProperty("encounterGroups", Required = Required.Always)]
        public List<CatalogueEncounterGroup> EncounterGroups { get; set; }

        /// <summary>
        /// All collectible identities present in this catalogue.
        /// Includes both normal and shiny entries.
        /// </summary>
        [JsonProperty("entries", Required = Required.Always)]
        public List<CatalogueEntry> Entries { get; set; }
    }

    /// <summary>
    /// One encounter group with its resolved rarity assignment.
    ///
    /// Multiple genders, shiny states, or costumes must not give the
    /// group extra tickets merely because it contains more entries.
    /// </summary>
    public sealed class CatalogueEncounterGroup
    {
        /// <summary>
        /// Stable group identifier referenced by collection entries.
        /// </summary>
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; set; }

        /// <summary>
        /// Species shared by all entries in this group.
        /// </summary>
        [JsonProperty("speciesId", Required = Required.Always)]
        public string SpeciesId { get; set; }

        /// <summary>
        /// National Pokédex number shared by the group's entries.
        /// </summary>
        [JsonProperty("nationalDexNumber", Required = Required.Always)]
        public int NationalDexNumber { get; set; }

        /// <summary>
        /// Permanent collection IDs belonging to this group.
        ///
        /// Membership does not grant eligibility. The encounter system
        /// must filter entries using the active game rules.
        /// </summary>
        [JsonProperty("entryIds", Required = Required.Always)]
        public List<string> EntryIds { get; set; }

        /// <summary>
        /// Resolved rarity class from rarity.json.
        ///
        /// Supported values are common, uncommon, rare, and ultraRare.
        /// Numerical weights are supplied separately by game.json.
        /// </summary>
        [JsonProperty("rarityClass", Required = Required.Always)]
        public string RarityClass { get; set; }
    }

    /// <summary>
    /// One permanent collectible Pokémon identity.
    ///
    /// This is a catalogue definition, not a catch record. A catch will
    /// reference this ID alongside the trainer, season, and timestamp.
    /// </summary>
    public sealed class CatalogueEntry
    {
        /// <summary>
        /// Permanent identity, for example "pikachu:female:shiny".
        /// Changing an ID used by saved catches requires a migration.
        /// </summary>
        [JsonProperty("id", Required = Required.Always)]
        public string Id { get; set; }

        /// <summary>
        /// Species slug shared by supported forms of the species.
        /// </summary>
        [JsonProperty("speciesId", Required = Required.Always)]
        public string SpeciesId { get; set; }

        [JsonProperty("nationalDexNumber", Required = Required.Always)]
        public int NationalDexNumber { get; set; }

        /// <summary>
        /// PokéAPI evolution-chain identifier used by family selectors.
        /// It does not override generation restrictions.
        /// </summary>
        [JsonProperty("evolutionChainId", Required = Required.Always)]
        public int EvolutionChainId { get; set; }

        [JsonProperty("isLegendary", Required = Required.Always)]
        public bool IsLegendary { get; set; }

        [JsonProperty("isMythical", Required = Required.Always)]
        public bool IsMythical { get; set; }

        /// <summary>
        /// Group used to select the Pokémon before choosing its variant.
        /// </summary>
        [JsonProperty("encounterGroupId", Required = Required.Always)]
        public string EncounterGroupId { get; set; }

        /// <summary>
        /// Stable identifier for the supported collection form.
        /// Gender-specific source resources may share this form ID.
        /// </summary>
        [JsonProperty("formId", Required = Required.Always)]
        public string FormId { get; set; }

        [JsonProperty("displayName", Required = Required.Always)]
        public string DisplayName { get; set; }

        /// <summary>
        /// Display label for the form, or null for the regular form.
        /// The JSON property must exist even when its value is null.
        /// </summary>
        [JsonProperty("formName", Required = Required.AllowNull)]
        public string FormName { get; set; }

        /// <summary>
        /// Regional form label, or null for a non-regional form.
        /// </summary>
        [JsonProperty("regionalForm", Required = Required.AllowNull)]
        public string RegionalForm { get; set; }

        /// <summary>
        /// Collection gender: male, female, or genderless.
        /// Male and female are separate collectible identities even
        /// when they share artwork. Single-gender forms retain only
        /// their supported gender.
        /// </summary>
        [JsonProperty("gender", Required = Required.Always)]
        public string Gender { get; set; }

        /// <summary>
        /// Whether this supported form has a visible gender difference.
        /// </summary>
        [JsonProperty("hasGenderDifference", Required = Required.Always)]
        public bool HasGenderDifference { get; set; }

        [JsonProperty("isShiny", Required = Required.Always)]
        public bool IsShiny { get; set; }

        [JsonProperty("isCostume", Required = Required.Always)]
        public bool IsCostume { get; set; }

        /// <summary>
        /// Whether an eligible active event is required for this entry.
        /// </summary>
        [JsonProperty("eventOnly", Required = Required.Always)]
        public bool EventOnly { get; set; }

        /// <summary>
        /// Generation introducing this form, which may differ from
        /// the generation introducing its base species.
        /// </summary>
        [JsonProperty("introducedGeneration", Required = Required.Always)]
        public int IntroducedGeneration { get; set; }

        /// <summary>
        /// One or two type slugs belonging to this particular form.
        /// </summary>
        [JsonProperty("types", Required = Required.Always)]
        public List<string> Types { get; set; }

        [JsonProperty("sprite", Required = Required.Always)]
        public CatalogueSpriteReference Sprite { get; set; }

        [JsonProperty("cries", Required = Required.Always)]
        public CatalogueCryReferences Cries { get; set; }
    }

    /// <summary>
    /// Artwork references for one collection entry.
    /// Missing artwork must not remove a valid collectible identity.
    /// </summary>
    public sealed class CatalogueSpriteReference
    {
        [JsonProperty("url", Required = Required.AllowNull)]
        public string Url { get; set; }

        [JsonProperty("fallbackUrl", Required = Required.AllowNull)]
        public string FallbackUrl { get; set; }
    }

    /// <summary>
    /// Optional audio references used by the catch overlay.
    /// Null means the source did not supply that recording.
    /// </summary>
    public sealed class CatalogueCryReferences
    {
        [JsonProperty("latest", Required = Required.AllowNull)]
        public string Latest { get; set; }

        [JsonProperty("legacy", Required = Required.AllowNull)]
        public string Legacy { get; set; }
    }
}