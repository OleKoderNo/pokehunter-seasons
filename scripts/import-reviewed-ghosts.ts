import assert from "node:assert/strict";
import { randomUUID } from "node:crypto";
import { mkdir, readFile, rename, rm, writeFile } from "node:fs/promises";
import { getPokeApiResource, isJsonObject } from "./lib/pokeapi.js";
import type { JsonObject } from "./lib/pokeapi.js";
import { createFormEntries } from "./lib/create-form-entries.js";
import type { CollectionEntry } from "./types/catalogue.js";

const DIRECTORY = new URL("../data/catalogue/", import.meta.url);
const DETAILS_FILE = new URL("halloween-2026-form-details.json", DIRECTORY);
const GEN1_FILE = new URL("generation-1.json", DIRECTORY);
const OUTPUT_FILE = new URL("halloween-2026-ghosts.json", DIRECTORY);

const resources = new Map<string, JsonObject>();

/** Reuses species and version-group objects within this import. */
async function loadResource(
  endpoint: string,
  name: string,
): Promise<JsonObject> {
  const key = `${endpoint}/${name}`;
  const existing = resources.get(key);

  if (existing !== undefined) {
    return existing;
  }

  const resource = await getPokeApiResource(endpoint, name);
  resources.set(key, resource);
  return resource;
}

function referenceName(value: unknown, context: string): string {
  if (!isJsonObject(value) || typeof value.name !== "string") {
    throw new Error(`Invalid resource reference: ${context}.`);
  }

  return value.name;
}

/**
 * Confirms exactly four identities for species whose gender forms
 * are represented differently by the API.
 */
function checkGenderEntries(
  entries: CollectionEntry[],
  speciesId: string,
): void {
  const ids = entries
    .filter((entry) => entry.speciesId === speciesId)
    .map((entry) => entry.id)
    .sort();

  assert.deepStrictEqual(ids, [
    `${speciesId}:female:normal`,
    `${speciesId}:female:shiny`,
    `${speciesId}:male:normal`,
    `${speciesId}:male:shiny`,
  ]);
}

/** Saves the completed catalogue through a temporary file. */
async function saveOutput(entries: CollectionEntry[]): Promise<void> {
  await mkdir(DIRECTORY, { recursive: true });

  const temporary = new URL(
    `halloween-2026-ghosts.${randomUUID()}.tmp`,
    DIRECTORY,
  );

  try {
    await writeFile(
      temporary,
      `${JSON.stringify(
        {
          schemaVersion: 1,
          sampleOnly: true,
          scope: "reviewed-ghost-type-forms",
          eventId: "halloween-2026",
          entries,
        },
        null,
        2,
      )}\n`,
      { encoding: "utf8", flag: "wx" },
    );

    await rename(temporary, OUTPUT_FILE);
  } finally {
    try {
      await rm(temporary, { force: true });
    } catch (error: unknown) {
      console.warn("Could not remove temporary Ghost catalogue:", error);
    }
  }
}

async function main(): Promise<void> {
  const details: unknown = JSON.parse(await readFile(DETAILS_FILE, "utf8"));

  if (
    !isJsonObject(details) ||
    details.schemaVersion !== 1 ||
    details.reviewOnly !== true ||
    details.eventId !== "halloween-2026" ||
    !Array.isArray(details.pokemon)
  ) {
    throw new Error("Expected the inspected Halloween form-details report.");
  }

  // This importer targets the exact roster we have reviewed.
  // A changed roster requires review rather than silent acceptance.
  assert.equal(details.pokemon.length, 96);

  const seenPokemon = new Set<string>();
  const seenForms = new Set<string>();
  const entriesById = new Map<string, CollectionEntry>();

  for (const item of details.pokemon as unknown[]) {
    if (
      !isJsonObject(item) ||
      typeof item.pokemonId !== "string" ||
      typeof item.speciesId !== "string" ||
      !Array.isArray(item.forms)
    ) {
      throw new Error("Invalid inspected Pokémon record.");
    }

    assert.ok(!seenPokemon.has(item.pokemonId), "Duplicate Pokémon record.");
    seenPokemon.add(item.pokemonId);

    const pokemon = await loadResource("pokemon", item.pokemonId);
    const species = await loadResource("pokemon-species", item.speciesId);

    if (!Array.isArray(pokemon.forms)) {
      throw new Error(`${item.pokemonId} has no valid form references.`);
    }

    const actualFormNames = pokemon.forms.map((value: unknown) =>
      referenceName(value, `${item.pokemonId}.forms`),
    );

    const reviewedFormNames: string[] = [];

    for (const reviewed of item.forms as unknown[]) {
      if (
        !isJsonObject(reviewed) ||
        typeof reviewed.resourceName !== "string" ||
        typeof reviewed.formName !== "string" ||
        typeof reviewed.versionGroup !== "string" ||
        typeof reviewed.apiIntroductionGeneration !== "string"
      ) {
        throw new Error(`Invalid inspected form for ${item.pokemonId}.`);
      }

      reviewedFormNames.push(reviewed.resourceName);

      assert.ok(
        !seenForms.has(reviewed.resourceName),
        `Duplicate form resource: ${reviewed.resourceName}.`,
      );

      seenForms.add(reviewed.resourceName);

      const form = await loadResource("pokemon-form", reviewed.resourceName);

      assert.equal(form.form_name, reviewed.formName);
      assert.equal(form.is_default, reviewed.isDefault);
      assert.equal(form.is_battle_only, reviewed.isBattleOnly);
      assert.equal(form.is_mega, reviewed.isMega);

      const versionGroupName = referenceName(
        form.version_group,
        `${reviewed.resourceName}.version_group`,
      );

      assert.equal(versionGroupName, reviewed.versionGroup);

      const versionGroup = await loadResource(
        "version-group",
        versionGroupName,
      );

      const generationName = referenceName(
        versionGroup.generation,
        `${versionGroupName}.generation`,
      );

      assert.equal(
        generationName,
        reviewed.apiIntroductionGeneration,
        `Introduction generation changed for ${reviewed.resourceName}.`,
      );

      const converted = createFormEntries(
        pokemon,
        species,
        form,
        generationName,
      );

      for (const entry of converted) {
        assert.ok(
          entry.types.includes("ghost"),
          `${entry.id} is not Ghost-type. Review its event selection.`,
        );

        assert.ok(
          !entriesById.has(entry.id),
          `Duplicate collection identity: ${entry.id}.`,
        );

        entriesById.set(entry.id, entry);
      }
    }

    assert.deepStrictEqual(
      reviewedFormNames.sort(),
      actualFormNames.sort(),
      `Form list changed for ${item.pokemonId}.`,
    );

    console.log(`[${seenPokemon.size}/96] Converted ${item.pokemonId}`);
  }

  const entries = [...entriesById.values()].sort(
    (left, right) =>
      left.nationalDexNumber - right.nationalDexNumber ||
      (left.id < right.id ? -1 : left.id > right.id ? 1 : 0),
  );

  assert.equal(seenForms.size, 102);
  assert.equal(entries.length, 204);
  assert.equal(new Set(entries.map((entry) => entry.speciesId)).size, 74);

  checkGenderEntries(entries, "frillish");
  checkGenderEntries(entries, "jellicent");
  checkGenderEntries(entries, "basculegion");

  for (const entry of entries) {
    const counterpartId =
      `${entry.formId}:${entry.gender}:` + (entry.isShiny ? "normal" : "shiny");

    assert.ok(
      entriesById.has(counterpartId),
      `Missing normal/shiny counterpart for ${entry.id}.`,
    );
  }

  // Shared identities must remain consistent across catalogue sources.
  // Currently this covers regular Gastly, Haunter, and Gengar.
  const gen1: unknown = JSON.parse(await readFile(GEN1_FILE, "utf8"));

  if (
    !isJsonObject(gen1) ||
    gen1.schemaVersion !== 1 ||
    !Array.isArray(gen1.entries)
  ) {
    throw new Error("Expected the generated Generation 1 catalogue.");
  }

  let sharedEntries = 0;

  for (const existing of gen1.entries as unknown[]) {
    if (!isJsonObject(existing) || typeof existing.id !== "string") {
      throw new Error("Invalid entry in the Generation 1 catalogue.");
    }

    const ghostEntry = entriesById.get(existing.id);

    if (ghostEntry !== undefined) {
      assert.deepStrictEqual(
        ghostEntry,
        existing,
        `Shared Gen 1 entry changed: ${existing.id}.`,
      );

      sharedEntries++;
    }
  }

  assert.equal(sharedEntries, 6);

  await saveOutput(entries);

  const missingSprites = entries.filter((entry) => entry.sprite.url === null);

  for (const entry of missingSprites) {
    console.warn(`[missing sprite] ${entry.id}`);
  }

  console.table({
    pokemonResources: seenPokemon.size,
    sourceForms: seenForms.size,
    species: new Set(entries.map((entry) => entry.speciesId)).size,
    collectionEntries: entries.length,
    normalEntries: entries.filter((entry) => !entry.isShiny).length,
    shinyEntries: entries.filter((entry) => entry.isShiny).length,
    sharedGen1EntriesVerified: sharedEntries,
    missingSpriteReferences: missingSprites.length,
  });

  console.log("\nAll reviewed Ghost catalogue checks passed.");
  console.log("Saved data/catalogue/halloween-2026-ghosts.json");
  console.log(
    "Legendary/Mythical entries remain in the catalogue. " +
      "Runtime season restrictions must still be applied.",
  );
}

main().catch((error: unknown) => {
  console.error("\nReviewed Ghost import failed.");

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
