# Architecture

PokéHunter separates catalogue generation, encounter logic, storage, and presentation.
For current project-wide status, see the [README](../README.md).

## Components

| Component | Responsibility | Current state |
| --- | --- | --- |
| TypeScript tools | Fetch/cache source data and generate reviewed collection entries and groups | Implemented for the current roster |
| JSON configuration | Editable game settings, schedules, rosters, rarity, and overlay appearance | Loaders/checks available; live reload not integrated |
| PokeHunter.Core | Configuration, catalogue validation/lookups, eligibility, category construction, deterministic weighted selection | Implemented; random-number source and session logic remain |
| PokeHunter.Storage | Local SQLite migrations and repositories | Trainer and season storage implemented; catches/redemptions remain |
| Streamer.bot actions | Host integration and manual checks | Manual checks implemented; live redemption action remains |
| Node overlay server and browser page | Relay and display catch previews | Test-only KapteinOle endpoint implemented |
| Website | Collection showcase and leaderboard | Deferred |

The importer runs during development/content updates, not during each redemption.
The overlay server must remain running while its browser sources are used.

## Data locations

| Location | Contents | Commit? |
| --- | --- | --- |
| `config/`, `scripts/`, `overlay/src/`, `streamerbot/`, `tests/`, `docs/` | Handwritten configuration, code, and docs | Yes, except local/build files |
| `data/samples/` | Existing tracked generated examples | Yes; regenerate, do not hand-edit |
| `data/catalogue/` | Generated catalogues and review reports | No |
| `cache/`, `.cache/` | Downloaded source responses | No |
| `dist/`, `overlay/dist/`, `bin/`, `obj/`, `node_modules/` | Dependencies/build output | No |
| `runtime/`, `backups/` | Local records, databases, and backups | No |
| `streamerbot/StreamerBot.local.props` | Personal installation path | No |

The current merged file is `data/catalogue/merged-entries.json`. It remains
marked `sampleOnly: true`; no final production packaging contract is established.

## Implemented catalogue flow

1. TypeScript converters generate reviewed entries from cached or downloaded source data.
2. The merger validates entries, rejects conflicts, deduplicates identical overlaps,
   checks counterparts, builds groups, and resolves rarity assignments.
3. `CatalogueLoader` reads and validates the intermediate file in C#.
4. `CatalogueIndex` owns independent copies and provides ID lookups without file reads.
5. `EligibilityPoolBuilder` resolves the active season/event at one supplied instant.
6. `EncounterCategoryBuilder` builds populated type/Event categories for one shiny state.

Group candidates retain their category-specific entry IDs. A group appears once
per category, and a variant cannot acquire eligibility through an unrelated member.
These builders do not inspect ownership or perform random rolls.
`WeightedSelector` selects from positive weights using a supplied value in [0, 1).
It does not generate random numbers or execute a complete encounter.

See [Data Model](DATA_MODEL.md) for identities and [C# Development](CSHARP_DEVELOPMENT.md)
for checks. The runtime must still retain the index and manage pool refreshes.

## Intended redemption flow — not yet integrated

1. Identify the viewer and redemption using stable Twitch IDs.
2. Detect previously processed deliveries; check cooldown and availability.
3. Capture acceptance time and a consistent season, event, catalogue, and settings snapshot.
4. Read ownership/progress and calculate attempts and shiny probability.
5. Roll encounters using the [game rules](GAME_RULES.md).
6. Commit the outcome, catch if any, retry state, and related records atomically.
7. Announce committed results and queue successful-catch notifications.

Viewer-specific filtering must not modify shared candidate pools. Owned regular
entries remain possible duplicates inside incomplete categories.

## Runtime lifecycle and concurrency — design requirements

- Initialize configuration, catalogue, and storage successfully before accepting catches.
- Retain validated lookups; do not parse the whole catalogue for every attempt.
- Refresh availability at season/event boundaries and after a controlled configuration reload.
- Build and validate replacements before switching. A failed reload keeps the last valid
  snapshot; a failed initial load leaves catching unavailable.
- Every accepted redemption keeps one consistent snapshot throughout all its attempts.
- Protect each viewer's progress against concurrent changes. Keep SQLite writes short
  and use bounded handling for a busy database.
- Never wait for animations, audio, or network calls inside a database transaction.
- A short processing queue is acceptable; overlay playback must not block other viewers.
- Restarting must preserve collections, retry streaks, and cooldowns.

## Recovery and presentation — remaining design work

Redelivery must not produce fresh rolls or a second catch. Recovery must distinguish
an unaccepted redemption, an accepted session without a committed outcome, and a
committed outcome awaiting notification. The mechanism for preserving accepted-session
random decisions or reproducible state remains to be designed and tested.

Gameplay failures and technical failures are different. A failed save or database lock
must not become a duplicate encounter or increment the gameplay failure streak.

Production notification retries must not rerun encounters. Stable notification IDs,
deduplication, acknowledgement, and reconnect behavior remain to be implemented.
The [current overlay preview](CATCH_OVERLAY.md) has in-memory queues and no replay
or delivery acknowledgement; a successful POST is not proof of display.

## Storage, backups, and future website

SQLite is local and requires no database server. [Database](DATABASE.md) owns the
schema, repository behavior, migration policy, and remaining persistence work.
Player data cannot be reconstructed from the Pokémon catalogue.

Before live catches: document and test backup/restore, preserve the catalogue and
configuration used by saved records, and verify interrupted-processing recovery.
Do not assume a copy of an actively written `.db` file alone is a valid backup.

The future website should consume a selected export or authenticated synchronization
feed, not connect directly to the streaming PC's SQLite file. Website downtime must
not stop local catches. Do not publish the full player database.

## Performance verification

Measure catalogue load time/memory, realistic ownership filtering, redemption bursts,
and database contention before claiming latency or throughput guarantees.
