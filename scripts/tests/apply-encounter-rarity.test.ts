import assert from "node:assert/strict";
import { test } from "node:test";
import { applyEncounterRarity } from "../lib/apply-encounter-rarity.js";
import type { EncounterGroup } from "../types/catalogue.js";

function createGroups(): EncounterGroup[] {
  return [
    {
      id: "pikachu",
      speciesId: "pikachu",
      nationalDexNumber: 25,
      entryIds: [
        "pikachu:male:normal",
        "pikachu:male:shiny",
        "pikachu-libre:female:normal",
        "pikachu-libre:female:shiny",
      ],
    },
    {
      id: "marowak-alola",
      speciesId: "marowak",
      nationalDexNumber: 105,
      entryIds: [
        "marowak-alola:unspecified:normal",
        "marowak-alola:unspecified:shiny",
      ],
    },
  ];
}

test("groups without overrides receive the configured default", () => {
  const result = applyEncounterRarity(createGroups(), {
    schemaVersion: 1,
    defaultClass: "common",
    overrides: {},
  });

  assert.deepEqual(
    result.map((group) => group.rarityClass),
    ["common", "common"],
  );
});

test("an override changes only its exact encounter group", () => {
  const result = applyEncounterRarity(createGroups(), {
    schemaVersion: 1,
    defaultClass: "common",
    overrides: {
      pikachu: "uncommon",
    },
  });

  assert.deepEqual(
    result.map((group) => group.rarityClass),
    ["uncommon", "common"],
  );

  // The group retains every variant; rarity does not filter eligibility.
  assert.deepEqual(result[0]?.entryIds, createGroups()[0]?.entryIds);
});

test("all four rarity classes are supported", () => {
  for (const rarityClass of ["common", "uncommon", "rare", "ultraRare"]) {
    const result = applyEncounterRarity(createGroups(), {
      schemaVersion: 1,
      defaultClass: rarityClass,
      overrides: {
        pikachu: rarityClass,
      },
    });

    assert.ok(result.every((group) => group.rarityClass === rarityClass));
  }
});

test("unknown encounter-group overrides are rejected", () => {
  assert.throws(
    () =>
      applyEncounterRarity(createGroups(), {
        schemaVersion: 1,
        defaultClass: "common",
        overrides: {
          pikachoo: "rare",
        },
      }),
    /unknown encounter group "pikachoo"/,
  );
});

test("unsupported override classes are rejected", () => {
  assert.throws(
    () =>
      applyEncounterRarity(createGroups(), {
        schemaVersion: 1,
        defaultClass: "common",
        overrides: {
          pikachu: "ultrarare",
        },
      }),
    /Invalid rarity class/,
  );
});

test("invalid configuration structures are rejected", () => {
  const invalidConfigurations: unknown[] = [
    null,
    [],
    {},
    {
      schemaVersion: 2,
      defaultClass: "common",
      overrides: {},
    },
    {
      schemaVersion: 1,
      defaultClass: "legendary",
      overrides: {},
    },
    {
      schemaVersion: 1,
      defaultClass: "common",
      overrides: [],
    },
    {
      schemaVersion: 1,
      defaultClass: "common",
      overrides: null,
    },
    {
      schemaVersion: 1,
      defaultClass: "common",
    },
    {
      schemaVersion: 1,
      defaultClass: "common",
      overrides: {},
      unexpected: true,
    },
  ];

  for (const configuration of invalidConfigurations) {
    assert.throws(() => applyEncounterRarity(createGroups(), configuration));
  }
});

test("empty or duplicate group lists are rejected", () => {
  const configuration = {
    schemaVersion: 1,
    defaultClass: "common",
    overrides: {},
  };

  assert.throws(
    () => applyEncounterRarity([], configuration),
    /empty encounter-group list/,
  );

  const groups = createGroups();
  const firstGroup = groups[0];

  assert.ok(firstGroup);

  assert.throws(
    () => applyEncounterRarity([...groups, firstGroup], configuration),
    /Duplicate encounter group ID/,
  );
});

test("rarity assignment does not mutate groups or configuration", () => {
  const groups = createGroups();
  const configuration = {
    schemaVersion: 1,
    defaultClass: "common",
    overrides: {
      pikachu: "rare",
    },
  };

  const originalGroups = structuredClone(groups);
  const originalConfiguration = structuredClone(configuration);

  const result = applyEncounterRarity(groups, configuration);
  const firstGroup = result[0];

  assert.ok(firstGroup);

  firstGroup.entryIds.push("test-only");

  assert.deepEqual(groups, originalGroups);
  assert.deepEqual(configuration, originalConfiguration);
});
