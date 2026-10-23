# Pokémon Data Model

This document describes the current generated collection entries and the
planned runtime catalogue for PokéHunter Seasons.

It distinguishes implemented data from planned behaviour.
A generated entry does not mean that catching, event eligibility, or
ownership tracking has been implemented.

For generation commands and existing checks, see
[Development Guide](DEVELOPMENT.md).

## Current implementation status

The TypeScript tools generate:

- Regular Pikachu sample entries.
- Representative regular-species sample entries.
- Regular Generation 1 entries.
- Regular entries for selected Halloween evolution families.
- Reviewed Ghost-type form entries.

Type discovery and form inspection also produce review reports.

Hangry Morpeko and the five female-only Pikachu costumes are generated
through a dedicated reviewed converter. Their output remains separate
from the other intermediate catalogues.

The generated files are currently separate intermediate outputs under
`data/samples/` and `data/catalogue/`.

The planned combined runtime file is `data/pokemon.json`.
It is not yet produced by the complete catalogue workflow.

The current generated entries repeat some species metadata.
Separate runtime species, family, and encounter-group records remain
planned.

## Identity levels

| Identity         | Purpose                                                        | Example                |
| ---------------- | -------------------------------------------------------------- | ---------------------- |
| Species          | Identifies the underlying Pokémon species                      | `pikachu`              |
| Encounter group  | Represents an option in weighted Pokémon selection             | `pikachu`              |
| Form             | Groups gender and shiny counterparts of a form or costume      | `pikachu-rock-star`    |
| Collection entry | Identifies one collectible form, gender, and shiny combination | `pikachu:female:shiny` |

These identities are not interchangeable.

A National Pokédex number identifies a species.
It cannot uniquely identify a form, costume, gender variant, or shiny
collection entry.

Several collection entries may belong to one encounter group.
Adding collection variants must not accidentally multiply a group's
weight in the initial Pokémon selection.

## Permanent collection IDs

The entry ID format is:

```text
<formId>:<gender>:<normal-or-shiny>
```

Examples currently generated include:

```text
pikachu:male:normal
pikachu:female:normal
pikachu:male:shiny
pikachu:female:shiny
bulbasaur:unspecified:normal
magnemite:genderless:normal
nidoran-f:female:normal
```

IDs are project-owned identities, even when their components initially
come from API slugs.

Once saved catches reference an ID, do not rename, remove, or reuse it
without a migration plan.

IDs must not depend on:

- Display names.
- Array positions.
- Sprite filenames.
- Current event membership.
- Current season availability.

Changing artwork or an event roster must not change historical catch
identity.

## Current collection-entry structure

The TypeScript contract is defined in `scripts/types/catalogue.ts`.

Example structure for a normal female Pikachu:

```json
{
  "id": "pikachu:female:normal",
  "speciesId": "pikachu",
  "nationalDexNumber": 25,
  "evolutionChainId": 10,
  "isLegendary": false,
  "isMythical": false,
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

The media fields are deliberately null in this example.
Generated entries use available source references.

### Entry fields

| Field                  | Meaning                                                          |
| ---------------------- | ---------------------------------------------------------------- |
| `id`                   | Permanent collection identity                                    |
| `speciesId`            | Underlying species identity                                      |
| `nationalDexNumber`    | National Pokédex number of the species                           |
| `evolutionChainId`     | PokéAPI evolution-chain reference                                |
| `isLegendary`          | Species Legendary classification                                 |
| `isMythical`           | Species Mythical classification                                  |
| `encounterGroupId`     | Identifier intended for weighted group selection                 |
| `formId`               | Identity shared by the form's gender and shiny counterparts      |
| `displayName`          | Current converter's human-readable species name                  |
| `formName`             | Separate reviewed form label, or null                            |
| `regionalForm`         | Regional identifier such as `alola`, or null                     |
| `gender`               | Indexed collection gender                                        |
| `hasGenderDifference`  | Currently populated from the species gender-difference flag      |
| `isShiny`              | Whether this entry is shiny                                      |
| `isCostume`            | Whether this entry represents a costume                          |
| `eventOnly`            | Whether a matching active event is required                      |
| `introducedGeneration` | Introduction generation used for the represented species or form |
| `types`                | Types of the represented Pokémon or form                         |
| `sprite.url`           | Matching source sprite reference, or null                        |
| `sprite.fallbackUrl`   | Explicit substitute reference, or null                           |
| `cries.latest`         | Modern cry reference, or null                                    |
| `cries.legacy`         | Legacy cry reference, or null                                    |

The current converters retain the species display name and store form
labels separately. They do not yet produce a final overlay announcement
name.

For example, an overlay will need to combine a species name and form
label appropriately rather than assuming `displayName` includes both.

## Gender indexing

Allowed values:

| Value         | Meaning                                                    |
| ------------- | ---------------------------------------------------------- |
| `male`        | A separately collected male entry                          |
| `female`      | A separately collected female entry                        |
| `genderless`  | A Pokémon without gender                                   |
| `unspecified` | Male and female are not separately collected for this form |

`unspecified` does not mean genderless.

The regular-species converter uses validated species gender metadata:

- Visible male/female differences produce separate entries.
- A single-gender species produces entries for that gender.
- A genderless species produces genderless entries.
- Other species use unspecified entries.

A missing sprite does not remove a known gender identity.

### Gender represented by separate API forms

API resource layout is not itself a collection rule.

The reviewed converter explicitly handles Frillish, Jellicent, and
Basculegion gender resources.

Their gender counterparts share a canonical form ID:

```text
frillish:male:normal
frillish:female:normal
basculegion:male:shiny
basculegion:female:shiny
```

This avoids interpreting the same gender distinction twice, once in
the form ID and again in the gender field.

### Form-specific gender limitations

A species-level gender flag does not prove that every form or costume
supports the same genders.

The current `hasGenderDifference` value is copied from species metadata.
It must not be treated as a complete form-specific gender policy.

Single-gender forms and costumes receive only their supported gender.

The five reviewed Pikachu costumes are female-only. Each receives one
female normal entry and one female shiny entry.

Their converter sets `hasGenderDifference` to false because these
individual costume forms do not have male and female counterparts.
This differs from the species-wide Pikachu flag.

Missing female sprite fields do not determine gender availability.
The reviewed costume mapping supplies that rule explicitly.

Do not infer supported genders solely from missing female sprite fields.

## Normal and shiny entries

Every included form and indexed gender receives:

- One normal entry.
- One shiny entry.

This applies even when the shiny is unavailable in official games.

The counterparts share their form and encounter-group identities.
Their entry IDs and `isShiny` values differ.

A missing shiny sprite must not remove the shiny entry.

Do not silently present a normal sprite as accurate shiny artwork.
Any eventual substitute must be handled explicitly in presentation.

## Form introduction generations

The introduction generation belongs to the represented form.

Unlocking a species' original generation must not automatically unlock
regional or other forms introduced later.

The regular converter reads the species introduction generation.

The reviewed form workflow reads the form's version group and resolves
its generation. These mappings are reviewed rather than blindly
assuming that every form debuted with its species.

Gender indexing does not create a separate introduction generation.
It follows the represented species or form.

A form's presence in the catalogue does not establish its availability
in the current season.

## Evolution families

The current entries contain `evolutionChainId`, a source reference used
to resolve family membership.

Family traversal includes every branch of the source evolution chain.

A family selector such as:

```json
"evolutionFamilies": ["dipplin"]
```

selects the family containing that species rather than only its direct
ancestors or descendants.

The current family importer generates regular entries for those species.
It does not automatically generate every regional form, transformation,
or costume belonging to the family.

The planned runtime family records will connect supported species and
allow selectors to resolve without calling PokéAPI during a redemption.

The final family-record identity scheme has not been implemented.
It must remain stable when future generations add evolutions.

Family membership does not bypass generation restrictions or
automatically enable costumes.

## Encounter groups

Encounter groups determine how Pokémon participate in weighted selection.

The intended design is:

- Regular Pikachu and its eligible costumes share one group.
- Distinct non-costume forms can have separate groups.
- Gender counterparts of the same form share a group.
- Normal and shiny counterparts share a group.
- Each group has a reviewed rarity class.

For example, the intended groups for regular and Alolan Ninetales are:

```text
ninetales
ninetales-alola
```

### Current converter grouping

The reviewed non-costume form converter assigns `encounterGroupId`
from the canonical `formId`.

Distinct forms therefore receive separate groups.
Explicitly mapped gender counterparts share a canonical form ID and
continue to share one encounter group.

For example:

- Alolan Marowak uses `marowak-alola`.
- Male and female Basculegion both use `basculegion`.

Standalone tests protect these mappings.

The converter produces group references, not complete encounter-group
records. Rarity assignments and runtime weighted selection remain
unimplemented.

Costumes require separate conversion that assigns them to their
underlying Pokémon group.

### Costume selection

Costumes are selected after the Pokémon group.

Adding eight costumes must not make Pikachu nine times as common in the
initial group selection.

Within the eligible variant selection, regular Pikachu and each
incomplete costume receive equal variant weight.

The applicable gender roll and costume gender protection then determine
the final collection entry.

A completed costume is removed for the selected normal or shiny state.
An eligible regular variant remains a possible duplicate.

These are planned encounter rules, not behaviours executed by the
catalogue converters.

## Event-only costumes

The five selected costume resources are:

- `pikachu-rock-star`
- `pikachu-belle`
- `pikachu-pop-star`
- `pikachu-phd`
- `pikachu-libre`

Their inspection reports identify Generation 6 introduction metadata.

The intended costume entries will have:

```text
speciesId: pikachu
encounterGroupId: pikachu
isCostume: true
eventOnly: true
introducedGeneration: 6
```

Each selected gender will receive normal and shiny entries.

Unlocking Generation 6 will not make these costumes available outside
matching events.

The dedicated converter generates ten entries across these five costumes:
one female normal entry and one female shiny entry per costume.

All five share the `pikachu` encounter group and remain event-only.
Their female artwork is read from the costume resource's default sprite
fields.

There is no male counterpart to redirect a costume catch toward.
For the selected normal or shiny state, a costume is complete after its
single female entry has been caught. Runtime ownership filtering remains
to be implemented.

Hangry Morpeko is generated separately as a non-costume Generation 8 form,
with unspecified normal and shiny entries in the `morpeko-hangry` group.

Its `eventOnly` flag is false. This does not make it available in Season 1
without an eligible event; generation restrictions still apply.

## Event membership and generation eligibility

Event membership is configured through `config/events.json`.

Do not permanently attach Halloween or Christmas flags to collection
entries.

The planned selector behaviour is:

- Type selectors match types on non-costume entries.
- Family selectors resolve species membership in evolution families.
- Form selectors match non-costume form IDs.
- Costume selectors match costume form IDs.

An entry can match several selectors.
It must still appear only once within the same encounter category.

A matching event may allow otherwise locked generations when configured
to do so.

Legendary and Mythical Pokémon still require an unlocked introduction
generation when the corresponding event restrictions are enabled.

The existence of a Legendary or Mythical entry in an intermediate
catalogue does not make it eligible in Season 1.

`eventOnly: false` does not mean an entry is always available.
Season and event eligibility must still be evaluated.

Changing a roster can avoid regeneration only when the required entries
already exist in the deployed catalogue.
New forms or missing species require catalogue generation and deployment.

## Category eligibility and variants

At runtime, category membership must use currently eligible entries
for the selected normal or shiny state.

Keep the eligible entry subset for each category and group.
Variant selection must remain inside that subset.

For example, when an event selects only Pikachu costumes:

- The Event category offers matching costumes.
- The Electric category may offer regular Pikachu and the active costumes
  when each is otherwise eligible.

An Event-category selection must not award an unrelated regular variant
that did not qualify for that category.

Completed categories are removed separately for normal and shiny
encounters, based on the viewer's current Seasonal Dex.

Completing the normal collection must not force a shiny result.
A failed shiny roll can still leave the redemption without an eligible
normal catch.

These runtime rules are not implemented by the import tools.

## Planned combined runtime catalogue

The intended combined structure is:

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

This is a design example, not the current generated output.

| Field               | Intended purpose                        |
| ------------------- | --------------------------------------- |
| `schemaVersion`     | Version of the runtime data structure   |
| `catalogueVersion`  | Identifier for the content release      |
| `generatedAt`       | UTC generation timestamp                |
| `species`           | Shared species metadata                 |
| `evolutionFamilies` | Family membership records               |
| `encounterGroups`   | Group identities and rarity assignments |
| `entries`           | Individually collectible variants       |

A completed runtime catalogue will use a real UTC timestamp.
The null value above marks an unfinished example.

Changing content does not necessarily require changing the schema.
Changing the data contract requires a documented compatibility plan.

The final treatment of repeated species metadata must be decided before
the runtime loader is implemented. Current intermediate entries include
that metadata directly.

## Media references

Media references come from source data.
Do not construct guessed sprite or cry URLs.

Missing optional references use `null`.

The current workflow checks references and reports availability.
It does not verify every remote media file.

A valid-looking URL is not proof that:

- The remote file exists.
- The image matches the intended gender or shiny state.
- The audio plays correctly.
- The asset will remain available indefinitely.

The current converters leave `sprite.fallbackUrl` null unless an
explicit implementation supplies a substitute.

Missing optional artwork or cries must not silently remove entries.

## Source data and project rules

PokéAPI supplies source data.
The project determines how that data becomes collectible entries.

Project-owned decisions include:

- Permanent collection identities.
- Canonical form and gender mappings.
- Encounter-group assignments.
- Rarity classes.
- Costume classification.
- Event-only restrictions.
- Allowing every included form to have a shiny entry.

Do not assume:

- Every API Pokémon resource represents a separate species.
- Every form needs a separate encounter group.
- A species gender flag fully describes every costume.
- Missing artwork means a variant does not exist.
- Every form debuted with its species.

Unsupported or ambiguous mappings should be reviewed before conversion.

The source API documentation is available at
[PokéAPI](https://pokeapi.co/docs/v2/).

## Existing checks and remaining validation

The current tools check parts of the model, including:

- Resource identity and required source fields.
- Representative gender conversions.
- Unique collection identities.
- Normal and shiny counterparts.
- Expected Generation 1 coverage.
- Reviewed Ghost form metadata.
- Consistency between overlapping generated entries.

Standalone fixture tests also cover collection gender rules, missing
media, invalid source fields, regional-form grouping, canonical gender
mappings, and sprite fallback. These tests run without API or cache access.

These checks do not yet constitute final runtime catalogue validation.

Before accepting a combined runtime catalogue, also verify:

- All IDs are unique within their record collection.
- Every species, family, and encounter-group reference resolves.
- Every entry belongs to the same species as its encounter group.
- Every species belongs to its referenced family.
- Rarity classes exist in the game configuration.
- Types and introduction generations are supported.
- No duplicate form, gender, and shiny combination exists.
- Costume entries are event-only.
- Counterpart metadata is consistent.
- Overlapping import outputs agree before deduplication.
- Every configured event selector resolves as intended.
- Required form and gender policies have been reviewed.

Missing media should produce a separate report.
Optional missing assets must not be confused with invalid identity data.

## Catalogue versus player storage

The catalogue describes what can exist in the game.

It must not contain:

- Viewer names or Twitch IDs.
- Ownership information.
- Catch timestamps.
- Retry streaks.
- Redemption records.

SQLite player storage is separate.
Trainer and season storage foundations are implemented; collection
ownership and catch recording remain future work.

The intended Seasonal Dex ownership key is:

```text
viewer Twitch ID + season ID + entry ID
```

The intended lifetime unique ownership key is:

```text
viewer Twitch ID + entry ID
```

Catching the same entry in another season adds to catch history and
total catches, but not to lifetime unique-entry counts.

Normal and shiny entries count separately.
The additional shiny milestone bonus uses unique shiny entries.

Historical catches must continue to resolve after a season or event ends.

## Merging intermediate entries

The merge function combines already validated collection entries.

Its rules are:

- Different entry IDs are retained.
- Identical entries appearing in different sources are kept once.
- Conflicting data for the same entry ID causes an error.
- Duplicate entry IDs within a single source cause an error.

Conflict messages identify the entry and both source names.
File order must never silently determine which conflicting copy wins.

Comparison includes all entry fields, including nested media references.
Differences must be reviewed and corrected upstream.

Merged entries are sorted by National Pokédex number and then entry ID.
The function returns independent copies without modifying its inputs.

The merge function does not validate external JSON, create missing
counterparts, or decide season and event eligibility.

The `merge:catalogue` command now validates and combines the four
generated entry catalogues into `data/catalogue/merged-entries.json`.

Each entry is checked for required and unexpected fields, supported
values, internal identity relationships, and media-reference structure.

Normal and shiny counterparts must exist and agree on all metadata
except their entry ID, shiny flag, and sprite references.

This validation does not verify every Pokémon fact, resolve all final
runtime record references, assign rarity, or enforce event eligibility.
The combined file remains an intermediate catalogue.

## Updating the catalogue

When generating a new catalogue release:

1. Preserve existing collection identities.
2. Validate new and changed entries.
3. Review form, gender, generation, group, and rarity changes.
4. Check agreement between overlapping source outputs.
5. Report missing media separately.
6. Preserve entries referenced by historical catches.
7. Validate the final combined catalogue.
8. Back up player data and the deployed catalogue before replacement.

Retiring an entry from future encounters must preserve its identity
for archives and the National Dex.

The final packaging, deployment, and compatibility workflow remains
to be implemented.
