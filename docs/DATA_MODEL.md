# Pokémon Data Model

This document defines the planned Pokémon catalogue format for
PokéHunter Seasons.

The catalogue will be generated into `data/pokemon.json`.

The importer is not implemented yet. Examples in this document describe
the intended structure; they are not a complete playable catalogue.

## Three different identities

The game needs to distinguish three things:

| Identity         | Purpose                                                          | Example                |
| ---------------- | ---------------------------------------------------------------- | ---------------------- |
| Species          | Links forms to their Pokémon species and National Pokédex number | `pikachu`              |
| Encounter group  | An option in the weighted Pokémon selection                      | `pikachu`              |
| Collection entry | One individually collectible normal or shiny variant             | `pikachu:female:shiny` |

These identities must not be treated as interchangeable.

Pikachu can have several collection entries while appearing only once
within a particular category's Pokémon selection.

A National Pokédex number identifies the species. It cannot uniquely
identify a form, costume, gender variant, or shiny entry.

## Catalogue structure

The catalogue has four main collections:

```json
{
  "schemaVersion": 1,
  "catalogueVersion": "initial",
  "generatedAt": null,
  "species": [],
  "evolutionFamilies": [],
  "encounterGroups": [],
  "entries": []
}
```

### `schemaVersion`

The version of the catalogue structure.

Change this only when a documented migration changes the format.

### `catalogueVersion`

An identifier for this particular catalogue release.

Changing catalogue content can require a new catalogue version without
changing the schema version.

### `generatedAt`

The UTC timestamp when the importer generated the catalogue.

The generated file must contain a timestamp. The `null` value above
only marks an unfinished example.

## Permanent IDs

All IDs are project-owned identifiers.

Once catches reference an ID, do not rename or reuse it without a
migration for the saved records.

IDs must not depend on:

- Display names.
- Array positions.
- Sprite filenames.
- Current event membership.
- Current season availability.

Names, artwork, and configuration can change without changing the
identity of an existing catch.

PokéAPI identifiers are source references, not substitutes for the
game's permanent collection IDs.

## Species records

Species records hold information shared by all forms of a species.

Example:

```json
{
  "id": "pikachu",
  "nationalDexNumber": 25,
  "displayName": "Pikachu",
  "introducedGeneration": 1,
  "evolutionFamilyId": "pichu-family",
  "isLegendary": false,
  "isMythical": false,
  "source": {
    "provider": "pokeapi",
    "speciesName": "pikachu"
  }
}
```

### Species fields

| Field                  | Purpose                                       |
| ---------------------- | --------------------------------------------- |
| `id`                   | Permanent species identifier                  |
| `nationalDexNumber`    | National Pokédex number                       |
| `displayName`          | Human-readable species name                   |
| `introducedGeneration` | Generation where the original species debuted |
| `evolutionFamilyId`    | Reference to its complete evolution family    |
| `isLegendary`          | Legendary classification                      |
| `isMythical`           | Mythical classification                       |
| `source`               | References used to trace imported data        |

Form availability uses the entry's introduction generation, not this
species-level generation.

## Evolution families

A family record connects all species in an evolution family.

Example:

```json
{
  "id": "pichu-family",
  "speciesIds": ["pichu", "pikachu", "raichu"]
}
```

Family IDs are permanent labels. Do not rename them merely because a
future generation adds another evolution.

An event selector such as:

```json
"evolutionFamilies": ["pikachu"]
```

means:

1. Find the species with ID `pikachu`.
2. Read its `evolutionFamilyId`.
3. Select supported non-costume entries for all species in that family.
4. Apply the event's generation restrictions.

The selector accepts a species ID, not the family ID itself.

Family membership does not bypass generation restrictions or
automatically select costumes.

## Encounter groups

An encounter group receives one rarity weight within a selected
category.

Example:

```json
{
  "id": "pikachu",
  "speciesId": "pikachu",
  "displayName": "Pikachu",
  "regularFormId": "pikachu",
  "rarityClass": "uncommon"
}
```

The example rarity assignment is provisional. Final rarity assignments
will be reviewed before the catalogue is used.

### Encounter group fields

| Field           | Purpose                              |
| --------------- | ------------------------------------ |
| `id`            | Permanent encounter group identifier |
| `speciesId`     | Reference to its species             |
| `displayName`   | Human-readable group name            |
| `regularFormId` | The group's non-costume form         |
| `rarityClass`   | Key from `game.json` rarity weights  |

Entries reference their encounter group through `encounterGroupId`.

Regular Pikachu and its selected costumes share the `pikachu` group.

Regional and other separate non-costume forms use their own groups.
For example, Ninetales and Alolan Ninetales use different groups:

```text
ninetales
ninetales-alola
```

Gender counterparts representing the same form share a group.

### Why costumes share a group

Costumes are selected after the Pokémon group.

Adding eight Pikachu costumes must not give Pikachu nine entries in the
initial weighted Pokémon selection.

Instead:

1. Select the Pikachu group.
2. Perform the applicable gender roll.
3. Select between the available regular variant and incomplete costumes.
4. Apply costume gender protection.
5. Check ownership of the final entry.

## Collection entries

Each entry represents one collectible combination of:

- Form or costume.
- Indexed gender.
- Normal or shiny state.

Example: a normal female Pikachu.

```json
{
  "id": "pikachu:female:normal",
  "speciesId": "pikachu",
  "encounterGroupId": "pikachu",
  "formId": "pikachu",
  "displayName": "Pikachu",
  "formName": null,
  "regionalForm": null,
  "gender": "female",
  "hasGenderDifference": true,
  "isShiny": false,
  "isCostume": false,
  "eventOnly": false,
  "introducedGeneration": 1,
  "types": ["electric"],
  "sprite": {
    "url": null,
    "fallbackUrl": null
  },
  "cries": {
    "latest": null,
    "legacy": null
  }
}
```

Asset values are deliberately `null` in this example. The importer will
populate available references rather than constructing guessed URLs.

### Entry fields

| Field                  | Purpose                                                     |
| ---------------------- | ----------------------------------------------------------- |
| `id`                   | Permanent collection entry identifier                       |
| `speciesId`            | Links to species data                                       |
| `encounterGroupId`     | Links to weighted group selection                           |
| `formId`               | Groups gender and shiny counterparts of one form or costume |
| `displayName`          | Overlay-ready name, including the named form or costume     |
| `formName`             | Form or costume label, or `null` for the base form          |
| `regionalForm`         | Region identifier, such as `alola`, or `null`               |
| `gender`               | Indexed gender value                                        |
| `hasGenderDifference`  | Whether this form has visibly distinct gender counterparts  |
| `isShiny`              | Whether this is the shiny collection entry                  |
| `isCostume`            | Whether this is a costume variant                           |
| `eventOnly`            | Whether an active matching event is required                |
| `introducedGeneration` | Generation where this form or costume debuted               |
| `types`                | Actual types for this form                                  |
| `sprite.url`           | Matching entry sprite, or `null`                            |
| `sprite.fallbackUrl`   | Optional substitute image, or `null`                        |
| `cries.latest`         | Preferred modern cry reference, or `null`                   |
| `cries.legacy`         | Legacy cry reference, or `null`                             |

Shared properties such as National Pokédex number and Legendary status
are stored once in the species record.

The game, overlay, and future website combine the entry with its species
record when they need those properties.

## Entry ID format

Use:

```text
<formId>:<gender>:<normal-or-shiny>
```

Examples:

```text
pikachu:male:normal
pikachu:female:normal
pikachu:male:shiny
pikachu:female:shiny
pikachu-rock-star:female:normal
pikachu-rock-star:female:shiny
ninetales-alola:unspecified:normal
ninetales-alola:unspecified:shiny
```

The seasonal ownership key is:

```text
viewer Twitch ID + season ID + entry ID
```

The lifetime unique ownership key is:

```text
viewer Twitch ID + entry ID
```

Catching the same entry in another season creates another catch record
but does not create another lifetime unique entry.

## Gender indexing

Allowed `gender` values are:

| Value         | Meaning                                            |
| ------------- | -------------------------------------------------- |
| `male`        | A separately indexed male entry                    |
| `female`      | A separately indexed female entry                  |
| `genderless`  | A Pokémon or form with no gender                   |
| `unspecified` | Genders are not separately collected for this form |

`unspecified` does not mean genderless.

For a form without visible gender differences, male and female catches
do not need separate entries.

For a visibly different male/female form, generate both genders.

For a female-only or male-only form, generate only the supported gender.
Do not invent an unavailable counterpart.

A missing sprite must not erase a known gender difference.

Gender indexing does not create a separate introduction generation.
It follows the generation of the represented species/form.

## Normal and shiny entries

Every included gender/form combination receives:

- One normal entry.
- One shiny entry.

This applies even when the shiny is unavailable in official games.

Both entries share the same `formId` and `encounterGroupId`, but have
different IDs and `isShiny` values.

A missing shiny sprite does not prevent the shiny entry from existing.

Do not silently present a normal sprite as accurate shiny artwork.
Substitutes must be labelled by the overlay.

## Regional form example

Alolan Ninetales uses:

```text
speciesId: ninetales
encounterGroupId: ninetales-alola
formId: ninetales-alola
displayName: Alolan Ninetales
formName: Alolan
regionalForm: alola
introducedGeneration: 7
types: ice, fairy
```

It shares the original species' National Pokédex number, but has its
own encounter group and collection entries.

Unlocking Generation 1 does not automatically unlock this form.

A matching event can temporarily allow it under the event rules.

## Costume example

Rock Star Pikachu uses:

```text
speciesId: pikachu
encounterGroupId: pikachu
formId: pikachu-rock-star
displayName: Pikachu Rock Star
formName: Rock Star
regionalForm: null
gender: female
hasGenderDifference: false
isCostume: true
eventOnly: true
introducedGeneration: 6
types: electric
```

It receives normal and shiny entries.

Its female-only status does not imply that a male counterpart exists.

The costume remains event-only after Generation 6 becomes unlocked.

An event selects it by matching `include.costumes` against `formId`.

## Event membership

Do not store a permanent `isHalloweenPokemon` or `isChristmasPokemon`
flag on each entry.

Event membership is determined from `config/events.json`:

- Type selectors match `types` on non-costume entries.
- Family selectors resolve `speciesId` through evolution families.
- Form selectors match `formId` on non-costume entries.
- Costume selectors match `formId` on costume entries.

This lets creators change an event roster without regenerating the
catalogue.

One entry can match several selectors but must not be duplicated in
the same encounter category.

The catalogue contains all included generations. Season and event
settings decide which entries are available at runtime.

## Category eligibility and variants

Build category membership from currently available entries for the
selected normal or shiny state.

Keep the eligible entry subset for each category and group.
After selecting a group, variant selection must stay within that subset.

This prevents an Event-category selection from awarding a regular
variant that does not itself match the event.

For example, if an event selects only Pikachu costumes:

- The Event category offers the matching costumes.
- The Electric category can offer regular Pikachu and those costumes,
  if regular Pikachu is seasonally available.

Within a category, an eligible regular variant remains a duplicate
possibility even when owned.

Completed costumes are removed for the selected normal/shiny state.

A category is removed when none of its currently available entries
remain missing from the viewer's Seasonal Dex.

## Source data and project decisions

PokéAPI provides source data. Our importer converts it into this model.

Project-owned decisions include:

- Permanent collection IDs.
- Encounter group assignments.
- Rarity classes.
- Costume classification.
- Event-only restrictions.
- Allowing all included forms to be shiny.

The importer must support reviewed overrides where source data does
not directly express a game rule.

Do not assume:

- Every API Pokémon resource is a separate species.
- Every form should become a separate encounter group.
- A species-level gender flag applies identically to every form.
- A missing sprite means a form or gender does not exist.
- Every form debuted in the original species' generation.

Record unresolved mappings for review rather than silently guessing.

Source reference:
https://pokeapi.co/docs/v2/

## Catalogue validation

Before accepting a generated catalogue, check that:

- All IDs are unique within their collection.
- Every reference resolves to an existing record.
- Every entry belongs to the same species as its encounter group.
- Every species belongs to its referenced evolution family.
- Every rarity class exists in the game configuration.
- Every entry has supported types and a valid introduction generation.
- Every included gender/form combination has normal and shiny entries.
- No duplicate form/gender/shiny combination exists.
- Costume entries are marked event-only.
- Form-level metadata agrees across gender and shiny counterparts.
- Missing asset references use `null`, not invented URLs.
- Every configured event selector resolves successfully.

Produce a separate report for missing artwork or cries.
Missing optional assets must not silently remove collection entries.

## Catalogue versus player data

`data/pokemon.json` describes what can exist in the game.

It must not contain:

- Viewer names or Twitch IDs.
- Ownership information.
- Catch timestamps.
- Retry streaks.
- Redemption records.

Those belong in separate player storage.

Player storage will reference permanent catalogue entry IDs.
Its format will be documented separately before implementing saves.

## Updating the catalogue

When regenerating the catalogue:

1. Preserve existing permanent IDs.
2. Validate new and changed entries.
3. Review changes to forms, groups, generations, and rarity assignments.
4. Report missing assets.
5. Keep references needed by historical catches.
6. Back up the current catalogue and player data before deployment.

Never silently delete an entry referenced by a saved catch.
Removing an entry from future encounters must preserve its historical
identity for archives and the National Dex.
