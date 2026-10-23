import type { JsonObject } from "../../lib/pokeapi.js";

/**
 * Minimal synthetic source data used by converter tests.
 *
 * These fixtures describe API structure, not a complete Pokémon record.
 * They never download resources or read the API cache.
 */
export function makeSpecies(overrides: JsonObject = {}): JsonObject {
  return {
    name: "pikachu",
    generation: { name: "generation-i" },
    gender_rate: 4,
    has_gender_differences: true,
    is_legendary: false,
    is_mythical: false,
    pokedex_numbers: [
      {
        entry_number: 25,
        pokedex: { name: "national" },
      },
    ],
    names: [
      {
        name: "Pikachu",
        language: { name: "en" },
      },
    ],
    evolution_chain: {
      url: "https://pokeapi.co/api/v2/evolution-chain/10/",
    },
    ...overrides,
  };
}

export function makeSprites(): JsonObject {
  return {
    front_default: "https://assets.invalid/normal.png",
    front_female: "https://assets.invalid/female.png",
    front_shiny: "https://assets.invalid/shiny.png",
    front_shiny_female: "https://assets.invalid/shiny-female.png",
  };
}

export function makePokemon(overrides: JsonObject = {}): JsonObject {
  return {
    name: "pikachu",
    is_default: true,
    species: { name: "pikachu" },
    types: [{ slot: 1, type: { name: "electric" } }],
    sprites: makeSprites(),
    cries: {
      latest: "https://assets.invalid/cry.ogg",
      legacy: null,
    },
    ...overrides,
  };
}

export function makeForm(overrides: JsonObject = {}): JsonObject {
  return {
    name: "pikachu",
    form_name: "",
    is_default: true,
    pokemon: { name: "pikachu" },
    types: [{ slot: 1, type: { name: "electric" } }],
    sprites: makeSprites(),
    ...overrides,
  };
}

/** Builds a regional-form example with a different species generation. */
export function makeAlolanMarowak() {
  const species = makeSpecies({
    name: "marowak",
    gender_rate: 4,
    has_gender_differences: false,
    pokedex_numbers: [{ entry_number: 105, pokedex: { name: "national" } }],
    names: [{ name: "Marowak", language: { name: "en" } }],
    evolution_chain: {
      url: "https://pokeapi.co/api/v2/evolution-chain/46/",
    },
  });

  const pokemon = makePokemon({
    name: "marowak-alola",
    is_default: false,
    species: { name: "marowak" },
    types: [
      { slot: 1, type: { name: "fire" } },
      { slot: 2, type: { name: "ghost" } },
    ],
  });

  const form = makeForm({
    name: "marowak-alola",
    form_name: "alola",
    pokemon: { name: "marowak-alola" },
    types: [
      { slot: 1, type: { name: "fire" } },
      { slot: 2, type: { name: "ghost" } },
    ],
  });

  return { pokemon, species, form };
}

/**
 * Represents a gender-specific Pokémon resource and its default form.
 * The fixture supports the three explicitly reviewed gender mappings.
 */
export function makeGenderForm(speciesId: string, gender: "male" | "female") {
  const resourceName = `${speciesId}-${gender}`;

  const species = makeSpecies({
    name: speciesId,
    names: [{ name: speciesId, language: { name: "en" } }],
  });

  const pokemon = makePokemon({
    name: resourceName,
    species: { name: speciesId },
    sprites: {
      front_default: `https://assets.invalid/${resourceName}.png`,
      front_shiny: `https://assets.invalid/${resourceName}-shiny.png`,
      front_female: null,
      front_shiny_female: null,
    },
  });

  const form = makeForm({
    name: resourceName,
    form_name: gender,
    pokemon: { name: resourceName },
  });

  return { pokemon, species, form };
}
