import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { getPokeApiResource, isJsonObject } from "./lib/pokeapi.js";
import { readEvolutionChainId } from "./lib/read-evolution-chain-id.js";
import type { JsonObject } from "./lib/pokeapi.js";

const EVENTS_FILE = new URL("../config/events.json", import.meta.url);
const OUTPUT_DIRECTORY = new URL("../data/catalogue/", import.meta.url);

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

interface TypeCandidate {
  /** Exact Pokémon resource, which may represent a regional or other form. */
  pokemonId: string;

  /** Species shared by its different Pokémon varieties. */
  speciesId: string;

  /** Event type selectors that matched this exact resource. */
  matchedTypes: string[];

  /** Current types reported on the Pokémon resource. */
  types: string[];

  evolutionChainId: number;

  /**
   * This is the species' introduction generation.
   * It must not be used as a form's introduction generation.
   */
  speciesGeneration: string;

  isLegendary: boolean;
  isMythical: boolean;

  /**
   * "regular" means the resource matches the structural requirements
   * of our regular-species converter. It is not a full conversion test.
   */
  reviewGroup: "regular" | "needs-form-review";

  /** Whether event policy requires an unlocked generation for this species. */
  requiresUnlockedGeneration: boolean;
}

interface EventTypeSettings {
  id: string;
  types: string[];
  allowLockedGenerations: boolean;
  requireUnlockedGenerationForLegendary: boolean;
  requireUnlockedGenerationForMythical: boolean;
}

/**
 * Reads the event settings needed for this review.
 *
 * The complete event configuration remains validated by the C# checks.
 * This script does not select events by date or decide live eligibility.
 */
async function readEventSettings(eventId: string): Promise<EventTypeSettings> {
  const data: unknown = JSON.parse(await readFile(EVENTS_FILE, "utf8"));

  if (
    !isJsonObject(data) ||
    data.schemaVersion !== 1 ||
    !Array.isArray(data.events)
  ) {
    throw new Error("Expected event configuration schema version 1.");
  }

  const matches = data.events.filter(
    (event: unknown) => isJsonObject(event) && event.id === eventId,
  );

  const event: unknown = matches[0];

  if (
    matches.length !== 1 ||
    !isJsonObject(event) ||
    !isJsonObject(event.include) ||
    !Array.isArray(event.include.types) ||
    typeof event.allowLockedGenerations !== "boolean" ||
    typeof event.requireUnlockedGenerationForLegendary !== "boolean" ||
    typeof event.requireUnlockedGenerationForMythical !== "boolean"
  ) {
    throw new Error(`Event "${eventId}" has invalid type-review settings.`);
  }

  const types = event.include.types.map((value: unknown): string => {
    if (typeof value !== "string" || !SUPPORTED_TYPES.has(value)) {
      throw new Error(`Event "${eventId}" contains an unsupported type.`);
    }

    return value;
  });

  assert.equal(
    new Set(types).size,
    types.length,
    "Event contains duplicate type selectors.",
  );

  return {
    id: eventId,
    types: types.sort(),
    allowLockedGenerations: event.allowLockedGenerations,
    requireUnlockedGenerationForLegendary:
      event.requireUnlockedGenerationForLegendary,
    requireUnlockedGenerationForMythical:
      event.requireUnlockedGenerationForMythical,
  };
}

/** Reads the exact Pokémon resource names listed by a type endpoint. */
function readTypePokemon(typeResource: JsonObject): string[] {
  if (!Array.isArray(typeResource.pokemon)) {
    throw new Error("Type resource has no valid Pokémon list.");
  }

  const names = typeResource.pokemon.map((record: unknown): string => {
    if (
      !isJsonObject(record) ||
      !isJsonObject(record.pokemon) ||
      typeof record.pokemon.name !== "string" ||
      !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(record.pokemon.name)
    ) {
      throw new Error("Type resource contains an invalid Pokémon reference.");
    }

    return record.pokemon.name;
  });

  assert.equal(
    new Set(names).size,
    names.length,
    "Type resource contains duplicate Pokémon references.",
  );

  return names.sort();
}

/** Reads current types from an individual Pokémon resource. */
function readPokemonTypes(pokemon: JsonObject): string[] {
  if (
    !Array.isArray(pokemon.types) ||
    pokemon.types.length < 1 ||
    pokemon.types.length > 2
  ) {
    throw new Error("Pokémon resource has an invalid type list.");
  }

  const types = pokemon.types.map((record: unknown): string => {
    if (
      !isJsonObject(record) ||
      !isJsonObject(record.type) ||
      typeof record.type.name !== "string" ||
      !SUPPORTED_TYPES.has(record.type.name)
    ) {
      throw new Error("Pokémon resource contains an invalid type record.");
    }

    return record.type.name;
  });

  assert.equal(
    new Set(types).size,
    types.length,
    "Pokémon resource contains duplicate types.",
  );

  return types.sort();
}

/**
 * Creates a review record without guessing form metadata.
 *
 * A differently named variety is sent for review even if PokéAPI marks
 * it as the species' default variety.
 */
function createCandidate(
  pokemon: JsonObject,
  species: JsonObject,
  matchedTypes: string[],
  settings: EventTypeSettings,
): TypeCandidate {
  if (
    typeof pokemon.name !== "string" ||
    typeof pokemon.is_default !== "boolean" ||
    !isJsonObject(pokemon.species) ||
    typeof species.name !== "string" ||
    pokemon.species.name !== species.name ||
    !isJsonObject(species.generation) ||
    typeof species.generation.name !== "string" ||
    !/^generation-[ivxlcdm]+$/.test(species.generation.name) ||
    typeof species.is_legendary !== "boolean" ||
    typeof species.is_mythical !== "boolean"
  ) {
    throw new Error("Invalid Pokémon or species metadata for type review.");
  }

  const types = readPokemonTypes(pokemon);

  for (const matchedType of matchedTypes) {
    assert.ok(
      types.includes(matchedType),
      `${pokemon.name} was listed under ${matchedType}, ` +
        "but its current type records do not match. " +
        "Review or refresh the relevant cached resources.",
    );
  }

  const isRegular = pokemon.is_default && pokemon.name === species.name;

  const requiresUnlockedGeneration =
    !settings.allowLockedGenerations ||
    (species.is_legendary && settings.requireUnlockedGenerationForLegendary) ||
    (species.is_mythical && settings.requireUnlockedGenerationForMythical);

  return {
    pokemonId: pokemon.name,
    speciesId: species.name,
    matchedTypes: [...matchedTypes].sort(),
    types,
    evolutionChainId: readEvolutionChainId(species),
    speciesGeneration: species.generation.name,
    isLegendary: species.is_legendary,
    isMythical: species.is_mythical,
    reviewGroup: isRegular ? "regular" : "needs-form-review",
    requiresUnlockedGeneration,
  };
}

/** Writes a complete review report without replacing partial output. */
async function saveReview(
  settings: EventTypeSettings,
  candidates: TypeCandidate[],
): Promise<void> {
  await mkdir(OUTPUT_DIRECTORY, { recursive: true });

  const destination = new URL(
    `${settings.id}-type-review.json`,
    OUTPUT_DIRECTORY,
  );

  const temporaryFile = new URL(
    `${settings.id}-type-review.${randomUUID()}.tmp`,
    OUTPUT_DIRECTORY,
  );

  const report = {
    schemaVersion: 1,
    reviewOnly: true,
    eventId: settings.id,
    typeSelectors: settings.types,
    generationPolicy: {
      allowLockedGenerations: settings.allowLockedGenerations,
      requireUnlockedGenerationForLegendary:
        settings.requireUnlockedGenerationForLegendary,
      requireUnlockedGenerationForMythical:
        settings.requireUnlockedGenerationForMythical,
    },
    candidates,
  };

  try {
    await writeFile(temporaryFile, `${JSON.stringify(report, null, 2)}\n`, {
      encoding: "utf8",
      flag: "wx",
    });

    await rename(temporaryFile, destination);
  } finally {
    try {
      await rm(temporaryFile, { force: true });
    } catch (error: unknown) {
      console.warn("Could not remove temporary type-review file:", error);
    }
  }
}

async function main(): Promise<void> {
  const eventId = process.argv[2] ?? "halloween-2026";

  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(eventId)) {
    throw new Error("Event ID must be a lowercase slug.");
  }

  const settings = await readEventSettings(eventId);

  // One Pokémon can match multiple selected types.
  // Keep one candidate and record all matching selectors.
  const matchesByPokemon = new Map<string, Set<string>>();

  for (const type of settings.types) {
    const resource = await getPokeApiResource("type", type);

    for (const pokemonName of readTypePokemon(resource)) {
      let matches = matchesByPokemon.get(pokemonName);

      if (matches === undefined) {
        matches = new Set<string>();
        matchesByPokemon.set(pokemonName, matches);
      }

      matches.add(type);
    }
  }

  // Different varieties often share the same species resource.
  // Keep these objects in memory to avoid repeated cache reads.
  const speciesByName = new Map<string, JsonObject>();
  const candidates: TypeCandidate[] = [];

  const pokemonNames = [...matchesByPokemon.keys()].sort();
  let completed = 0;

  for (const pokemonName of pokemonNames) {
    try {
      const pokemon = await getPokeApiResource("pokemon", pokemonName);

      if (
        !isJsonObject(pokemon.species) ||
        typeof pokemon.species.name !== "string"
      ) {
        throw new Error("Pokémon has no valid species reference.");
      }

      const speciesName = pokemon.species.name;
      let species = speciesByName.get(speciesName);

      if (species === undefined) {
        species = await getPokeApiResource("pokemon-species", speciesName);

        speciesByName.set(speciesName, species);
      }

      const matchedTypes = matchesByPokemon.get(pokemonName);

      assert.ok(matchedTypes, "Missing type-selector matches.");

      candidates.push(
        createCandidate(pokemon, species, [...matchedTypes], settings),
      );

      completed++;

      console.log(
        `[${completed}/${pokemonNames.length}] Reviewed ${pokemonName}`,
      );
    } catch (error: unknown) {
      throw new Error(`Could not review "${pokemonName}".`, {
        cause: error,
      });
    }
  }

  assert.equal(
    new Set(candidates.map((candidate) => candidate.pokemonId)).size,
    pokemonNames.length,
    "Type review did not produce one record per Pokémon resource.",
  );

  await saveReview(settings, candidates);

  const formCandidates = candidates.filter(
    (candidate) => candidate.reviewGroup === "needs-form-review",
  );

  const restrictedCandidates = candidates.filter(
    (candidate) => candidate.isLegendary || candidate.isMythical,
  );

  console.log("\nForms requiring review:");

  console.table(
    formCandidates.map((candidate) => ({
      pokemon: candidate.pokemonId,
      species: candidate.speciesId,
      types: candidate.types.join(", "),
      legendary: candidate.isLegendary,
      mythical: candidate.isMythical,
    })),
  );

  console.log("\nLegendary/Mythical candidates:");

  console.table(
    restrictedCandidates.map((candidate) => ({
      pokemon: candidate.pokemonId,
      speciesGeneration: candidate.speciesGeneration,
      requiresUnlockedGeneration: candidate.requiresUnlockedGeneration,
    })),
  );

  console.table({
    typeSelectors: settings.types.length,
    pokemonResources: candidates.length,
    regularCandidates: candidates.length - formCandidates.length,
    requiresFormReview: formCandidates.length,
    legendaryOrMythicalCandidates: restrictedCandidates.length,
  });

  console.log("\nAll event-type review checks passed.");
  console.log(`Saved data/catalogue/${eventId}-type-review.json`);
  console.log(
    "Review only: no collection entries or encounter eligibility were generated.",
  );
}

main().catch((error: unknown) => {
  console.error("\nEvent-type review failed.");

  let current: unknown = error;

  while (current instanceof Error) {
    console.error(current.message);
    current = current.cause;
  }

  if (current !== undefined) {
    console.error(current);
  }

  process.exitCode = 1;
});
