import { isJsonObject } from "./pokeapi.js";
import type {
  EncounterGroup,
  RatedEncounterGroup,
  RarityClass,
} from "../types/catalogue.js";

/**
 * Narrows an unknown value to a supported rarity class.
 *
 * Names are case-sensitive and match game.json's rarityWeights keys.
 */
function isRarityClass(value: unknown): value is RarityClass {
  return (
    value === "common" ||
    value === "uncommon" ||
    value === "rare" ||
    value === "ultraRare"
  );
}

/**
 * Validates rarity configuration and assigns a class to every group.
 *
 * Overrides use exact encounter-group IDs. Unknown IDs are rejected
 * so a typo cannot silently leave a Pokémon at the default rarity.
 *
 * This function does not mutate the groups or configuration.
 * It does not calculate probabilities or grant encounter eligibility.
 */
export function applyEncounterRarity(
  groups: readonly EncounterGroup[],
  configuration: unknown,
): RatedEncounterGroup[] {
  if (!isJsonObject(configuration)) {
    throw new Error("Rarity configuration must be a JSON object.");
  }

  const allowedKeys = new Set(["schemaVersion", "defaultClass", "overrides"]);

  for (const key of Object.keys(configuration)) {
    if (!allowedKeys.has(key)) {
      throw new Error(`Unknown rarity configuration property: "${key}".`);
    }
  }

  if (configuration.schemaVersion !== 1) {
    throw new Error("Rarity configuration schemaVersion must be 1.");
  }

  const defaultClass = configuration.defaultClass;

  if (!isRarityClass(defaultClass)) {
    throw new Error(
      "Rarity configuration defaultClass must be common, uncommon, " +
        "rare, or ultraRare.",
    );
  }

  if (!isJsonObject(configuration.overrides)) {
    throw new Error("Rarity configuration overrides must be a JSON object.");
  }

  if (groups.length === 0) {
    throw new Error("Cannot assign rarity to an empty encounter-group list.");
  }

  const groupIds = new Set<string>();

  for (const group of groups) {
    if (groupIds.has(group.id)) {
      throw new Error(`Duplicate encounter group ID: "${group.id}".`);
    }

    groupIds.add(group.id);
  }

  const overrides = new Map<string, RarityClass>();

  for (const [groupId, rarityClass] of Object.entries(
    configuration.overrides,
  )) {
    if (!groupIds.has(groupId)) {
      throw new Error(
        `Rarity override references unknown encounter group "${groupId}".`,
      );
    }

    if (!isRarityClass(rarityClass)) {
      throw new Error(
        `Invalid rarity class for encounter group "${groupId}". ` +
          "Expected common, uncommon, rare, or ultraRare.",
      );
    }

    overrides.set(groupId, rarityClass);
  }

  return groups.map((group) => ({
    ...group,
    // Copy the array so the generated records do not share mutable
    // entry-ID arrays with the input groups.
    entryIds: [...group.entryIds],
    rarityClass: overrides.get(group.id) ?? defaultClass,
  }));
}
