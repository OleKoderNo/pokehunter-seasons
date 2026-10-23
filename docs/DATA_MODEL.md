# Database

SQLite runs locally through the C# library; no separate database server is required.
Initialization, trainer profiles, and season registration are implemented and checked
inside Streamer.bot. Catch storage and redemption processing are still planned.

For installation use [SQLite Setup](SQLITE_SETUP.md); for running actions use
[C# Development](CSHARP_DEVELOPMENT.md#run-manual-streamerbot-checks).

## Schema and initialization

Current schema version: **3**. The storage project is
`streamerbot/storage/PokeHunter.Storage.csproj`.

| Version | Embedded SQL file                | Recorded name            |
| ------- | -------------------------------- | ------------------------ |
| 1       | `001_CreateMigrationHistory.sql` | Create migration history |
| 2       | `002_CreateTrainers.sql`         | Create trainers          |
| 3       | `003_CreateSeasons.sql`          | Create seasons           |

Files live in `streamerbot/storage/migrations/`. The csproj embeds them with explicit
LogicalName values; the initializer registry determines execution order. Embedding does
not execute SQL. Rebuild/redeploy Storage after changing migration source.

`DatabaseInitializer.Initialize(databasePath)` requires a full normalized absolute path.
It creates missing parent directories, opens/creates the database, enables/verifies foreign
keys, starts an immediate transaction, verifies application marker/version/history, applies
missing migrations, validates the result, and commits. Pending migrations and history
updates commit together; failure attempts rollback.

Fresh databases receive 1–3. Supported existing databases receive missing migrations only.
A valid version-3 database needs no migration; existing history timestamps are preserved.
Every future connection must enable foreign-key enforcement too.

The initializer rejects negative/newer versions, unrelated version-0 databases with
objects or an existing marker, incorrect initialized markers, and missing/unexpected/
mismatched history. History checks ordered versions, names, and nonempty timestamps;
this is not a complete integrity audit.

## Trainer repository

`TrainerRepository` requires an initialized database with the expected marker/current
version. It does not create databases or migrate them.

| Column in trainers | Purpose                            |
| ------------------ | ---------------------------------- |
| `twitch_user_id`   | Permanent identity and primary key |
| `login_name`       | Latest saved Twitch login          |
| `display_name`     | Latest saved display name          |
| `created_at_utc`   | First saved timestamp              |
| `updated_at_utc`   | Last profile-change timestamp      |

All columns are non-null and reject empty/ordinary-space-only text. Repository validation
also rejects missing values, surrounding whitespace, and control characters; it preserves
capitalization/international text rather than reproducing every Twitch naming rule.

- `FindByTwitchUserId` returns an immutable Trainer snapshot or null if unknown.
- `SaveProfile` creates or updates names under the same Twitch ID.
- Unchanged profiles preserve both timestamps. Renames preserve creation time and update
  the modified time; names do not create a new trainer.
- Parameterized SQL separates values from instructions. Save lookup/write uses an immediate
  transaction. Database failures propagate; they are not reported as success or absence.

## Season repository

`seasons` stores identity, display name, UTC boundaries, and registration timestamp.
`season_generations` stores one row per unlocked generation; its composite primary key
prevents duplicates. A foreign key requires an existing season and restricts deletion or
renaming while generation rows reference it.

`StoredSeason` is an immutable snapshot with a copied read-only generation list.
`SeasonRepository` requires initialized storage and provides:

- `FindById`: return a season or null.
- `Register`: insert a new definition or return its equivalent stored record.

IDs use C# OrdinalIgnoreCase, including international text; original capitalization is
preserved. This is implemented explicitly rather than relying on SQLite's ASCII-only
NOCASE behavior. Equivalent registration requires an exact name, equivalent boundary
instants, and the same generation set regardless of order. It preserves the original
record and creation time without duplicate rows.

Different names, boundaries, or generations for an existing ID are rejected. There is
no automatic stored-season update workflow. Schedule continuity belongs to configuration
validation; the repository checks the individual definition.

Season and generation rows are inserted in one BEGIN IMMEDIATE transaction. A failed
insert rolls back the whole registration. Configuration chooses the active season;
storage preserves the history reference. This uses schema 3 without an additional migration.

## Verified checks and limits

| Check              | Evidence                                                                                                                                  |
| ------------------ | ----------------------------------------------------------------------------------------------------------------------------------------- |
| SQLite persistence | Counter survives reopening and normal Streamer.bot restart                                                                                |
| Initialization     | History preserved; existing upgrades and fresh/repeated schema-3 initialization; expected columns                                         |
| Rejection          | Newer/unrelated fixture databases rejected without changed file bytes                                                                     |
| Trainers           | Create/read across repositories; rename/unchanged timestamps; isolation, no duplicate rows; blank login rejected                          |
| Seasons            | UTC equivalence; case-insensitive IDs; conflicts/invalid generations rejected; deliberate child-insert failure rolls back; retry succeeds |

The persistent test database upgraded from 1→2→3. Later runs test idempotence, not those
upgrades again. Column checks do not prove every constraint. Concurrent initialization,
concurrent repository writes, partial migration failure, and all input-validation branches
have not been exhaustively tested.

## Database locations

| Path                                      | Use                                                           |
| ----------------------------------------- | ------------------------------------------------------------- |
| `runtime/pokehunter.db`                   | Planned production database; live catches are not implemented |
| `runtime/sqlite-persistence-test.db`      | Disposable persistent run-counter test                        |
| `runtime/database-initialization-test.db` | Persistent migration/idempotence test                         |
| Operating-system temporary directories    | Isolated rejection/repository fixtures; cleanup attempted     |

Do not turn a test database into the live database. Ignore databases, journal/WAL companion
files, runtime records, and backups in Git. None are source files.

## Planned catch and redemption storage

Successful catches are permanent history. Seasonal and National Dex views must derive
consistent results from those catches; any future ownership tables are transactional
indexes/projections, not independently maintained competing collections.

| Identity / record   | Planned requirement                                                                                  |
| ------------------- | ---------------------------------------------------------------------------------------------------- |
| Seasonal ownership  | Unique `(Twitch user ID, season ID, entry ID)`                                                       |
| Lifetime uniqueness | Distinct `(Twitch user ID, entry ID)` across seasons                                                 |
| Catch               | Trainer, season, stable entry, shiny state, UTC time, originating redemption, event/category context |
| Seasonal progress   | Consecutive fully failed redemptions; success resets, full failure increments once                   |
| Redemption          | Stable delivery identity, acceptance context, processing outcome; failures also recorded             |
| Notification        | Saved-catch identity and delivery state; exact schema remains to be designed                         |

Recatching the same entry in another season adds a catch, not a new lifetime unique entry.
Normal/shiny entries are separate. Historical snapshot fields and catalogue compatibility
must be settled alongside catch storage. Shiny protection is session-local, not a persistent
next-redemption guarantee.

The database must enforce at most one catch per redemption, seasonal uniqueness, stable
redemption uniqueness, and valid trainer/season references. Record result, catch, and
progress changes atomically. Announce only after commit. A technical failure is not an
ordinary gameplay failure. See [Architecture](ARCHITECTURE.md) for recovery and concurrency.

## Migration and backup policy

Add a new numbered migration for a schema change; do not rewrite an already-applied
migration to upgrade existing databases. Update the registry, checks, and this guide.
Reject unsupported newer databases instead of deleting data to make them fit.

Before live use, implement/document backup and restore, including a restore test. Preserve
player data, relevant configuration, and catalogue definitions used by catches. Use a
supported SQLite backup mechanism or close database access before copying files; a live
main-file-only copy can omit journal/WAL state. Keep another backup outside the streaming PC.

Next storage work is catch/redemption records, ownership/progress constraints, cooldown and
recovery state, then integration with the encounter engine. Trainer/season foundations do
not yet implement those features.
