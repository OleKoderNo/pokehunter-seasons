import { isJsonObject } from "./pokeapi.js";

/**
 * Collects every species in an evolution tree.
 *
 * Includes the first stage, later evolutions, and alternate branches.
 * Does not resolve regional forms or decide encounter eligibility.
 */
export function collectEvolutionSpecies(root: unknown): string[] {
  const pending: unknown[] = [root];
  const speciesNames = new Set<string>();

  // Also rejects circular/reused object references in programmatic input.
  // Downloaded JSON cannot directly contain circular references.
  const visited = new Set<object>();

  while (pending.length > 0) {
    const node = pending.pop();

    if (
      !isJsonObject(node) ||
      !isJsonObject(node.species) ||
      typeof node.species.name !== "string" ||
      !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(node.species.name) ||
      !Array.isArray(node.evolves_to)
    ) {
      throw new Error("Invalid evolution-chain node.");
    }

    if (visited.has(node)) {
      throw new Error("Evolution chain contains a repeated node reference.");
    }

    visited.add(node);
    speciesNames.add(node.species.name);

    for (const child of node.evolves_to) {
      pending.push(child);
    }
  }

  return [...speciesNames].sort();
}
