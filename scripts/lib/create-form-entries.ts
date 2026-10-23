import { isJsonObject } from "./pokeapi.js";
import type { JsonObject } from "./pokeapi.js";
import {
  readAssetUrl,
  readCollectionGenders,
  readDisplayName,
  readNationalDexNumber,
  readTypes,
} from "./create-regular-entries.js";
import { readEvolutionChainId } from "./read-evolution-chain-id.js";
import type { CollectionEntry, CollectionGender } from "../types/catalogue.js";

const GENERATIONS: Readonly<Record<string, number>> = {
  "generation-i": 1,
  "generation-ii": 2,
  "generation-iii": 3,
  "generation-iv": 4,
  "generation-v": 5,
  "generation-vi": 6,
  "generation-vii": 7,
  "generation-viii": 8,
  "generation-ix": 9,
};

/**
 * Reviewed presentation labels for the current Ghost-type roster.
 * Collection identity must not depend on this displayed wording.
 */
const FORM_LABELS: Readonly<Record<string, string>> = {
  alola: "Alolan",
  altered: "Altered Forme",
  antique: "Antique Form",
  artisan: "Artisan Form",
  average: "Average Size",
  blade: "Blade Forme",
  busted: "Busted Form",
  chest: "Chest Form",
  confined: "Confined",
  counterfeit: "Counterfeit Form",
  dawn: "Dawn Wings",
  disguised: "Disguised Form",
  galar: "Galarian",
  gmax: "Gigantamax",
  hisui: "Hisuian",
  large: "Large Size",
  masterpiece: "Masterpiece Form",
  mega: "Mega",
  "mega-z": "Mega Z",
  origin: "Origin Forme",
  phony: "Phony Form",
  roaming: "Roaming Form",
  sensu: "Sensu Style",
  shadow: "Shadow Rider",
  shield: "Shield Forme",
  small: "Small Size",
  super: "Super Size",
  totem: "Totem",
  "totem-busted": "Totem Busted Form",
  "totem-disguised": "Totem Disguised Form",
  unremarkable: "Unremarkable Form",
};

/**
 * These API forms represent genders of one collection form.
 * Suffixes alone are not used to infer gender identity.
 */
const GENDER_FORMS: Readonly<
  Record<string, { speciesId: string; gender: "male" | "female" }>
> = {
  "frillish-male": { speciesId: "frillish", gender: "male" },
  "frillish-female": { speciesId: "frillish", gender: "female" },
  "jellicent-male": { speciesId: "jellicent", gender: "male" },
  "jellicent-female": { speciesId: "jellicent", gender: "female" },
  "basculegion-male": { speciesId: "basculegion", gender: "male" },
  "basculegion-female": { speciesId: "basculegion", gender: "female" },
};

const REGIONAL_FORMS: Readonly<Record<string, string>> = {
  "marowak-alola": "alola",
  "marowak-totem": "alola",
  "corsola-galar": "galar",
  "yamask-galar": "galar",
  "typhlosion-hisui": "hisui",
  "zorua-hisui": "hisui",
  "zoroark-hisui": "hisui",
};

/**
 * Converts a reviewed non-costume form into collection entries.
 *
 * generationName comes from the form's version group.
 * Costume conversion and runtime eligibility are separate concerns.
 */
export function createFormEntries(
  pokemon: JsonObject,
  species: JsonObject,
  form: JsonObject,
  generationName: string,
): CollectionEntry[] {
  if (
    typeof pokemon.name !== "string" ||
    typeof species.name !== "string" ||
    typeof form.name !== "string" ||
    typeof form.form_name !== "string" ||
    typeof form.is_default !== "boolean" ||
    !isJsonObject(pokemon.species) ||
    pokemon.species.name !== species.name ||
    !isJsonObject(form.pokemon) ||
    form.pokemon.name !== pokemon.name ||
    typeof species.is_legendary !== "boolean" ||
    typeof species.is_mythical !== "boolean" ||
    typeof species.has_gender_differences !== "boolean"
  ) {
    throw new Error(
      "Form conversion received invalid or mismatched resources.",
    );
  }

  const introducedGeneration = GENERATIONS[generationName];

  if (introducedGeneration === undefined) {
    throw new Error(`Unsupported form generation: ${generationName}.`);
  }

  const genderMapping = GENDER_FORMS[form.name];

  let formId = form.name;
  let formName: string | null = null;
  let genders: CollectionGender[];

  if (genderMapping !== undefined) {
    if (
      genderMapping.speciesId !== species.name ||
      !species.has_gender_differences ||
      !readCollectionGenders(species).includes(genderMapping.gender)
    ) {
      throw new Error(
        `Gender mapping disagrees with species data: ${form.name}.`,
      );
    }

    // Gender counterparts share one form and encounter group.
    formId = species.name;
    genders = [genderMapping.gender];
  } else {
    genders = readCollectionGenders(species);

    if (form.form_name !== "") {
      const label = FORM_LABELS[form.form_name];

      if (label === undefined) {
        throw new Error(`Unreviewed form label: ${form.form_name}.`);
      }

      formName = label;
    }
  }

  const nationalDexNumber = readNationalDexNumber(species);
  const evolutionChainId = readEvolutionChainId(species);
  const displayName = readDisplayName(species);

  // Type-changing forms must use their own types.
  const types = readTypes(form);

  const cries = {
    latest: readAssetUrl(pokemon.cries, "latest"),
    legacy: readAssetUrl(pokemon.cries, "legacy"),
  };

  const entries: CollectionEntry[] = [];

  for (const isShiny of [false, true]) {
    for (const gender of genders) {
      const defaultField = isShiny ? "front_shiny" : "front_default";
      const femaleField = isShiny ? "front_shiny_female" : "front_female";

      // A gender-specific form already depicts that gender in its
      // default fields. Ordinary visible differences use female fields.
      const useFemaleField =
        genderMapping === undefined &&
        species.has_gender_differences &&
        gender === "female";

      const spriteField = useFemaleField ? femaleField : defaultField;

      let spriteUrl = readAssetUrl(form.sprites, spriteField);

      // Alternate forms must not inherit artwork for a different form.
      if (spriteUrl === null && form.is_default) {
        let parentField = spriteField;

        if (genderMapping !== undefined) {
          const parentMapping = GENDER_FORMS[pokemon.name];

          const parentRepresentsSelectedGender =
            parentMapping !== undefined &&
            parentMapping.speciesId === species.name &&
            parentMapping.gender === genderMapping.gender;

          // Basculegion-female's parent default sprite is already female.
          // A shared parent needs its dedicated female field instead.
          parentField =
            genderMapping.gender === "female" && !parentRepresentsSelectedGender
              ? femaleField
              : defaultField;
        }

        spriteUrl = readAssetUrl(pokemon.sprites, parentField);
      }

      entries.push({
        id: `${formId}:${gender}:${isShiny ? "shiny" : "normal"}`,
        speciesId: species.name,
        nationalDexNumber,
        evolutionChainId,
        isLegendary: species.is_legendary,
        isMythical: species.is_mythical,

        // Separate non-costume forms receive separate groups.
        // Reviewed gender counterparts already share a canonical formId.
        encounterGroupId: formId,
        formId,

        displayName,
        formName,
        regionalForm: REGIONAL_FORMS[form.name] ?? null,
        gender,
        hasGenderDifference: species.has_gender_differences,
        isShiny,
        isCostume: false,
        eventOnly: false,
        introducedGeneration,
        types: [...types],
        sprite: {
          url: spriteUrl,
          fallbackUrl: null,
        },
        cries: { ...cries },
      });
    }
  }

  return entries;
}
