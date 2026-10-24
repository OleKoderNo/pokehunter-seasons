import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { mkdir, rename, rm, writeFile } from "node:fs/promises";
import { getPokeApiResource, isJsonObject } from "./lib/pokeapi.js";
import {
  createEventVariantEntries,
  EVENT_VARIANTS,
} from "./lib/create-event-variant-entries.js";
import { validateCollectionEntry } from "./lib/validate-collection-entry.js";
import { validateEntryCounterparts } from "./lib/validate-entry-counterparts.js";
import type { CollectionEntry } from "./types/catalogue.js";

// Compiled scripts run from dist/, one level below the repository root.
const OUTPUT_DIRECTORY = new URL("../data/catalogue/", import.meta.url);
const OUTPUT_FILE = new URL("halloween-2026-variants.json", OUTPUT_DIRECTORY);

/** Validates a named reference before using it in an API request. */
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
 * Hangry Morpeko has male and female normal/shiny entries.
 * Each Pikachu costume has only female normal/shiny entries.
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

    // A changed source structure requires another review.
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

    // Use the form's introduction generation, not the base species'.
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

    const expectedIds = [false, true].flatMap((isShiny) =>
      definition.genders.map(
        (gender) =>
          `${definition.pokemonId}:${gender}:${isShiny ? "shiny" : "normal"}`,
      ),
    );

    assert.deepStrictEqual(
      converted.map((entry) => entry.id),
      expectedIds,
      `Unexpected collection identities for ${definition.pokemonId}.`,
    );

    for (let index = 0; index < converted.length; index += 1) {
      validateCollectionEntry(
        converted[index],
        `${definition.pokemonId}.entries[${index}]`,
      );
    }

    entries.push(...converted);
  }

  // Reviewed roster: four Morpeko entries plus five female-only pairs.
  assert.equal(entries.length, 14);
  assert.equal(new Set(entries.map((entry) => entry.id)).size, 14);
  assert.equal(entries.filter((entry) => entry.isCostume).length, 10);
  assert.equal(entries.filter((entry) => !entry.isShiny).length, 7);
  assert.equal(entries.filter((entry) => entry.isShiny).length, 7);

  validateEntryCounterparts(entries);

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

  // Complete conversion and validation before replacing the old output.
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
      gender: entry.gender,
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
