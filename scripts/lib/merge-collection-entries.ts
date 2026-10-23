import { isDeepStrictEqual } from "node:util";
import type { CollectionEntry } from "../types/catalogue.js";

/** A named collection of already validated entries. */
export interface CollectionSource {
  name: string;
  entries: readonly CollectionEntry[];
}

export interface CollectionMergeResult {
  entries: CollectionEntry[];
  inputEntries: number;
  duplicatesRemoved: number;
}

/**
 * Combines validated collection entries from multiple sources.
 *
 * Identical entries may overlap across sources. Conflicting copies
 * are rejected rather than allowing the first or last source to win.
 *
 * This function does not parse or validate external JSON.
 * File readers must validate their input before calling it.
 *
 * Inputs are not modified. Returned entries are independent copies.
 */
export function mergeCollectionEntries(
  sources: readonly CollectionSource[],
): CollectionMergeResult {
  if (sources.length === 0) {
    throw new Error("At least one collection source is required.");
  }

  const sourceNames = new Set<string>();
  const entriesById = new Map<
    string,
    { entry: CollectionEntry; sourceName: string }
  >();

  let inputEntries = 0;
  let duplicatesRemoved = 0;

  for (const source of sources) {
    if (source.name.trim().length === 0 || source.name !== source.name.trim()) {
      throw new Error("Collection source names must be nonempty and trimmed.");
    }

    if (sourceNames.has(source.name)) {
      throw new Error(`Duplicate collection source name: "${source.name}".`);
    }

    sourceNames.add(source.name);

    if (source.entries.length === 0) {
      throw new Error(`Collection source "${source.name}" is empty.`);
    }

    const sourceEntryIds = new Set<string>();

    for (const entry of source.entries) {
      inputEntries += 1;

      // An overlap between different sources is expected.
      // Repeating an ID within one source is an upstream error.
      if (sourceEntryIds.has(entry.id)) {
        throw new Error(
          `Duplicate entry "${entry.id}" within source "${source.name}".`,
        );
      }

      sourceEntryIds.add(entry.id);

      const existing = entriesById.get(entry.id);

      if (existing !== undefined) {
        if (!isDeepStrictEqual(existing.entry, entry)) {
          throw new Error(
            `Conflicting entry "${entry.id}" between sources ` +
              `"${existing.sourceName}" and "${source.name}".`,
          );
        }

        duplicatesRemoved += 1;
        continue;
      }

      entriesById.set(entry.id, {
        entry: structuredClone(entry),
        sourceName: source.name,
      });
    }
  }

  const entries = [...entriesById.values()].map((record) => record.entry);

  // Use a stable ordering that does not depend on source-file order
  // or the machine's language settings.
  entries.sort((left, right) => {
    const dexDifference = left.nationalDexNumber - right.nationalDexNumber;

    if (dexDifference !== 0) {
      return dexDifference;
    }

    return left.id < right.id ? -1 : left.id > right.id ? 1 : 0;
  });

  return {
    entries,
    inputEntries,
    duplicatesRemoved,
  };
}
