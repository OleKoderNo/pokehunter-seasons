import { isJsonObject } from "./pokeapi.js";
import type { JsonObject } from "./pokeapi.js";
import type { CollectionEntry, CollectionGender } from "../types/catalogue.js";
import { readEvolutionChainId } from "./read-evolution-chain-id.js";

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

const SUPPORTED_TYPES = new Set([
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
  "fairy",
]);

/**
 * Reads optional artwork or audio.
 *
 * Missing media is allowed; malformed media references are rejected.
 * Missing artwork must not remove an otherwise valid shiny entry.
 */
function readAssetUrl(container: unknown, field: string): string | null {
  if (container === null || container === undefined) {
    return null;
  }

  if (!isJsonObject(container)) {
    throw new Error(`Invalid asset container for "${field}".`);
  }

  const value = container[field];

  if (value === null || value === undefined) {
    return null;
  }

  if (typeof value !== "string" || value.trim().length === 0) {
    throw new Error(`Invalid asset reference for "${field}".`);
  }

  const url = new URL(value);

  if (url.protocol !== "https:" && url.protocol !== "http:") {
    throw new Error(`Unsupported asset URL protocol for "${field}".`);
  }

  return url.href;
}

/** Reads the explicitly identified National Pokédex number. */
function readNationalDexNumber(species: JsonObject): number {
  if (!Array.isArray(species.pokedex_numbers)) {
    throw new Error("Species has no valid Pokédex records.");
  }

  const records = species.pokedex_numbers.filter(
    (record: unknown) =>
      isJsonObject(record) &&
      isJsonObject(record.pokedex) &&
      record.pokedex.name === "national",
  );

  const record: unknown = records[0];

  if (
    records.length !== 1 ||
    !isJsonObject(record) ||
    typeof record.entry_number !== "number" ||
    !Number.isSafeInteger(record.entry_number) ||
    record.entry_number <= 0
  ) {
    throw new Error("Expected exactly one valid National Pokédex record.");
  }

  return record.entry_number;
}

/**
 * Reads the English display name.
 *
 * Do not generate names by capitalizing API slugs: names may contain
 * punctuation or gender symbols that the slug does not preserve.
 */
function readDisplayName(species: JsonObject): string {
  if (!Array.isArray(species.names)) {
    throw new Error("Species has no valid localized names.");
  }

  const names = species.names.filter(
    (record: unknown) =>
      isJsonObject(record) &&
      isJsonObject(record.language) &&
      record.language.name === "en",
  );

  const record: unknown = names[0];

  if (
    names.length !== 1 ||
    !isJsonObject(record) ||
    typeof record.name !== "string" ||
    record.name.trim().length === 0
  ) {
    throw new Error("Expected exactly one valid English species name.");
  }

  return record.name;
}

/** Reads distinct supported types in their API slot order. */
function readTypes(pokemon: JsonObject): string[] {
  if (
    !Array.isArray(pokemon.types) ||
    pokemon.types.length < 1 ||
    pokemon.types.length > 2
  ) {
    throw new Error("Expected one or two Pokémon type records.");
  }

  const records = pokemon.types.map((record: unknown) => {
    if (
      !isJsonObject(record) ||
      typeof record.slot !== "number" ||
      !Number.isInteger(record.slot) ||
      !isJsonObject(record.type) ||
      typeof record.type.name !== "string" ||
      !SUPPORTED_TYPES.has(record.type.name)
    ) {
      throw new Error("Invalid Pokémon type record.");
    }

    return { slot: record.slot, name: record.type.name };
  });

  records.sort((left, right) => left.slot - right.slot);

  if (
    records.some((record, index) => record.slot !== index + 1) ||
    new Set(records.map((record) => record.name)).size !== records.length
  ) {
    throw new Error("Pokémon types contain duplicate names or invalid slots.");
  }

  return records.map((record) => record.name);
}

/**
 * Determines collection identities, not encounter probabilities.
 *
 * Species with both genders but no visible difference receive one
 * shared identity per shiny state.
 */
function readCollectionGenders(species: JsonObject): CollectionGender[] {
  const rate = species.gender_rate;
  const hasDifference = species.has_gender_differences;

  if (
    typeof rate !== "number" ||
    !Number.isInteger(rate) ||
    rate < -1 ||
    rate > 8 ||
    typeof hasDifference !== "boolean"
  ) {
    throw new Error("Species has invalid gender metadata.");
  }

  // Visible male/female differences require both genders to exist.
  if (hasDifference) {
    if (rate <= 0 || rate >= 8) {
      throw new Error(
        "Visible gender differences conflict with gender availability.",
      );
    }

    return ["male", "female"];
  }

  if (rate === -1) {
    return ["genderless"];
  }

  if (rate === 0) {
    return ["male"];
  }

  if (rate === 8) {
    return ["female"];
  }

  return ["unspecified"];
}

/**
 * Creates collection entries for a regular default Pokémon resource.
 *
 * This currently requires the Pokémon and species slugs to match.
 * Named varieties, regional forms, and costumes require a separate
 * reviewed mapping rather than guessed metadata.
 *
 * Produces both normal and shiny entries even when artwork is missing.
 */
export function createRegularEntries(
  pokemon: JsonObject,
  species: JsonObject,
): CollectionEntry[] {
  if (
    typeof species.name !== "string" ||
    !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(species.name) ||
    pokemon.name !== species.name ||
    pokemon.is_default !== true ||
    !isJsonObject(pokemon.species) ||
    pokemon.species.name !== species.name
  ) {
    throw new Error(
      "Expected matching regular default Pokémon and species resources.",
    );
  }

  const speciesId = species.name;

  if (!isJsonObject(species.generation)) {
    throw new Error(`${speciesId} has no valid introduction generation.`);
  }

  const generationName = species.generation.name;

  const introducedGeneration =
    typeof generationName === "string"
      ? GENERATIONS[generationName]
      : undefined;

  if (introducedGeneration === undefined) {
    throw new Error(`${speciesId} has an unsupported introduction generation.`);
  }

  if (
    typeof species.is_legendary !== "boolean" ||
    typeof species.is_mythical !== "boolean"
  ) {
    throw new Error(`${speciesId} has invalid Legendary or Mythical flags.`);
  }

  const nationalDexNumber = readNationalDexNumber(species);
  const evolutionChainId = readEvolutionChainId(species);
  const displayName = readDisplayName(species);
  const types = readTypes(pokemon);
  const genders = readCollectionGenders(species);
  const hasGenderDifference = species.has_gender_differences === true;

  const cries = {
    latest: readAssetUrl(pokemon.cries, "latest"),
    legacy: readAssetUrl(pokemon.cries, "legacy"),
  };

  const entries: CollectionEntry[] = [];

  // Keep normal entries before shiny entries, matching the Pikachu sample.
  for (const isShiny of [false, true]) {
    for (const gender of genders) {
      // Female-only species use their default sprite.
      // Dedicated female fields are used for visible gender differences.
      const useFemaleSprite = hasGenderDifference && gender === "female";

      const spriteField = useFemaleSprite
        ? isShiny
          ? "front_shiny_female"
          : "front_female"
        : isShiny
          ? "front_shiny"
          : "front_default";

      entries.push({
        id: `${speciesId}:${gender}:${isShiny ? "shiny" : "normal"}`,
        speciesId,
        nationalDexNumber,
        evolutionChainId,
        isLegendary: species.is_legendary,
        isMythical: species.is_mythical,
        encounterGroupId: speciesId,
        formId: speciesId,
        displayName,
        formName: null,
        regionalForm: null,
        gender,
        hasGenderDifference,
        isShiny,
        isCostume: false,
        eventOnly: false,
        introducedGeneration,
        types: [...types],
        sprite: {
          url: readAssetUrl(pokemon.sprites, spriteField),
          fallbackUrl: null,
        },
        cries: { ...cries },
      });
    }
  }

  return entries;
}
