import assert from "node:assert/strict";
import { test } from "node:test";
import { createRegularEntries } from "../lib/create-regular-entries.js";
import { mergeCollectionEntries } from "../lib/merge-collection-entries.js";
import { makePokemon, makeSpecies } from "./fixtures/catalogue-fixtures.js";

/** Fresh valid entries for each test, without API or cache access. */
function makeEntries() {
  return createRegularEntries(makePokemon(), makeSpecies());
}

test("distinct entry IDs are retained", () => {
  const entries = makeEntries();

  const result = mergeCollectionEntries([
    { name: "normal", entries: entries.filter((entry) => !entry.isShiny) },
    { name: "shiny", entries: entries.filter((entry) => entry.isShiny) },
  ]);

  assert.equal(result.entries.length, 4);
  assert.equal(result.inputEntries, 4);
  assert.equal(result.duplicatesRemoved, 0);
});

test("identical entries across sources are merged once", () => {
  const entries = makeEntries();

  const result = mergeCollectionEntries([
    { name: "generation-1", entries },
    { name: "event-family", entries: structuredClone(entries) },
  ]);

  assert.equal(result.entries.length, 4);
  assert.equal(result.inputEntries, 8);
  assert.equal(result.duplicatesRemoved, 4);
});

test("conflicting encounter groups report the ID and both sources", () => {
  const entries = makeEntries();
  const conflicting = structuredClone(entries);

  const changedEntry = conflicting[0];
  assert.ok(changedEntry);

  changedEntry.encounterGroupId = "incorrect-group";

  assert.throws(
    () =>
      mergeCollectionEntries([
        { name: "generation-1", entries },
        { name: "event-family", entries: conflicting },
      ]),
    (error: unknown) => {
      assert.ok(error instanceof Error);
      assert.match(error.message, /Conflicting entry/);
      assert.ok(error.message.includes(changedEntry.id));
      assert.ok(error.message.includes("generation-1"));
      assert.ok(error.message.includes("event-family"));
      return true;
    },
  );
});

test("conflicting nested media references are rejected", () => {
  const entries = makeEntries();
  const conflicting = structuredClone(entries);

  const changedEntry = conflicting[0];
  assert.ok(changedEntry);

  changedEntry.sprite.url = "https://assets.invalid/different.png";

  assert.throws(
    () =>
      mergeCollectionEntries([
        { name: "first", entries },
        { name: "second", entries: conflicting },
      ]),
    /Conflicting entry/,
  );
});

test("duplicate IDs within one source are rejected even when identical", () => {
  const entries = makeEntries();
  const repeatedEntry = entries[0];
  assert.ok(repeatedEntry);

  assert.throws(
    () =>
      mergeCollectionEntries([
        {
          name: "broken-import",
          entries: [...entries, structuredClone(repeatedEntry)],
        },
      ]),
    /Duplicate entry .* within source "broken-import"/,
  );
});

test("source order does not change the merged output", () => {
  const entries = makeEntries();

  const normal = {
    name: "normal",
    entries: entries.filter((entry) => !entry.isShiny),
  };

  const shiny = {
    name: "shiny",
    entries: entries.filter((entry) => entry.isShiny),
  };

  assert.deepStrictEqual(
    mergeCollectionEntries([normal, shiny]),
    mergeCollectionEntries([shiny, normal]),
  );
});

test("output is sorted by National Dex number and then entry ID", () => {
  const entries = makeEntries();
  const result = mergeCollectionEntries([
    { name: "reversed", entries: [...entries].reverse() },
  ]);

  assert.deepStrictEqual(
    result.entries.map((entry) => entry.id),
    [
      "pikachu:female:normal",
      "pikachu:female:shiny",
      "pikachu:male:normal",
      "pikachu:male:shiny",
    ],
  );
});

test("merging and editing its output do not mutate the source entries", () => {
  const entries = makeEntries();
  const original = structuredClone(entries);

  const result = mergeCollectionEntries([{ name: "sample", entries }]);

  assert.deepStrictEqual(entries, original);

  const outputEntry = result.entries[0];
  assert.ok(outputEntry);

  outputEntry.types.push("ghost");
  outputEntry.sprite.url = null;

  assert.deepStrictEqual(entries, original);
});

test("at least one source is required", () => {
  assert.throws(
    () => mergeCollectionEntries([]),
    /At least one collection source/,
  );
});

test("empty sources are rejected", () => {
  assert.throws(
    () => mergeCollectionEntries([{ name: "empty", entries: [] }]),
    /Collection source "empty" is empty/,
  );
});

test("source names must be unique", () => {
  assert.throws(
    () =>
      mergeCollectionEntries([
        { name: "same", entries: makeEntries() },
        { name: "same", entries: makeEntries() },
      ]),
    /Duplicate collection source name/,
  );
});

test("blank and untrimmed source names are rejected", () => {
  for (const name of ["", " ", " sample", "sample "]) {
    assert.throws(
      () => mergeCollectionEntries([{ name, entries: makeEntries() }]),
      /source names must be nonempty and trimmed/,
    );
  }
});
