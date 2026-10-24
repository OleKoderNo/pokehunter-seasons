import assert from "node:assert/strict";
import { mkdir, writeFile } from "node:fs/promises";
import { getPokeApiResource } from "./lib/pokeapi.js";
import { createPikachuEntries } from "./lib/create-pikachu-entries.js";
import { createRegularEntries } from "./lib/create-regular-entries.js";
import { readEvolutionChainId } from "./lib/read-evolution-chain-id.js";
import { validateCollectionEntry } from "./lib/validate-collection-entry.js";
import { validateEntryCounterparts } from "./lib/validate-entry-counterparts.js";
import type { CollectionEntry } from "./types/catalogue.js";

const SAMPLE_DIRECTORY = new URL("../data/samples/", import.meta.url);
const SAMPLE_FILE = new URL("regular-species.json", SAMPLE_DIRECTORY);

/**
 * Reviewed examples covering every collection-gender branch.
 * Expected genders are independent of the converter's output.
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
    genders: ["male", "female"],
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
 * Checks reference parsing independently of downloaded API data.
 * Invalid references must not silently assign the wrong evolution family.
 */
function checkEvolutionChainReferences(): void {
  assert.equal(
    readEvolutionChainId({
      evolution_chain: {
        url: "https://pokeapi.co/api/v2/evolution-chain/10/",
      },
    }),
    10,
  );

  assert.equal(
    readEvolutionChainId({
      evolution_chain: {
        url: "https://pokeapi.co/api/v2/evolution-chain/10",
      },
    }),
    10,
  );

  assert.throws(() => readEvolutionChainId({}), /evolution-chain/);

  assert.throws(
    () => readEvolutionChainId({ evolution_chain: null }),
    /evolution-chain/,
  );

  const invalidUrls = [
    "not-a-url",
    "https://example.com/api/v2/evolution-chain/10/",
    "https://pokeapi.co/api/v2/pokemon/10/",
    "https://pokeapi.co/api/v2/evolution-chain/0/",
    "https://pokeapi.co/api/v2/evolution-chain/-1/",
    "https://pokeapi.co/api/v2/evolution-chain/1.5/",
    "https://pokeapi.co/api/v2/evolution-chain/9007199254740992/",
    "https://pokeapi.co/api/v2/evolution-chain/10/?unexpected=true",
    "https://pokeapi.co/api/v2/evolution-chain/10/#unexpected",
  ];

  for (const url of invalidUrls) {
    assert.throws(
      () =>
        readEvolutionChainId({
          evolution_chain: { url },
        }),
      /evolution-chain/,
      `Expected rejection of evolution-chain reference: ${url}`,
    );
  }

  console.log("PASS: Evolution-chain references were validated.");
}

/**
 * Imports a small reviewed sample.
 * All checks complete before writing the sample file.
 */
async function main(): Promise<void> {
  checkEvolutionChainReferences();

  const entries: CollectionEntry[] = [];

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
      validateCollectionEntry(entry);

      assert.equal(entry.nationalDexNumber, example.nationalDexNumber);
      assert.equal(entry.hasGenderDifference, example.hasGenderDifference);
      assert.equal(entry.introducedGeneration, 1);
      assert.equal(entry.isLegendary, false);
      assert.equal(entry.isMythical, false);
      assert.equal(entry.isCostume, false);
      assert.equal(entry.eventOnly, false);
    }

    if (example.slug === "pikachu") {
      assert.ok(
        converted.every((entry) => entry.evolutionChainId === 10),
        "Pikachu entries must reference evolution chain 10.",
      );

      assert.deepStrictEqual(
        converted,
        createPikachuEntries(pokemon, species),
        "The general converter changed the existing Pikachu output.",
      );

      console.log(
        "PASS: General Pikachu output matches the original converter.",
      );
    }

    if (example.slug === "bulbasaur") {
      for (const isShiny of [false, true]) {
        const male = converted.find(
          (entry) => entry.gender === "male" && entry.isShiny === isShiny,
        );

        const female = converted.find(
          (entry) => entry.gender === "female" && entry.isShiny === isShiny,
        );

        assert.ok(male, "Bulbasaur is missing its male entry.");
        assert.ok(female, "Bulbasaur is missing its female entry.");

        assert.notEqual(male.id, female.id);
        assert.deepStrictEqual(male.sprite, female.sprite);
        assert.equal(male.encounterGroupId, female.encounterGroupId);
      }

      console.log(
        "PASS: Bulbasaur genders have separate identities and shared artwork.",
      );
    }

    entries.push(...converted);

    console.log(
      `PASS: ${example.slug} produced its expected ${converted.length} entries.`,
    );
  }

  assert.equal(entries.length, 14, "Expected fourteen sample entries.");

  assert.equal(
    new Set(entries.map((entry) => entry.id)).size,
    entries.length,
    "The sample contains duplicate collection IDs.",
  );

  assert.equal(entries.filter((entry) => !entry.isShiny).length, 7);
  assert.equal(entries.filter((entry) => entry.isShiny).length, 7);

  validateEntryCounterparts(entries);

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
