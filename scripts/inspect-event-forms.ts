import { randomUUID } from "node:crypto";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { getPokeApiResource, isJsonObject } from "./lib/pokeapi.js";
import type { JsonObject } from "./lib/pokeapi.js";

const CATALOGUE_DIRECTORY = new URL("../data/catalogue/", import.meta.url);

/**
 * Avoids repeatedly reading the same cached species or version group.
 * The existing API client still handles persistent caching.
 */
const resources = new Map<string, JsonObject>();

async function loadResource(
  endpoint: string,
  name: string,
): Promise<JsonObject> {
  const key = `${endpoint}/${name}`;
  const existing = resources.get(key);

  if (existing !== undefined) {
    return existing;
  }

  const resource = await getPokeApiResource(endpoint, name);
  resources.set(key, resource);

  return resource;
}

/** Reads a named API reference without guessing from a URL or suffix. */
function readReferenceName(value: unknown, context: string): string {
  if (
    !isJsonObject(value) ||
    typeof value.name !== "string" ||
    !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value.name)
  ) {
    throw new Error(`Invalid named reference: ${context}.`);
  }

  return value.name;
}

/**
 * Keeps the four front-facing sprite references needed for collection
 * identity review. Null represents absent artwork, not an absent entry.
 */
function readFrontSprites(value: unknown): Record<string, string | null> {
  if (!isJsonObject(value)) {
    throw new Error("Expected a sprite object.");
  }

  const result: Record<string, string | null> = {};

  for (const field of [
    "front_default",
    "front_female",
    "front_shiny",
    "front_shiny_female",
  ]) {
    const reference = value[field];

    if (reference === null || reference === undefined) {
      result[field] = null;
      continue;
    }

    if (typeof reference !== "string" || reference.trim() === "") {
      throw new Error(`Invalid sprite reference: ${field}.`);
    }

    const url = new URL(reference);

    if (url.protocol !== "https:" && url.protocol !== "http:") {
      throw new Error(`Unsupported sprite protocol: ${field}.`);
    }

    result[field] = url.href;
  }

  return result;
}

/**
 * Reads form metadata, including the generation of its version group.
 *
 * This is API evidence for the reviewed mapping. It does not reuse the
 * base species' generation as the form's introduction generation.
 */
async function inspectForm(formName: string, pokemonName: string) {
  const form = await loadResource("pokemon-form", formName);

  const owner = readReferenceName(form.pokemon, `${formName}.pokemon`);

  if (owner !== pokemonName) {
    throw new Error(
      `Form "${formName}" belongs to "${owner}", not "${pokemonName}".`,
    );
  }

  const versionGroupName = readReferenceName(
    form.version_group,
    `${formName}.version_group`,
  );

  const versionGroup = await loadResource("version-group", versionGroupName);

  const generation = readReferenceName(
    versionGroup.generation,
    `${versionGroupName}.generation`,
  );

  if (
    typeof form.form_name !== "string" ||
    typeof form.is_default !== "boolean" ||
    typeof form.is_battle_only !== "boolean" ||
    typeof form.is_mega !== "boolean"
  ) {
    throw new Error(`Invalid form metadata for "${formName}".`);
  }

  return {
    resourceName: formName,
    formName: form.form_name,
    isDefault: form.is_default,
    isBattleOnly: form.is_battle_only,
    isMega: form.is_mega,
    versionGroup: versionGroupName,
    apiIntroductionGeneration: generation,
    sprites: readFrontSprites(form.sprites),
  };
}

/** Collects the source evidence needed to define a Pokémon's variants. */
async function inspectPokemon(pokemonName: string) {
  const pokemon = await loadResource("pokemon", pokemonName);

  const speciesName = readReferenceName(
    pokemon.species,
    `${pokemonName}.species`,
  );

  const species = await loadResource("pokemon-species", speciesName);

  if (
    typeof pokemon.is_default !== "boolean" ||
    typeof species.gender_rate !== "number" ||
    !Number.isInteger(species.gender_rate) ||
    species.gender_rate < -1 ||
    species.gender_rate > 8 ||
    typeof species.has_gender_differences !== "boolean" ||
    !Array.isArray(species.varieties) ||
    !Array.isArray(pokemon.forms) ||
    pokemon.forms.length === 0
  ) {
    throw new Error(`Invalid variant metadata for "${pokemonName}".`);
  }

  const varieties = species.varieties.map((value: unknown) => {
    if (!isJsonObject(value) || typeof value.is_default !== "boolean") {
      throw new Error(`Invalid variety for "${speciesName}".`);
    }

    return {
      pokemonId: readReferenceName(value.pokemon, `${speciesName}.varieties`),
      isDefault: value.is_default,
    };
  });

  const formNames = pokemon.forms.map((value: unknown) =>
    readReferenceName(value, `${pokemonName}.forms`),
  );

  if (new Set(formNames).size !== formNames.length) {
    throw new Error(`Duplicate form references for "${pokemonName}".`);
  }

  const forms = [];

  for (const formName of formNames.sort()) {
    forms.push(await inspectForm(formName, pokemonName));
  }

  return {
    pokemonId: pokemonName,
    speciesId: speciesName,
    isDefaultPokemon: pokemon.is_default,
    genderRate: species.gender_rate,
    hasGenderDifference: species.has_gender_differences,
    sprites: readFrontSprites(pokemon.sprites),
    varieties,
    forms,
  };
}

/** Inspects every candidate, including defaults with cosmetic forms. */
async function main(): Promise<void> {
  const eventId = process.argv[2] ?? "halloween-2026";

  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(eventId)) {
    throw new Error("Event ID must be a lowercase slug.");
  }

  const reviewFile = new URL(
    `${eventId}-type-review.json`,
    CATALOGUE_DIRECTORY,
  );

  const review: unknown = JSON.parse(await readFile(reviewFile, "utf8"));

  if (
    !isJsonObject(review) ||
    review.schemaVersion !== 1 ||
    review.reviewOnly !== true ||
    review.eventId !== eventId ||
    !Array.isArray(review.candidates)
  ) {
    throw new Error("Expected the generated event-type review report.");
  }

  const pokemonNames = review.candidates.map((candidate: unknown): string => {
    if (
      !isJsonObject(candidate) ||
      typeof candidate.pokemonId !== "string" ||
      !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(candidate.pokemonId)
    ) {
      throw new Error("Invalid Pokémon ID in the type-review report.");
    }

    return candidate.pokemonId;
  });

  if (new Set(pokemonNames).size !== pokemonNames.length) {
    throw new Error("The type-review report contains duplicate Pokémon IDs.");
  }

  const inspected = [];

  for (const pokemonName of pokemonNames.sort()) {
    try {
      inspected.push(await inspectPokemon(pokemonName));

      console.log(
        `[${inspected.length}/${pokemonNames.length}] Inspected ${pokemonName}`,
      );
    } catch (error: unknown) {
      throw new Error(`Could not inspect forms for "${pokemonName}".`, {
        cause: error,
      });
    }
  }

  const output = {
    schemaVersion: 1,
    reviewOnly: true,
    eventId,
    pokemon: inspected,
  };

  await mkdir(CATALOGUE_DIRECTORY, { recursive: true });

  const destination = new URL(
    `${eventId}-form-details.json`,
    CATALOGUE_DIRECTORY,
  );

  const temporaryFile = new URL(
    `${eventId}-form-details.${randomUUID()}.tmp`,
    CATALOGUE_DIRECTORY,
  );

  try {
    await writeFile(temporaryFile, `${JSON.stringify(output, null, 2)}\n`, {
      encoding: "utf8",
      flag: "wx",
    });

    await rename(temporaryFile, destination);
  } finally {
    try {
      await rm(temporaryFile, { force: true });
    } catch (error: unknown) {
      console.warn("Could not remove temporary form-details file:", error);
    }
  }

  console.table(
    inspected
      .filter(
        (item) =>
          item.pokemonId !== item.speciesId ||
          item.hasGenderDifference ||
          item.forms.length > 1,
      )
      .map((item) => ({
        pokemon: item.pokemonId,
        genderRate: item.genderRate,
        genderDifference: item.hasGenderDifference,
        femaleSprite: item.sprites.front_female !== null,
        forms: item.forms.map((form) => form.resourceName).join(", "),
        generations: [
          ...new Set(item.forms.map((form) => form.apiIntroductionGeneration)),
        ].join(", "),
      })),
  );

  console.log(`\nInspected ${inspected.length} Pokémon resources.`);
  console.log(`Saved data/catalogue/${eventId}-form-details.json`);
  console.log("Source metadata only; no collection identities were changed.");
}

main().catch((error: unknown) => {
  console.error("\nEvent-form inspection failed.");

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
