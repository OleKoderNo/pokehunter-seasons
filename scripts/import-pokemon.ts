import { mkdir, writeFile } from "node:fs/promises";
import { getPokeApiResource } from "./lib/pokeapi.js";
import { createPikachuEntries } from "./lib/create-pikachu-entries.js";

// The compiled script lives in dist/, one level below the project root.
const SAMPLE_DIRECTORY = new URL("../data/samples/", import.meta.url);
const SAMPLE_FILE = new URL("pikachu.json", SAMPLE_DIRECTORY);

/**
 * Generates a sample containing regular Pikachu's four collection entries.
 *
 * The sample is separate from the future production catalogue.
 */
async function main(): Promise<void> {
  const pokemon = await getPokeApiResource("pokemon", "pikachu");
  const species = await getPokeApiResource("pokemon-species", "pikachu");

  const entries = createPikachuEntries(pokemon, species);

  // Guard against accidentally producing duplicate collection identities.
  const uniqueIds = new Set(entries.map((entry) => entry.id));

  if (entries.length !== 4 || uniqueIds.size !== 4) {
    throw new Error("Expected exactly four unique Pikachu entries.");
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

  console.log(`\nGenerated ${entries.length} collection entries.`);
  console.log("Saved data/samples/pikachu.json");
  console.log("This is a sample, not the complete game catalogue.");
}

main().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);

  console.error(`\nImport failed: ${message}`);

  if (error instanceof Error && error.cause !== undefined) {
    console.error("Cause:", error.cause);
  }

  process.exitCode = 1;
});
