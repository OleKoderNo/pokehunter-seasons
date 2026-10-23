import { randomUUID } from "node:crypto";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { isJsonObject } from "./lib/pokeapi.js";
import { mergeCollectionEntries } from "./lib/merge-collection-entries.js";
import type { CollectionSource } from "./lib/merge-collection-entries.js";
import { validateCollectionEntry } from "./lib/validate-collection-entry.js";
import { validateEntryCounterparts } from "./lib/validate-entry-counterparts.js";
import { createEncounterGroups } from "./lib/create-encounter-groups.js";
import { applyEncounterRarity } from "./lib/apply-encounter-rarity.js";
import type { CollectionEntry } from "./types/catalogue.js";

// The compiled script runs from dist/, one level below the project root.
const DIRECTORY = new URL("../data/catalogue/", import.meta.url);
const OUTPUT_FILE = new URL("merged-entries.json", DIRECTORY);
const RARITY_FILE = new URL("../config/rarity.json", import.meta.url);

/**
 * Reviewed intermediate catalogues included in this merge.
 *
 * Inclusion does not override season, event, or generation restrictions.
 */
const INPUTS = [
  { filename: "generation-1.json", eventId: null },
  { filename: "halloween-2026-families.json", eventId: "halloween-2026" },
  { filename: "halloween-2026-ghosts.json", eventId: "halloween-2026" },
  { filename: "halloween-2026-variants.json", eventId: "halloween-2026" },
] as const;

/**
 * Reads JSON without assuming that its contents have the correct shape.
 *
 * Callers must validate the parsed value before using its fields.
 */
async function readJson(file: URL, label: string): Promise<unknown> {
  const text = await readFile(file, "utf8");

  try {
    return JSON.parse(text);
  } catch (error: unknown) {
    throw new Error(`Invalid JSON in ${label}.`, { cause: error });
  }
}

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
  const document = await readJson(new URL(filename, DIRECTORY), filename);

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

    validateCollectionEntry(entry, `${filename}.entries[${index}]`);
    entries.push(entry);
  }

  return { name: filename, entries };
}

/**
 * Combines reviewed entries, creates groups, and resolves rarity classes.
 *
 * This remains an intermediate catalogue. Runtime eligibility and
 * catching logic are separate responsibilities.
 */
async function main(): Promise<void> {
  const sources: CollectionSource[] = [];

  for (const input of INPUTS) {
    sources.push(await readSource(input.filename, input.eventId));
  }

  const result = mergeCollectionEntries(sources);

  validateEntryCounterparts(result.entries);

  const groups = createEncounterGroups(result.entries);
  const rarityConfiguration = await readJson(RARITY_FILE, "config/rarity.json");

  const encounterGroups = applyEncounterRarity(groups, rarityConfiguration);

  const output = {
    schemaVersion: 1,
    sampleOnly: true,
    scope: "merged-collection-entries",
    sources: sources.map((source) => source.name),
    encounterGroups,
    entries: result.entries,
  };

  // All validation happens before the existing output is replaced.
  await mkdir(DIRECTORY, { recursive: true });

  const temporaryFile = new URL(
    `.merged-entries-${randomUUID()}.tmp`,
    DIRECTORY,
  );

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

  console.table({
    commonGroups: encounterGroups.filter(
      (group) => group.rarityClass === "common",
    ).length,
    uncommonGroups: encounterGroups.filter(
      (group) => group.rarityClass === "uncommon",
    ).length,
    rareGroups: encounterGroups.filter((group) => group.rarityClass === "rare")
      .length,
    ultraRareGroups: encounterGroups.filter(
      (group) => group.rarityClass === "ultraRare",
    ).length,
  });

  console.log("Saved data/catalogue/merged-entries.json");
  console.log("Entry merge, encounter grouping, and rarity assignment passed.");
  console.log("This is not yet the runtime catalogue.");
}

main().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);

  console.error(`\nCatalogue merge failed: ${message}`);

  if (error instanceof Error && error.cause !== undefined) {
    console.error("Cause:", error.cause);
  }

  process.exitCode = 1;
});
