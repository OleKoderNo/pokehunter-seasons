# PokéHunter Seasons

A seasonal Pokémon collection game for Streamer.bot, built for **KapteinOle**
and designed to be customizable by other creators.

Viewers will spend channel points to catch Pokémon, build a fresh Seasonal Dex,
and preserve their collection history in a lifetime National Dex.

## Status

**In development — not ready for live channel-point redemptions.**

Working foundations:

- Game, season, and event configuration with validation and schedule selection.
- SQLite migrations, trainer profiles, and persistent season registration.
- Generation 1 and Halloween catalogue generation, merging, and rarity assignments.
- C# catalogue loading, protected lookups, eligibility, and weighted category construction.
- An OBS catch-overlay preview with avatar, sprite animation, cry, and notification queue.

Still to build: random encounter selection, viewer ownership filtering, catch
storage, shiny/retry processing and live redemption integration.
The website/leaderboard and December content are later work.

## The game

- **500 channel points** and no automatic refunds.
- At most one catch per redemption; fully failed redemptions earn another attempt next time.
- Separate collectibles for normal/shiny states, supported forms, costumes, and visible genders.
- Annual seasons by default; old catches remain in the National Dex.
- Base shiny odds of **1 in 8,192**, improved by unique collection milestones and subscription tier.
- Themed events expand the roster and add a weighted Event category.

See [Game Rules](docs/GAME_RULES.md) for the complete intended behavior.
These rules are not all implemented yet.

## Start here

| I want to…                                        | Guide                                        |
| ------------------------------------------------- | -------------------------------------------- |
| Generate Pokémon data or run TypeScript checks    | [Development](docs/DEVELOPMENT.md)           |
| Build C#, run checks, or update Streamer.bot DLLs | [C# Development](docs/CSHARP_DEVELOPMENT.md) |
| Install SQLite dependencies                       | [SQLite Setup](docs/SQLITE_SETUP.md)         |
| Preview the overlay in OBS                        | [Catch Overlay](docs/CATCH_OVERLAY.md)       |
| Customize seasons, events, odds, or rarity        | [Configuration](docs/CONFIGURATION.md)       |
| Understand how the pieces fit together            | [Architecture](docs/ARCHITECTURE.md)         |
| Understand collection IDs and catalogue records   | [Data Model](docs/DATA_MODEL.md)             |
| Understand migrations and player storage          | [Database](docs/DATABASE.md)                 |

Development uses Node.js 24+, npm, and Windows C# tooling. The verified C# setup
uses .NET SDK 10.0.401, targets .NET Framework 4.8.1, and runs in Streamer.bot 1.0.7 x64.
An OBS Browser Source is required for the optional overlay preview.

Generated catalogues, downloaded caches, build output, personal installation
settings, runtime data, and backups stay out of Git. Existing tracked samples
remain reviewable examples.

## Credits

Created by **OleKoderNo**, known on Twitch as **KapteinOle**.

Pokémon source data, sprite references, and cry references come from
[PokéAPI](https://pokeapi.co/).

This is an unofficial fan project and is not affiliated with Pokémon,
Nintendo, Game Freak, or Creatures.
