# Database

SQLite runs locally through the C# library; no separate database server is required.
Initialization, trainer profiles, and season registration are implemented and checked
inside Streamer.bot. Schema constraints for redemption results and seasonal progress are also verified.
Saving encounter-engine results and live redemption processing are not yet connected.

For installation use [SQLite Setup](SQLITE_SETUP.md); for running actions use
[C# Development](CSHARP_DEVELOPMENT.md#run-manual-streamerbot-checks).

## Schema and initialization

Current schema version: **4**. The storage project is
`streamerbot/storage/PokeHunter.Storage.csproj`.

| Version | Embedded SQL file                 | Recorded name             |
| ------- | --------------------------------- | ------------------------- |
| 1       | `001_CreateMigrationHistory.sql`  | Create migration history  |
| 2       | `002_CreateTrainers.sql`          | Create trainers           |
| 3       | `003_CreateSeasons.sql`           | Create seasons            |
| 4       | `004_CreateRedemptionResults.sql` | Create redemption results |

Files live in `streamerbot/storage/migrations/`. The csproj embeds them with explicit
LogicalName values; the initializer registry determines execution order. Embedding does
not execute SQL. Rebuild/redeploy Storage after changing migration source.

`DatabaseInitializer.Initialize(databasePath)` requires a full normalized absolute path.
It creates missing parent directories, opens/creates the database, enables/verifies foreign
keys, starts an immediate transaction, verifies application marker/version/history, applies
missing migrations, validates the result, and commits. Pending migrations and history
updates commit together; failure attempts rollback.

Fresh databases receive migrations 1–4. Supported existing databases receive missing migrations only.
A valid version-4 database needs no migration; existing history timestamps are preserved.
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
storage preserves the history reference. These tables were introduced in migration 3 and remain part of schema 4.

## Verified checks and limits

| Check              | Evidence                                                                                                                                  |
| ------------------ | ----------------------------------------------------------------------------------------------------------------------------------------- |
| SQLite persistence | Counter survives reopening and normal Streamer.bot restart                                                                                |
| Initialization     | History preserved; existing upgrades and fresh/repeated schema-4 initialization; expected columns                                         |
| Rejection          | Newer/unrelated fixture databases rejected without changed file bytes                                                                     |
| Trainers           | Create/read across repositories; rename/unchanged timestamps; isolation, no duplicate rows; blank login rejected                          |
| Seasons            | UTC equivalence; case-insensitive IDs; conflicts/invalid generations rejected; deliberate child-insert failure rolls back; retry succeeds |

The persistent test database now contains migrations 1–4. Later runs test idempotence, not those
upgrades again. Column checks verify column availability; the separate redemption schema check exercises the constraints listed below. Concurrent initialization,
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

## Redemption results and seasonal progress

Migration 4 creates two tables. The schema is implemented; the repository that saves
encounter-engine results is the next step.

| Table                     | Purpose                                                                 |
| ------------------------- | ----------------------------------------------------------------------- |
| `redemption_results`      | One completed result per redemption, including unsuccessful redemptions |
| `trainer_season_progress` | Current consecutive-failure streak per trainer and season               |

A successful result is also its catch record. It stores the trainer, season, entry ID,
shiny state, category/group, optional event ID, acceptance/completion timestamps, and
an entry snapshot in `entry_snapshot_json`. There is no separate catch table in this schema.

The database enforces:

- A unique redemption ID, allowing at most one saved result/catch per redemption.
- A unique successful `(twitch_user_id, season_id, entry_id)` combination.
- Existing trainer and season references through foreign keys.
- An attempt budget equal to the previous failure streak plus one.
- At least one used attempt, without exceeding that budget.
- Success resetting the next streak to zero and requiring all catch fields.
- Failure using every available attempt, incrementing the streak once, and having no catch fields.
- One nonnegative progress streak per trainer and season.

Normal and shiny entries have different entry IDs. Catching an entry again in another
season adds another catch, but must not increase lifetime unique-entry bonuses.
National Dex totals will be derived from successful results; lifetime uniqueness uses
distinct entry IDs per trainer across seasons.

Season foreign keys and indexes use SQLite's ASCII-only `NOCASE` collation. Future
storage code must use the canonical season ID returned by `SeasonRepository`, whose
identity handling also supports international IDs.

`DatabaseRedemptionSchemaCheck` verifies duplicate prevention, cross-season and shiny
identities, required catch fields, attempt/streak constraints, parent references, and
rollback of result and progress changes together. Its synthetic records live only in
a temporary database; cleanup is attempted after execution.

The rollback check exercises an explicit SQLite transaction, not a production save
repository. SQL currently requires nonempty snapshot/timestamp text; it does not validate
snapshot JSON, catalogue metadata, timestamp formats, or consistency between an entry ID
and its shiny flag. Those checks belong in the storage implementation.

## Collection reader

`CollectionRepository.ReadSnapshot(twitchUserId, seasonId)` reads:

- Successful collection entry IDs in the requested season.
- The season's consecutive-failure streak, defaulting to zero when no progress row exists.
- Lifetime unique normal and shiny entry counts across all seasons.

All reads use one database transaction so the values describe a consistent
database snapshot. Unknown trainers or seasons are rejected.

Season IDs follow the same case-insensitive rules as `SeasonRepository`,
including international characters. Returned snapshots preserve the stored
season ID and expose a copied, read-only entry list.

Repeat catches across seasons count once toward lifetime uniqueness.
Failed redemptions do not contribute to ownership or collection bonuses.
Conflicting shiny flags for the same permanent entry ID are rejected.

`CollectionStorageCheck` verifies these behaviours using a temporary
database. It also checks trainer isolation and that later database writes
do not change an already-returned snapshot.

A snapshot can become outdated after another redemption commits.
The future save workflow must guard against stale state; reading a
snapshot does not reserve a redemption.

## Redemption repository

`RedemptionRepository.FindById` returns a saved redemption or null when
the ID is unknown. Database errors are reported as exceptions.

`RedemptionRepository.Save` saves a completed result and updates seasonal
progress in one immediate transaction.

Before inserting, it:

- Checks whether the redemption ID already exists.
- Verifies that an existing result belongs to the same trainer and season.
- Compares current collection state with the snapshot used for rolling.
- Checks that acceptance occurred within the stored season boundaries.
- Validates caught-entry metadata against the supplied catalogue.
- Rejects an entry already owned in that season.

Repeated delivery returns the original result without changing newer
progress. `RedemptionSaveResult.WasCreated` distinguishes a new save
from an existing result.

Successful results reset the failure streak. Failed results exhaust
their attempt budget and increment the streak once. Both the result
and progress update commit together; errors trigger rollback.

Stored catches contain canonical serialized catalogue metadata.
Encounter eligibility remains the encounter engine's responsibility.

### Verified save checks

`RedemptionStorageCheck` uses a temporary database to verify:

- Successful and failed saves.
- Catch metadata read through another repository instance.
- Repeated deliveries preserving newer progress.
- Stale collection state rejected without changes.
- A deliberately failed progress update rolling back the catch insert.
- Successful retry after removing that deliberate failure.
- Catalogue metadata mismatches rejected.
- Seasonal duplicates and redemption IDs reused by another trainer rejected.
- Cross-season recatches and separate shiny identities counted correctly.

These checks do not yet exercise simultaneous save operations from
multiple threads or processes.

## Connected redemption processing

`RedemptionProcessor` connects collection reads, shiny probability
calculation, encounter sessions, and transactional result storage.

It checks for an existing redemption before reading collection state or
consuming randomness. Existing results are returned without changing progress.

For new redemptions, it reads one collection snapshot, calculates shiny
probability once, runs the available attempts, and saves the completed result.

Technical errors propagate without becoming ordinary gameplay failures.
The processor does not automatically reroll failed processing operations.

Calls through one processor instance are serialized. Other instances and
processes still rely on database transactions, uniqueness constraints,
and stale-state rejection. Simultaneous processing has not yet been tested.

The caller must supply categories prepared for the correct catalogue,
season, event, and acceptance time.

`RedemptionProcessingCheck` verifies normal and shiny catches, increased
retry budgets, repeated delivery without random draws, preservation of
newer progress, and propagation of an injected technical failure.
It uses controlled randomness and a temporary database.

## Remaining storage work

Connect the processor to live Twitch redemption delivery, including
trainer registration, schedule selection, and encounter-category preparation.

Cooldown enforcement, concurrent rolls, interrupted-session recovery,
and reliable notification delivery remain to be implemented.

Announce catches only after commit. A technical error or stale-state
conflict must not become an ordinary gameplay failure.

`WasCreated` helps identify repeated processing but does not guarantee
exactly-once chat or overlay delivery.

Next: implement the live redemption adapter and per-viewer cooldown,
then connect committed results to chat and the overlay.

## Migration and backup policy

Add a new numbered migration for a schema change; do not rewrite an already-applied
migration to upgrade existing databases. Update the registry, checks, and this guide.
Reject unsupported newer databases instead of deleting data to make them fit.

Before live use, implement/document backup and restore, including a restore test. Preserve
player data, relevant configuration, and catalogue definitions used by catches. Use a
supported SQLite backup mechanism or close database access before copying files; a live
main-file-only copy can omit journal/WAL state. Keep another backup outside the streaming PC.

Next: connect encounter sessions to the repository and implement
the live redemption workflow.
