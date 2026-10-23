import { mkdir, readFile, writeFile } from "node:fs/promises";

const API_BASE_URL = "https://pokeapi.co/api/v2/";

// Source: scripts/lib/pokeapi.ts
// Compiled: dist/lib/pokeapi.js
// Both locations are two directories below the repository root.
const CACHE_DIRECTORY = new URL("../../cache/", import.meta.url);

const REQUEST_TIMEOUT_MS = 15_000;

export type JsonObject = Record<string, unknown>;

/**
 * Checks whether a value is a non-null object rather than an array.
 *
 * This lets TypeScript safely narrow unknown JSON values.
 */
export function isJsonObject(value: unknown): value is JsonObject {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}

/**
 * Checks the identity of a named API resource.
 *
 * This is not complete validation of every endpoint field.
 * Converters validate the additional fields they consume.
 */
function validateResource(
  data: unknown,
  resourceName: string,
): asserts data is JsonObject {
  if (
    !isJsonObject(data) ||
    typeof data.id !== "number" ||
    !Number.isInteger(data.id) ||
    data.id <= 0 ||
    data.name !== resourceName
  ) {
    throw new Error(`Invalid resource data for "${resourceName}".`);
  }
}

/**
 * Loads a named PokéAPI resource from the local cache or the API.
 *
 * Only a missing cache file triggers a download. Invalid cached data
 * is reported so it can be inspected rather than silently replaced.
 *
 * This helper expects a named resource with an ID and name.
 * It does not handle list endpoints or unnamed resources.
 */
export async function getPokeApiResource(
  resourceType: string,
  resourceName: string,
): Promise<JsonObject> {
  // Restrict both arguments to lowercase endpoint names and slugs.
  // This also prevents paths from escaping the cache directory.
  const validSlug = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

  if (!validSlug.test(resourceType) || !validSlug.test(resourceName)) {
    throw new Error("Resource type and name must be lowercase slugs.");
  }

  const resourcePath = `${resourceType}/${resourceName}`;

  const cacheFile = new URL(
    `${resourceType}--${resourceName}.json`,
    CACHE_DIRECTORY,
  );

  let cachedText: string | undefined;

  try {
    cachedText = await readFile(cacheFile, "utf-8");
  } catch (error: unknown) {
    // Permission errors and other filesystem failures must not be
    // mistaken for a missing cache file.
    const isMissingFile = isJsonObject(error) && error.code === "ENOENT";

    if (!isMissingFile) {
      throw new Error(`Cannot read cache for ${resourcePath}.`, {
        cause: error,
      });
    }
  }

  if (cachedText !== undefined) {
    try {
      const cachedData: unknown = JSON.parse(cachedText);

      validateResource(cachedData, resourceName);

      console.log(`[cache] ${resourcePath}`);
      return cachedData;
    } catch (error: unknown) {
      throw new Error(
        `Invalid cache file for ${resourcePath}. ` +
          "Delete that resource's cache and run the importer again.",
        { cause: error },
      );
    }
  }

  console.log(`[download] ${resourcePath}`);

  const url = new URL(`${resourcePath}/`, API_BASE_URL);

  console.log(`[request] ${url.href}`);

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

  // Validate the resource identity before saving it to the cache.
  validateResource(data, resourceName);

  await mkdir(CACHE_DIRECTORY, { recursive: true });

  await writeFile(cacheFile, `${JSON.stringify(data, null, 2)}\n`, "utf-8");

  return data;
}
