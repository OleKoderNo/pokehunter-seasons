import { mkdir, readFile, writeFile } from "node:fs/promises";

const API_BASE_URL = "https://pokeapi.co/api/v2/";

// Source: scripts/lib/pokeapi.ts
// Compiled: dist/lib/pokeapi.js
// Both locations are two directories below the repository root.
const CACHE_DIRECTORY = new URL("../../cache/", import.meta.url);

const REQUEST_TIMEOUT_MS = 15_000;

export type JsonObject = Record<string, unknown>;

type ResourceValidator = (data: unknown) => JsonObject;

/** Narrows unknown JSON to a non-null object rather than an array. */
export function isJsonObject(value: unknown): value is JsonObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Shared cache and download handling.
 *
 * Callers validate path components before using this private helper.
 * Each caller supplies validation appropriate to its endpoint.
 */
async function loadResource(
  resourceType: string,
  identifier: string,
  validate: ResourceValidator,
): Promise<JsonObject> {
  const resourcePath = `${resourceType}/${identifier}`;

  const cacheFile = new URL(
    `${resourceType}--${identifier}.json`,
    CACHE_DIRECTORY,
  );

  let cachedText: string | undefined;

  try {
    cachedText = await readFile(cacheFile, "utf8");
  } catch (error: unknown) {
    const isMissingFile = isJsonObject(error) && error.code === "ENOENT";

    if (!isMissingFile) {
      throw new Error(`Cannot read cache for ${resourcePath}.`, {
        cause: error,
      });
    }
  }

  if (cachedText !== undefined) {
    try {
      const data: unknown = JSON.parse(cachedText);
      const validated = validate(data);

      console.log(`[cache] ${resourcePath}`);
      return validated;
    } catch (error: unknown) {
      throw new Error(
        `Invalid cache file for ${resourcePath}. ` +
          "Delete that resource's cache and run the importer again.",
        { cause: error },
      );
    }
  }

  const url = new URL(`${resourcePath}/`, API_BASE_URL);

  console.log(`[download] ${resourcePath}`);

  const response = await fetch(url, {
    headers: {
      Accept: "application/json",
    },
    signal: AbortSignal.timeout(REQUEST_TIMEOUT_MS),
  });

  if (!response.ok) {
    throw new Error(
      `PokéAPI request failed for ${resourcePath}: ` +
        `${response.status} ${response.statusText}`,
    );
  }

  const data: unknown = await response.json();
  const validated = validate(data);

  await mkdir(CACHE_DIRECTORY, { recursive: true });

  await writeFile(cacheFile, `${JSON.stringify(validated, null, 2)}\n`, "utf8");

  return validated;
}

/**
 * Loads a named resource such as a Pokémon, species, type, or generation.
 *
 * This checks resource identity. Converters validate the fields they use.
 */
export async function getPokeApiResource(
  resourceType: string,
  resourceName: string,
): Promise<JsonObject> {
  const validSlug = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

  if (!validSlug.test(resourceType) || !validSlug.test(resourceName)) {
    throw new Error("Resource type and name must be lowercase slugs.");
  }

  return loadResource(resourceType, resourceName, (data) => {
    if (
      !isJsonObject(data) ||
      typeof data.id !== "number" ||
      !Number.isSafeInteger(data.id) ||
      data.id <= 0 ||
      data.name !== resourceName
    ) {
      throw new Error(`Invalid resource data for "${resourceName}".`);
    }

    return data;
  });
}

/**
 * Loads an evolution chain by numeric ID.
 *
 * Unlike named resources, an evolution chain has no top-level name.
 * Its tree is validated separately when collecting family members.
 */
export async function getPokeApiEvolutionChain(
  evolutionChainId: number,
): Promise<JsonObject> {
  if (!Number.isSafeInteger(evolutionChainId) || evolutionChainId <= 0) {
    throw new Error("Evolution-chain ID must be a positive safe integer.");
  }

  return loadResource("evolution-chain", String(evolutionChainId), (data) => {
    if (
      !isJsonObject(data) ||
      data.id !== evolutionChainId ||
      !isJsonObject(data.chain)
    ) {
      throw new Error(
        `Invalid evolution-chain data for ID ${evolutionChainId}.`,
      );
    }

    return data;
  });
}
