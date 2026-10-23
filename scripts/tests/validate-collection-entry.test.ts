import assert from "node:assert/strict";
import { test } from "node:test";
import { createRegularEntries } from "../lib/create-regular-entries.js";
import { validateCollectionEntry } from "../lib/validate-collection-entry.js";
import { validateEntryCounterparts } from "../lib/validate-entry-counterparts.js";
import type { JsonObject } from "../lib/pokeapi.js";
import { makePokemon, makeSpecies } from "./fixtures/catalogue-fixtures.js";

function makeEntries() {
  return createRegularEntries(makePokemon(), makeSpecies());
}

/** A mutable object for intentionally creating malformed saved data. */
function makeEntryObject(): JsonObject {
  const entry = makeEntries()[0];
  assert.ok(entry);

  return { ...entry };
}

test("valid converter output passes saved-entry validation", () => {
  const entries = makeEntries();

  for (const entry of entries) {
    validateCollectionEntry(entry);
  }

  validateEntryCounterparts(entries);
});

test("missing optional media remains valid when represented by null", () => {
  const entry = makeEntryObject();

  entry.sprite = { url: null, fallbackUrl: null };
  entry.cries = { latest: null, legacy: null };

  assert.doesNotThrow(() => validateCollectionEntry(entry));
});

test("missing required properties identify their source location", () => {
  const entry = makeEntryObject();
  delete entry.speciesId;

  assert.throws(
    () => validateCollectionEntry(entry, "sample.json.entries[0]"),
    /sample\.json\.entries\[0\]: missing required property "speciesId"/,
  );
});

test("unexpected entry properties are rejected", () => {
  const entry = makeEntryObject();
  entry.isShinny = false;

  assert.throws(
    () => validateCollectionEntry(entry),
    /unexpected property "isShinny"/,
  );
});

test("IDs must agree with form, gender, and shiny state", () => {
  const entry = makeEntryObject();
  entry.id = "pikachu:male:shiny";

  assert.throws(() => validateCollectionEntry(entry), /entry\.id: expected/);
});

test("non-costume groups must match the canonical form", () => {
  const entry = makeEntryObject();
  entry.encounterGroupId = "wrong-group";

  assert.throws(
    () => validateCollectionEntry(entry),
    /entry\.encounterGroupId: expected/,
  );
});

test("costumes must be event-only", () => {
  const entry = makeEntryObject();
  entry.isCostume = true;
  entry.eventOnly = false;

  assert.throws(
    () => validateCollectionEntry(entry),
    /costumes must be event-only/,
  );
});

test("invalid numerical fields are rejected", () => {
  for (const field of ["nationalDexNumber", "evolutionChainId"]) {
    const entry = makeEntryObject();
    entry[field] = 0;

    assert.throws(
      () => validateCollectionEntry(entry),
      /expected a positive safe integer/,
    );
  }

  const entry = makeEntryObject();
  entry.introducedGeneration = 10;

  assert.throws(
    () => validateCollectionEntry(entry),
    /supported generation from 1 through 9/,
  );
});

test("duplicate types are rejected", () => {
  const entry = makeEntryObject();
  entry.types = ["electric", "electric"];

  assert.throws(
    () => validateCollectionEntry(entry),
    /contains duplicate types/,
  );
});

test("missing nested media properties are rejected", () => {
  const entry = makeEntryObject();
  entry.sprite = { url: null };

  assert.throws(
    () => validateCollectionEntry(entry),
    /missing required property "fallbackUrl"/,
  );
});

test("unsupported media URL protocols are rejected", () => {
  const entry = makeEntryObject();
  entry.sprite = {
    url: "file:///private/image.png",
    fallbackUrl: null,
  };

  assert.throws(
    () => validateCollectionEntry(entry),
    /expected an HTTP or HTTPS URL/,
  );
});

test("missing shiny counterparts are rejected", () => {
  const entries = makeEntries().filter((entry) => !entry.isShiny);

  assert.throws(
    () => validateEntryCounterparts(entries),
    /is missing counterpart/,
  );
});

test("counterpart metadata must agree", () => {
  const entries = makeEntries();
  const shiny = entries.find((entry) => entry.isShiny);
  assert.ok(shiny);

  shiny.introducedGeneration = 2;

  assert.throws(
    () => validateEntryCounterparts(entries),
    /Counterpart metadata disagrees/,
  );
});

test("normal and shiny counterparts may have different artwork", () => {
  const entries = makeEntries();
  const shiny = entries.find((entry) => entry.isShiny);
  assert.ok(shiny);

  shiny.sprite.url = null;

  assert.doesNotThrow(() => validateEntryCounterparts(entries));
});
