# Configuration Guide

PokéHunter Seasons separates editable settings from the catching logic.

This document describes the intended behavior of `config/game.json`.
The configuration loader and game logic are not implemented yet.
Creating or editing this file alone does not change Streamer.bot or Twitch.

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
