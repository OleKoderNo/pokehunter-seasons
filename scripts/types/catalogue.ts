/**
 * "unspecified" means genders are not collected separately.
 * It does not mean the Pokémon is genderless.
 */
export type IndexedGender = "male" | "female" | "genderless" | "unspecified";

/**
 * One collectible form, indexed gender, and shiny combination.
 *
 * Shared species information is stored separately in the full catalogue.
 */
export interface CollectionEntry {
  id: string;
  speciesId: string;
  encounterGroupId: string;
  formId: string;
  displayName: string;
  formName: string | null;
  regionalForm: string | null;
  gender: IndexedGender;
  hasGenderDifference: boolean;
  isShiny: boolean;
  isCostume: boolean;
  eventOnly: boolean;
  introducedGeneration: number;
  types: string[];
  sprite: {
    url: string | null;
    fallbackUrl: string | null;
  };
  cries: {
    latest: string | null;
    legacy: string | null;
  };
}
