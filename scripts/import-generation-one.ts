import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { mkdir, rename, rm, writeFile } from "node:fs/promises";
import { getPokeApiResource, isJsonObject } from "./lib/pokeapi.js";
import { createRegularEntries } from "./lib/create-regular-entries.js";
import type { JsonObject } from "./lib/pokeapi.js";
import type { CollectionEntry } from "./types/catalogue.js";

const OUTPUT_DIRECTORY = new URL("../data/catalogue/", import.meta.url);
const OUTPUT_FILE = new URL("generation-1.json", OUTPUT_DIRECTORY);

const EXPECTED_SPECIES_COUNT = 151;

/**
 * Reads and validates the species list supplied by the generation endpoint.
 *
 * Sorting the names makes request order predictable between runs.
 * Final catalogue entries are sorted separately by National Dex number.
 */
function readSpeciesNames(generation: JsonObject): string[] {
  if (
    generation.id !== 1 ||
    generation.name !== "generation-i" ||
    !Array.isArray(generation.pokemon_species)
  ) {
    throw new Error("Expected a valid Generation 1 resource.");
  }

  const names = generation.pokemon_species.map((resource: unknown): string => {
    if (
      !isJsonObject(resource) ||
      typeof resource.name !== "string" ||
      !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(resource.name)
    ) {
      throw new Error("Generation 1 contains an invalid species reference.");
    }

    return resource.name;
  });

  assert.equal(
    names.length,
    EXPECTED_SPECIES_COUNT,
    "Expected exactly 151 species in Generation 1.",
  );

  assert.equal(
    new Set(names).size,
    EXPECTED_SPECIES_COUNT,
    "Generation 1 contains duplicate species names.",
  );

  return names.sort();
}

/**
 * Confirms that the selected resource is the species' default variety.
 *
 * Gen 1 regular resources are expected to use the same slug as their
 * species. A different mapping requires review rather than guessing.
 */
function requireRegularVariety(species: JsonObject, speciesName: string): void {
  if (!Array.isArray(species.varieties)) {
    throw new Error(`${speciesName} has no valid variety list.`);
  }

  const defaults = species.varieties.filter(
    (variety: unknown) => isJsonObject(variety) && variety.is_default === true,
  );

  const defaultVariety: unknown = defaults[0];

  if (
    defaults.length !== 1 ||
    !isJsonObject(defaultVariety) ||
    !isJsonObject(defaultVariety.pokemon) ||
    defaultVariety.pokemon.name !== speciesName
  ) {
    throw new Error(
      `${speciesName} requires a reviewed default-variety mapping.`,
    );
  }
}

/**
 * Validates the assembled catalogue before it replaces previous output.
 *
 * These checks supplement the individual converter's validation.
 * They verify completeness and relationships across all entries.
 */
function validateCatalogue(
  entries: CollectionEntry[],
  speciesNames: string[],
): void {
  const expectedSpecies = new Set(speciesNames);
  const actualSpecies = new Set<string>();
  const entriesById = new Map<string, CollectionEntry>();
  const speciesByDexNumber = new Map<number, string>();
  const metadataBySpecies = new Map<string, string>();

  for (const entry of entries) {
    assert.ok(
      expectedSpecies.has(entry.speciesId),
      `Unexpected species: ${entry.speciesId}.`,
    );

    assert.ok(
      !entriesById.has(entry.id),
      `Duplicate collection ID: ${entry.id}.`,
    );

    assert.equal(
      entry.id,
      `${entry.formId}:${entry.gender}:${entry.isShiny ? "shiny" : "normal"}`,
      `Unexpected identity format: ${entry.id}.`,
    );

    assert.equal(entry.formId, entry.speciesId);
    assert.equal(entry.encounterGroupId, entry.speciesId);
    assert.equal(entry.introducedGeneration, 1);
    assert.equal(entry.isCostume, false);
    assert.equal(entry.eventOnly, false);
    assert.equal(entry.formName, null);
    assert.equal(entry.regionalForm, null);

    assert.ok(
      Number.isSafeInteger(entry.nationalDexNumber) &&
        entry.nationalDexNumber >= 1 &&
        entry.nationalDexNumber <= EXPECTED_SPECIES_COUNT,
      `Invalid Gen 1 National Dex number for ${entry.id}.`,
    );

    assert.ok(
      Number.isSafeInteger(entry.evolutionChainId) &&
        entry.evolutionChainId > 0,
      `Invalid evolution family for ${entry.id}.`,
    );

    const previousSpecies = speciesByDexNumber.get(entry.nationalDexNumber);

    assert.ok(
      previousSpecies === undefined || previousSpecies === entry.speciesId,
      `National Dex number ${entry.nationalDexNumber} belongs to multiple species.`,
    );

    // All gender/shiny entries for one regular species must agree on
    // their shared metadata. Sprite references may legitimately differ.
    const metadata = JSON.stringify({
      nationalDexNumber: entry.nationalDexNumber,
      evolutionChainId: entry.evolutionChainId,
      displayName: entry.displayName,
      isLegendary: entry.isLegendary,
      isMythical: entry.isMythical,
      hasGenderDifference: entry.hasGenderDifference,
      types: entry.types,
    });

    const previousMetadata = metadataBySpecies.get(entry.speciesId);

    assert.ok(
      previousMetadata === undefined || previousMetadata === metadata,
      `Conflicting metadata for ${entry.speciesId}.`,
    );

    metadataBySpecies.set(entry.speciesId, metadata);
    speciesByDexNumber.set(entry.nationalDexNumber, entry.speciesId);
    actualSpecies.add(entry.speciesId);
    entriesById.set(entry.id, entry);
  }

  assert.equal(
    actualSpecies.size,
    EXPECTED_SPECIES_COUNT,
    "The catalogue does not represent all 151 species.",
  );

  // Check every number individually: a total alone would not detect
  // a missing number combined with an unexpected replacement.
  for (let dexNumber = 1; dexNumber <= EXPECTED_SPECIES_COUNT; dexNumber++) {
    assert.ok(
      speciesByDexNumber.has(dexNumber),
      `Missing National Dex number ${dexNumber}.`,
    );
  }

  for (const entry of entries) {
    const counterpartId =
      `${entry.formId}:${entry.gender}:` + (entry.isShiny ? "normal" : "shiny");

    const counterpart = entriesById.get(counterpartId);

    assert.ok(counterpart, `Missing counterpart for ${entry.id}.`);
    assert.equal(counterpart.isShiny, !entry.isShiny);

    if (entry.hasGenderDifference) {
      assert.ok(
        entry.gender === "male" || entry.gender === "female",
        `Unexpected gender identity for ${entry.id}.`,
      );

      const otherGender = entry.gender === "male" ? "female" : "male";

      const otherGenderId =
        `${entry.formId}:${otherGender}:` +
        (entry.isShiny ? "shiny" : "normal");

      assert.ok(
        entriesById.has(otherGenderId),
        `Missing visible gender counterpart for ${entry.id}.`,
      );
    }
  }
}

/**
 * Replaces output only after the complete JSON has been written.
 *
 * The temporary file is beside the destination so the rename stays
 * within the same filesystem.
 */
async function saveCatalogue(entries: CollectionEntry[]): Promise<void> {
  await mkdir(OUTPUT_DIRECTORY, { recursive: true });

  const temporaryFile = new URL(
    `generation-1.${randomUUID()}.tmp`,
    OUTPUT_DIRECTORY,
  );

  const catalogue = {
    schemaVersion: 1,

    // This is a complete regular Gen 1 import, but remains development
    // data until encounter classifications and runtime loading are ready.
    sampleOnly: true,
    scope: "generation-1-regular",
    speciesCount: EXPECTED_SPECIES_COUNT,
    entries,
  };

  try {
    await writeFile(temporaryFile, `${JSON.stringify(catalogue, null, 2)}\n`, {
      encoding: "utf8",
      flag: "wx",
    });

    await rename(temporaryFile, OUTPUT_FILE);
  } finally {
    // Preserve the original failure if cleanup also fails.
    try {
      await rm(temporaryFile, { force: true });
    } catch (error: unknown) {
      console.warn("Could not remove temporary catalogue file:", error);
    }
  }
}

/**
 * Imports every regular Gen 1 species using cached API resources.
 *
 * Requests run sequentially. If a later request fails, earlier cached
 * resources can be reused when the importer is run again.
 */
async function main(): Promise<void> {
  const generation = await getPokeApiResource("generation", "generation-i");
  const speciesNames = readSpeciesNames(generation);

  const entries: CollectionEntry[] = [];
  let completed = 0;

  for (const speciesName of speciesNames) {
    try {
      const species = await getPokeApiResource("pokemon-species", speciesName);

      requireRegularVariety(species, speciesName);

      const pokemon = await getPokeApiResource("pokemon", speciesName);
      const converted = createRegularEntries(pokemon, species);

      entries.push(...converted);
      completed++;

      console.log(
        `[${completed}/${EXPECTED_SPECIES_COUNT}] ` +
          `${speciesName}: ${converted.length} entries`,
      );
    } catch (error: unknown) {
      throw new Error(`Could not import regular species "${speciesName}".`, {
        cause: error,
      });
    }
  }

  // Stable ordering avoids unrelated output changes between imports.
  entries.sort((left, right) => {
    const dexDifference = left.nationalDexNumber - right.nationalDexNumber;

    if (dexDifference !== 0) {
      return dexDifference;
    }

    return left.id < right.id ? -1 : left.id > right.id ? 1 : 0;
  });

  validateCatalogue(entries, speciesNames);
  await saveCatalogue(entries);

  const normalCount = entries.filter((entry) => !entry.isShiny).length;
  const shinyCount = entries.filter((entry) => entry.isShiny).length;
  const missingSprites = entries.filter((entry) => entry.sprite.url === null);

  const missingCrySpecies = new Set(
    entries
      .filter(
        (entry) => entry.cries.latest === null && entry.cries.legacy === null,
      )
      .map((entry) => entry.speciesId),
  );

  for (const entry of missingSprites) {
    console.warn(`[missing sprite] ${entry.id}`);
  }

  for (const speciesId of missingCrySpecies) {
    console.warn(`[missing cry] ${speciesId}`);
  }

  console.log("\nAll Generation 1 catalogue checks passed.");

  console.table({
    species: EXPECTED_SPECIES_COUNT,
    collectionEntries: entries.length,
    normalEntries: normalCount,
    shinyEntries: shinyCount,
    missingSpriteReferences: missingSprites.length,
    speciesWithoutCryReferences: missingCrySpecies.size,
  });

  console.log("Saved data/catalogue/generation-1.json");
  console.log(
    "Development catalogue: regular Gen 1 species only. " +
      "Forms, costumes, event additions, and rarity assignments are still pending.",
  );
}

main().catch((error: unknown) => {
  console.error("\nGeneration 1 import failed.");

  // Display nested errors so a converter failure is not hidden behind
  // the outer message identifying the species being imported.
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
