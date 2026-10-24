import { isJsonObject } from "./pokeapi.js";
import type { JsonObject } from "./pokeapi.js";
import type { CollectionEntry } from "../types/catalogue.js";

const SUPPORTED_TYPES = new Set([
  "normal",
  "fire",
  "water",
  "electric",
  "grass",
  "ice",
  "fighting",
  "poison",
  "ground",
  "flying",
  "psychic",
  "bug",
  "rock",
  "ghost",
  "dragon",
  "dark",
  "steel",
  "fairy",
]);

const ENTRY_KEYS = [
  "id",
  "speciesId",
  "nationalDexNumber",
  "evolutionChainId",
  "isLegendary",
  "isMythical",
  "encounterGroupId",
  "formId",
  "displayName",
  "formName",
  "regionalForm",
  "gender",
  "hasGenderDifference",
  "isShiny",
  "isCostume",
  "eventOnly",
  "introducedGeneration",
  "types",
  "sprite",
  "cries",
];

/** Throws an error containing the location of invalid input. */
function requireValue(
  condition: unknown,
  path: string,
  message: string,
): asserts condition {
  if (!condition) {
    throw new Error(`${path}: ${message}`);
  }
}

/** Requires exactly the documented fields, including nullable fields. */
function requireKeys(
  value: JsonObject,
  keys: readonly string[],
  path: string,
): void {
  for (const key of keys) {
    requireValue(
      Object.hasOwn(value, key),
      path,
      `missing required property "${key}".`,
    );
  }

  for (const key of Object.keys(value)) {
    requireValue(keys.includes(key), path, `unexpected property "${key}".`);
  }
}

function isText(value: unknown): value is string {
  return (
    typeof value === "string" && value.length > 0 && value === value.trim()
  );
}

function isSlug(value: unknown): value is string {
  return typeof value === "string" && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(value);
}

function isPositiveInteger(value: unknown): value is number {
  return typeof value === "number" && Number.isSafeInteger(value) && value > 0;
}

/**
 * Checks media references without requesting the remote file.
 * Explicit null is allowed; absent or malformed values are not.
 */
function validateAsset(value: unknown, path: string): void {
  if (value === null) {
    return;
  }

  requireValue(isText(value), path, "expected a URL string or null.");

  let url: URL;

  try {
    url = new URL(value);
  } catch {
    throw new Error(`${path}: invalid URL.`);
  }

  requireValue(
    url.protocol === "https:" || url.protocol === "http:",
    path,
    "expected an HTTP or HTTPS URL.",
  );
}

/**
 * Validates one saved entry and narrows unknown JSON to CollectionEntry.
 *
 * Gender identifies the collectible. hasGenderDifference describes
 * appearance and does not determine whether genders are separate.
 *
 * This checks the contract, not Pokémon facts or runtime eligibility.
 */
export function validateCollectionEntry(
  value: unknown,
  path = "entry",
): asserts value is CollectionEntry {
  requireValue(isJsonObject(value), path, "expected an object.");
  requireKeys(value, ENTRY_KEYS, path);

  for (const field of ["speciesId", "formId", "encounterGroupId"]) {
    requireValue(isSlug(value[field]), `${path}.${field}`, "expected a slug.");
  }

  for (const field of ["nationalDexNumber", "evolutionChainId"]) {
    requireValue(
      isPositiveInteger(value[field]),
      `${path}.${field}`,
      "expected a positive safe integer.",
    );
  }

  requireValue(
    isPositiveInteger(value.introducedGeneration) &&
      value.introducedGeneration <= 9,
    `${path}.introducedGeneration`,
    "expected a supported generation from 1 through 9.",
  );

  requireValue(
    isText(value.displayName),
    `${path}.displayName`,
    "expected nonempty trimmed text.",
  );

  requireValue(
    value.formName === null || isText(value.formName),
    `${path}.formName`,
    "expected nonempty trimmed text or null.",
  );

  requireValue(
    value.regionalForm === null || isSlug(value.regionalForm),
    `${path}.regionalForm`,
    "expected a slug or null.",
  );

  for (const field of [
    "isLegendary",
    "isMythical",
    "hasGenderDifference",
    "isShiny",
    "isCostume",
    "eventOnly",
  ]) {
    requireValue(
      typeof value[field] === "boolean",
      `${path}.${field}`,
      "expected a boolean.",
    );
  }

  requireValue(
    value.gender === "male" ||
      value.gender === "female" ||
      value.gender === "genderless",
    `${path}.gender`,
    "unsupported collection gender.",
  );

  // False permits either or both gender identities.
  // True cannot describe a genderless entry.
  requireValue(
    !value.hasGenderDifference ||
      value.gender === "male" ||
      value.gender === "female",
    `${path}.gender`,
    "visible gender differences require an indexed male or female entry.",
  );

  const expectedId =
    `${value.formId}:${value.gender}:` +
    `${value.isShiny ? "shiny" : "normal"}`;

  requireValue(
    value.id === expectedId,
    `${path}.id`,
    `expected "${expectedId}".`,
  );

  const expectedGroup = value.isCostume ? value.speciesId : value.formId;

  requireValue(
    value.encounterGroupId === expectedGroup,
    `${path}.encounterGroupId`,
    `expected "${expectedGroup}".`,
  );

  requireValue(
    !value.isCostume || value.eventOnly === true,
    `${path}.eventOnly`,
    "costumes must be event-only.",
  );

  requireValue(
    Array.isArray(value.types) &&
      value.types.length >= 1 &&
      value.types.length <= 2,
    `${path}.types`,
    "expected one or two types.",
  );

  for (const type of value.types) {
    requireValue(
      typeof type === "string" && SUPPORTED_TYPES.has(type),
      `${path}.types`,
      "contains an unsupported type.",
    );
  }

  requireValue(
    new Set(value.types).size === value.types.length,
    `${path}.types`,
    "contains duplicate types.",
  );

  requireValue(
    isJsonObject(value.sprite),
    `${path}.sprite`,
    "expected an object.",
  );

  requireKeys(value.sprite, ["url", "fallbackUrl"], `${path}.sprite`);
  validateAsset(value.sprite.url, `${path}.sprite.url`);
  validateAsset(value.sprite.fallbackUrl, `${path}.sprite.fallbackUrl`);

  requireValue(
    isJsonObject(value.cries),
    `${path}.cries`,
    "expected an object.",
  );

  requireKeys(value.cries, ["latest", "legacy"], `${path}.cries`);
  validateAsset(value.cries.latest, `${path}.cries.latest`);
  validateAsset(value.cries.legacy, `${path}.cries.legacy`);
}
