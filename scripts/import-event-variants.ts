import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { mkdir, rename, rm, writeFile } from "node:fs/promises";
import { getPokeApiResource, isJsonObject } from "./lib/pokeapi.js";
import {
  createEventVariantEntries,
  EVENT_VARIANTS,
} from "./lib/create-event-variant-entries.js";
import type { CollectionEntry } from "./types/catalogue.js";

// Compiled scripts run from dist/, one level below the repository root.
const OUTPUT_DIRECTORY = new URL("../data/catalogue/", import.meta.url);
const OUTPUT_FILE = new URL("halloween-2026-variants.json", OUTPUT_DIRECTORY);

/**
 * Reads a named API reference after checking its structure.
 *
 * External JSON must be validated before its properties are used.
 */
function readReferenceName(value: unknown, context: string): string {
  if (
    !isJsonObject(value) ||
    typeof value.name !== "string" ||
    value.name.trim().length === 0
  ) {
    throw new Error(`Invalid named reference: ${context}.`);
  }

  return value.name;
}

/**
 * Generates the six reviewed explicit event variants.
 *
 * Each variant receives normal and shiny entries.
 * Costume entries are female-only according to their reviewed mappings.
 *
 * This generates catalogue data; it does not activate encounters.
 */
async function main(): Promise<void> {
  const entries: CollectionEntry[] = [];

  for (const definition of EVENT_VARIANTS) {
    const pokemon = await getPokeApiResource("pokemon", definition.pokemonId);

    const species = await getPokeApiResource(
      "pokemon-species",
      definition.speciesId,
    );

    // The inspected resources each have one reviewed form.
    // Stop for review if the source structure changes.
    if (!Array.isArray(pokemon.forms) || pokemon.forms.length !== 1) {
      throw new Error(
        `Expected one reviewed form for ${definition.pokemonId}.`,
      );
    }

    const formId = readReferenceName(
      pokemon.forms[0],
      `${definition.pokemonId}.forms`,
    );

    assert.equal(
      formId,
      definition.pokemonId,
      "The form reference must match the reviewed variant.",
    );

    const form = await getPokeApiResource("pokemon-form", formId);

    // Resolve the form's introduction generation rather than using
    // the generation of its original species.
    const versionGroupId = readReferenceName(
      form.version_group,
      `${formId}.version_group`,
    );

    const versionGroup = await getPokeApiResource(
      "version-group",
      versionGroupId,
    );

    const generationName = readReferenceName(
      versionGroup.generation,
      `${versionGroupId}.generation`,
    );

    const converted = createEventVariantEntries(
      pokemon,
      species,
      form,
      generationName,
    );

    assert.equal(converted.length, 2);
    assert.deepStrictEqual(
      converted.map((entry) => entry.isShiny),
      [false, true],
    );

    entries.push(...converted);
  }

  // Check the complete output before replacing any existing catalogue.
  assert.equal(entries.length, 12);
  assert.equal(new Set(entries.map((entry) => entry.id)).size, 12);
  assert.equal(entries.filter((entry) => entry.isCostume).length, 10);

  const output = {
    schemaVersion: 1,
    sampleOnly: true,
    scope: "reviewed-explicit-event-variants",
    eventId: "halloween-2026",
    entries,
  };

  await mkdir(OUTPUT_DIRECTORY, { recursive: true });

  const temporaryFile = new URL(
    `.halloween-2026-variants-${randomUUID()}.tmp`,
    OUTPUT_DIRECTORY,
  );

  // Write the complete output first, then replace the previous file.
  // A failed conversion never reaches this replacement step.
  try {
    await writeFile(
      temporaryFile,
      `${JSON.stringify(output, null, 2)}\n`,
      "utf8",
    );

    await rename(temporaryFile, OUTPUT_FILE);
  } finally {
    await rm(temporaryFile, { force: true });
  }

  console.table(
    entries.map((entry) => ({
      id: entry.id,
      group: entry.encounterGroupId,
      eventOnly: entry.eventOnly,
      spriteAvailable: entry.sprite.url !== null,
    })),
  );

  const missingSprites = entries.filter((entry) => entry.sprite.url === null);

  for (const entry of missingSprites) {
    console.warn(`[missing sprite] ${entry.id}`);
  }

  console.table({
    collectionEntries: entries.length,
    normalEntries: entries.filter((entry) => !entry.isShiny).length,
    shinyEntries: entries.filter((entry) => entry.isShiny).length,
    costumeEntries: entries.filter((entry) => entry.isCostume).length,
    missingSpriteReferences: missingSprites.length,
  });

  console.log("Saved data/catalogue/halloween-2026-variants.json");
  console.log("This is an intermediate catalogue, not the complete game.");
}

main().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);

  console.error(`\nEvent variant import failed: ${message}`);

  if (error instanceof Error && error.cause !== undefined) {
    console.error("Cause:", error.cause);
  }

  process.exitCode = 1;
});
