# Game Rules

This is the intended gameplay contract. Rules below include behavior not yet implemented;
see the [README](../README.md) for status and [Configuration](CONFIGURATION.md) for settings.

## Redemption and collection basics

| Rule                | Behavior                                                                                          |
| ------------------- | ------------------------------------------------------------------------------------------------- |
| Cost                | 500 channel points covers every attempt in one redemption                                         |
| Catch limit         | At most one successful catch per redemption                                                       |
| Cooldown            | 60 seconds per viewer, beginning at acceptance; other viewers can redeem                          |
| Usage limit         | No per-stream limit beyond the per-viewer cooldown                                                |
| Refunds             | No automatic refunds, including duplicates, empty pools, cooldown rejections, or technical errors |
| Seasonal uniqueness | Each collectible entry can be caught once per season                                              |
| Lifetime history    | Every successful catch remains recorded across seasons                                            |

Use Twitch user IDs for identity and cooldowns. Renaming a viewer must not reset progress.
A cooldown rejection performs no rolls, does not alter the streak, and does not extend
cooldown. Explain the cooldown and no-refund policy in the reward description.

National Dex totals distinguish total catches from unique entries. Catching the same
normal Abra in Seasons 1, 3, and 5 means three catches but one lifetime unique entry.
Failed duplicates do not count. A dual-type catch contributes to both type totals, so
summed type totals can exceed total catches. Default chat progress is seasonal;
National totals are for the later showcase/leaderboard.

## Collectible identity

Normal/shiny states, supported forms, costumes, and visible gender differences count
separately. Regular Pikachu has male/female × normal/shiny entries. Pokémon without
supported visible gender differences share a gender identity; single-gender forms
retain only their supported gender. Never invent a counterpart.

Every included form/gender has a shiny entry, even if officially shiny-locked. Missing
artwork does not remove an entry; any substitute must be clearly identified as a fallback.
Forms unlock using their own introduction generation, not their species' debut.

Distinct non-costume forms have separate encounter groups. Gender and shiny counterparts
share a group; costumes share the underlying Pokémon group. See [Data Model](DATA_MODEL.md).

## Encounter sequence

At acceptance, capture season/event settings, seasonal ownership, retry attempts,
lifetime unique counts, and the current subscription multiplier. Each redemption uses
one consistent snapshot throughout processing, even across a schedule boundary.

For each attempt:

1. Roll shiny unless protection already makes the attempt shiny.
2. Build eligible categories for that shiny state; exclude empty and completed categories.
3. Select a category by its weight.
4. Select a Pokémon encounter group by its rarity weight.
5. Flip a fair gender coin when both indexed genders are supported.
6. Select the available regular variant or an incomplete eligible costume with equal weights.
7. Apply costume gender protection, then check seasonal ownership.
8. Save one new catch and end the redemption, or continue after a failed attempt.

Variant selection stays inside the selected category's eligible entries. For example,
if only costumes qualify for the Event category, that category cannot award regular
Pikachu. The Electric category can include regular Pikachu when it is independently eligible.

### Category and group weights

Each populated ordinary type category has weight 1 by default. An active, nonempty Event
category has its configured weight, initially 4. Event Pokémon also join their actual types.
Multiple matching event selectors do not duplicate a group within a category.

With all 18 types and Event available, total category weight is 22: each type has 1/22
and Event has 4/22. Empty/completed categories change that denominator. Event weight is
not a fixed percentage or a direct multiplier on every event Pokémon's final odds.

| Group rarity | Default weight |
| ------------ | -------------: |
| common       |             10 |
| uncommon     |              6 |
| rare         |              3 |
| ultraRare    |              1 |

Select individual groups using their weights; do not roll a rarity class first.
With one group of each class, their probabilities are 50%, 30%, 15%, and 5%.
Different group counts change those probabilities. There is no BST roll.
Extra genders or costumes do not give a group extra tickets.

### Completion and duplicates

Category completion uses currently obtainable entries in the Seasonal Dex, separately
for normal and shiny states. A new event can reopen a previously completed type.
If one type is complete, a dual-type Pokémon can still appear through its other category.

Do not remove all owned regular entries from incomplete categories. They can still
produce duplicates. If no eligible category remains for the rolled state, the attempt
fails. Completing the normal collection never forces a shiny result. The same failure
rule applies when both collections are complete.

### Costume variants and gender

Within the selected category, the eligible regular variant and each incomplete costume
receive equal variant weight. Regular plus eight costumes means 1/9 for regular and
8/9 for any costume combined. The regular variant remains a possible duplicate.

For a selected costume:

- Both supported genders missing: retain the fair coin result.
- One supported gender missing: award that gender regardless of the initial coin.
- Only one supported gender exists: use it.
- All supported genders owned for this shiny state: remove that costume option.

Award at most one entry, never both genders. Normal costume completion does not remove
its shiny option. Costumes require explicit inclusion in an active event, regardless of
whether their generation is unlocked. The five ORAS costumes are female-only.

## Shiny progression

Calculate probability once per redemption. Let N be lifetime unique normal entries,
S lifetime unique shiny entries, and T the current subscription multiplier.

```text
combinedMilestones = floor((N + S) / 50)
shinyMilestones = floor(S / 10)
collectionMultiplier = 1 + combinedMilestones * 0.10 + shinyMilestones * 0.05
shinyProbability = min(1, collectionMultiplier * T / 8192)
```

Bonuses are additive, not compounded. Only full milestones count. A shiny contributes
both to N+S and to the extra shiny bonus. Recatching an entry in another season gives
no additional unique bonus.

| Subscription   | Multiplier |
| -------------- | ---------: |
| None           |          1 |
| Prime / Tier 1 |          2 |
| Tier 2         |        2.5 |
| Tier 3         |        3.5 |

Apply only the applicable tier, after collection bonuses. At N=100, S=100 and Tier 1,
the multiplier is `(1 + 0.40 + 0.50) * 2 = 3.80`: approximately 1 in 2,156 per shiny roll.

Once an attempt rolls shiny, every remaining attempt in that redemption stays shiny.
Re-select categories and Pokémon on subsequent attempts; duplicates remain possible.
Protection ends with the redemption and never carries to the next one.

## Retry streaks

```text
availableAttempts = 1 + consecutiveFullyFailedRedemptions
```

| Outcome                                 | Effect                                                              |
| --------------------------------------- | ------------------------------------------------------------------- |
| New seasonal entry                      | Save one catch, stop, reset streak to zero, discard unused attempts |
| All attempts fail                       | No catch; increment streak once for the redemption                  |
| Technical failure                       | Log separately; do not silently count it as a gameplay failure      |
| Repeated delivery of a saved redemption | Reuse the stored outcome; no fresh rolls or second streak change    |

Example: with eight attempts, normal duplicates on 1–2 and a shiny duplicate on 3
make attempts 4–8 shiny. A new shiny on 5 ends the redemption; 6–8 are unused.
The next redemption has one attempt and the usual shiny probability.

## Seasons

The default schedule uses Europe/Oslo and cumulative unlocks:

| Season | Start                                            | Exclusive end   | Generations |
| ------ | ------------------------------------------------ | --------------- | ----------- |
| 1      | October 3, 2026 (configured launch-season start) | January 1, 2027 | 1           |
| 2      | January 1, 2027                                  | January 1, 2028 | 1–2         |
| 3      | January 1, 2028                                  | January 1, 2029 | 1–3         |

Review the initial launch date before live use. Custom seasons need not be annual.
Starts are inclusive; ends exclusive. Consecutive seasons must meet without gaps or
overlaps. Future seasons are explicit, not automatically created.

New seasons start fresh seasonal ownership and retry streaks. Preserve archived catches,
National totals, and unique bonuses. Cooldown does not reset at a season boundary.
A redemption belongs to the season selected at acceptance, including recovery after errors.

## Events

One enabled event can be active at a time. Event gaps and an empty schedule are valid.
Enabled events cannot overlap. Disabling an event does not erase previous catches.
Dates are explicit; events do not automatically repeat next year.

Selectors combine matching supported entries without duplication:

- Types select non-costume forms with that type, including dual types.
- Families select all represented stages, branches, and non-costume forms in the family.
- Forms select specific non-costume forms without adding unrelated forms/family members.
- Costumes require explicit costume selectors.

By default events may unlock matching later-generation entries, except Legendary and
Mythical entries whose form introduction generation remains locked. Already-unlocked
Legendary/Mythical entries remain available and may join a matching Event category.
The restriction flags are configurable for other creators; this is the project's policy.

When the event ends, entries depending on it leave availability; otherwise-unlocked
entries remain. Preserve every catch. Future history should record season, time,
active event, roster membership, and selected encounter category. An event-roster catch
can come through an ordinary type category.

### Initial Halloween roster

October 1 through October 31, 2026; exclusive end November 1 at Oslo midnight.

- All supported Ghost-type entries, subject to generation restrictions above.
- Full families selected by: houndoom, zoroark, absol, hatterene, mawile, grimmsnarl,
  spinarak, cubone, murkrow, gothitelle, dipplin, swirlix, delphox, sableye.
- `morpeko-hangry` specifically; this does not add Full Belly Mode to the event exception.
- `pikachu-rock-star`, `pikachu-belle`, `pikachu-pop-star`, `pikachu-phd`, `pikachu-libre`.

Ghost typing alone does not add non-Ghost evolution relatives. Family selectors cover
supported represented forms, including Alolan Marowak. GO-exclusive costumes are excluded,
including witch, Mimikyu, Charizard, Umbreon, Rayquaza, and Lucario-themed Pikachu costumes.
December and later rosters are deferred.

## Saved results and presentation

Production catches must be committed before chat or overlay announcements. Duplicate
redemption deliveries must not award extra catches. An overlay/media failure never
rerolls, removes a saved catch, or changes the streak.

The intended notification shows avatar/name, catch text (explicitly identifying shiny),
then the matching sprite rising and gently bobbing, with one cry. Missing media must
not block later notifications. Queue display independently of game processing.
Production delivery needs deduplication; the current preview does not implement it.
See [Catch Overlay](CATCH_OVERLAY.md) for actual setup, behavior, and limits.
