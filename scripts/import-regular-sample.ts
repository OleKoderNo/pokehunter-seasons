import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";
import { getPokeApiResource } from "./lib/pokeapi.js";
import { createPikachuEntries } from "./lib/create-pikachu-entries.js";
import { createRegularEntries } from "./lib/create-regular-entries.js";
import type { CollectionEntry } from "./types/catalogue.js";

const SAMPLE_DIRECTORY = new URL("../data/samples/", import.meta.url);
const SAMPLE_FILE = new URL("regular-species.json", SAMPLE_DIRECTORY);

/**
 * Fixed examples covering the collection-gender branches.
 *
 * These expectations are independent of the converter's output.
 * A change in identity must be reviewed rather than silently accepted.
 */
const CASES = [
  {
    slug: "pikachu",
    nationalDexNumber: 25,
    genders: ["male", "female"],
    hasGenderDifference: true,
  },
  {
    slug: "bulbasaur",
    nationalDexNumber: 1,
    genders: ["unspecified"],
    hasGenderDifference: false,
  },
  {
    slug: "magnemite",
    nationalDexNumber: 81,
    genders: ["genderless"],
    hasGenderDifference: false,
  },
  {
    slug: "nidoran-f",
    nationalDexNumber: 29,
    genders: ["female"],
    hasGenderDifference: false,
  },
  {
    slug: "nidoran-m",
    nationalDexNumber: 32,
    genders: ["male"],
    hasGenderDifference: false,
  },
] as const;

/**
 * Imports a small reviewed sample before attempting a full catalogue.
 *
 * All assertions run before the output file is written.
 * The original Pikachu sample is not overwritten.
 */
async function main(): Promise<void> {
  const entries: CollectionEntry[] = [];

  // Sequential requests keep this small importer easy to inspect
  // and avoid sending a burst of requests to the API.
  for (const example of CASES) {
    const pokemon = await getPokeApiResource("pokemon", example.slug);
    const species = await getPokeApiResource("pokemon-species", example.slug);

    const converted = createRegularEntries(pokemon, species);

    const expectedIds: string[] = [];

    for (const state of ["normal", "shiny"]) {
      for (const gender of example.genders) {
        expectedIds.push(`${example.slug}:${gender}:${state}`);
      }
    }

    assert.deepStrictEqual(
      converted.map((entry) => entry.id),
      expectedIds,
      `${example.slug}: unexpected collection identities.`,
    );

    for (const entry of converted) {
      assert.equal(entry.nationalDexNumber, example.nationalDexNumber);
      assert.equal(entry.hasGenderDifference, example.hasGenderDifference);
      assert.equal(entry.introducedGeneration, 1);
      assert.equal(entry.isLegendary, false);
      assert.equal(entry.isMythical, false);
      assert.equal(entry.isCostume, false);
      assert.equal(entry.eventOnly, false);
    }

    if (example.slug === "pikachu") {
      assert.deepStrictEqual(
        converted,
        createPikachuEntries(pokemon, species),
        "The general converter changed the existing Pikachu output.",
      );

      console.log(
        "PASS: General Pikachu output matches the original converter.",
      );
    }

    entries.push(...converted);

    console.log(
      `PASS: ${example.slug} produced its expected ${converted.length} entries.`,
    );
  }

  assert.equal(entries.length, 12, "Expected twelve sample entries.");

  assert.equal(
    new Set(entries.map((entry) => entry.id)).size,
    entries.length,
    "The sample contains duplicate collection IDs.",
  );

  // Every identity must have both shiny states.
  // Sprite availability must not determine whether an entry exists.
  for (const entry of entries) {
    const counterpartId =
      `${entry.formId}:${entry.gender}:` + (entry.isShiny ? "normal" : "shiny");

    assert.ok(
      entries.some((candidate) => candidate.id === counterpartId),
      `Missing normal/shiny counterpart for ${entry.id}.`,
    );
  }

  const sample = {
    schemaVersion: 1,
    sampleOnly: true,
    entries,
  };

  await mkdir(SAMPLE_DIRECTORY, { recursive: true });

  await writeFile(SAMPLE_FILE, `${JSON.stringify(sample, null, 2)}\n`, "utf8");

  console.table(
    entries.map((entry) => ({
      id: entry.id,
      dex: entry.nationalDexNumber,
      gender: entry.gender,
      shiny: entry.isShiny,
      spriteAvailable: entry.sprite.url !== null,
    })),
  );

  for (const entry of entries) {
    if (entry.sprite.url === null) {
      console.warn(`[missing sprite] ${entry.id}`);
    }
  }

  console.log("\nAll regular-species sample checks passed.");
  console.log("Saved data/samples/regular-species.json");
  console.log("This sample is not the complete game catalogue.");
}

main().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);

  console.error(`\nRegular-species import failed: ${message}`);

  if (error instanceof Error && error.cause !== undefined) {
    console.error("Cause:", error.cause);
  }

  process.exitCode = 1;
});
