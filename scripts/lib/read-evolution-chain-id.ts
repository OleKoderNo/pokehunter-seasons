import { isJsonObject } from "./pokeapi.js";
import type { JsonObject } from "./pokeapi.js";

/**
 * Reads the evolution-family identifier from a species resource.
 *
 * Example:
 * https://pokeapi.co/api/v2/evolution-chain/10/ -> 10
 *
 * No network request is made. This reads a reference already present
 * in the downloaded species data.
 */
export function readEvolutionChainId(species: JsonObject): number {
  if (
    !isJsonObject(species.evolution_chain) ||
    typeof species.evolution_chain.url !== "string"
  ) {
    throw new Error("Species has no valid evolution-chain reference.");
  }

  let url: URL;

  try {
    url = new URL(species.evolution_chain.url);
  } catch {
    throw new Error("Species evolution-chain reference is not a valid URL.");
  }

  // Verify the reference identifies the expected API endpoint.
  // Do not accept arbitrary URLs merely because they end with a number.
  if (
    url.origin !== "https://pokeapi.co" ||
    url.username !== "" ||
    url.password !== "" ||
    url.search !== "" ||
    url.hash !== ""
  ) {
    throw new Error("Species has an unsupported evolution-chain URL.");
  }

  const match = /^\/api\/v2\/evolution-chain\/([1-9]\d*)\/?$/.exec(
    url.pathname,
  );

  const idText = match?.[1];

  if (idText === undefined) {
    throw new Error("Species evolution-chain URL has an invalid path.");
  }

  const evolutionChainId = Number(idText);

  if (!Number.isSafeInteger(evolutionChainId)) {
    throw new Error(
      "Species evolution-chain ID is outside the supported range.",
    );
  }

  return evolutionChainId;
}
