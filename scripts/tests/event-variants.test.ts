import assert from "node:assert/strict";
import { test } from "node:test";
import { createEventVariantEntries } from "../lib/create-event-variant-entries.js";
import {
  makeForm,
  makePokemon,
  makeSpecies,
} from "./fixtures/catalogue-fixtures.js";

/** Builds fresh synthetic source objects for one Pikachu costume. */
function makeCostume(label: string) {
  const name = `pikachu-${label}`;

  return {
    pokemon: makePokemon({
      name,
      is_default: false,
    }),
    species: makeSpecies(),
    form: makeForm({
      name,
      form_name: label,
      pokemon: { name },
    }),
  };
}

for (const label of ["rock-star", "belle", "pop-star", "phd", "libre"]) {
  test(`${label} produces only female normal and shiny costume entries`, () => {
    const { pokemon, species, form } = makeCostume(label);

    const entries = createEventVariantEntries(
      pokemon,
      species,
      form,
      "generation-vi",
    );

    assert.deepStrictEqual(
      entries.map((entry) => entry.id),
      [`pikachu-${label}:female:normal`, `pikachu-${label}:female:shiny`],
    );

    for (const entry of entries) {
      assert.equal(entry.encounterGroupId, "pikachu");
      assert.equal(entry.speciesId, "pikachu");
      assert.equal(entry.formId, `pikachu-${label}`);
      assert.equal(entry.gender, "female");
      assert.equal(entry.hasGenderDifference, false);
      assert.equal(entry.isCostume, true);
      assert.equal(entry.eventOnly, true);
      assert.equal(entry.introducedGeneration, 6);
    }

    // Female-only costume resources use default artwork fields.
    assert.deepStrictEqual(
      entries.map((entry) => entry.sprite.url),
      ["https://assets.invalid/normal.png", "https://assets.invalid/shiny.png"],
    );
  });
}

test("Hangry Morpeko produces two unspecified entries in its own group", () => {
  const pokemon = makePokemon({
    name: "morpeko-hangry",
    species: { name: "morpeko" },
  });

  const species = makeSpecies({
    name: "morpeko",
    has_gender_differences: false,
  });

  const form = makeForm({
    name: "morpeko-hangry",
    form_name: "hangry",
    pokemon: { name: "morpeko-hangry" },
    types: [
      { slot: 1, type: { name: "electric" } },
      { slot: 2, type: { name: "dark" } },
    ],
  });

  const entries = createEventVariantEntries(
    pokemon,
    species,
    form,
    "generation-viii",
  );

  assert.deepStrictEqual(
    entries.map((entry) => entry.id),
    ["morpeko-hangry:unspecified:normal", "morpeko-hangry:unspecified:shiny"],
  );

  for (const entry of entries) {
    assert.equal(entry.encounterGroupId, "morpeko-hangry");
    assert.equal(entry.isCostume, false);
    assert.equal(entry.eventOnly, false);
    assert.equal(entry.introducedGeneration, 8);
    assert.deepStrictEqual(entry.types, ["electric", "dark"]);
  }
});

test("missing costume artwork and cries do not remove entries", () => {
  const { pokemon, species, form } = makeCostume("belle");

  pokemon.sprites = null;
  pokemon.cries = null;
  form.sprites = null;

  const entries = createEventVariantEntries(
    pokemon,
    species,
    form,
    "generation-vi",
  );

  assert.equal(entries.length, 2);

  for (const entry of entries) {
    assert.equal(entry.sprite.url, null);
    assert.deepStrictEqual(entry.cries, { latest: null, legacy: null });
  }
});

test("missing shiny artwork never borrows the normal costume sprite", () => {
  const { pokemon, species, form } = makeCostume("libre");

  const normalOnly = {
    front_default: "https://assets.invalid/costume-normal.png",
    front_shiny: null,
  };

  pokemon.sprites = { ...normalOnly };
  form.sprites = { ...normalOnly };

  const entries = createEventVariantEntries(
    pokemon,
    species,
    form,
    "generation-vi",
  );

  const shiny = entries.find((entry) => entry.isShiny);

  assert.ok(shiny);
  assert.equal(shiny.sprite.url, null);
});

test("a missing form sprite can use the matching costume parent", () => {
  const { pokemon, species, form } = makeCostume("phd");
  form.sprites = null;

  const entries = createEventVariantEntries(
    pokemon,
    species,
    form,
    "generation-vi",
  );

  assert.deepStrictEqual(
    entries.map((entry) => entry.sprite.url),
    ["https://assets.invalid/normal.png", "https://assets.invalid/shiny.png"],
  );
});

test("an unreviewed costume is rejected", () => {
  const { pokemon, species, form } = makeCostume("unreviewed");

  assert.throws(
    () => createEventVariantEntries(pokemon, species, form, "generation-vi"),
    /Unsupported explicit event variant/,
  );
});

test("a costume cannot borrow a different costume's form", () => {
  const { pokemon, species } = makeCostume("belle");
  const { form } = makeCostume("libre");

  assert.throws(
    () => createEventVariantEntries(pokemon, species, form, "generation-vi"),
    /resources disagree/,
  );
});

test("the species generation cannot replace the costume generation", () => {
  const { pokemon, species, form } = makeCostume("rock-star");

  assert.throws(
    () => createEventVariantEntries(pokemon, species, form, "generation-i"),
    /introduction generation disagrees/,
  );
});

test("unexpected species gender metadata is rejected", () => {
  const { pokemon, species, form } = makeCostume("pop-star");
  species.gender_rate = 0;

  assert.throws(
    () => createEventVariantEntries(pokemon, species, form, "generation-vi"),
    /species gender metadata disagrees/,
  );
});
