/**
 * Gender identity used for collection entries.
 *
 * Male and female entries are separated when the supported form
 * has a visible gender difference.
 *
 * Forms or costumes supporting only one gender use that gender.
 * "Unspecified" represents a shared identity for Pokémon with both
 * genders but no supported visible gender difference.
 */
export type CollectionGender = "male" | "female" | "genderless" | "unspecified";

/**
 * A reference to the entry's sprite and an optional fallback.
 *
 * Missing artwork does not change the entry's collection identity.
 */
export interface SpriteReference {
  url: string | null;
  fallbackUrl: string | null;
}

/**
 * Available Pokémon cry references.
 *
 * Null means the source did not provide that recording.
 */
export interface CryReferences {
  latest: string | null;
  legacy: string | null;
}

/**
 * One collectible Pokémon identity.
 *
 * Different supported forms, visible gender variants, and shiny
 * states can produce separate entries.
 *
 * This describes catalogue data, not a viewer's ownership or
 * an individual catch record.
 */
export interface CollectionEntry {
  /**
   * Stable collection identity.
   * Example: "pikachu:female:shiny".
   *
   * Do not change existing IDs after catches reference them without
   * providing an explicit data migration.
   */
  id: string;

  /** Species slug shared by its supported forms. */
  speciesId: string;

  /** National Pokédex number shared by the species' forms. */
  nationalDexNumber: number;

  /**
   * PokéAPI evolution-chain identifier shared by the species' family.
   *
   * Used to resolve evolution-family event selectors.
   * This does not grant eligibility or override generation restrictions.
   */
  evolutionChainId: number;

  /** Whether the species is classified as Legendary. */
  isLegendary: boolean;

  /** Whether the species is classified as Mythical. */
  isMythical: boolean;

  /**
   * References the encounter group selected before variant selection.
   *
   * Sharing a group does not make every variant eligible to be caught.
   */
  encounterGroupId: string;

  /** Identifier for the specific form represented by this entry. */
  formId: string;

  /** Human-readable Pokémon name. */
  displayName: string;

  /** Human-readable form label, or null for the regular form. */
  formName: string | null;

  /** Regional form label, or null when this is not a regional form. */
  regionalForm: string | null;

  /** Gender identity represented by this collection entry. */
  gender: CollectionGender;

  /** Whether this supported form has a visible gender difference. */
  hasGenderDifference: boolean;

  /** Shiny and non-shiny versions are separate collection entries. */
  isShiny: boolean;

  /** Whether this entry represents a costume variant. */
  isCostume: boolean;

  /** Whether obtaining this entry requires an eligible active event. */
  eventOnly: boolean;

  /**
   * Generation in which this form was introduced.
   * A regional form can have a later generation than its base species.
   */
  introducedGeneration: number;

  /** Pokémon type slugs used for encounter categories and statistics. */
  types: string[];

  /** Sprite references for this particular collection entry. */
  sprite: SpriteReference;

  /** Cry references used by the optional catch overlay. */
  cries: CryReferences;
}

/**
 * One Pokémon group used during encounter selection.
 *
 * Collection variants are referenced by their permanent entry IDs.
 * Having more variants must not automatically increase the group's
 * chance of being selected.
 *
 * Eligibility is evaluated from the referenced entries at runtime.
 * This record does not grant access to locked forms or costumes.
 */
export interface EncounterGroup {
  /** Stable ID already referenced by CollectionEntry.encounterGroupId. */
  id: string;

  /** Species shared by every entry in this group. */
  speciesId: string;

  /** National Pokédex number shared by this group's entries. */
  nationalDexNumber: number;

  /**
   * All collection identities belonging to this group.
   *
   * These include normal and shiny entries. Runtime selection must
   * filter them for the rolled shiny state and current eligibility.
   */
  entryIds: string[];
}
