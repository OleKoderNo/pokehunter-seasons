/**
 * Gender identity used for collection entries.
 *
 * Male and female are separate collectibles whenever both are supported,
 * including when they share the same artwork.
 *
 * Single-gender forms retain only their supported gender.
 * Pokémon without a gender use "genderless".
 */
export type CollectionGender = "male" | "female" | "genderless";

/**
 * Artwork for one entry.
 * Missing artwork does not change collection identity.
 */
export interface SpriteReference {
  url: string | null;
  fallbackUrl: string | null;
}

/** Null means the source did not supply that recording. */
export interface CryReferences {
  latest: string | null;
  legacy: string | null;
}

/**
 * One collectible Pokémon identity.
 *
 * Supported forms, genders, and shiny states can produce separate entries.
 * This describes catalogue data, not ownership or an individual catch.
 */
export interface CollectionEntry {
  /**
   * Permanent collection ID, for example "braixen:female:shiny".
   * Changing IDs referenced by saved catches requires a migration.
   */
  id: string;

  /** Species slug shared by its supported forms. */
  speciesId: string;

  /** National Pokédex number shared by the species' forms. */
  nationalDexNumber: number;

  /**
   * PokéAPI evolution-chain identifier used by family event selectors.
   * Family membership does not override generation restrictions.
   */
  evolutionChainId: number;

  isLegendary: boolean;
  isMythical: boolean;

  /**
   * Group selected before its collection variant.
   * Membership does not make every variant eligible.
   */
  encounterGroupId: string;

  /** Stable identifier for the supported form. */
  formId: string;

  displayName: string;

  /** Display label, or null for the regular form. */
  formName: string | null;

  /** Regional form label, or null for a non-regional form. */
  regionalForm: string | null;

  /** The gender represented by this particular collectible identity. */
  gender: CollectionGender;

  /**
   * Whether the supported form has a visible gender difference.
   *
   * This describes appearance only. False does not combine male and
   * female identities or prevent both genders from being collected.
   */
  hasGenderDifference: boolean;

  /** Normal and shiny states are separate collectibles. */
  isShiny: boolean;

  isCostume: boolean;

  /** Whether obtaining this entry requires an eligible active event. */
  eventOnly: boolean;

  /**
   * Generation introducing this form.
   * It may differ from the base species' introduction generation.
   */
  introducedGeneration: number;

  /** Type slugs belonging to this form. */
  types: string[];

  sprite: SpriteReference;
  cries: CryReferences;
}

/**
 * A Pokémon group selected before its collection variants.
 *
 * Additional genders or shiny entries do not increase its group weight.
 * Runtime eligibility is evaluated from its referenced entries.
 */
export interface EncounterGroup {
  id: string;
  speciesId: string;
  nationalDexNumber: number;

  /**
   * Permanent collection IDs belonging to this group.
   * Includes both shiny states and every supported gender.
   */
  entryIds: string[];
}

/** Numerical selection weights are configured separately in game.json. */
export type RarityClass = "common" | "uncommon" | "rare" | "ultraRare";

/** Every generated group receives an explicit resolved rarity class. */
export interface RatedEncounterGroup extends EncounterGroup {
  rarityClass: RarityClass;
}
