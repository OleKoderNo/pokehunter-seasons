import { isJsonObject } from "./pokeapi.js";
import type { JsonObject } from "./pokeapi.js";
import {
  readAssetUrl,
  readDisplayName,
  readNationalDexNumber,
  readTypes,
} from "./create-regular-entries.js";
import { readEvolutionChainId } from "./read-evolution-chain-id.js";
import type { CollectionEntry } from "../types/catalogue.js";

/**
 * Reviewed rules for the six explicit Halloween variants.
 *
 * Costume gender restrictions are form-specific. They must not be
 * inferred from Pikachu's species-wide gender metadata.
 */
interface VariantDefinition {
  pokemonId: string;
  speciesId: string;
  sourceFormName: string;
  formName: string;
  generationName: string;
  introducedGeneration: number;
  gender: "female" | "unspecified";
  isCostume: boolean;
}

export const EVENT_VARIANTS: readonly Readonly<VariantDefinition>[] = [
  {
    pokemonId: "morpeko-hangry",
    speciesId: "morpeko",
    sourceFormName: "hangry",
    formName: "Hangry Mode",
    generationName: "generation-viii",
    introducedGeneration: 8,
    gender: "unspecified",
    isCostume: false,
  },
  {
    pokemonId: "pikachu-rock-star",
    speciesId: "pikachu",
    sourceFormName: "rock-star",
    formName: "Rock Star",
    generationName: "generation-vi",
    introducedGeneration: 6,
    gender: "female",
    isCostume: true,
  },
  {
    pokemonId: "pikachu-belle",
    speciesId: "pikachu",
    sourceFormName: "belle",
    formName: "Belle",
    generationName: "generation-vi",
    introducedGeneration: 6,
    gender: "female",
    isCostume: true,
  },
  {
    pokemonId: "pikachu-pop-star",
    speciesId: "pikachu",
    sourceFormName: "pop-star",
    formName: "Pop Star",
    generationName: "generation-vi",
    introducedGeneration: 6,
    gender: "female",
    isCostume: true,
  },
  {
    pokemonId: "pikachu-phd",
    speciesId: "pikachu",
    sourceFormName: "phd",
    formName: "Ph. D.",
    generationName: "generation-vi",
    introducedGeneration: 6,
    gender: "female",
    isCostume: true,
  },
  {
    pokemonId: "pikachu-libre",
    speciesId: "pikachu",
    sourceFormName: "libre",
    formName: "Libre",
    generationName: "generation-vi",
    introducedGeneration: 6,
    gender: "female",
    isCostume: true,
  },
];

/**
 * Creates normal and shiny entries for one explicitly reviewed variant.
 *
 * The caller resolves generationName from the form's version group.
 * Missing media does not remove either collection entry.
 *
 * This function does not decide whether the variant is currently
 * available. Season and event eligibility belong to the runtime.
 */
export function createEventVariantEntries(
  pokemon: JsonObject,
  species: JsonObject,
  form: JsonObject,
  generationName: string,
): CollectionEntry[] {
  const definition = EVENT_VARIANTS.find(
    (candidate) => candidate.pokemonId === pokemon.name,
  );

  if (definition === undefined) {
    throw new Error("Unsupported explicit event variant.");
  }

  if (
    species.name !== definition.speciesId ||
    !isJsonObject(pokemon.species) ||
    pokemon.species.name !== definition.speciesId ||
    form.name !== definition.pokemonId ||
    form.form_name !== definition.sourceFormName ||
    form.is_default !== true ||
    !isJsonObject(form.pokemon) ||
    form.pokemon.name !== definition.pokemonId
  ) {
    throw new Error(
      "Event variant resources disagree with the reviewed mapping.",
    );
  }

  if (generationName !== definition.generationName) {
    throw new Error(
      "Event variant introduction generation disagrees with review.",
    );
  }

  if (species.is_legendary !== false || species.is_mythical !== false) {
    throw new Error("Unexpected Legendary or Mythical classification.");
  }

  // These reviewed resources belong to mixed-gender species.
  // The costume mapping narrows Pikachu to female-only collection entries.
  if (
    species.gender_rate !== 4 ||
    species.has_gender_differences !== definition.isCostume
  ) {
    throw new Error(
      "Event variant species gender metadata disagrees with review.",
    );
  }

  const nationalDexNumber = readNationalDexNumber(species);
  const evolutionChainId = readEvolutionChainId(species);
  const displayName = readDisplayName(species);
  const types = readTypes(form);

  const cries = {
    latest: readAssetUrl(pokemon.cries, "latest"),
    legacy: readAssetUrl(pokemon.cries, "legacy"),
  };

  return [false, true].map((isShiny): CollectionEntry => {
    // These resources already depict their supported form and gender.
    // The female-only costumes therefore use default sprite fields.
    const spriteField = isShiny ? "front_shiny" : "front_default";

    const spriteUrl =
      readAssetUrl(form.sprites, spriteField) ??
      readAssetUrl(pokemon.sprites, spriteField);

    return {
      id: `${definition.pokemonId}:${definition.gender}:${isShiny ? "shiny" : "normal"}`,
      speciesId: definition.speciesId,
      nationalDexNumber,
      evolutionChainId,
      isLegendary: false,
      isMythical: false,

      // Costumes share Pikachu's selection weight.
      // Hangry Morpeko is a separate non-costume form.
      encounterGroupId: definition.isCostume
        ? definition.speciesId
        : definition.pokemonId,

      formId: definition.pokemonId,
      displayName,
      formName: definition.formName,
      regionalForm: null,
      gender: definition.gender,

      // None of these individual forms has two visibly distinct genders.
      hasGenderDifference: false,

      isShiny,
      isCostume: definition.isCostume,
      eventOnly: definition.isCostume,
      introducedGeneration: definition.introducedGeneration,
      types: [...types],
      sprite: {
        url: spriteUrl,
        fallbackUrl: null,
      },
      cries: { ...cries },
    };
  });
}
