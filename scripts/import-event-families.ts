import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import {
  getPokeApiEvolutionChain,
  getPokeApiResource,
  isJsonObject,
} from "./lib/pokeapi.js";
import { readEvolutionChainId } from "./lib/read-evolution-chain-id.js";
import { collectEvolutionSpecies } from "./lib/collect-evolution-species.js";
import { createRegularEntries } from "./lib/create-regular-entries.js";
import type { CollectionEntry } from "./types/catalogue.js";

const EVENTS_FILE = new URL("../config/events.json", import.meta.url);
const OUTPUT_DIRECTORY = new URL("../data/catalogue/", import.meta.url);

interface ResolvedFamily {
  evolutionChainId: number;
  selectors: string[];
  species: string[];
}

/**
 * Reads only the configuration fields needed by this importer.
 *
 * The C# configuration checks remain responsible for validating the
 * complete event configuration, including dates and gameplay settings.
 */
async function readFamilySelectors(eventId: string): Promise<string[]> {
  const data: unknown = JSON.parse(await readFile(EVENTS_FILE, "utf8"));

  if (
    !isJsonObject(data) ||
    data.schemaVersion !== 1 ||
    !Array.isArray(data.events)
  ) {
    throw new Error("Expected event configuration schema version 1.");
  }

  const matchingEvents = data.events.filter(
    (event: unknown) => isJsonObject(event) && event.id === eventId,
  );

  const event: unknown = matchingEvents[0];

  if (
    matchingEvents.length !== 1 ||
    !isJsonObject(event) ||
    !isJsonObject(event.include) ||
    !Array.isArray(event.include.evolutionFamilies)
  ) {
    throw new Error(`Expected one event with family selectors: ${eventId}.`);
  }

  const selectors = event.include.evolutionFamilies.map(
    (value: unknown): string => {
      if (
        typeof value !== "string" ||
        !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value)
      ) {
        throw new Error("Event contains an invalid evolution-family selector.");
      }

      return value;
    },
  );

  assert.equal(
    new Set(selectors).size,
    selectors.length,
    "Event contains duplicate evolution-family selectors.",
  );

  return selectors.sort();
}

/**
 * Verifies traversal using a fictional branching family.
 *
 * This catches an implementation that follows only the first evolution
 * or misses an earlier stage.
 */
function checkFamilyTraversal(): void {
  const tree = {
    species: { name: "test-root" },
    evolves_to: [
      {
        species: { name: "test-left" },
        evolves_to: [
          {
            species: { name: "test-final" },
            evolves_to: [],
          },
        ],
      },
      {
        species: { name: "test-right" },
        evolves_to: [],
      },
    ],
  };

  assert.deepStrictEqual(collectEvolutionSpecies(tree), [
    "test-final",
    "test-left",
    "test-right",
    "test-root",
  ]);

  assert.throws(
    () =>
      collectEvolutionSpecies({
        species: { name: "test-root" },
        evolves_to: null,
      }),
    /Invalid evolution-chain node/,
  );

  console.log("PASS: Family traversal includes every branch and stage.");
}

/** Writes the completed output through a temporary file. */
async function saveOutput(
  eventId: string,
  families: ResolvedFamily[],
  entries: CollectionEntry[],
): Promise<void> {
  await mkdir(OUTPUT_DIRECTORY, { recursive: true });

  const destination = new URL(`${eventId}-families.json`, OUTPUT_DIRECTORY);

  const temporaryFile = new URL(
    `${eventId}-families.${randomUUID()}.tmp`,
    OUTPUT_DIRECTORY,
  );

  const output = {
    schemaVersion: 1,
    sampleOnly: true,
    scope: "event-evolution-families-regular",
    eventId,
    families,
    entries,
  };

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
      console.warn("Could not remove temporary event-family file:", error);
    }
  }
}

async function main(): Promise<void> {
  checkFamilyTraversal();

  const eventId = process.argv[2] ?? "halloween-2026";

  // The ID becomes part of a filename, so restrict it before use.
  if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(eventId)) {
    throw new Error("Event ID must be a lowercase slug.");
  }

  const selectors = await readFamilySelectors(eventId);
  const familiesById = new Map<number, ResolvedFamily>();

  for (const selector of selectors) {
    const species = await getPokeApiResource("pokemon-species", selector);
    const evolutionChainId = readEvolutionChainId(species);

    let family = familiesById.get(evolutionChainId);

    if (family === undefined) {
      const resource = await getPokeApiEvolutionChain(evolutionChainId);

      family = {
        evolutionChainId,
        selectors: [],
        species: collectEvolutionSpecies(resource.chain),
      };

      familiesById.set(evolutionChainId, family);
    }

    assert.ok(
      family.species.includes(selector),
      `Family ${evolutionChainId} does not contain selector "${selector}".`,
    );

    family.selectors.push(selector);
  }

  const families = [...familiesById.values()].sort(
    (left, right) => left.evolutionChainId - right.evolutionChainId,
  );

  const entriesById = new Map<string, CollectionEntry>();
  const importedSpecies = new Set<string>();

  for (const family of families) {
    for (const speciesName of family.species) {
      const species = await getPokeApiResource("pokemon-species", speciesName);

      assert.equal(
        readEvolutionChainId(species),
        family.evolutionChainId,
        `${speciesName} disagrees with its resolved family.`,
      );

      // Existing converter guards reject unsupported default-variety
      // mappings. Such failures must be reviewed, never silently skipped.
      const pokemon = await getPokeApiResource("pokemon", speciesName);
      const entries = createRegularEntries(pokemon, species);

      for (const entry of entries) {
        const existing = entriesById.get(entry.id);

        if (existing !== undefined) {
          assert.deepStrictEqual(
            existing,
            entry,
            `Conflicting catalogue entry: ${entry.id}.`,
          );
        } else {
          entriesById.set(entry.id, entry);
        }
      }

      importedSpecies.add(speciesName);
    }
  }

  const expectedSpecies = new Set(families.flatMap((family) => family.species));

  assert.deepStrictEqual(
    [...importedSpecies].sort(),
    [...expectedSpecies].sort(),
    "Not every resolved family member was imported.",
  );

  const entries = [...entriesById.values()].sort(
    (left, right) =>
      left.nationalDexNumber - right.nationalDexNumber ||
      (left.id < right.id ? -1 : left.id > right.id ? 1 : 0),
  );

  for (const entry of entries) {
    const counterpartId =
      `${entry.formId}:${entry.gender}:` + (entry.isShiny ? "normal" : "shiny");

    assert.ok(
      entriesById.has(counterpartId),
      `Missing normal/shiny counterpart for ${entry.id}.`,
    );
  }

  await saveOutput(eventId, families, entries);

  console.table(
    families.map((family) => ({
      family: family.evolutionChainId,
      selectors: family.selectors.join(", "),
      species: family.species.join(", "),
    })),
  );

  const missingSprites = entries.filter((entry) => entry.sprite.url === null);

  for (const entry of missingSprites) {
    console.warn(`[missing sprite] ${entry.id}`);
  }

  console.table({
    selectors: selectors.length,
    uniqueFamilies: families.length,
    species: importedSpecies.size,
    collectionEntries: entries.length,
    missingSpriteReferences: missingSprites.length,
  });

  console.log("\nAll event-family import checks passed.");
  console.log(`Saved data/catalogue/${eventId}-families.json`);
  console.log(
    "Partial event expansion: regular evolution-family members only. " +
      "Type selectors, regional forms, explicit forms, and costumes are pending.",
  );
}

main().catch((error: unknown) => {
  console.error("\nEvent-family import failed.");

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
