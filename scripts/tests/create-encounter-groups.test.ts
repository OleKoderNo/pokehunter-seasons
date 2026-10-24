import assert from "node:assert/strict";
import { test } from "node:test";
import { createEncounterGroups } from "../lib/create-encounter-groups.js";
import type { CollectionEntry } from "../types/catalogue.js";

/**
 * Provides a complete entry for relationship tests.
 * API parsing and individual entry validation are tested separately.
 */
function createEntry(
  overrides: Partial<CollectionEntry> = {},
): CollectionEntry {
  return {
    id: "pikachu:male:normal",
    speciesId: "pikachu",
    nationalDexNumber: 25,
    evolutionChainId: 10,
    isLegendary: false,
    isMythical: false,
    encounterGroupId: "pikachu",
    formId: "pikachu",
    displayName: "Pikachu",
    formName: null,
    regionalForm: null,
    gender: "male",
    hasGenderDifference: true,
    isShiny: false,
    isCostume: false,
    eventOnly: false,
    introducedGeneration: 1,
    types: ["electric"],
    sprite: {
      url: null,
      fallbackUrl: null,
    },
    cries: {
      latest: null,
      legacy: null,
    },
    ...overrides,
  };
}

test("regular genders, shiny entries, and costumes share their mapped group", () => {
  const entries = [
    createEntry(),
    createEntry({
      id: "pikachu:female:normal",
      gender: "female",
    }),
    createEntry({
      id: "pikachu:male:shiny",
      isShiny: true,
    }),
    createEntry({
      id: "pikachu-libre:female:normal",
      formId: "pikachu-libre",
      formName: "Libre",
      gender: "female",
      hasGenderDifference: false,
      isCostume: true,
      eventOnly: true,
      introducedGeneration: 6,
    }),
  ];

  const groups = createEncounterGroups(entries);

  assert.deepEqual(groups, [
    {
      id: "pikachu",
      speciesId: "pikachu",
      nationalDexNumber: 25,
      entryIds: entries.map((entry) => entry.id).sort(),
    },
  ]);
});

test("genders without visible differences share one encounter group", () => {
  const male = createEntry({
    id: "bulbasaur:male:normal",
    speciesId: "bulbasaur",
    nationalDexNumber: 1,
    evolutionChainId: 1,
    encounterGroupId: "bulbasaur",
    formId: "bulbasaur",
    displayName: "Bulbasaur",
    gender: "male",
    hasGenderDifference: false,
    types: ["grass", "poison"],
  });

  const entries = [
    male,
    createEntry({
      ...male,
      id: "bulbasaur:female:normal",
      gender: "female",
    }),
    createEntry({
      ...male,
      id: "bulbasaur:male:shiny",
      isShiny: true,
    }),
    createEntry({
      ...male,
      id: "bulbasaur:female:shiny",
      gender: "female",
      isShiny: true,
    }),
  ];

  const groups = createEncounterGroups(entries);

  assert.deepEqual(groups, [
    {
      id: "bulbasaur",
      speciesId: "bulbasaur",
      nationalDexNumber: 1,
      entryIds: [
        "bulbasaur:female:normal",
        "bulbasaur:female:shiny",
        "bulbasaur:male:normal",
        "bulbasaur:male:shiny",
      ],
    },
  ]);
});

test("regional forms keep separate groups even when their species matches", () => {
  const regular = createEntry({
    id: "marowak:male:normal",
    speciesId: "marowak",
    nationalDexNumber: 105,
    evolutionChainId: 46,
    encounterGroupId: "marowak",
    formId: "marowak",
    displayName: "Marowak",
    gender: "male",
    hasGenderDifference: false,
    types: ["ground"],
  });

  const regional = createEntry({
    ...regular,
    id: "marowak-alola:male:normal",
    encounterGroupId: "marowak-alola",
    formId: "marowak-alola",
    formName: "Alolan",
    regionalForm: "alola",
    introducedGeneration: 7,
    types: ["fire", "ghost"],
  });

  const groups = createEncounterGroups([regional, regular]);

  assert.deepEqual(
    groups.map((group) => group.id),
    ["marowak", "marowak-alola"],
  );
});

test("group creation rejects an empty catalogue", () => {
  assert.throws(() => createEncounterGroups([]), /empty catalogue/);
});

test("duplicate collection identities are rejected", () => {
  assert.throws(
    () => createEncounterGroups([createEntry(), createEntry()]),
    /Duplicate collection entry ID/,
  );
});

test("one group cannot contain different species", () => {
  const otherSpecies = createEntry({
    id: "raichu:male:normal",
    speciesId: "raichu",
    nationalDexNumber: 26,
    formId: "raichu",
    displayName: "Raichu",
    // Deliberately incorrect: this entry points to Pikachu's group.
    encounterGroupId: "pikachu",
  });

  assert.throws(
    () => createEncounterGroups([createEntry(), otherSpecies]),
    /conflicting species or National Pokédex numbers/,
  );
});

test("one group cannot contain conflicting National Pokédex numbers", () => {
  const incorrect = createEntry({
    id: "pikachu:female:normal",
    gender: "female",
    nationalDexNumber: 26,
  });

  assert.throws(
    () => createEncounterGroups([createEntry(), incorrect]),
    /conflicting species or National Pokédex numbers/,
  );
});

test("group ordering is independent of input order", () => {
  const entries = [
    createEntry(),
    createEntry({
      id: "bulbasaur:male:normal",
      speciesId: "bulbasaur",
      nationalDexNumber: 1,
      evolutionChainId: 1,
      encounterGroupId: "bulbasaur",
      formId: "bulbasaur",
      displayName: "Bulbasaur",
      gender: "male",
      hasGenderDifference: false,
      types: ["grass", "poison"],
    }),
    createEntry({
      id: "pikachu:female:normal",
      gender: "female",
    }),
  ];

  const groups = createEncounterGroups(entries);

  assert.deepEqual(groups, createEncounterGroups([...entries].reverse()));

  assert.deepEqual(
    groups.map((group) => group.id),
    ["bulbasaur", "pikachu"],
  );
});

test("creating and editing group records does not mutate input entries", () => {
  const entries = [
    createEntry(),
    createEntry({
      id: "pikachu:female:normal",
      gender: "female",
    }),
  ];

  const original = structuredClone(entries);
  const groups = createEncounterGroups(entries);
  const firstGroup = groups[0];

  assert.ok(firstGroup);
  firstGroup.entryIds.push("test-only");

  assert.deepEqual(entries, original);
});
