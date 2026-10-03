import { isJsonObject } from "./pokeapi.js";
import type { JsonObject } from "./pokeapi.js";
import type { CollectionEntry } from "../types/catalogue.js";

/**
 * Reads an optional media reference.
 *
 * Missing references become null. Malformed references are reported
 * instead of silently being treated as missing.
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

/**
 * Creates regular Pikachu's four collection entries.
 *
 * This converter is intentionally specific to Pikachu. Its mappings
 * must not be applied to other Pokémon without reviewing their forms
 * and gender differences.
 */
export function createPikachuEntries(
  pokemon: JsonObject,
  species: JsonObject,
): CollectionEntry[] {
  // Confirm we received the regular Pikachu resources.
  if (
    pokemon.name !== "pikachu" ||
    species.name !== "pikachu" ||
    !isJsonObject(pokemon.species) ||
    pokemon.species.name !== "pikachu" ||
    species.has_gender_differences !== true ||
    !isJsonObject(species.generation) ||
    species.generation.name !== "generation-i"
  ) {
    throw new Error(
      "Expected regular Pikachu and its Generation 1 species data.",
    );
  }

  if (!Array.isArray(pokemon.types) || pokemon.types.length === 0) {
    throw new Error("Pikachu has no valid type records.");
  }

  const types = pokemon.types.map((item: unknown): string => {
    if (
      !isJsonObject(item) ||
      !isJsonObject(item.type) ||
      typeof item.type.name !== "string" ||
      item.type.name.length === 0
    ) {
      throw new Error("Pikachu contains an invalid type record.");
    }

    return item.type.name;
  });

  const cries = {
    latest: readAssetUrl(pokemon.cries, "latest"),
    legacy: readAssetUrl(pokemon.cries, "legacy"),
  };

  // For regular Pikachu, the default front sprite represents the male.
  // Female and shiny counterparts have their own source fields.
  const variants = [
    {
      gender: "male",
      isShiny: false,
      spriteField: "front_default",
    },
    {
      gender: "female",
      isShiny: false,
      spriteField: "front_female",
    },
    {
      gender: "male",
      isShiny: true,
      spriteField: "front_shiny",
    },
    {
      gender: "female",
      isShiny: true,
      spriteField: "front_shiny_female",
    },
  ] as const;

  return variants.map(
    ({ gender, isShiny, spriteField }): CollectionEntry => ({
      id: `pikachu:${gender}:${isShiny ? "shiny" : "normal"}`,
      speciesId: "pikachu",
      encounterGroupId: "pikachu",
      formId: "pikachu",
      displayName: "Pikachu",
      formName: null,
      regionalForm: null,
      gender,
      hasGenderDifference: true,
      isShiny,
      isCostume: false,
      eventOnly: false,
      introducedGeneration: 1,
      types: [...types],
      sprite: {
        url: readAssetUrl(pokemon.sprites, spriteField),
        fallbackUrl: null,
      },
      cries: { ...cries },
    }),
  );
}
