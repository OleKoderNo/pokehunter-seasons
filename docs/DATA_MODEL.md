# Pokémon Data Model

This guide owns catalogue identities and structure. For generation commands, see
[Development](DEVELOPMENT.md); for player records, see [Database](DATABASE.md).

## Current output

`data/catalogue/merged-entries.json` combines Generation 1, Halloween families,
reviewed Ghost forms, Hangry Morpeko, and five ORAS costumes. It contains entries
and encounter groups with resolved rarity. C# loading and relationship checks exist.

The verified roster has 996 entries (498 normal and 498 shiny) in 277 groups.
Counts are a snapshot, not constants future catalogues must retain.

The document remains an intermediate format:

```json
{
  "schemaVersion": 1,
  "sampleOnly": true,
  "scope": "merged-collection-entries",
  "sources": [],
  "encounterGroups": [],
  "entries": []
}
```

The empty arrays illustrate shape only; the validator requires populated sources,
groups, and entries. No separate species/family arrays or release-version metadata
are produced yet. Earlier `data/pokemon.json` sketches were proposals, not an output contract.

## Identity levels

| Identity | Meaning                                              | Example                |
| -------- | ---------------------------------------------------- | ---------------------- |
| Species  | Underlying species                                   | `pikachu`              |
| Group    | One weighted Pokémon option in a category            | `pikachu`              |
| Form     | Form/costume shared by gender and shiny counterparts | `pikachu-libre`        |
| Entry    | One collectible identity                             | `pikachu:female:shiny` |

Entry IDs follow `<formId>:<gender>:<normal-or-shiny>`. They do not depend on display
names, media URLs, array positions, or the active season/event. Once catches reference
an ID, do not rename, reuse, or remove it without a migration/compatibility plan.
The National Pokédex number identifies a species, not a unique collection entry.

## Collection entry fields

Contracts: `scripts/types/catalogue.ts` and
`streamerbot/configuration/catalogue/CatalogueDocument.cs`.

| Field                              | Meaning                                                 |
| ---------------------------------- | ------------------------------------------------------- |
| `id`                               | Permanent collection identity                           |
| `speciesId`                        | Underlying species slug                                 |
| `nationalDexNumber`                | Positive National Pokédex number                        |
| `evolutionChainId`                 | Positive PokéAPI evolution-chain reference              |
| `isLegendary`, `isMythical`        | Species classifications                                 |
| `encounterGroupId`                 | Group used before variant selection                     |
| `formId`                           | Canonical supported form identity                       |
| `displayName`                      | Species display name                                    |
| `formName`                         | Reviewed form label, or null                            |
| `regionalForm`                     | Regional identifier such as `alola`, or null            |
| `gender`                           | `male`, `female`, or `genderless`                       |
| `hasGenderDifference`              | Visible gender difference for the supported form        |
| `isShiny`                          | Normal/shiny state                                      |
| `isCostume`                        | Costume classification                                  |
| `eventOnly`                        | Requires eligible active-event inclusion                |
| `introducedGeneration`             | Form's introduction generation; currently supported 1–9 |
| `types`                            | One or two distinct supported type slugs in slot order  |
| `sprite.url`, `sprite.fallbackUrl` | Matching/reference fallback artwork URL, or null        |
| `cries.latest`, `cries.legacy`     | Cry URLs, or null                                       |

The overlay test combines species name and form label for announcements. Do not assume
`displayName` already includes the form. Null media properties remain present in JSON.

## Gender and shiny mappings

- Forms supporting both genders receive separate male and female entries,
  even when their artwork is identical.
- Single-gender species/forms retain that gender; genderless species use `genderless`.
- `hasGenderDifference` describes appearance, not whether genders count separately.
- Reviewed Frillish, Jellicent, and Basculegion gender resources share a canonical form
  ID; avoid encoding the gender twice in form and entry identity.
- Species metadata is not sufficient for every costume. The five ORAS costumes are
  female-only and set `hasGenderDifference: false` for those particular forms.
- Missing artwork never removes a supported gender identity. Forms without visible
  differences can share artwork between their male and female entries.

Every included form/gender has both normal and shiny entries, even when officially
shiny-locked. Counterparts agree on all metadata except `id`, `isShiny`, and `sprite`.
Missing shiny artwork does not remove the shiny entry or justify presenting normal
artwork as an accurate shiny image.

## Encounter groups and rarity

A generated group contains:

| Field                            | Meaning                                      |
| -------------------------------- | -------------------------------------------- |
| `id`                             | ID referenced by entries                     |
| `speciesId`, `nationalDexNumber` | Shared species identity                      |
| `entryIds`                       | All member collection IDs                    |
| `rarityClass`                    | `common`, `uncommon`, `rare`, or `ultraRare` |

Non-costume entries use canonical `formId` as their group. Costumes use `speciesId`.
Thus regular and Alolan Marowak are separate groups, gendered Basculegion shares one,
and Pikachu costumes join `pikachu`. Extra variants do not multiply group weight.

`config/rarity.json` resolves a default plus exact-ID overrides during merging.
`game.json` holds numerical weights. Rarity changes do not change collection identity
or grant eligibility. See [Configuration](CONFIGURATION.md#rarity-assignments).

## Families, forms, and events

Regular entries use species introduction metadata; reviewed forms use their resolved
form version-group generation. Gender indexing follows the represented form's generation.

Family import traverses all source evolution branches, but generates regular entries;
reviewed form import supplies additional forms separately. Runtime family selectors
resolve a species slug to `evolutionChainId` and match represented entries. This does
not prove the catalogue contains every possible member or form of that family.

Do not store permanent Halloween/Christmas flags on entries. Event configuration
selects types, families, forms, and explicit costumes. Generation restrictions still
apply after matching. `eventOnly: false` does not mean universally available.

The costume converter produces ten entries: female normal/shiny for each of the five
ORAS outfits, introduced in Generation 6, grouped under Pikachu, and event-only.
Hangry Morpeko has four entries: male and female, each normal and shiny. It has its
own group, was introduced in Generation 8, and uses `eventOnly: false`; it still
needs an event exception during Season 1.

## C# availability structures

| Class                      | Responsibility                                                                        |
| -------------------------- | ------------------------------------------------------------------------------------- |
| `CatalogueLoader`          | Read and deserialize the intermediate file; invoke validation                         |
| `CatalogueValidator`       | Check values, counterparts, groups, and references                                    |
| `CatalogueIndex`           | Owned copies and exact case-sensitive ID lookups                                      |
| `EligibleCataloguePool`    | General eligible IDs and an Event subset for one instant                              |
| `EligibilityPoolBuilder`   | Resolve schedules/selectors and generation restrictions                               |
| `EncounterCategoryBuilder` | Build populated categories/groups for one shiny state                                 |
| `WeightedSelector`         | Select a weighted item using a supplied value in [0, 1); does not generate randomness |

Returned index records are independent copies, including nested lists and media.
Unknown entry/group lookups return null; unknown group-member requests throw.
The runtime must retain the index and refresh pools; these classes do not create a
live cache automatically.

Category candidates retain eligible entry subsets. An Event category selecting only
costumes cannot award a regular variant that failed its selectors. An ordinary type
can include both when independently eligible. EncounterOwnershipFilter removes completed
categories using seasonal ownership while retaining all candidates in incomplete categories.
EncounterVariantSelector selects a form and gender from the chosen group's
category-specific entries, removing completed costumes and protecting their
missing genders. Regular entries remain possible duplicates.

## Merge and validation guarantees

- Duplicate IDs within a source fail. Identical entries across sources deduplicate.
- Conflicting copies of an ID fail with source context; file order does not choose a winner.
- Entries are sorted by dex number then ID; group generation has deterministic ordering.
- TypeScript validates required/unknown fields, supported values, identity relationships,
  media-reference structure, and matching normal/shiny counterparts.
- Group generation rejects conflicting species/dex metadata. Rarity rejects unknown
  group overrides and unsupported classes.
- C# validates counterparts, group references, exact membership, supported metadata,
  and required properties. Its loader rejects unknown properties and trailing content.
- Active-event resolution rejects absent family-selector species, forms, and costumes.
  It does not resolve every inactive event or certify complete catalogue coverage.

Validation is not verification of every Pokémon fact or remote media file. C# and
TypeScript validators have separate implementations; do not assume identical coercion
behavior for arbitrary malformed JSON. Use the tested generation workflow.

## Media and source boundaries

Store references, not embedded image/audio data. Read source URLs instead of guessing
them. Null means missing; an HTTP(S) reference does not prove reachability or correct
artwork/audio. Reviewed fallback rules must not substitute the wrong alternate form.

PokéAPI supplies data; the project owns identity, grouping, costume classification,
rarity, and shiny availability decisions. Ambiguous forms need review before conversion.
See [PokéAPI](https://pokeapi.co/docs/v2/) for source documentation.

## Player data and future releases

The catalogue contains no viewer IDs, ownership, timestamps, streaks, or redemptions.
Intended ownership keys are `(viewer, season, entry)` for seasonal uniqueness and
`(viewer, entry)` for lifetime uniqueness. Recatches across seasons add history, not
another unique bonus.

For content updates: preserve IDs, validate changed mappings and overlapping sources,
review rarity, report missing media, and retain definitions needed by historical catches.
Retiring availability must not destroy history. Release metadata, final packaging,
controlled deployment, and archival compatibility remain to be implemented.
