# PokéHunter Seasons

A seasonal Pokémon collection game for Streamer.bot, where viewers use Twitch channel points to encounter and catch Pokémon.

Originally created for the KapteinOle community, the project is also intended to help other creators bring a customizable Pokémon catching game to their streams.

## Project status

**In development.**

This project is being built from scratch. A sample Pokémon data importer
and a SQLite persistence test are working. The gameplay features below
are planned and are not yet implemented.

## Planned features

### Seasonal and National Pokédex

- Annual seasons that gradually unlock additional Pokémon generations.
- A fresh Seasonal Pokédex at the beginning of each season.
- Archived seasons that preserve previous catches.
- A permanent National Pokédex tracking unique entries and total catches across seasons.
- Separate entries for normal and shiny Pokémon, supported forms, costumes, and visible gender differences.

### Encounters and retries

- Channel-point redemptions with a maximum of one successful catch per redemption.
- Weighted Pokémon types and encounter classes.
- Duplicate encounters that can cause an attempt to fail.
- An additional attempt on the next redemption for each consecutive failed redemption.
- Removal of completed types from the relevant normal or shiny encounter pool.
- A configurable cooldown for each viewer.
- No refunds for unsuccessful encounters.

### Shiny progression

- A base shiny probability of 1 in 8,192.
- Increased shiny probability through unique National Pokédex milestones.
- An additional bonus for collecting unique shiny entries.
- Configurable subscriber-tier multipliers.
- Shiny protection that keeps remaining attempts shiny after a shiny encounter within the same redemption.

### Events and variants

- Seasonal events with themed Pokémon and costumes.
- An additional weighted Event encounter category.
- Temporary access to selected Pokémon from otherwise locked generations.
- Generation restrictions that still apply to locked Legendary and Mythical Pokémon.
- Event-only costumes with equal variant weights and protection against selecting an already-owned costume gender when another is missing.

### On-stream catch overlay

- An optional OBS overlay celebrating successful catches.
- Displays the viewer’s name first, followed by
  “Caught a [Pokémon name]!”
- Animates the caught Pokémon’s sprite upward from below into
  position beneath the text.
- Plays the Pokémon’s cry once when the sprite appears.
- Gently moves the sprite up and down while the notification
  remains visible.
- Displays the appropriate sprite for the caught form, gender,
  and shiny variant where available.
- Clearly identifies shiny catches in the announcement.
- Queues catch notifications so each appears individually,
  without overlapping animations or cries.
- Supports configurable styling, display duration, animation
  speed, movement distance, and audio volume.
- Allows Pokémon cries to be muted.
- Continues displaying the catch if its audio is unavailable.

### Future website support

- Catch records containing the information needed for a future collection showcase and leaderboard.
- Pokémon names, Pokédex numbers, forms, types, gender variants, shiny status, and sprite references.
- Catch history showing when and during which season each Pokémon was obtained.

## Customization

The project will separate configurable settings from the catching logic wherever practical.

Creators will be able to adjust encounter weights, shiny bonuses, season schedules, event rosters, cooldowns, and chat messages. Setup instructions and development documentation will be added alongside the implementation.

## Planned requirements

- Streamer.bot
- A Twitch channel with channel-point rewards enabled
- Pokémon data generated from PokéAPI

Additional requirements and installation instructions will be documented as development progresses.

## Documentation

- [SQLite setup and persistence test](docs/SQLITE_SETUP.md)

## Credits

Created by **OleKoderNo**, known on Twitch as **KapteinOle**.

Pokémon data and sprite references will be sourced from [PokéAPI](https://pokeapi.co/).

This is an unofficial fan project and is not affiliated with Pokémon, Nintendo, Game Freak, or Creatures.
