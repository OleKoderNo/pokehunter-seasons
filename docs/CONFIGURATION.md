# Configuration Guide

PokéHunter Seasons separates editable settings from the catching logic.

This document explains how to customize the game using:

- `config/game.json` — redemption settings, shiny bonuses, and encounter weights.
- `config/seasons.json` — season schedules and generation unlocks.
- `config/events.json` — event schedules, themed Pokémon, costumes, and event weights.
- `config/catch-overlay.json` — catch notification appearance, animation, audio, and display queue.

The loader and value validator for `config/game.json` are implemented.
They have passed initial standalone checks and a manual configuration
check inside Streamer.bot using the shared `PokeHunter.Core.dll` library.

The manual check reads and validates settings, then logs the results.
It does not update Twitch rewards or run the catching game.

Loaders and value validators for `config/game.json` and
`config/seasons.json` are implemented and have passed initial standalone
checks.

Game configuration loading has also passed a manual check inside
Streamer.bot using the shared `PokeHunter.Core.dll` library.

Season checks cover configuration loading, adjoining boundaries,
overlapping seasons, and timestamps requiring explicit UTC offsets.
Named time-zone resolution and active-season selection are not yet
implemented.

The manual Streamer.bot check reads and validates game settings, then
logs the results. It does not update Twitch rewards or run the catching
game.

Loaders for event and overlay configuration are not implemented yet.

## Editing the file

`config/game.json` uses standard JSON.

- Keep property names inside double quotes.
- Write numbers without quotes.
- Use a decimal point, such as `2.5`.
- Do not add comments or trailing commas.
- Keep property names unchanged unless you also update the code that reads them.

Percentage bonuses use decimal values:

| Value  | Percentage increase |
| ------ | ------------------- |
| `0.05` | 5%                  |
| `0.1`  | 10%                 |
| `0.25` | 25%                 |

Settings must be validated before accepting redemptions. An invalid
configuration should produce a clear error identifying the setting.

## Configuration version

### `schemaVersion`

The version of the configuration's structure.

Initial value: `1`.

This is not the season number or the project's release version.
Only change it when a documented configuration migration requires it.

## Redemption settings

### `redemption.cost`

The intended channel-point price for one redemption.

Default: `500`.

One redemption includes all attempts earned through the viewer's retry
streak. Additional attempts within that redemption do not cost extra.

The Twitch reward must have the matching price. This setting alone does
not update Twitch; reward setup or synchronization must handle that.

### `redemption.perUserCooldownSeconds`

How many seconds a viewer must wait between accepted redemptions.

Default: `60`.

The cooldown:

- Starts when a redemption is accepted.
- Uses the viewer's Twitch user ID.
- Applies only to that viewer.
- Allows other viewers to redeem during that time.

A redemption received during the cooldown does not roll, change the retry
streak, or extend the cooldown. Points are not refunded.

Twitch's reward description should explain the per-viewer cooldown and
no-refund behavior.

## Shiny settings

### `shiny.baseOddsDenominator`

The denominator of the base shiny probability.

Default: `8192`, meaning a base probability of 1 in 8,192.

A smaller value makes shinies more common. A larger value makes them rarer.

This must be a whole number greater than zero.

### Unique collection entries

Collection bonuses use the lifetime National Dex.

Normal and shiny entries count separately. Supported forms, costumes,
and visible gender differences also have their own entry identities.

Catching an already-recorded entry in a later season adds to catch
history, but does not increase the unique-entry bonus.

### `shiny.collectionBonus.uniqueEntriesPerMilestone`

The number of combined unique normal and shiny entries needed for one
collection milestone.

Default: `50`.

Only complete milestones count:

- 49 unique entries: zero milestones.
- 50 unique entries: one milestone.
- 100 unique entries: two milestones.

This must be a whole number greater than zero.

### `shiny.collectionBonus.bonusPerUniqueEntryMilestone`

The additional shiny-probability multiplier earned per collection milestone.

Default: `0.1`, meaning an additional 10% of the base probability.

Bonuses are added together; they do not compound.

### `shiny.collectionBonus.uniqueShiniesPerMilestone`

The number of unique shiny entries needed for one additional shiny milestone.

Default: `10`.

Shiny entries contribute to both the combined collection milestones and
these additional shiny milestones.

This must be a whole number greater than zero.

### `shiny.collectionBonus.bonusPerShinyMilestone`

The additional shiny-probability multiplier earned per shiny milestone.

Default: `0.05`, meaning an additional 5% of the base probability.

Set either milestone bonus to `0` to disable that bonus. Keep its
milestone size greater than zero.

### `shiny.subscriberMultipliers`

The multiplier applied after calculating collection bonuses.

| Subscription   | Default multiplier |
| -------------- | ------------------ |
| Not subscribed | `1`                |
| Prime          | `2`                |
| Tier 1         | `2`                |
| Tier 2         | `2.5`              |
| Tier 3         | `3.5`              |

Use only the viewer's applicable subscription multiplier.
Subscription tiers do not stack with one another.

Set every value to `1` to give all subscription statuses the same odds.

### Shiny formula

Let:

- `N` = lifetime unique normal entries.
- `S` = lifetime unique shiny entries.
- `T` = the viewer's subscription multiplier.
- `floor` = round down to the nearest whole number.

Using the default configuration:

```text
collectionMilestones = floor((N + S) / 50)
shinyMilestones = floor(S / 10)

collectionMultiplier =
    1
    + (collectionMilestones × 0.10)
    + (shinyMilestones × 0.05)

shinyProbability =
    min(1, (1 / 8192) × collectionMultiplier × T)
```

The probability is capped at 100%.

Example: a Tier 1 subscriber with 100 unique normal entries and
100 unique shiny entries:

```text
collectionMilestones = floor(200 / 50) = 4
shinyMilestones = floor(100 / 10) = 10

collectionMultiplier = 1 + 0.40 + 0.50 = 1.90
shinyProbability = 1.90 × 2 / 8192
```

This is approximately 1 in 2,156 per shiny check.

Calculate the probability once when accepting a redemption.

Once an attempt rolls shiny, every remaining attempt in that redemption
stays shiny. Those attempts do not perform another shiny check.
This protection ends with the redemption.

## Encounter category weights

### `encounters.categoryWeights.type`

The weight assigned to each eligible ordinary type category.

Default: `1`.

Only categories eligible for the current attempt participate.
Empty or fully collected categories are removed for the selected
normal or shiny state.

### `encounters.categoryWeights.event`

The default weight assigned to the active Event category.

Default: `4`.

An event may specify its own category weight in its event configuration.
If it does not, use this default.

This does not mean a 4% probability. A category's probability is:

```text
category probability =
    category weight / total weight of eligible categories
```

With all 18 ordinary types and an Event category:

```text
Total weight = (18 × 1) + 4 = 22

Each ordinary type = 1 / 22
Event category = 4 / 22
```

Event Pokémon also appear in their eligible ordinary type categories.

The Event category only participates when an event is active and the
category has eligible, incomplete entries for the attempt.

## Pokémon rarity weights

### `encounters.rarityWeights`

Relative weights used to select a Pokémon encounter group within the
chosen category.

| Class      | Default weight |
| ---------- | -------------- |
| Common     | `10`           |
| Uncommon   | `6`            |
| Rare       | `3`            |
| Ultra rare | `1`            |

Each eligible group receives its assigned class weight.

With exactly one group of each class:

```text
Total weight = 10 + 6 + 3 + 1 = 20

Common group = 10 / 20 = 50%
Uncommon group = 6 / 20 = 30%
Rare group = 3 / 20 = 15%
Ultra rare group = 1 / 20 = 5%
```

These percentages change when the category contains different numbers
of groups. The game does not roll a rarity class separately first.

Rarity weights do not control costume selection. After selecting an
encounter group, the regular variant and each available incomplete
costume receive equal variant weights.

Category and rarity weights must be finite numbers greater than zero.

## Rules outside this configuration

The following behavior is defined in [Game Rules](GAME_RULES.md):

- One successful catch at most per redemption.
- A fully failed redemption adds one attempt to the next redemption.
- A successful catch resets the retry streak.
- No automatic redemption refunds.
- Shiny protection lasts only for the current redemption.
- Seasonal duplicates fail unless a documented costume rule applies.

Season schedules, event rosters, and overlay settings will have their
own configuration files.

## Season configuration

`config/seasons.json` defines the season schedule and generation unlocks.

The game determines the active season from the current time. There is no
manually maintained active-season setting.

### `schemaVersion`

The version of the season configuration structure.

Initial value: `1`.

This is independent of the season number.

### `timeZone`

The named time zone used for the season calendar and human-readable dates.

Default: `Europe/Oslo`.

Each timestamp also includes an explicit UTC offset. This makes the exact
transition instant unambiguous.

For the configured dates:

- `+02:00` represents Oslo summer time.
- `+01:00` represents Oslo winter time.

Changing `timeZone` alone does not change the timestamps. When customizing
the schedule, update the timestamps and their offsets to match the chosen
time zone.

### `seasons`

The list of explicitly configured seasons.

Each season must have:

- A unique, permanent ID.
- A display name.
- A start timestamp.
- An exclusive end timestamp.
- A list of unlocked generations.

Keep the list in chronological order for readability.

### `seasons[].id`

The permanent identifier stored with catches and seasonal progress.

Examples: `season-1`, `season-2`.

Do not rename an ID after catches have been recorded against it.
Changing an ID could separate existing records from their season.

### `seasons[].name`

The human-readable season name.

Examples: `Season 1`, `Season 2`.

This can be changed without changing the season's identity.

### `seasons[].startsAt`

The instant the season becomes active, including that instant.

The initial Season 1 start date is provisional. Set it to the intended
launch date before accepting real catches.

### `seasons[].endsAtExclusive`

The instant the season stops being active.

A season is active when:

```text
startsAt <= current time < endsAtExclusive
```

For example:

```text
Season 1 ends:   2027-01-01T00:00:00+01:00
Season 2 starts: 2027-01-01T00:00:00+01:00
```

A redemption accepted exactly at this timestamp belongs to Season 2.

Using the next season's start as the previous season's exclusive end
avoids gaps and overlapping boundary timestamps.

### `seasons[].unlockedGenerations`

The generations normally available during the season.

Examples:

```json
[1]
```

Allows Generation 1.

```json
[1, 2]
```

Allows Generations 1 and 2.

Each season lists its full set of unlocked generations explicitly.
The game does not infer unlocks from the season number or automatically
inherit the previous season's list.

Generation availability uses the included form's introduction generation,
not just the original species' generation.

For example, unlocking Generation 1 does not normally unlock Alolan forms.

Active events can temporarily allow selected forms from locked generations,
subject to the event rules.

Legendary and Mythical Pokémon require their generation to be unlocked,
even when they match an event.

Costumes remain event-only regardless of generation unlocks.

### Initial schedule

| Season   | Start                         | End, exclusive  | Generations |
| -------- | ----------------------------- | --------------- | ----------- |
| Season 1 | October 3, 2026 — provisional | January 1, 2027 | 1           |
| Season 2 | January 1, 2027               | January 1, 2028 | 1–2         |
| Season 3 | January 1, 2028               | January 1, 2029 | 1–3         |

All boundaries use local midnight in Europe/Oslo.

Season 1 is a shortened launch season. Later configured seasons cover
a full calendar year.

### Season transitions

At the start of a new season:

- The viewer begins a fresh Seasonal Dex.
- The viewer's seasonal retry streak starts at zero.
- Previous seasons remain available as archives.
- National Dex catch history and totals remain intact.
- Lifetime unique-entry and shiny bonuses remain intact.

A season transition must not delete or overwrite old catches.

Seasonal collections and retry streaks must be associated with their
season ID, so old progress can remain stored without carrying forward.

The per-viewer redemption cooldown is independent of the season and
does not reset at a season boundary.

### Redemptions crossing a season boundary

Use the acceptance timestamp to select the redemption's season.

All attempts within that redemption use the same season, even if processing
finishes after the next season begins.

Record this season ID with the redemption so retries after a technical
error cannot move it into a different season.

Any resulting catch or retry-streak change belongs to that recorded season.

### Adding future seasons

Before the final configured season ends:

1. Add a new season with a new permanent ID.
2. Set its start to the previous season's exclusive end.
3. Set its exclusive end.
4. List every generation that should be unlocked.
5. Validate the updated schedule.

Keep previous season definitions so archived records retain their context.

New seasons are not created automatically after the configured schedule ends.

### Validation and missing seasons

Before accepting redemptions, validate that:

- Season IDs are non-empty and unique.
- Display names are non-empty.
- Timestamps are valid and include explicit UTC offsets.
- Timestamp offsets match the configured time zone at those dates.
- Each start is earlier than its exclusive end.
- Seasons do not overlap.
- Consecutive seasons meet at the same boundary.
- Generation lists are non-empty and contain no duplicates.
- Generation values are positive integers supported by the catalogue.

If no season is active, catching is unavailable. Do not silently fall back
to an old season or invent a new one.

The integration should pause the reward where possible and clearly log the
problem. Any redemption that still arrives follows the documented
no-refund policy and does not alter the viewer's retry streak.

## Tutorial: Customize your seasons

You can change season names, dates, lengths, and available generations.
Seasons do not have to last a year.

Make these changes in `config/seasons.json`.

### 1. Choose your schedule

Decide:

- When your first season starts.
- How long each season lasts.
- Which generations each season allows.
- Which time zone you use.

For example, you could have:

- One season per calendar year.
- A new season every three months.
- Seasons starting on your streaming anniversary.

The dates control season length. There is no separate duration setting.

### 2. Set your time zone

Find this property near the top of the file:

```json
"timeZone": "Europe/Oslo"
```

Replace it with the appropriate named time zone if necessary, such as:

- `Europe/London`
- `America/New_York`
- `Asia/Singapore`

Keep this property inside double quotes.

The timestamps must also use the correct UTC offset for that time zone
on each date. Changing the time-zone name does not automatically rewrite
the timestamps.

### 3. Give the season an ID and a name

Each object inside the `seasons` array represents one season.

```json
"id": "season-1",
"name": "Season 1"
```

The ID links saved catches to their season. Choose it before launching
and keep it unchanged once catches have been recorded.

The name is the display label. You can customize it:

```json
"id": "season-1",
"name": "The Kanto Adventure"
```

Changing the display name does not reset progress or create a new season.

To start a fresh Seasonal Dex, add a new season with a new ID.

### 4. Set when the season starts and ends

Example:

```json
"startsAt": "2027-01-01T00:00:00+01:00",
"endsAtExclusive": "2027-04-01T00:00:00+02:00"
```

This season runs from January 1 through March 31 in Europe/Oslo.

The timestamp format is:

```text
YYYY-MM-DDTHH:mm:ss±HH:mm
```

Its parts are:

- `2027-01-01`: the date, written as year-month-day.
- `T`: separates the date from the time.
- `00:00:00`: midnight, using a 24-hour clock.
- `+01:00`: the UTC offset for this date.

The example uses different offsets because Oslo is on winter time in
January and summer time in April.

`endsAtExclusive` is the first instant that no longer belongs to the
season. To include all of March 31, use midnight on April 1.

Do not use `23:59:59` as the end of the season.

### 5. Choose the available generations

Edit `unlockedGenerations`:

| Value       | Available generations   |
| ----------- | ----------------------- |
| `[1]`       | Generation 1            |
| `[1, 2]`    | Generations 1 and 2     |
| `[1, 2, 3]` | Generations 1, 2, and 3 |
| `[3]`       | Generation 3 only       |
| `[1, 3]`    | Generations 1 and 3     |

Each season has its own complete list.

For example, Season 2 does not automatically include Generation 1.
You must include `1` in its list if you want that generation available.

Only use generations supported by your Pokémon catalogue.

Regional and other forms use their own introduction generation.
Costumes still require an active event.

### 6. Add the next season

Copy an existing season object and paste it inside the `seasons` array.

Then:

1. Give it a new unique ID.
2. Set its display name.
3. Set its start to exactly the previous season's end.
4. Choose its new end date.
5. Set its available generations.

Separate season objects with a comma. Do not put a comma after the
last object in the array.

### Complete example: Two three-month seasons

This example replaces the entire contents of `config/seasons.json`.
It demonstrates a different schedule; it is not the project's default.

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

The first season covers January through March.
The second season covers April through June.

At midnight on April 1, new accepted redemptions belong to Season 2.

A third season must be configured before July 1 to continue catching
without a break.

### 7. Check your changes

Before using your schedule, confirm:

- Every season has a different ID.
- Each end is later than its start.
- Each following season starts exactly when the previous one ends.
- UTC offsets are correct for the chosen dates and time zone.
- Generation lists contain everything you intend to allow.
- The file has no missing commas, extra commas, or comments.

The configuration loader will also need to validate these rules.
Runtime reload instructions will be documented when that loader is
implemented.

### Changing a schedule after launch

Back up your configuration and player data before changing a live schedule.

- You may rename a season without changing its ID.
- You may extend the current season by moving its end and the next
  season's start to the same new timestamp.
- Avoid changing boundaries that have already passed.
- Do not rename or reuse IDs belonging to recorded seasons.
- Keep old season objects so archived catches retain their context.

Changing generation availability affects future encounters. It must
not delete Pokémon viewers have already caught.

Editing the configuration does not move existing catches between seasons.
Their recorded season IDs remain unchanged.

## Event configuration

`config/events.json` defines scheduled events and their themed Pokémon.

Events can temporarily expand the available roster without changing
the season's permanent generation unlocks.

Creating this file alone does not activate an event in Streamer.bot.
The event loader and encounter logic still need to be implemented.

### `schemaVersion`

The version of the event configuration structure.

Initial value: `1`.

### `timeZone`

The named time zone used for the event calendar.

Default: `Europe/Oslo`.

As with seasons, timestamps include explicit UTC offsets. Their offsets
must match the chosen time zone on those dates.

### `events`

The list of configured events.

Each event has its own identity, schedule, settings, and roster selectors.

An event is active when:

```text
enabled is true
AND startsAt <= redemption acceptance time < endsAtExclusive
```

Use the same accepted event settings throughout a redemption, even if
the event ends while its attempts are being processed.

### `events[].id`

The permanent identifier recorded with event-related catches.

Example: `halloween-2026`.

Use a new ID for each scheduled occurrence, such as `halloween-2027`.

Do not rename an ID after catches have been recorded against it.
Keep past event definitions so catch history retains its context.

### `events[].name`

The event's display name.

Example: `Halloween 2026`.

You can rename the display label without changing its ID.

### `events[].enabled`

Whether the event is allowed to activate during its schedule.

- `true`: follow the configured dates.
- `false`: keep the event disabled.

Enabling an event does not override its dates.

Disabling an event does not delete previously caught Pokémon.

### `events[].startsAt`

The instant the event begins, including that instant.

### `events[].endsAtExclusive`

The first instant that no longer belongs to the event.

The initial Halloween event runs through all of October:

```text
Start: October 1, 2026 at midnight
End:   November 1, 2026 at midnight, exclusive
```

The timestamps use different offsets because Oslo changes from summer
time to winter time during October.

Dates are explicit. The event does not automatically repeat next year.

### `events[].categoryWeight`

The relative weight of the Event encounter category.

Default for Halloween: `4`.

Each eligible ordinary type has the weight configured in
`config/game.json`, initially `1`.

With 18 eligible ordinary types:

```text
Total weight = 18 + 4 = 22
Event category probability = 4 / 22
```

This is not a flat 4% probability or a guaranteed fourfold increase
for every event Pokémon.

Event Pokémon also appear in their eligible ordinary type categories.

If `categoryWeight` is omitted, use the default Event category weight
from `config/game.json`.

Use a finite number greater than zero. To disable an event, set
`enabled` to `false` instead of setting its weight to zero.

### Overlapping events

For the initial implementation, enabled event schedules must not overlap.

This keeps one Event category and one event weight active at a time.
The loader must reject overlapping enabled schedules with a clear error.

Supporting simultaneous events would require an additional rule for
combining their rosters and category weights.

### Generation settings

These settings govern the event roster. They do not remove Pokémon
already available through the season.

#### `allowLockedGenerations`

Default: `true`.

Allows selected event Pokémon from generations not normally unlocked
in the current season.

Set this to `false` to restrict the event roster to unlocked generations.

#### `requireUnlockedGenerationForLegendary`

Default: `true`.

A Legendary Pokémon must belong to an unlocked generation to enter the
event roster, even if it matches a selector.

An already-unlocked Legendary can still match the event and appear
in the Event category.

#### `requireUnlockedGenerationForMythical`

Default: `true`.

Applies the same restriction to Mythical Pokémon.

Setting either restriction to `false` permits matching Pokémon of that
classification from locked generations only when
`allowLockedGenerations` is also `true`.

Generation checks use the included form's introduction generation.

## Selecting event Pokémon

The `include` object supports four ways of selecting Pokémon:

- `types`: match actual Pokémon types.
- `evolutionFamilies`: include complete evolution families.
- `forms`: select individual non-costume forms.
- `costumes`: select specific event-only costumes.

Selectors are combined as a union: matching any selector is enough.
Generation restrictions are applied afterward.

Matching multiple selectors does not create extra copies in the same
encounter category.

An empty selector array selects nothing.

### `include.types`

Includes supported non-costume forms with any listed type.

Example:

```json
"types": ["ghost"]
```

This includes Ghost-type forms, including dual types.

It does not automatically include their non-Ghost evolution relatives.
Use an evolution-family selector when you want the whole family.

Costumes require explicit selection through `include.costumes`.

### `include.evolutionFamilies`

Includes the full evolution family containing each named species.

This includes:

- Earlier evolutions.
- Later evolutions.
- Branching evolutions.
- Supported non-costume forms of those family members.

For example:

```json
"evolutionFamilies": ["cubone"]
```

Includes Cubone and Marowak, including Alolan Marowak.

You only need one species reference per family. Listing both Spinarak
and Ariados would not make that family more common.

Family selection does not automatically include costumes.

### `include.forms`

Includes specific supported non-costume forms without adding their
whole family or other forms.

Example:

```json
"forms": ["morpeko-hangry"]
```

This selects Hangry Mode for the event.

It does not add Full Belly Mode to the event roster. Full Belly Mode
can still be available normally if its generation is unlocked.

### `include.costumes`

Includes individual event-only costumes.

The initial Halloween roster contains all five selected ORAS costumes:

```json
"costumes": [
  "pikachu-rock-star",
  "pikachu-belle",
  "pikachu-pop-star",
  "pikachu-phd",
  "pikachu-libre"
]
```

Pokémon GO-exclusive costumes are outside the initial catalogue scope.

Selecting a costume does not automatically select its evolution family
or unlock its regular form.

A regular variant participates only when it is independently available
through the season or event.

### Gender and shiny variants

Selectors automatically cover all supported indexed genders and both
normal and shiny entries.

Do not duplicate selectors for male, female, normal, or shiny variants.

Every included form and costume may be shiny in this game, even when
its shiny is unavailable in the official games.

Only supported genders are generated. The selected ORAS costumes are
female-only; no male counterparts should be invented.

### Costume selection

Costumes join their Pokémon's encounter group instead of becoming
separate entries in the initial Pokémon selection.

After selecting the group:

1. Perform the applicable gender roll.
2. Give the available regular variant one variant entry.
3. Give each active, incomplete costume one variant entry.
4. Select one variant with equal weights.
5. Apply missing-gender protection if the selected variant is a costume.

For a group with an available regular variant and eight incomplete costumes:

```text
Regular variant: 1 / 9
Any costume:     8 / 9
```

An owned regular variant remains an option and can fail as a duplicate.

An entirely collected costume is removed from variant selection for
the current normal or shiny state.

If a costume has only one missing gender, award that missing gender.
One successful redemption still awards only one entry.

## Tutorial: Customize an event

### 1. Choose the event to edit

Open `config/events.json`.

Find the relevant object inside the `events` array.

To create a new event, copy an existing event object and give the copy
a new unique ID.

### 2. Set its name and dates

Change:

- `id` for a new event.
- `name` for its display label.
- `startsAt` for its beginning.
- `endsAtExclusive` for its ending.

Use the first instant after the event as its exclusive end.

For example, an event ending after October 31 should use midnight
on November 1.

Check UTC offsets for the dates you choose.

### 3. Choose its Pokémon

Edit the four arrays inside `include`.

Examples:

- Keep `"ghost"` in `types` to include Ghost types.
- Remove a family name to stop selecting that full family.
- Add a supported form ID to `forms` for a specific form.
- Add a supported costume ID to `costumes` for an event-only costume.

Use `[]` for a selector you do not need:

```json
"costumes": []
```

Removing a selector only removes that reason for inclusion. A Pokémon
can still match another selector.

For example, removing a family reference does not exclude a Ghost-type
member if `"ghost"` remains selected.

The initial configuration has no explicit exclusion list.

### 4. Choose generation behavior

For the project's default behavior, keep:

```json
"allowLockedGenerations": true,
"requireUnlockedGenerationForLegendary": true,
"requireUnlockedGenerationForMythical": true
```

For an event restricted entirely to unlocked generations, change
`allowLockedGenerations` to `false`.

### 5. Adjust the Event category weight

Change `categoryWeight` to make selection of the Event category more
or less likely relative to ordinary types.

A larger weight increases its share of the category roll.

It does not change shiny probability, rarity-class weights, or costume
variant weights.

### 6. Enable and validate it

Set `enabled` to `true` when the event is ready to follow its schedule.

Before accepting redemptions, validation must check:

- IDs are non-empty and unique.
- Names are non-empty.
- Boolean settings contain `true` or `false`.
- Dates and UTC offsets are valid.
- Each start is earlier than its exclusive end.
- Enabled event schedules do not overlap.
- Category weights are finite and greater than zero.
- Every selector references a supported catalogue identifier.
- Form selectors and costume selectors use the correct category.

Unknown identifiers must produce a clear error rather than silently
omitting Pokémon.

The available identifier reference will be documented when the
catalogue is generated.

## What happens when an event ends?

Event-dependent availability stops for new redemptions.

- Pokémon still eligible through the season remain available.
- Pokémon requiring the event leave the encounter pools.
- Event-only costumes leave variant selection.
- Previously caught entries remain in both Dex records as applicable.
- Historical event information remains attached to catches.

Event activation can reopen a previously completed type category when
it introduces missing entries.

Completion is always checked against the current eligible roster,
separately for normal and shiny entries.

## Event information in catch history

Record:

- The active event ID, if any.
- Whether the caught entry matched the event roster.
- The category used for the encounter.
- The season ID and redemption acceptance timestamp.

An event Pokémon caught through its ordinary type category still
counts as matching the event roster.

A regular Pokémon caught while an event is active is not automatically
an event-roster catch.

December's roster and schedule will be added separately.

## Overlay configuration

`config/catch-overlay.json` controls the planned OBS catch overlay.

The overlay displays successful catches after they have been saved.
It does not control encounter probabilities or collection progress.

The browser overlay and its connection to Streamer.bot are not
implemented yet. This file defines the settings they will use.

### `schemaVersion`

The version of the overlay configuration structure.

Initial value: `1`.

### `enabled`

Whether to show catch notifications.

Default: `true`.

Set this to `false` to disable both overlay visuals and cries.
Catching, saving progress, and chat announcements continue normally.

Notifications received while disabled are not replayed when re-enabled.

## Overlay layout

### `layout.canvasWidth` and `layout.canvasHeight`

The overlay's design dimensions in pixels.

Defaults: `1920` by `1080`.

Use the same dimensions for the OBS Browser Source.
The page background remains transparent.

### `layout.positionX` and `layout.positionY`

The position of the notification within the overlay canvas.

- `positionX` is the horizontal center of the notification.
- `positionY` is the top of the notification.

Coordinates start at the top-left corner of the canvas.

Defaults:

```text
positionX = 960
positionY = 180
```

On a 1920-pixel-wide canvas, `960` centers the notification horizontally.

Increase `positionX` to move it right. Decrease it to move it left.
Increase `positionY` to move it down. Decrease it to move it up.

### `layout.width`

The maximum notification width in pixels.

Default: `720`.

Text is centered and wraps within this width.
Long names must not overflow the notification.

### `layout.textGapPixels`

The vertical space between the username and catch message.

Default: `12`.

### `layout.spriteGapPixels`

The vertical space between the catch message and the sprite's resting
position.

Default: `24`.

The sprite appears below the text. Its entrance animation does not move
the username or catch message.

## Overlay text

### Text templates

The templates control the wording:

```json
"usernameTemplate": "{displayName}",
"catchTemplate": "Caught a {pokemonName}!",
"shinyCatchTemplate": "Caught a shiny {pokemonName}!"
```

Supported placeholders:

| Placeholder     | Meaning                                                                                 |
| --------------- | --------------------------------------------------------------------------------------- |
| `{displayName}` | The viewer's Twitch display name                                                        |
| `{pokemonName}` | The caught Pokémon's display name, including its named form or costume where applicable |

The shiny template is used only for shiny catches.

Example customization:

```json
"catchTemplate": "Added {pokemonName} to the team!"
```

Keep placeholder spelling and capitalization unchanged.

Templates and substituted values must be displayed as plain text,
not interpreted as HTML.

### Font settings

- `text.fontFamily`: CSS font family or fallback list.
- `text.usernameFontSizePixels`: username font size.
- `text.messageFontSizePixels`: catch message font size.

The default font setting is `Arial, sans-serif`.

Changing the font name does not download or bundle a font.
A custom font must be available to the browser overlay.

### Color settings

- `text.usernameColor`: username text color.
- `text.messageColor`: normal catch message color.
- `text.shinyMessageColor`: shiny catch message color.
- `text.shadowColor`: text shadow color.

Use six-digit hexadecimal colors, such as `#FFFFFF`.

`text.shadowBlurPixels` controls how soft the text shadow appears.
Set it to `0` to disable the shadow.

## Sprite settings

### `sprite.sizePixels`

The width and height of the sprite's display box.

Default: `192`.

Fit the image inside this box while preserving its aspect ratio.
Do not stretch the Pokémon to fill the box.

### `sprite.pixelated`

Whether to use crisp pixel-style scaling.

Default: `true`.

Set it to `false` for smooth image scaling.

### `sprite.fallbackLabel`

The visible label used when the correct sprite is unavailable.

Default: `Sprite unavailable`.

Use the caught entry's form, gender, and shiny sprite when available.

Any substitute image must be visibly identified as a fallback.
If no usable image exists, show the label in the sprite area.

A missing image must not prevent the username, catch message, or next
notification from displaying.

## Animation settings

### Entrance timeline

All entrance delays are measured from the start of the notification.

With the defaults:

1. At 0 milliseconds, the username begins its pop-in animation.
2. At 250 milliseconds, the catch message appears.
3. At 500 milliseconds, the sprite starts rising into position.
4. At 1100 milliseconds, the sprite reaches its resting position.
5. The completed notification stays visible for 4000 milliseconds.
6. The notification exits over 400 milliseconds.

The default visual sequence lasts 5500 milliseconds.
The queue then waits 300 milliseconds before starting the next notification.

### `animation.usernameEnterMilliseconds`

The duration of the username's pop-in animation.

Default: `250`.

### `animation.messageDelayMilliseconds`

How long after notification start the catch message appears.

Default: `250`.

### `animation.spriteDelayMilliseconds`

How long after notification start the sprite entrance begins.

Default: `500`.

The cry is triggered once when this entrance begins.

### `animation.spriteRiseMilliseconds`

How long the sprite takes to rise into its resting position.

Default: `600`.

### `animation.spriteRiseDistancePixels`

How far below its resting position the sprite starts.

Default: `120`.

The sprite rises within the notification area. This value does not mean
it starts at the bottom edge of the entire OBS canvas.

Increase it for a longer upward entrance.

### `animation.holdMilliseconds`

How long the fully entered notification remains visible before exiting.

Default: `4000`.

The hold starts after all entrance steps have finished.

Calculate its starting time as:

```text
hold starts at the largest of:

usernameEnterMilliseconds
messageDelayMilliseconds
spriteDelayMilliseconds + spriteRiseMilliseconds
```

### `animation.exitMilliseconds`

The duration of the notification's exit fade.

Default: `400`.

### Bobbing animation

After the sprite finishes rising, it gently moves vertically.

- `animation.bobEnabled`: turns bobbing on or off.
- `animation.bobDistancePixels`: maximum displacement above or below its resting position.
- `animation.bobCycleMilliseconds`: duration of one complete up-and-down cycle.

Defaults:

```text
bobEnabled = true
bobDistancePixels = 6
bobCycleMilliseconds = 1200
```

A distance of `6` produces 12 pixels of total travel between the highest
and lowest positions.

Increase the cycle duration for slower movement.
Decrease the distance for subtler movement.

The text remains stationary.

Bobbing works with a static sprite; an animated image is not required.

## Cry audio

### `audio.enabled`

Whether to play a cry for each displayed catch.

Default: `true`.

Set this to `false` to keep the visual notification without sound.

### `audio.volume`

Playback volume from `0` to `1`.

Examples:

| Value  | Volume |
| ------ | ------ |
| `0`    | Muted  |
| `0.25` | 25%    |
| `0.5`  | 50%    |
| `1`    | 100%   |

Default: `0.5`.

OBS audio settings can further affect the audible volume.

### `audio.preferredCry` and `audio.fallbackCry`

Which catalogue cry references to try.

Defaults:

```json
"preferredCry": "latest",
"fallbackCry": "legacy"
```

Allowed values are `latest` and `legacy`.

Try the preferred reference first. If it is missing or cannot start
playing, try the fallback reference.

If neither works, continue the notification silently.

Play at most one successful cry per notification. Do not loop it.

### `audio.maxPlaybackMilliseconds`

The maximum permitted cry playback duration.

Default: `5000`.

Stop audio at this limit or when the notification finishes,
whichever comes first.

A delayed audio load must not start playing after its notification ends.

## Notification queue

### `queue.gapMilliseconds`

The pause between one notification finishing and the next starting.

Default: `300`.

Display successful catches one at a time in arrival order.
Finish the current notification and stop its audio before starting
the next one.

The overlay queue is separate from game processing.
Other viewers can catch Pokémon while a notification is showing.

Deduplicate notifications using the saved catch identifier so repeated
delivery does not announce the same catch twice.

## Tutorial: Customize the overlay

### 1. Open the configuration

Open `config/catch-overlay.json`.

The examples below show individual settings to edit inside the existing
file. They are not replacements for the entire configuration.

### 2. Move the notification

Edit `layout.positionX` and `layout.positionY`.

For example, on the default 1920-by-1080 canvas:

```json
"positionX": 960,
"positionY": 500
```

This keeps it horizontally centered and places its top farther down.

Leave enough room beneath it for the message, sprite, and entrance motion.

### 3. Change the size

Adjust:

- `text.usernameFontSizePixels` for the viewer's name.
- `text.messageFontSizePixels` for the catch message.
- `sprite.sizePixels` for the Pokémon.
- `layout.width` for text wrapping.

For example, change `sprite.sizePixels` from `192` to `256`
to display a larger Pokémon.

### 4. Change the wording and colors

Edit the text templates and color settings.

Keep `{displayName}` and `{pokemonName}` wherever you want those values
to appear.

Keep an explicit shiny announcement in the shiny template so viewers
can identify a shiny catch without relying on color alone.

### 5. Adjust the movement

For gentler bobbing, edit these settings inside `animation`:

```json
"bobDistancePixels": 3,
"bobCycleMilliseconds": 1800
```

To disable bobbing:

```json
"bobEnabled": false
```

To make the notification stay longer, increase `holdMilliseconds`.
For example, `6000` means six seconds after the entrance completes.

Keep `messageDelayMilliseconds` at or after the username entrance
finishes, and `spriteDelayMilliseconds` at or after the message appears,
to preserve the intended username → message → sprite sequence.

### 6. Adjust the sound

For quieter cries, change this setting inside `audio`:

```json
"volume": 0.25
```

To disable cries entirely, change this setting inside `audio`:

```json
"enabled": false
```

The top-level `enabled` setting has a different purpose:
it disables the entire overlay.

### 7. Check the settings

Before loading the overlay, validate that:

- Boolean settings contain `true` or `false`.
- Canvas dimensions, notification width, sprite size, and font sizes are positive.
- Positions fit within the configured canvas.
- Gaps, distances, delays, and animation durations are non-negative.
- Pixel and millisecond values are finite whole numbers.
- The bob cycle is greater than zero when bobbing is enabled.
- Audio volume is a finite number between `0` and `1`.
- Maximum audio playback duration is greater than zero.
- Cry preferences use supported values.
- Colors use valid six-digit hexadecimal values.
- Templates contain only supported placeholders.

Check the result visually for clipping, wrapping, and readability.

Preview and reload instructions will be added when the browser overlay
is implemented.

## Overlay failures

A catch must be saved before sending its notification.

An overlay connection failure, missing sprite, or failed cry does not
undo the catch or change the viewer's retry streak.

Log display failures separately from gameplay failures.
