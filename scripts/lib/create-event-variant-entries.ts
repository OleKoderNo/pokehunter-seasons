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

/**
 * Reviewed rules for the six explicit Halloween variants.
 *
 * Gender restrictions belong to the supported form. A costume does not
 * automatically support every gender available to its base species.
 */
interface VariantDefinition {
  pokemonId: string;
  speciesId: string;
  sourceFormName: string;
  formName: string;
  generationName: string;
  introducedGeneration: number;
  genders: readonly CollectionGender[];
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
    genders: ["male", "female"],
    isCostume: false,
  },
  {
    pokemonId: "pikachu-rock-star",
    speciesId: "pikachu",
    sourceFormName: "rock-star",
    formName: "Rock Star",
    generationName: "generation-vi",
    introducedGeneration: 6,
    genders: ["female"],
    isCostume: true,
  },
  {
    pokemonId: "pikachu-belle",
    speciesId: "pikachu",
    sourceFormName: "belle",
    formName: "Belle",
    generationName: "generation-vi",
    introducedGeneration: 6,
    genders: ["female"],
    isCostume: true,
  },
  {
    pokemonId: "pikachu-pop-star",
    speciesId: "pikachu",
    sourceFormName: "pop-star",
    formName: "Pop Star",
    generationName: "generation-vi",
    introducedGeneration: 6,
    genders: ["female"],
    isCostume: true,
  },
  {
    pokemonId: "pikachu-phd",
    speciesId: "pikachu",
    sourceFormName: "phd",
    formName: "Ph. D.",
    generationName: "generation-vi",
    introducedGeneration: 6,
    genders: ["female"],
    isCostume: true,
  },
  {
    pokemonId: "pikachu-libre",
    speciesId: "pikachu",
    sourceFormName: "libre",
    formName: "Libre",
    generationName: "generation-vi",
    introducedGeneration: 6,
    genders: ["female"],
    isCostume: true,
  },
];

/**
 * Creates both shiny states for every reviewed supported gender.
 *
 * The caller resolves generationName from the form's version group.
 * Missing media preserves all collection identities.
 * Runtime rules determine whether the entries are currently obtainable.
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

  // These reviewed species support both genders. Pikachu has visible
  // species-level differences; Morpeko does not.
  if (
    species.gender_rate !== 4 ||
    species.has_gender_differences !== definition.isCostume
  ) {
    throw new Error(
      "Event variant species gender metadata disagrees with review.",
    );
  }

  const speciesGenders = readCollectionGenders(species);

  if (
    definition.genders.length === 0 ||
    new Set(definition.genders).size !== definition.genders.length ||
    definition.genders.some((gender) => !speciesGenders.includes(gender))
  ) {
    throw new Error(
      "Event variant genders disagree with the supported species genders.",
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

  const entries: CollectionEntry[] = [];

  for (const isShiny of [false, true]) {
    // Female-only costumes use their default artwork.
    // Morpeko's two genders share the same artwork for each shiny state.
    const spriteField = isShiny ? "front_shiny" : "front_default";

    const spriteUrl =
      readAssetUrl(form.sprites, spriteField) ??
      readAssetUrl(pokemon.sprites, spriteField);

    for (const gender of definition.genders) {
      entries.push({
        id: `${definition.pokemonId}:${gender}:${isShiny ? "shiny" : "normal"}`,
        speciesId: definition.speciesId,
        nationalDexNumber,
        evolutionChainId,
        isLegendary: false,
        isMythical: false,

        // Costume entries share Pikachu's group.
        // More genders do not create extra encounter groups.
        encounterGroupId: definition.isCostume
          ? definition.speciesId
          : definition.pokemonId,

        formId: definition.pokemonId,
        displayName,
        formName: definition.formName,
        regionalForm: null,
        gender,

        // Appearance metadata does not determine collection separation.
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
      });
    }
  }

  return entries;
}
