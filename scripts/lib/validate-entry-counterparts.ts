import { isDeepStrictEqual } from "node:util";
import type { CollectionEntry } from "../types/catalogue.js";

/**
 * Verifies normal/shiny counterpart completeness and shared metadata.
 *
 * Call this after individual validation and duplicate resolution.
 * Artwork is allowed to differ between normal and shiny counterparts.
 */
export function validateEntryCounterparts(
  entries: readonly CollectionEntry[],
): void {
  const entriesById = new Map(entries.map((entry) => [entry.id, entry]));

  for (const entry of entries) {
    const counterpartId =
      `${entry.formId}:${entry.gender}:` +
      `${entry.isShiny ? "normal" : "shiny"}`;

    const counterpart = entriesById.get(counterpartId);

    if (counterpart === undefined) {
      throw new Error(
        `Entry "${entry.id}" is missing counterpart "${counterpartId}".`,
      );
    }

    // Only these fields may differ between normal and shiny entries.
    const expectedCounterpart = {
      ...entry,
      id: counterpart.id,
      isShiny: counterpart.isShiny,
      sprite: counterpart.sprite,
    };

    if (!isDeepStrictEqual(counterpart, expectedCounterpart)) {
      throw new Error(
        `Counterpart metadata disagrees between "${entry.id}" ` +
          `and "${counterpartId}".`,
      );
    }
  }
}
