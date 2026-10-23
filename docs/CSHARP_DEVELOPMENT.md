# Configuration Guide

Use this guide to edit settings. [Game Rules](GAME_RULES.md) owns gameplay behavior;
[Catch Overlay](CATCH_OVERLAY.md) owns overlay settings and preview setup.

## Choose the right file

| File                        | Controls                                                       | How changes are applied today                                 |
| --------------------------- | -------------------------------------------------------------- | ------------------------------------------------------------- |
| `config/game.json`          | Cost, cooldown, shiny bonuses, category/rarity weights         | Configuration checks read it; live redeem integration remains |
| `config/seasons.json`       | Explicit season schedule and generation unlocks                | Configuration checks/selectors and eligibility builder        |
| `config/events.json`        | Event schedule, selectors, generation exceptions, Event weight | Configuration checks/selectors and eligibility builder        |
| `config/rarity.json`        | Rarity class assigned to each encounter group                  | Rerun `npm run merge:catalogue`                               |
| `config/catch-overlay.json` | Appearance, animation, audio, queue                            | Refresh the browser source after editing                      |

All current formats use `schemaVersion: 1`. This is a format version, not a season
number. Do not change it without a documented migration. Editing JSON does not update
the Twitch reward, install DLLs, or start a live catching action.

## Editing safely

Use standard JSON: double-quoted property names, unquoted numbers/booleans, decimal
points, no comments, and no trailing commas. Preserve required properties even when a
selector list is empty. Unknown or missing required configuration properties are rejected.

Bonuses use decimals: `0.05` is 5%; `0.1` is 10%. Keep a reviewable Git diff and validate
after changes. Do not hand-edit generated catalogues or cached API responses.

## Game settings

Paths below are relative to the root of `game.json`.

| Property                                             | Initial value | Meaning                                                                                        |
| ---------------------------------------------------- | ------------- | ---------------------------------------------------------------------------------------------- |
| `redemption.cost`                                    | 500           | Price for the entire redemption, including extra attempts                                      |
| `redemption.perUserCooldownSeconds`                  | 60            | Per-viewer delay from accepted redemption                                                      |
| `shiny.baseOddsDenominator`                          | 8192          | Base probability is 1 divided by this positive integer                                         |
| `shiny.collectionBonus.uniqueEntriesPerMilestone`    | 50            | Combined lifetime unique normal + shiny milestone size                                         |
| `shiny.collectionBonus.bonusPerUniqueEntryMilestone` | 0.1           | Additive bonus per complete combined milestone                                                 |
| `shiny.collectionBonus.uniqueShiniesPerMilestone`    | 10            | Additional unique-shiny milestone size                                                         |
| `shiny.collectionBonus.bonusPerShinyMilestone`       | 0.05          | Additive bonus per complete shiny milestone                                                    |
| `encounters.categoryWeights.type`                    | 1             | Weight of each populated ordinary type category                                                |
| `encounters.categoryWeights.event`                   | 4             | Existing game-level setting; current builder uses the active event's required `categoryWeight` |

Milestone sizes must be positive integers. To disable a bonus, set its bonus value to
zero, not its milestone size. Repeated catches of an existing lifetime entry earn no
new unique bonus. See [Shiny progression](GAME_RULES.md#shiny-progression) for the formula.

`shiny.subscriberMultipliers` contains `none: 1`, `prime: 2`, `tier1: 2`, `tier2: 2.5`,
`tier3: 3.5`. Apply only the applicable tier after adding collection bonuses. Setting
all multipliers to 1 removes subscription differences.

`encounters.rarityWeights` contains `common: 10`, `uncommon: 6`, `rare: 3`, `ultraRare: 1`.
Each group receives its class weight; there is no separate rarity-class roll.
Category and rarity weights must be finite and greater than zero.

With 18 type categories of weight 1 and Event weight 4, Event has 4/22 of the category
roll. This is neither 4% nor a guaranteed fourfold increase for each event Pokémon.
Actual group odds depend on the selected category and its members.

The configured cost must match the Twitch reward manually until synchronization exists.
The reward description must explain per-user cooldown and no automatic refunds.

## Seasons: customize in seven steps

1. Open `config/seasons.json` and choose your schedule. Annual, quarterly, and other
   lengths are supported; duration comes from the dates.
2. Set a resolvable `timeZone`, such as `Europe/Oslo`, for local-time display.
3. Give every season a unique permanent `id` and a readable `name`.
4. Set `startsAt` and `endsAtExclusive`, including seconds and an explicit offset or `Z`.
5. Set the complete `unlockedGenerations` list. Nothing is inherited automatically.
6. Start the next season at exactly the previous season's exclusive end.
7. Run the configuration checks linked below before using the schedule.

| Field                 | Rule                                                                                |
| --------------------- | ----------------------------------------------------------------------------------- |
| `schemaVersion`       | 1                                                                                   |
| `timeZone`            | Must resolve; it does not rewrite timestamp offsets                                 |
| `seasons`             | Nonempty list, no gaps or overlaps between consecutive seasons                      |
| `id`                  | Nonempty, no surrounding whitespace, unique ignoring capitalization                 |
| `name`                | Nonempty label, no surrounding whitespace                                           |
| `startsAt`            | Inclusive start                                                                     |
| `endsAtExclusive`     | Exclusive end, later than start                                                     |
| `unlockedGenerations` | Nonempty, distinct positive integers; use generations represented in your catalogue |

`[1, 2]` unlocks both generations; `[3]` unlocks only Generation 3. Forms use their own
introduction generation, and costumes still require events. Configuration validation
checks positive generation numbers, not complete catalogue coverage.

### Understand timestamps

A season is active when `startsAt <= instant < endsAtExclusive`. To include March 31,
end at midnight April 1, not at `23:59:59` on March 31.

```text
2027-01-01T00:00:00+01:00
2026-12-31T23:00:00Z
```

These represent the same instant. Seconds and an explicit offset are required; up to
seven fractional-second digits are supported. `2027-01-01T00:00:00` is rejected.

The named zone controls display, while explicit offsets control boundaries. The
validator permits equivalent UTC/offset representations; it does not require the
written offset to match the named zone. When entering local clock times, choose the
correct offset for that date. Oslo is +01:00 in winter and +02:00 in summer.

### Example: two quarterly seasons

This is an alternative complete example, not a replacement for the project's default
annual schedule unless you deliberately choose it.

```json
{
  "schemaVersion": 1,
  "timeZone": "Europe/Oslo",
  "seasons": [
    {
      "id": "season-1",
      "name": "The Kanto Adventure",
      "startsAt": "2027-01-01T00:00:00+01:00",
      "endsAtExclusive": "2027-04-01T00:00:00+02:00",
      "unlockedGenerations": [1]
    },
    {
      "id": "season-2",
      "name": "Journey to Johto",
      "startsAt": "2027-04-01T00:00:00+02:00",
      "endsAtExclusive": "2027-07-01T00:00:00+02:00",
      "unlockedGenerations": [1, 2]
    }
  ]
}
```

Add a third season before July 1 to continue availability. The selector returns null
outside the configured schedule; it never invents a season or reuses an expired one.
The project's initial annual schedule is listed in [Game Rules](GAME_RULES.md#seasons).

### Changing registered seasons

Before registration you can edit names, dates, and generations. After registration,
`SeasonRepository.Register` rejects changed names, boundaries, or generation sets for
an existing ID. A display-name change does not change identity, but it is not currently
an automatic supported update to the stored record.

Do not rename/reuse historical IDs or delete archives. An explicit editing/migration
workflow is required for already-stored changes; it is not implemented. Back up player
data and configuration before future live schedule changes. Editing JSON does not move
saved catches. Acceptance time will bind a redemption to its season; cooldown is independent.

## Events: customize in six steps

1. Open `config/events.json`; copy an event for a new occurrence and give it a new ID.
2. Set name, enabled state, inclusive start, and exclusive end.
3. Edit all four arrays in `include`; unused arrays remain `[]`.
4. Choose generation exceptions and Legendary/Mythical restrictions.
5. Set the required positive `categoryWeight` (Halloween uses 4).
6. Validate configuration and ensure selected content exists in the generated catalogue.

| Field                                   | Rule / effect                                                                       |
| --------------------------------------- | ----------------------------------------------------------------------------------- |
| `timeZone`                              | Resolvable display zone; timestamps determine instants                              |
| `events`                                | May be empty; gaps are permitted                                                    |
| `id`, `name`                            | Stable unique occurrence ID and readable label                                      |
| `enabled`                               | False prevents selection, but does not bypass field validation                      |
| `startsAt`, `endsAtExclusive`           | Same timestamp rules as seasons                                                     |
| `categoryWeight`                        | Required finite positive Event category weight; no omission fallback is implemented |
| `allowLockedGenerations`                | Allow matching entries from locked form-introduction generations                    |
| `requireUnlockedGenerationForLegendary` | Keep matching locked Legendary entries out when true                                |
| `requireUnlockedGenerationForMythical`  | Same restriction for Mythical entries                                               |

Enabled events cannot overlap. Adjacent events may share an end/start instant. No active
event is normal; no active season prevents pool construction. Events do not repeat annually
automatically. Keep historical IDs stable and use a new ID for each occurrence.

The project's generation policy is `true` for all three flags. Turning a classification
restriction off permits that classification from locked generations only if
`allowLockedGenerations` is also true. These flags do not remove normally unlocked entries.

### Select the roster

| Selector                    | Matches                                                              | Example              |
| --------------------------- | -------------------------------------------------------------------- | -------------------- |
| `include.types`             | Non-costume forms with any listed type                               | `["ghost"]`          |
| `include.evolutionFamilies` | Represented members/forms sharing the named species' evolution chain | `["cubone"]`         |
| `include.forms`             | Exact non-costume form IDs                                           | `["morpeko-hangry"]` |
| `include.costumes`          | Exact costume form IDs only                                          | `["pikachu-libre"]`  |

All four arrays must exist; at least one selector is required. Use supported lowercase
types and lowercase hyphenated IDs. Duplicates within one list are rejected. Matching
multiple selectors does not add tickets. There is no exclusion list: removing one selector
does not exclude a Pokémon still matching another.

Type matching alone does not include nonmatching evolution relatives. Family matching
includes represented branches and forms but never automatically includes costumes.
Selectors cover all supported genders and shiny states. Single-gender costumes stay
single-gender; the five ORAS outfits are female-only.

Active-event resolution rejects missing family-selector species, unknown forms, and
unknown costumes. It does not verify every inactive event or prove that every family
member exists. New content must be imported; editing a selector cannot create entries.
Current importer scripts still include Halloween-specific paths and reviewed mappings.

For the roster, see [Initial Halloween roster](GAME_RULES.md#initial-halloween-roster).
A roster change only needs eligibility rebuilding when the entries already exist;
missing forms/species need generation, merging, and deployment too.

## Rarity assignments

`rarity.json` assigns classes; `game.json` assigns their numerical weights.
The initial custom balance is 62 common, 91 uncommon, 91 rare, and 33 ultraRare groups.
These counts are a roster snapshot and the assignments are not official encounter rates.

To edit:

1. Find the exact group `id` in `data/catalogue/merged-entries.json`.
2. Add/change that ID in `config/rarity.json` under `overrides`.
3. Choose `common`, `uncommon`, `rare`, or `ultraRare` (case-sensitive).
4. Run `npm run merge:catalogue` and review its summary.

Example configuration shape:

```json
{
  "schemaVersion": 1,
  "defaultClass": "common",
  "overrides": {
    "pikachu": "uncommon",
    "marowak-alola": "rare"
  }
}
```

Keep the project's other assignments when making an individual change. Unlisted groups,
including newly added groups, inherit the default. Unknown IDs/classes/properties fail
merging, including overrides for groups absent from the generated roster.

An override affects the whole group, including its costume members; it does not change
shiny odds, equal variant weights, or event eligibility. Commit rarity.json, not generated
catalogues. Review new groups before accepting their default rarity.

## Validate and apply changes

- Game/seasons/events: run the [standalone configuration checks](CSHARP_DEVELOPMENT.md#standalone-checks).
- Rarity: rerun `npm run merge:catalogue`, then the catalogue checks.
- Catalogue or eligibility code: run the catalogue suite; it includes fixed event fixtures,
  not an exhaustive check of every event in your current events.json.
- Overlay: refresh the browser source and use its [manual checklist](CATCH_OVERLAY.md#verification-and-limits).

The configuration action reads game/seasons/events on each execution. Changing JSON
alone does not require a new DLL, but the receiving component must read it again.
Automatic production reload and live reward enforcement remain unimplemented.
