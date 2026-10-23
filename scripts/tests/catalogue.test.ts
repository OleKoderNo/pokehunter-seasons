import assert from "node:assert/strict";
import { test } from "node:test";
import { createRegularEntries } from "../lib/create-regular-entries.js";
import { createFormEntries } from "../lib/create-form-entries.js";
import {
  makeAlolanMarowak,
  makeForm,
  makeGenderForm,
  makePokemon,
  makeSpecies,
} from "./fixtures/catalogue-fixtures.js";

test("regular Pikachu produces four distinct collection identities", () => {
  const entries = createRegularEntries(makePokemon(), makeSpecies());

  assert.deepStrictEqual(
    entries.map((entry) => entry.id),
    [
      "pikachu:male:normal",
      "pikachu:female:normal",
      "pikachu:male:shiny",
      "pikachu:female:shiny",
    ],
  );

  assert.equal(new Set(entries.map((entry) => entry.id)).size, 4);
  assert.ok(entries.every((entry) => entry.encounterGroupId === "pikachu"));

  assert.deepStrictEqual(
    entries.map((entry) => entry.sprite.url),
    [
      "https://assets.invalid/normal.png",
      "https://assets.invalid/female.png",
      "https://assets.invalid/shiny.png",
      "https://assets.invalid/shiny-female.png",
    ],
  );
});

for (const [rate, expectedGender] of [
  [-1, "genderless"],
  [0, "male"],
  [8, "female"],
  [4, "unspecified"],
] as const) {
  test(`gender rate ${rate} without visible differences produces ${expectedGender} entries`, () => {
    const species = makeSpecies({
      gender_rate: rate,
      has_gender_differences: false,
    });

    const entries = createRegularEntries(makePokemon(), species);

    assert.deepStrictEqual(
      entries.map((entry) => entry.id),
      [`pikachu:${expectedGender}:normal`, `pikachu:${expectedGender}:shiny`],
    );

    // Single-gender species use default fields, including female-only ones.
    assert.deepStrictEqual(
      entries.map((entry) => entry.sprite.url),
      ["https://assets.invalid/normal.png", "https://assets.invalid/shiny.png"],
    );
  });
}

test("missing media preserves every normal and shiny identity", () => {
  const pokemon = makePokemon({ sprites: null, cries: null });
  const entries = createRegularEntries(pokemon, makeSpecies());

  assert.equal(entries.length, 4);
  assert.equal(entries.filter((entry) => entry.isShiny).length, 2);

  for (const entry of entries) {
    assert.deepStrictEqual(entry.sprite, { url: null, fallbackUrl: null });
    assert.deepStrictEqual(entry.cries, { latest: null, legacy: null });
  }
});

test("malformed sprite protocols are rejected", () => {
  const pokemon = makePokemon({
    sprites: { front_default: "file:///private/image.png" },
  });

  assert.throws(
    () => createRegularEntries(pokemon, makeSpecies()),
    /Unsupported asset URL protocol/,
  );
});

test("contradictory gender metadata is rejected", () => {
  const species = makeSpecies({
    gender_rate: 8,
    has_gender_differences: true,
  });

  assert.throws(
    () => createRegularEntries(makePokemon(), species),
    /Visible gender differences conflict/,
  );
});

test("missing National Pokédex records are rejected", () => {
  const species = makeSpecies({ pokedex_numbers: [] });

  assert.throws(
    () => createRegularEntries(makePokemon(), species),
    /Expected exactly one valid National Pokédex record/,
  );
});

test("mismatched Pokémon and species resources are rejected", () => {
  const pokemon = makePokemon({ species: { name: "raichu" } });

  assert.throws(
    () => createRegularEntries(pokemon, makeSpecies()),
    /Expected matching regular default/,
  );
});

test("duplicate type names are rejected", () => {
  const pokemon = makePokemon({
    types: [
      { slot: 1, type: { name: "electric" } },
      { slot: 2, type: { name: "electric" } },
    ],
  });

  assert.throws(
    () => createRegularEntries(pokemon, makeSpecies()),
    /duplicate names or invalid slots/,
  );
});

test("Alolan Marowak has its own encounter group and form generation", () => {
  const { pokemon, species, form } = makeAlolanMarowak();

  // Deliberately conflicting parent types prove that form types win.
  pokemon.types = [{ slot: 1, type: { name: "ground" } }];

  const entries = createFormEntries(pokemon, species, form, "generation-vii");

  assert.deepStrictEqual(
    entries.map((entry) => entry.id),
    ["marowak-alola:unspecified:normal", "marowak-alola:unspecified:shiny"],
  );

  for (const entry of entries) {
    assert.equal(entry.speciesId, "marowak");
    assert.equal(entry.encounterGroupId, "marowak-alola");
    assert.equal(entry.formId, "marowak-alola");
    assert.equal(entry.introducedGeneration, 7);
    assert.equal(entry.regionalForm, "alola");
    assert.deepStrictEqual(entry.types, ["fire", "ghost"]);
  }
});

for (const speciesId of ["frillish", "jellicent", "basculegion"]) {
  test(`${speciesId} gender counterparts share one canonical form and group`, () => {
    const entries = (["male", "female"] as const).flatMap((gender) => {
      const { pokemon, species, form } = makeGenderForm(speciesId, gender);

      return createFormEntries(pokemon, species, form, "generation-viii");
    });

    assert.deepStrictEqual(entries.map((entry) => entry.id).sort(), [
      `${speciesId}:female:normal`,
      `${speciesId}:female:shiny`,
      `${speciesId}:male:normal`,
      `${speciesId}:male:shiny`,
    ]);

    assert.ok(
      entries.every(
        (entry) =>
          entry.formId === speciesId && entry.encounterGroupId === speciesId,
      ),
    );
  });
}

test("female-specific parent artwork uses its default sprite fields", () => {
  const { pokemon, species, form } = makeGenderForm("basculegion", "female");

  form.sprites = null;

  const entries = createFormEntries(pokemon, species, form, "generation-viii");

  assert.deepStrictEqual(
    entries.map((entry) => entry.sprite.url),
    [
      "https://assets.invalid/basculegion-female.png",
      "https://assets.invalid/basculegion-female-shiny.png",
    ],
  );
});

test("a shared parent uses female fields for a female gender form", () => {
  const { pokemon, species, form } = makeGenderForm("frillish", "female");

  // Exercise the fallback rule with a parent that represents another
  // gender and explicitly supplies matching female artwork.
  pokemon.name = "frillish-male";
  pokemon.sprites = {
    front_default: "https://assets.invalid/male.png",
    front_shiny: "https://assets.invalid/male-shiny.png",
    front_female: "https://assets.invalid/female.png",
    front_shiny_female: "https://assets.invalid/female-shiny.png",
  };
  form.pokemon = { name: "frillish-male" };
  form.sprites = null;

  const entries = createFormEntries(pokemon, species, form, "generation-v");

  assert.deepStrictEqual(
    entries.map((entry) => entry.sprite.url),
    [
      "https://assets.invalid/female.png",
      "https://assets.invalid/female-shiny.png",
    ],
  );
});

test("alternate forms do not borrow default-form artwork", () => {
  const { pokemon, species, form } = makeAlolanMarowak();

  form.is_default = false;
  form.sprites = null;

  const entries = createFormEntries(pokemon, species, form, "generation-vii");

  assert.equal(entries.length, 2);
  assert.ok(entries.every((entry) => entry.sprite.url === null));
});

test("missing default-form artwork can use matching parent artwork", () => {
  const { pokemon, species, form } = makeAlolanMarowak();

  form.sprites = null;

  const entries = createFormEntries(pokemon, species, form, "generation-vii");

  assert.deepStrictEqual(
    entries.map((entry) => entry.sprite.url),
    ["https://assets.invalid/normal.png", "https://assets.invalid/shiny.png"],
  );
});

test("unsupported form labels are rejected", () => {
  const form = makeForm({ form_name: "unreviewed-costume" });

  assert.throws(
    () => createFormEntries(makePokemon(), makeSpecies(), form, "generation-i"),
    /Unreviewed form label/,
  );
});

test("unsupported form generations are rejected", () => {
  assert.throws(
    () =>
      createFormEntries(
        makePokemon(),
        makeSpecies(),
        makeForm(),
        "generation-unknown",
      ),
    /Unsupported form generation/,
  );
});

test("a form belonging to another Pokémon resource is rejected", () => {
  const form = makeForm({ pokemon: { name: "raichu" } });

  assert.throws(
    () => createFormEntries(makePokemon(), makeSpecies(), form, "generation-i"),
    /invalid or mismatched resources/,
  );
});
