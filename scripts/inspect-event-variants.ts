import { getPokeApiResource, isJsonObject } from "./lib/pokeapi.js";
import type { JsonObject } from "./lib/pokeapi.js";

const VARIANTS = [
  "morpeko-hangry",
  "pikachu-rock-star",
  "pikachu-belle",
  "pikachu-pop-star",
  "pikachu-phd",
  "pikachu-libre",
] as const;

/**
 * Reads the name from an API resource reference.
 *
 * A reference looks like:
 * { name: "pikachu", url: "https://..." }
 */
function readReferenceName(value: unknown, context: string): string {
  if (
    !isJsonObject(value) ||
    typeof value.name !== "string" ||
    value.name.trim().length === 0
  ) {
    throw new Error(`Invalid resource reference: ${context}.`);
  }

  return value.name;
}

/**
 * Selects the four sprite fields relevant to collection entries.
 *
 * This is an inspection report, not catalogue validation.
 * Missing fields are displayed as null so they remain visible.
 */
function summarizeSprites(value: unknown): JsonObject {
  if (!isJsonObject(value)) {
    return {
      front_default: null,
      front_female: null,
      front_shiny: null,
      front_shiny_female: null,
    };
  }

  return {
    front_default: value.front_default ?? null,
    front_female: value.front_female ?? null,
    front_shiny: value.front_shiny ?? null,
    front_shiny_female: value.front_shiny_female ?? null,
  };
}

async function main(): Promise<void> {
  const report: JsonObject[] = [];

  for (const pokemonId of VARIANTS) {
    const pokemon = await getPokeApiResource("pokemon", pokemonId);

    const speciesId = readReferenceName(
      pokemon.species,
      `${pokemonId}.species`,
    );

    const species = await getPokeApiResource("pokemon-species", speciesId);

    if (!Array.isArray(pokemon.forms) || pokemon.forms.length === 0) {
      throw new Error(`${pokemonId} has no form references.`);
    }

    const forms: JsonObject[] = [];

    for (const reference of pokemon.forms) {
      const formId = readReferenceName(reference, `${pokemonId}.forms`);

      const form = await getPokeApiResource("pokemon-form", formId);

      // Make sure this form belongs to the requested Pokémon resource.
      const ownerId = readReferenceName(form.pokemon, `${formId}.pokemon`);

      if (ownerId !== pokemonId) {
        throw new Error(`${formId} belongs to ${ownerId}, not ${pokemonId}.`);
      }

      const versionGroupId = readReferenceName(
        form.version_group,
        `${formId}.version_group`,
      );

      const versionGroup = await getPokeApiResource(
        "version-group",
        versionGroupId,
      );

      const generationId = readReferenceName(
        versionGroup.generation,
        `${versionGroupId}.generation`,
      );

      forms.push({
        formId,
        formName: form.form_name,
        isDefault: form.is_default,
        isBattleOnly: form.is_battle_only,
        isMega: form.is_mega,
        versionGroup: versionGroupId,
        apiIntroductionGeneration: generationId,
        types: form.types,
        sprites: summarizeSprites(form.sprites),
      });
    }

    report.push({
      pokemonId,
      speciesId,

      // These are species-wide values. They do not necessarily describe
      // gender restrictions belonging to a particular costume or form.
      speciesGenderRate: species.gender_rate,
      speciesHasGenderDifferences: species.has_gender_differences,

      isDefaultPokemon: pokemon.is_default,
      types: pokemon.types,
      sprites: summarizeSprites(pokemon.sprites),
      cries: pokemon.cries ?? null,
      forms,
    });
  }

  console.log("\nEvent variant inspection:");
  console.log(JSON.stringify(report, null, 2));
  console.log(`\nInspected ${report.length} Pokémon resources.`);
  console.log("No collection catalogue files were changed.");
}

main().catch((error: unknown) => {
  const message = error instanceof Error ? error.message : String(error);

  console.error(`\nEvent variant inspection failed: ${message}`);

  if (error instanceof Error && error.cause !== undefined) {
    console.error("Cause:", error.cause);
  }

  process.exitCode = 1;
});
