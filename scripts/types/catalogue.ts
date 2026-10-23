/**
 * Gender identity used for collection entries.
 *
 * Male and female entries are separated when the supported form
 * has a visible gender difference.
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

  /** Whether the species is classified as Legendary. */
  isLegendary: boolean;

  /** Whether the species is classified as Mythical. */
  isMythical: boolean;

  /** Groups entries for encounter selection before variant selection. */
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
