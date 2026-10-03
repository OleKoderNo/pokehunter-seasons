# Project Architecture

PokéHunter Seasons separates catalogue generation, game processing,
player storage, and on-stream presentation.

This keeps configuration editable, avoids repeated API requests during
streams, and preserves player progress across restarts and seasons.

## Implementation status

Currently implemented:

- TypeScript importer setup.
- Local caching of selected PokéAPI responses.
- Generation of regular Pikachu's four collection entries.

Planned:

- Full Pokémon catalogue generation and validation.
- Streamer.bot C# catching action.
- Catalogue and configuration loading.
- SQLite player storage.
- Chat announcements and browser overlay integration.
- Leaderboard export or website synchronization.

SQLite is the selected storage design, but the C# library and its
compatibility with Streamer.bot must be tested before implementation.

## Component responsibilities

| Component                  | Responsibility                                                 | When it runs                                              |
| -------------------------- | -------------------------------------------------------------- | --------------------------------------------------------- |
| TypeScript importer        | Converts source data into the game catalogue                   | During development and catalogue updates                  |
| JSON configuration         | Defines game settings, seasons, events, and overlay appearance | Loaded when the relevant component initializes or reloads |
| Streamer.bot C# action     | Processes redemptions and encounter rules                      | During streams                                            |
| SQLite database            | Stores player progress and redemption outcomes                 | Read and updated during game processing                   |
| Browser overlay            | Displays saved catches and plays cries                         | When connected and receiving notifications                |
| Future website integration | Publishes selected collection and leaderboard data             | Through a later export or synchronization process         |

The TypeScript importer does not need to remain running during streams.

## Project data locations

| Location              | Purpose                             | Commit to Git? |
| --------------------- | ----------------------------------- | -------------- |
| `config/`             | Editable game configuration         | Yes            |
| `scripts/`            | TypeScript importer source          | Yes            |
| `docs/`               | Rules and development documentation | Yes            |
| `data/samples/`       | Reviewable generated examples       | Yes            |
| `data/pokemon.json`   | Planned full catalogue              | Yes            |
| `.cache/` or `cache/` | Downloaded source API responses     | No             |
| `dist/`               | Compiled importer JavaScript        | No             |
| `node_modules/`       | Installed development dependencies  | No             |
| `runtime/`            | Local database and runtime files    | No             |
| `backups/`            | Local backup files                  | No             |

Runtime and backup paths are defaults for the planned implementation.
Their configuration will be documented when storage is implemented.

## Pokémon catalogue

The catalogue describes which Pokémon entries exist.

It contains:

- Species information.
- Evolution families.
- Encounter groups and rarity classes.
- Forms, costumes, and indexed genders.
- Separate normal and shiny entries.
- Sprite and cry references.

It does not contain viewer ownership or catch history.

The generated catalogue format is defined in `DATA_MODEL.md`.

### Loading strategy

The game loads and validates the catalogue when its runtime initializes.

After loading, it builds in-memory lookup structures:

- Entries by permanent entry ID.
- Species by species ID.
- Encounter groups and their entries.
- Evolution-family membership.
- Category candidates for the active season and event.

Normal and shiny eligibility are tracked separately.

Redemptions use these loaded structures. They do not reread and parse
the complete catalogue for every attempt.

The catalogue stores media references, not embedded image or audio data.

### Viewer-specific eligibility

Shared candidate pools describe what is currently available.

A viewer's Seasonal Dex determines which categories are complete and
which costume variants should be removed.

Load the relevant ownership IDs into a set for membership checks.

Do not remove every owned Pokémon from incomplete categories.
Regular duplicates remain possible under the game rules.

Viewer-specific filtering must not modify shared candidate pools.

### Reloading

Catalogue and configuration updates require a controlled reload.

The reload process must:

1. Read the proposed files.
2. Validate them and their references.
3. Build replacement lookup structures.
4. Switch to the replacement only after all checks succeed.

If a reload fails, keep the last valid loaded version and report the error.

At initial startup, if no valid version can be loaded, catching remains
unavailable.

Each accepted redemption uses one consistent configuration and catalogue
snapshot throughout its attempts.

### Scheduled transitions

Keeping data in memory must not freeze the active season or event.

Before accepting a redemption, resolve the applicable season and event
from its acceptance timestamp.

Refresh shared candidate pools when their schedule or configuration
changes, including when an event starts or ends.

## Player storage

Player progress will be stored in a local SQLite database, initially:

```text
runtime/pokehunter.db
```

SQLite runs through the database library used by the Streamer.bot C#
action. It does not require a separate database server.

The file persists after Streamer.bot closes.

### Planned stored information

- Twitch user IDs and latest known display names.
- Successful catches and timestamps.
- Seasonal ownership.
- Lifetime unique ownership.
- Seasonal retry streaks.
- Per-viewer cooldown state.
- Redemption IDs and processing outcomes.
- Relevant season, event, and catalogue version references.
- Pending notifications for successfully saved catches.

Twitch user IDs identify players. Display-name changes must not create
a new player or separate their existing collection.

The exact tables and indexes will be documented before implementation.

### Collection identity

Seasonal uniqueness uses:

```text
Twitch user ID + season ID + entry ID
```

Lifetime uniqueness uses:

```text
Twitch user ID + entry ID
```

A repeated catch in a later season creates another successful catch
record while preserving a single lifetime unique ownership record.

Normal and shiny entries remain separate identities.

### Database constraints

The database should enforce uniqueness for:

- Redemption IDs within the configured channel.
- Seasonal ownership keys.
- Lifetime unique ownership keys.
- Notification identities associated with saved catches.

Application checks improve error messages, but database constraints
provide a final safeguard against duplicate records.

## Redemption processing

The intended processing sequence is:

1. Read the redemption ID and viewer's Twitch user ID.
2. Check whether this redemption has already been processed.
3. Check cooldown and availability.
4. Record the acceptance timestamp and selected season.
5. Read the viewer's relevant progress.
6. Calculate the session's attempts and shiny probability.
7. Perform encounter rolls using the loaded catalogue.
8. Save the outcome and related state changes together.
9. Send the chat result and queue any successful-catch notification.

An outcome can be a successful catch, a fully failed session, or a
documented rejection such as an active cooldown.

Technical errors must be recorded separately from gameplay failures.

### Consistent saves

Use a database transaction for changes that must succeed together.

For a successful catch, this includes:

- Recording the catch.
- Recording seasonal ownership.
- Adding lifetime unique ownership when applicable.
- Updating the retry streak.
- Recording the completed redemption outcome.
- Recording the pending catch notification.

Cooldown acceptance state must also be persisted consistently with
the chosen redemption-processing design.

On a failed transaction, do not report the catch as saved.

Do not perform network requests, play animations, or wait for audio
inside a database transaction.

### Duplicate delivery and recovery

Receiving the same redemption more than once must not generate another
session or award another catch.

Completed redemptions return their stored outcome.

If processing is interrupted, recovery must distinguish:

- A redemption that was never accepted.
- An accepted redemption whose outcome is not committed.
- A committed outcome awaiting notification.

Persist enough information to resume an accepted session consistently,
including its season and the random decisions or reproducible random
state needed to avoid granting a fresh roll after an interruption.

The exact recovery mechanism must be designed and tested before launch.

## Concurrency

Different viewers can submit redemptions at the same time.

Protect each viewer's progress from concurrent changes while calculating
and committing a session.

SQLite writes take turns. Keep transactions short and implement bounded
handling for a busy database.

A database lock or technical failure must not be counted as a duplicate
Pokémon encounter or increase the viewer's retry streak.

A short game-processing queue is acceptable. The game must not wait for
an overlay notification to finish before processing the next viewer.

## Overlay and chat

Presentation happens after the gameplay outcome is committed.

A catch notification includes enough resolved information to display:

- Viewer display name.
- Pokémon display name.
- Entry and catch identifiers.
- Shiny state.
- Correct sprite reference or labelled fallback.
- Cry references.

The overlay processes notifications sequentially to avoid overlapping
animations and audio.

### Notification failures

An unavailable overlay or failed media download does not undo a catch.

Keep notification delivery separate from catch creation. Retrying a
notification must never rerun the encounter.

Use a stable notification ID so the receiving overlay can recognize
repeated deliveries.

Delivery acknowledgement and replay behavior after reconnecting must
be defined during overlay implementation. Do not assume that sending
a message proves it was displayed.

## Startup and shutdown

On runtime initialization:

1. Load and validate configuration.
2. Load and validate the catalogue.
3. Build lookup structures.
4. Open the player database.
5. Apply supported database migrations.
6. Recover interrupted processing and pending notifications as designed.
7. Allow catching when initialization succeeds.

On restart, rebuild in-memory structures and reopen the existing database.

Do not reset collections, retry streaks, or cooldowns merely because
Streamer.bot restarted.

The exact initialization hooks and database-library lifecycle must be
verified with the installed Streamer.bot version.

## Database migrations

The database has its own schema version, separate from:

- Configuration schema versions.
- Catalogue versions.
- Season numbers.

Changes to tables or constraints require explicit migrations.

Back up the database before migrations. If a migration fails, report
the error and leave catching unavailable until storage is usable.

Do not delete player data to resolve a version mismatch.

## Backups

Player data cannot be reconstructed from the Pokémon catalogue.

The backup plan must cover:

- The SQLite database.
- The catalogue version referenced by saved records.
- Relevant configuration files.

Use SQLite's supported backup mechanism for live backups, or a documented
procedure that closes database access before copying its files.

Do not assume copying only the main `.db` file while it is being written
produces a complete backup. SQLite can use companion journal or WAL files.

Store backups outside Git. Keep an additional copy on another device
or backup service.

Restore instructions and a restore test are required before live use.

## Future website integration

The website will not connect directly to the SQLite file on the
streaming PC.

A later integration can publish selected data through:

- A generated leaderboard snapshot.
- An authenticated synchronization endpoint.
- Another documented export process.

The local game remains the authority for catches.

Website downtime must not prevent local catching or saving progress.

Do not publish the complete runtime database. Export only the fields
needed for the public collection showcase and leaderboard.

## SQLite compatibility milestone

Before implementing player storage, verify that the selected C# SQLite
library works inside Streamer.bot.

The test must demonstrate:

1. Loading the required library and any native dependencies.
2. Creating and reopening a disposable test database.
3. Inserting and reading a record.
4. Committing and rolling back a transaction.
5. Enforcing a uniqueness constraint.
6. Handling competing writes without losing data.
7. Preserving records across a Streamer.bot restart.

Use a test database, not future production player data.

Document the tested Streamer.bot version, library version, installation
steps, and limitations in the development guide.

## Performance verification

No specific latency or memory usage is guaranteed before measurement.

Once the full catalogue exists, measure:

- Catalogue size and startup load time.
- Memory used by the loaded catalogue and lookup structures.
- Redemption processing with realistic collection sizes.
- Database behavior during bursts of redemptions.
- Leaderboard queries against a growing catch history.

Use these measurements to guide optimization.
