import { randomUUID } from "node:crypto";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { isJsonObject } from "./lib/pokeapi.js";
import { mergeCollectionEntries } from "./lib/merge-collection-entries.js";
import type { CollectionSource } from "./lib/merge-collection-entries.js";
import { validateCollectionEntry } from "./lib/validate-collection-entry.js";
import { validateEntryCounterparts } from "./lib/validate-entry-counterparts.js";
import { createEncounterGroups } from "./lib/create-encounter-groups.js";
import type { CollectionEntry } from "./types/catalogue.js";

// The compiled script runs from dist/, one level below the project root.
const DIRECTORY = new URL("../data/catalogue/", import.meta.url);
const OUTPUT_FILE = new URL("merged-entries.json", DIRECTORY);

/**
 * Reviewed intermediate catalogues included in this merge.
 *
 * Event sources must identify the expected event. Being included here
 * does not make every entry eligible during a season or event.
 */
const INPUTS = [
  { filename: "generation-1.json", eventId: null },
  { filename: "halloween-2026-families.json", eventId: "halloween-2026" },
  { filename: "halloween-2026-ghosts.json", eventId: "halloween-2026" },
  { filename: "halloween-2026-variants.json", eventId: "halloween-2026" },
] as const;

/**
 * Reads an intermediate catalogue and validates its entry records.
 *
 * Wrapper metadata differs between importers, so only the required
 * wrapper fields are checked here. Entry fields are checked strictly.
 */
async function readSource(
  filename: string,
  eventId: string | null,
): Promise<CollectionSource> {
  const file = new URL(filename, DIRECTORY);
  const text = await readFile(file, "utf8");

  let document: unknown;

  try {
    document = JSON.parse(text);
  } catch (error: unknown) {
    throw new Error(`Invalid JSON in ${filename}.`, { cause: error });
  }

  if (
    !isJsonObject(document) ||
    document.schemaVersion !== 1 ||
    document.sampleOnly !== true ||
    !Array.isArray(document.entries) ||
    document.entries.length === 0
  ) {
    throw new Error(`Invalid intermediate catalogue wrapper: ${filename}.`);
  }

  if (eventId !== null && document.eventId !== eventId) {
    throw new Error(`Unexpected event ID in ${filename}.`);
  }

  const entries: CollectionEntry[] = [];

  for (let index = 0; index < document.entries.length; index += 1) {
    const entry: unknown = document.entries[index];

    // Validation narrows unknown JSON to a checked CollectionEntry.
    validateCollectionEntry(entry, `${filename}.entries[${index}]`);
    entries.push(entry);
  }

  return { name: filename, entries };
}

/**
 * Combines reviewed entry sources and builds their encounter groups.
 *
 * This remains an intermediate catalogue. Runtime eligibility,
 * rarity assignment, and catching logic are separate responsibilities.
 */
async function main(): Promise<void> {
  const sources: CollectionSource[] = [];

  for (const input of INPUTS) {
    sources.push(await readSource(input.filename, input.eventId));
  }

  // Identical entries shared by sources are merged. Conflicting entries
  // with the same collection identity must be rejected.
  const result = mergeCollectionEntries(sources);

  // Every collection identity must have its corresponding shiny state.
  validateEntryCounterparts(result.entries);

  // Build groups from the reviewed mappings already stored on entries.
  const encounterGroups = createEncounterGroups(result.entries);

  const output = {
    schemaVersion: 1,
    sampleOnly: true,
    scope: "merged-collection-entries",
    sources: sources.map((source) => source.name),
    encounterGroups,
    entries: result.entries,
  };

  // Nothing is written until entries, counterparts, and groups pass.
  await mkdir(DIRECTORY, { recursive: true });

  const temporaryFile = new URL(
    `.merged-entries-${randomUUID()}.tmp`,
    DIRECTORY,
  );

  try {
    // Write a complete temporary file before replacing the output.
    await writeFile(
      temporaryFile,
      `${JSON.stringify(output, null, 2)}\n`,
      "utf8",
    );

    await rename(temporaryFile, OUTPUT_FILE);
  } finally {
    // Remove a leftover temporary file if writing or replacement failed.
    await rm(temporaryFile, { force: true });
  }

  console.table(
    sources.map((source) => ({
      source: source.name,
      entries: source.entries.length,
    })),
  );

  console.table({
    inputEntries: result.inputEntries,
    duplicatesRemoved: result.duplicatesRemoved,
    mergedEntries: result.entries.length,
    encounterGroups: encounterGroups.length,
    normalEntries: result.entries.filter((entry) => !entry.isShiny).length,
    shinyEntries: result.entries.filter((entry) => entry.isShiny).length,
    missingSpriteReferences: result.entries.filter(
      (entry) => entry.sprite.url === null,
    ).length,
  });

  console.log("Saved data/catalogue/merged-entries.json");
  console.log(
    "Entry merge and encounter grouping passed. " +
      "This is not yet the runtime catalogue.",
  );
}

main().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);

  console.error(`\nCatalogue merge failed: ${message}`);

  if (error instanceof Error && error.cause !== undefined) {
    console.error("Cause:", error.cause);
  }

  process.exitCode = 1;
});
