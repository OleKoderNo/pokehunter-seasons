import type { CollectionEntry, EncounterGroup } from "../types/catalogue.js";

/**
 * Builds encounter groups from validated collection entries.
 *
 * The caller validates individual entry fields and shiny counterparts.
 * This function checks relationships between entries and their groups.
 *
 * It does not modify the supplied entries, assign rarity, or decide
 * whether an entry is currently obtainable.
 */
export function createEncounterGroups(
  entries: readonly CollectionEntry[],
): EncounterGroup[] {
  if (entries.length === 0) {
    throw new Error("Cannot create encounter groups from an empty catalogue.");
  }

  const seenEntryIds = new Set<string>();
  const groups = new Map<string, EncounterGroup>();

  for (const entry of entries) {
    if (seenEntryIds.has(entry.id)) {
      throw new Error(`Duplicate collection entry ID: ${entry.id}.`);
    }

    seenEntryIds.add(entry.id);

    const existing = groups.get(entry.encounterGroupId);

    if (existing === undefined) {
      groups.set(entry.encounterGroupId, {
        id: entry.encounterGroupId,
        speciesId: entry.speciesId,
        nationalDexNumber: entry.nationalDexNumber,
        entryIds: [entry.id],
      });

      continue;
    }

    // A group represents variants of one species, never unrelated Pokémon.
    if (
      existing.speciesId !== entry.speciesId ||
      existing.nationalDexNumber !== entry.nationalDexNumber
    ) {
      throw new Error(
        `Encounter group "${existing.id}" contains conflicting species ` +
          `or National Pokédex numbers at entry "${entry.id}".`,
      );
    }

    existing.entryIds.push(entry.id);
  }

  const result = [...groups.values()];

  // Sort newly created arrays only; the input catalogue remains unchanged.
  for (const group of result) {
    group.entryIds.sort(compareIds);
  }

  result.sort(
    (left, right) =>
      left.nationalDexNumber - right.nationalDexNumber ||
      compareIds(left.id, right.id),
  );

  return result;
}

/**
 * Uses consistent lexical ordering without depending on the machine's locale.
 */
function compareIds(left: string, right: string): number {
  return left < right ? -1 : left > right ? 1 : 0;
}
