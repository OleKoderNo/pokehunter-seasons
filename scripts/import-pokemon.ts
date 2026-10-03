import { getPokeApiResource, isJsonObject } from "./lib/pokeapi.js";
import type { JsonObject } from "./lib/pokeapi.js";

interface SampleSummary {
  speciesName: string;
  types: string[];
  hasGenderDifferences: boolean;
  assets: {
    normalSprite: boolean;
    femaleSprite: boolean;
    shinySprite: boolean;
    shinyFemaleSprite: boolean;
    latestCry: boolean;
    legacyCry: boolean;
  };
}

/**
 * Reports whether an optional asset field contains a non-empty string.
 *
 * This checks reference presence, not whether the URL is reachable.
 */
function hasAssetReference(container: unknown, field: string): boolean {
  if (!isJsonObject(container)) {
    return false;
  }

  const value = container[field];

  return typeof value === "string" && value.trim().length > 0;
}

function createSummary(
  pokemon: JsonObject,
  species: JsonObject,
): SampleSummary {
  const speciesReference = pokemon.species;
  const typeRecords = pokemon.types;

  if (
    typeof species.name !== "string" ||
    typeof species.has_gender_differences !== "boolean" ||
    !isJsonObject(speciesReference) ||
    speciesReference.name !== species.name ||
    !Array.isArray(typeRecords) ||
    typeRecords.length === 0
  ) {
    throw new Error("Pikachu source data is missing expected fields.");
  }

  const types = typeRecords.map((item: unknown): string => {
    if (
      !isJsonObject(item) ||
      !isJsonObject(item.type) ||
      typeof item.type.name !== "string"
    ) {
      throw new Error("Pikachu source data contains an invalid type.");
    }

    return item.type.name;
  });

  return {
    speciesName: species.name,
    types,
    hasGenderDifferences: species.has_gender_differences,
    assets: {
      normalSprite: hasAssetReference(pokemon.sprites, "front_default"),
      femaleSprite: hasAssetReference(pokemon.sprites, "front_female"),
      shinySprite: hasAssetReference(pokemon.sprites, "front_shiny"),
      shinyFemaleSprite: hasAssetReference(
        pokemon.sprites,
        "front_shiny_female",
      ),
      latestCry: hasAssetReference(pokemon.cries, "latest"),
      legacyCry: hasAssetReference(pokemon.cries, "legacy"),
    },
  };
}

/**
 * Downloads and summarizes a small source-data sample.
 *
 * This does not generate collection entries or a playable catalogue yet.
 */
async function main(): Promise<void> {
  const pokemon = await getPokeApiResource("pokemon", "pikachu");
  const species = await getPokeApiResource("pokemon-species", "pikachu");

  const summary = createSummary(pokemon, species);

  console.log("\nPikachu source data is ready.");
  console.log(`Species: ${summary.speciesName}`);
  console.log(`Types: ${summary.types.join(", ")}`);
  console.log(`Visible gender differences: ${summary.hasGenderDifferences}`);

  console.table(summary.assets);

  console.log("Cached responses are in .cache/pokeapi/.");
  console.log("No game catalogue or player data was changed.");
}

// Catch failures at the entry point and signal an unsuccessful run.
main().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);

  console.error(`\nError: ${message}`);

  if (error instanceof Error && error.cause !== undefined) {
    console.error(`Cause: ${error.cause}`);
  }

  process.exitCode = 1;
});
