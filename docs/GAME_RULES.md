# Game Rules

This document defines the intended behaviour of PokéHunter Seasons.
It serves as a reference for implementation, testing, and customization.

These rules describe planned functionality and do not indicate that a
feature has already been implemented.

## Seasonal and National Pokédex

### Seasonal Pokédex

Each viewer has a separate collection for each season.

A viewer can catch each distinct collectible entry once per season.
Normal and shiny Pokémon count as separate entries, as do supported
forms, costumes, and visible gender differences.

Encountering an entry already owned in the current season causes that
attempt to fail.

Ownership in an earlier season does not prevent the same entry from
being caught again in a later season.

### National Pokédex

The National Pokédex preserves successful catches across all seasons.

It tracks:

- Total successful catches.
- Unique normal entries.
- Unique shiny entries.
- How many times each entry has been caught.
- The seasons and timestamps associated with those catches.
- Total successful catches associated with each Pokémon type.

For example, catching the same normal Abra in Seasons 1, 3, and 5
produces three lifetime catches but only one unique National Pokédex
entry.

Type totals include successful catches across all seasons.

A dual-type Pokémon contributes one catch to each of its types.
As a result, the sum of type totals can exceed the overall catch total.

Failed duplicate encounters do not increase these totals.

Shiny progression bonuses use unique lifetime entries. Recatching an
entry in another season does not increase those bonuses.

### Archived seasons

Previous seasonal collections remain available as historical records.
Starting a new season does not delete earlier catches or National
Pokédex progress.

### Chat messages

Default catch messages display current seasonal progress.

National Pokédex totals are reserved for the future collection
showcase and leaderboard.

## Pokémon Forms, Genders, and Costumes

### Separate collectible entries

Supported forms, costumes, visible gender differences, and shiny
variants count as separate collectible entries.

For example, standard Pikachu has four entries:

- Normal male Pikachu.
- Normal female Pikachu.
- Shiny male Pikachu.
- Shiny female Pikachu.

Each entry can be caught once per season and contributes once toward
unique National Pokédex progress.

Pokémon without a visible gender difference are not split into
separate male and female collection entries.

Forms or costumes restricted to one gender do not receive an
invented counterpart.

### Regional and other forms

Regional and other supported forms are distinct collectibles.

For example, Ninetales and Alolan Ninetales have separate normal
and shiny entries.

A form unlocks according to the generation in which that form was
introduced, rather than the debut generation of its original species.
An eligible event can temporarily unlock it earlier.

Regional and other non-costume forms use their own encounter groups
and actual Pokémon types. Gender counterparts share an encounter
group and are resolved through the gender roll.

### Shiny availability

Every included collectible supports a shiny version, even if that
version cannot normally be obtained in the official Pokémon games.

Missing shiny artwork does not prevent a shiny catch. Any displayed
fallback artwork must be clearly identified as a fallback.

### Event costumes

Costumes are only obtainable during an active event that includes
them. Unlocking their debut generation does not make them available
outside events.

Costumes are selected after their Pokémon encounter group has been
chosen. They do not each receive a separate entry in the initial
Pokémon selection.

Pokémon GO exclusive costumes are excluded from the initial scope.
All five ORAS Cosplay Pikachu outfits are included in Halloween.

### Gender selection

After selecting a Pokémon encounter group, flip a fair coin when
both male and female collectible variants are supported.

This gives each gender a 50% chance, regardless of the species'
gender ratio in the official games.

Then select between the regular variant and eligible costumes.

For a regular Pokémon, retain the gender result. If that entry is
already owned in the current season, the attempt fails.

### Costume selection

The regular variant and each incomplete active costume receive
equal weight in the variant selection.

For example, regular Pikachu plus eight incomplete costumes creates
nine equally likely options:

- Regular Pikachu: 1/9.
- Each individual costume: 1/9.
- Any costume combined: 8/9.

The regular variant remains an option even when its gender entries
are already owned.

For costumes:

- If both supported genders are missing, retain the gender coin flip.
- If only one supported gender is missing, award that gender,
  regardless of the initial coin flip.
- If every supported gender is owned, remove that costume from the
  variant pool.
- If the costume only supports one gender, use that gender and
  remove the costume once its entry is owned.

Selecting a costume awards at most one Pokémon. Automatically
selecting a missing gender does not award both genders.

### Normal and shiny costume completion

Costume completion is checked separately for normal and shiny entries,
using the current season's collection.

Completing a normal costume does not remove its shiny version from
shiny encounters.

Only active event costumes are considered. Costumes from inactive
events cannot enter the variant selection.

### Interaction with category completion

Owning every entry for one Pokémon does not automatically remove its
regular encounter group from an otherwise incomplete type category.
It can still produce a duplicate.

However, a type category is removed when every currently obtainable
entry within it is owned for the rolled normal or shiny variant.

An event can reopen a completed category by introducing missing forms,
genders, or costumes.

## Encounter Sequence and Weight Classes

### Preparing a redemption

Before rolling encounters, the system determines:

- The current season and its unlocked generations.
- Any active events and their eligible Pokémon.
- The viewer's current seasonal collection.
- Their available attempts, based on consecutive failed redemptions.
- Their shiny probability, including lifetime collection bonuses
  and their current subscription tier.

Each redemption can award a maximum of one Pokémon.

### Encounter sequence

Each available attempt follows this sequence:

1. Roll whether the encounter is shiny, unless shiny protection
   has already activated during this redemption.
2. Build the eligible encounter categories for that normal or
   shiny variant.
3. Select an encounter category using category weights.
4. Select a Pokémon encounter group using its weight class.
5. Roll gender where separate male and female entries exist.
6. Select the regular variant or an eligible incomplete costume.
7. Apply the costume missing-gender rule where applicable.
8. Check whether the resulting entry is already owned this season.
9. Save a new catch and end the redemption, or continue to the
   next available attempt if the attempt fails.

A successful catch resets the consecutive failure streak.
Unused attempts do not carry over.

### Encounter categories

The category pool contains eligible Pokémon types and an additional
Event category while an event is active.

Each ordinary type has a default weight of 1.
The Event category has a default weight of 4.

Categories containing no currently obtainable entries are excluded.

A category is also excluded when the viewer owns every currently
obtainable entry within it for the rolled normal or shiny variant.

Completing a normal category does not remove that category from
shiny encounters.

### Category weight example

With all 18 ordinary types eligible and one active Event category:

- Total category weight: 18 + 4 = 22.
- Each ordinary type has a 1/22 chance.
- The Event category has a 4/22 chance.

These probabilities change when categories are empty or completed.
The event weight is relative to other categories, not a fixed
percentage or a direct multiplier on each Pokémon's final chance.

### Event Pokémon and ordinary types

Event Pokémon are included in both the Event category and their
actual type categories.

Each encounter group appears only once within each category,
regardless of how many event selection rules it matches.

A dual-type Pokémon can be selected through either of its types.
If one type category is completed and removed, the Pokémon can
still appear through its other eligible category.

### Pokémon weight classes

After selecting a category, the system selects a Pokémon encounter
group using its assigned weight class.

The initial configurable weights are:

| Weight class | Weight |
| ------------ | -----: |
| Common       |     10 |
| Uncommon     |      6 |
| Rare         |      3 |
| Ultra Rare   |      1 |

These values are relative weights, not fixed percentages.

An individual Common encounter group has ten times the selection
weight of an individual Ultra Rare group in the same category.

Its actual probability depends on the combined weights of all
encounter groups within that category.

### Weight example

If a category contains one encounter group from each class:

- Total weight: 10 + 6 + 3 + 1 = 20.
- Common: 10/20 = 50%.
- Uncommon: 6/20 = 30%.
- Rare: 3/20 = 15%.
- Ultra Rare: 1/20 = 5%.

Adding more Pokémon changes these percentages.

### Encounter groups and variants

Supported gender counterparts share an encounter group.
Costumes are selected after their Pokémon encounter group.

Having additional genders or costumes does not give a Pokémon
additional entries in the initial Pokémon selection.

Regional and other non-costume forms use their own encounter groups
and their actual types.

There is no separate base-stat-total (BST) roll in the encounter
sequence.

### Duplicate encounters and empty pools

Owned regular entries remain possible within incomplete categories.
They are not automatically filtered out to guarantee a new catch.

If no eligible category exists for the rolled normal or shiny
variant, that attempt fails.

An empty normal pool never forces a shiny encounter.

If every attempt fails, the redemption ends unsuccessfully and
the consecutive failure streak increases by one.

## Shiny Odds and Progression

### Base shiny probability

Each attempt that requires a shiny roll begins with a base shiny
probability of 1 in 8,192, before collection bonuses and
subscription multipliers.

Shiny probability is calculated at the start of each redemption.

### Unique collection bonuses

Every 50 unique National Pokédex entries adds 10% of the base
shiny probability.

Normal and shiny entries both contribute to this milestone.

Every 10 unique shiny entries adds another 5% of the base
shiny probability.

A shiny entry therefore contributes toward both the combined
collection milestone and the additional shiny milestone.

Only completed milestones count. Recatching an entry in another
season does not increase either bonus.

### Subscription multipliers

| Subscription            | Shiny probability multiplier |
| ----------------------- | ---------------------------: |
| Non-subscriber          |                            1 |
| Tier 1, including Prime |                            2 |
| Tier 2                  |                          2.5 |
| Tier 3                  |                          3.5 |

The viewer's current subscription multiplier is applied after
the collection bonuses have been added together.

### Shiny formula

Let:

- N = unique normal National Pokédex entries.
- S = unique shiny National Pokédex entries.
- T = the viewer's subscription multiplier.
- floor = round down to the nearest whole number.

The calculation is:

    combinedMilestones = floor((N + S) / 50)
    shinyMilestones = floor(S / 10)

    collectionMultiplier =
        1
        + (combinedMilestones × 0.10)
        + (shinyMilestones × 0.05)

    shinyProbability =
        (1 / 8192) × collectionMultiplier × T

The collection bonuses are additive, not compounded.

For custom configurations, probability must never exceed 100%.

### Calculation example

A viewer has 100 unique normal entries and 100 unique shiny entries.

- Combined entries: 200.
- Combined milestones: 200 / 50 = 4.
- Combined-entry bonus: 4 × 10% = 40%.
- Shiny milestones: 100 / 10 = 10.
- Additional shiny bonus: 10 × 5% = 50%.
- Collection multiplier: 1 + 0.40 + 0.50 = 1.90.

With a Tier 1 subscription:

    shinyProbability = (1 / 8192) × 1.90 × 2
                     = 3.80 / 8192

This is approximately a 1 in 2,156 chance per shiny roll.

### Shiny protection within a redemption

Once an attempt rolls shiny, every remaining attempt within that
redemption also uses the shiny variant.

If the shiny encounter is a duplicate, the next attempt remains
shiny but selects its category and Pokémon again.

Shiny protection does not guarantee a new catch. Further shiny
duplicates can still cause attempts to fail.

The protection ends when the redemption ends. It never carries
over to the next redemption.

### Completed normal collections

Owning every currently available normal entry does not guarantee
a shiny encounter or grant a separate completion bonus.

Each attempt still performs its shiny roll unless shiny protection
has already activated within the current redemption.

When protection is active, remaining attempts stay shiny.
Completing the normal collection does not activate this protection.

If the result is normal and no normal entries remain obtainable,
that attempt fails. Any remaining attempts continue normally.

The same failure behaviour applies when both normal and shiny
collections are complete.

## Duplicate Failures and Retry Streaks

### Attempts per redemption

A viewer begins with one attempt per redemption.

Every consecutive fully unsuccessful redemption adds one additional
attempt to their next redemption:

    availableAttempts = 1 + consecutiveFailedRedemptions

| Consecutive failed redemptions | Attempts on next redemption |
| ------------------------------ | --------------------------: |
| 0                              |                           1 |
| 1                              |                           2 |
| 2                              |                           3 |
| 3                              |                           4 |
| 7                              |                           8 |

The streak increases once per fully failed redemption, not once
per failed attempt.

### Successful redemption

When an attempt produces a new seasonal entry:

- Award and record that one Pokémon.
- End the redemption immediately.
- Reset the consecutive failure streak to zero.
- Discard any unused attempts.

The next redemption begins with one attempt.

### Unsuccessful redemption

If every available attempt fails:

- Award no Pokémon.
- Increase the consecutive failure streak by one.
- Give the viewer one more attempt on their next redemption.
- Do not refund the channel points.

A failed attempt does not count as a successful catch or contribute
toward National Pokédex bonuses.

### Example with shiny protection

A viewer enters a redemption with eight available attempts:

| Attempt | Result                                     |
| ------- | ------------------------------------------ |
| 1       | Normal duplicate; continue                 |
| 2       | Normal duplicate; continue                 |
| 3       | Shiny duplicate; activate shiny protection |
| 4       | Shiny duplicate; continue                  |
| 5       | New shiny entry; catch succeeds            |
| 6–8     | Unused because the redemption has ended    |

The viewer receives one shiny Pokémon.

Their failure streak resets to zero, and their next redemption
starts with one attempt using their usual calculated shiny
probability.

### Gameplay failures and technical errors

Duplicate encounters and empty eligible pools are gameplay failures.

Invalid configuration, unavailable services, or failed data saves
are technical errors. They must not be presented as ordinary
unsuccessful encounters or silently increase the failure streak.

Technical errors must be logged for investigation. They do not
trigger automatic refunds.

Processing the same redemption again must not award an additional
catch or increase the streak a second time if its result was
already saved.

## Seasons and Generation Unlocks

### Season schedule

Seasons follow calendar years, with a shorter first season ending
on December 31, 2026.

Season boundaries use the Europe/Oslo timezone.

The initial schedule is:

| Season | Period                           | Normally available generations |
| ------ | -------------------------------- | ------------------------------ |
| 1      | Launch through December 31, 2026 | Generation 1                   |
| 2      | January 1–December 31, 2027      | Generations 1–2                |
| 3      | January 1–December 31, 2028      | Generations 1–3                |

Generation unlocks are cumulative. Introducing a new generation
does not remove Pokémon from earlier generations.

Season dates and unlocked generations must be configurable.

### Starting a new season

At the start of a new season:

- Create a fresh Seasonal Pokédex for each participating viewer.
- Reset their seasonal consecutive failure streak to zero.
- Preserve all previous seasonal collections as archives.
- Preserve National Pokédex history and unique-entry bonuses.

A viewer can catch an entry again in the new season, even if they
caught it in an earlier season.

### Form generation restrictions

Forms unlock according to their own debut generation.

A Generation 1 species does not automatically unlock regional or
other forms introduced in later generations.

Costumes remain restricted to their assigned events, even after
their debut generation has been unlocked.

### Catalogue and availability

The catalogue contains supported Pokémon and variants from all
included generations from the beginning.

Being present in the catalogue does not mean an entry is currently
obtainable.

Season and event configuration determines the active encounter pool.

## Events and Temporary Availability

### Event schedules

Each event defines:

- A unique event identifier and display name.
- Its active dates.
- Its Pokémon, form, family, and costume selections.
- Its Event category weight.
- Whether it is enabled.

Event dates use the same timezone as seasons.

The initial Halloween schedule is October 1 through October 31.
December and other events will be configured later.

### Event encounter categories

An active event adds an Event encounter category with a default
weight of 4.

Eligible event Pokémon also appear in their ordinary type categories.

Event membership does not change a Pokémon's actual typing.

A Pokémon matching multiple selection rules for the same event
appears only once within each relevant encounter category.

### Temporary generation exceptions

An event can temporarily make selected Pokémon and forms from
locked generations obtainable.

These entries are available only while that event is active,
unless their generation is already unlocked normally.

Costumes remain event-only regardless of generation unlocks.

When the event ends:

- Remove its Event category.
- Remove entries that depended on the event for availability.
- Preserve every successful catch in seasonal and lifetime history.

### Legendary and Mythical restrictions

Events cannot unlock Legendary or Mythical Pokémon or forms from
generations that the current season has not unlocked.

Legendaries and Mythicals from unlocked generations remain
obtainable during events.

If an unlocked Legendary or Mythical also matches the event roster,
it can appear through the Event category.

These restrictions apply after event roster selection. Selecting
a type or evolution family does not override them.

### Evolution families and costumes

When an event selection includes a full evolution family, it includes
the earlier stages, later stages, and branches in that family.

Supported non-costume forms in that family are considered for event
availability, subject to the Legendary and Mythical restrictions.

Costumes require explicit event selection. Including a Pokémon's
family does not automatically include every costume associated
with that species.

### Reopening completed categories

Category completion is evaluated against currently obtainable
entries for the rolled normal or shiny variant.

If an event introduces an entry the viewer is missing, a previously
completed type category can become eligible again.

For example, collecting all normally available Ghost entries does
not keep Ghost excluded when Halloween introduces new Ghost entries.

### Event catch records

Event catches follow the same seasonal ownership rules as other
catches.

Their records preserve:

- The exact collectible entry.
- The season and catch timestamp.
- The encounter category used.
- The active event and any matching event association.

A Pokémon caught through an ordinary type category can still be
recorded as belonging to the active event roster.

## Initial Halloween Roster

### Ghost Pokémon

Include all supported Ghost-type Pokémon and forms, subject to
the current season's Legendary and Mythical restrictions.

Ghost typing alone does not automatically include non-Ghost
evolution relatives unless their family is also selected below.

### Additional evolution families

Include the full evolution families of:

- Houndoom.
- Zoroark.
- Absol.
- Hatterene.
- Mawile.
- Grimmsnarl.
- Spinarak and Ariados.
- Cubone and Marowak, including Alolan Marowak.
- Murkrow.
- Gothitelle.
- Dipplin.
- Swirlix.
- Delphox.
- Sableye.

Overlapping selections do not create duplicate entries in a pool.

### Specifically selected form

Include Morpeko in Hangry Mode.

This selection does not automatically add Full Belly Mode to the
Halloween exception. Full Belly Mode can still become available
through its normal generation unlock.

### ORAS Cosplay Pikachu

Include all five outfits:

- Pikachu Rock Star.
- Pikachu Belle.
- Pikachu Pop Star.
- Pikachu Ph.D.
- Pikachu Libre.

Each outfit supports normal and shiny entries in this game.

These costumes use their supported female gender and remain
event-only collectibles.

### Excluded Pokémon GO costumes

Pokémon GO exclusive costumes are excluded from the initial scope,
including:

- Witch-hat Pikachu.
- Mimikyu-costume Pikachu.
- Charizard-hat Pikachu.
- Umbreon-hat Pikachu.
- Rayquaza-hat Pikachu.
- Lucario-hat Pikachu.

### Future event rosters

Christmas and other event rosters will be selected separately.

Adding an event should primarily involve configuring entries that
already exist in the catalogue, rather than changing the core
encounter logic.

## Redemption Settings

### Channel-point cost

Each redemption costs 500 channel points by default.

This price covers the entire redemption, including any additional
attempts granted by the viewer's failure streak.

Additional attempts do not require additional points.

The price must be configurable and match the actual Twitch reward.

### Per-viewer cooldown

Each viewer has a 60-second cooldown between accepted redemptions.

The cooldown begins when their redemption is accepted for processing.

- Other viewers can redeem during that time.
- There is no shared cooldown.
- There is no per-stream redemption limit.
- Additional attempts within an accepted redemption run without
  another cooldown.

The cooldown must use the viewer's Twitch user ID so a username
change does not create a separate cooldown.

### Redemptions during cooldown

The per-viewer cooldown is checked when the redemption reaches
the game.

If the viewer redeems before their cooldown expires:

- Do not perform any encounter rolls.
- Do not change their failure streak.
- Do not restart or extend their cooldown.
- Inform them how long remains.
- Do not automatically refund their points.

The reward description must explain this behaviour before viewers
spend points.

### No automatic refunds

Redemptions are not automatically refunded for:

- Duplicate encounters.
- Entirely unsuccessful redemptions.
- Completing the available normal collection.
- Completing both available normal and shiny collections.
- Redeeming during the per-viewer cooldown.

Technical errors also do not trigger automatic refunds. They must
be logged and investigated separately from gameplay failures.

### Safe processing

Multiple viewers may redeem close together.

Collection updates must be processed safely so one viewer's result
cannot overwrite another's.

Each Twitch redemption ID must be processed only once. Receiving
the same redemption again must not create another catch, another
set of attempts, or another failure-streak increase.

Catch data must be saved successfully before announcing the catch
in chat or displaying it in the overlay.

## On-Stream Catch Overlay

### Purpose

An optional OBS overlay celebrates successful catches.

Unsuccessful attempts, duplicate encounters, and cooldown messages
do not trigger a successful-catch animation.

### Display sequence

Each catch notification follows this sequence:

1. The viewer's display name pops into view.
2. "Caught a [Pokémon name]!" appears beneath the viewer's name.
3. The Pokémon's sprite rises from below the visible overlay area
   into position beneath the text.
4. The Pokémon's cry plays once as the sprite appears.
5. The sprite gently moves up and down while the text stays still.
6. The notification exits after its configured display duration.

Shiny catches must be clearly identified, such as:
"Caught a shiny [Pokémon name]!"

Form and gender information must be available for display where
needed to distinguish the caught entry.

### Sprite selection

Use the sprite matching the caught Pokémon's:

- Species.
- Form or costume.
- Indexed gender, where applicable.
- Normal or shiny variant.

If the exact sprite is unavailable, use a clearly identified
fallback or a neutral placeholder.

Missing artwork must not invalidate a successful catch or prevent
the notification queue from continuing.

### Sprite animation

The entrance animation moves the sprite upward into position.

After the entrance finishes, a separate gentle bobbing animation
moves the sprite a small distance up and down.

The bobbing can animate a static image. It does not require an
animated sprite file.

### Pokémon cries

Play the caught Pokémon's cry once per notification.

Use a form-specific cry where available. Otherwise, use the
appropriate species cry when available.

Do not replay the cry on every bobbing animation cycle.

If audio is missing or fails to load, continue displaying the
notification without sound.

### Notification queue

Successful catches enter a display queue in the order they are
saved.

Only one catch notification is displayed at a time. Its animation
and audio finish before the next notification begins.

The display queue is separate from encounter processing. Waiting
for an overlay animation must not prevent other viewers' catches
from being processed and saved.

A repeated delivery of the same catch notification must not add
another copy to the display queue.

### Customization

Creators must be able to configure:

- Whether the overlay is enabled.
- Text styling, colours, and placement.
- Sprite size and placement.
- Entrance and exit timing.
- Notification display duration.
- Bobbing speed and movement distance.
- Cry volume and mute settings.

### Overlay failures

The overlay is a presentation feature.

If it is disconnected, unavailable, or unable to display an asset,
the saved catch remains valid.

An overlay failure must never reroll the encounter, remove the
catch, or change the viewer's failure streak.
