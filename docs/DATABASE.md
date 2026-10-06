# Database Design

PokéHunter Seasons will use SQLite to store viewer progress locally.

Database initialization is implemented and verified inside Streamer.bot
using dedicated test databases.

The current schema version is 3.

- Migration 1 creates the migration-history table.
- Migration 2 creates the trainer table.
- Migration 3 creates the `seasons` and `season_generations` tables.

All three migrations are stored as SQL files embedded in
`PokeHunter.Storage.dll`.

Trainer profile creation, retrieval, and name updates are implemented
and verified through a manual Streamer.bot test action.

Catch storage, redemption processing, and live game integration are
not implemented yet.

The collection design below describes the planned storage behaviour.

## Implemented initialization

The storage library is defined by:

`streamerbot/storage/PokeHunter.Storage.csproj`

Its entry point is:

`DatabaseInitializer.Initialize(databasePath)`

The initializer requires a full, normalized absolute file path.
It creates missing parent directories and opens or creates the database.

Initialization:

1. Enables and verifies foreign-key enforcement for its connection.
2. Starts an immediate write transaction.
3. Checks the application identifier and schema version.
4. Validates existing migration history, or requires an empty database.
5. Applies missing migrations in order.
6. Validates the resulting migration history.
7. Commits and returns the installed schema version.

All pending migrations and their history records commit together.
If initialization fails, it attempts to roll back the transaction.

Future storage connections must also enable foreign-key enforcement.

### Migration files

| Version | File                             | Recorded name            |
| ------- | -------------------------------- | ------------------------ |
| 1       | `001_CreateMigrationHistory.sql` | Create migration history |
| 2       | `002_CreateTrainers.sql`         | Create trainers          |
| 3       | `003_CreateSeasons.sql`          | Create seasons           |

Files are located in `streamerbot/storage/migrations/`.

Each SQL file is embedded in the storage DLL using an explicit
`LogicalName` in the project file. Embedding a file does not execute it:
the initializer's migration registry determines execution order.

SQL files define structural changes. The initializer manages the
transaction, inserts migration-history records, and updates the version.

Do not rename or change previously applied migrations casually.
Introduce a new migration for subsequent structural changes.

### Database markers and migration history

SQLite's `application_id` contains the stable marker `0x50485331`.
Its `user_version` contains the installed schema version.

The `schema_migrations` table records:

- `version`: the unique, positive migration number.
- `name`: the migration description.
- `applied_at_utc`: when the migration was applied.

Existing history records are preserved during upgrades.

A fresh database receives migrations 1 through 3. Existing supported
databases receive only their missing migrations. A valid version-3
database requires no migration.

### Trainer table

The `trainers` table contains:

| Column           | Purpose                                        |
| ---------------- | ---------------------------------------------- |
| `twitch_user_id` | Permanent trainer identity and primary key.    |
| `login_name`     | Most recently saved Twitch login name.         |
| `display_name`   | Most recently saved display name.              |
| `created_at_utc` | When the trainer was first saved in this game. |
| `updated_at_utc` | When the saved profile last changed.           |

All columns require non-null values and reject empty or
ordinary-space-only text.

Timestamp formatting and fuller input validation belong to the
upcoming storage methods.

`Trainer.cs` represents an immutable snapshot of a saved trainer.

`TrainerRepository.cs` provides:

- `FindByTwitchUserId`: returns the saved trainer or `null` when missing.
- `SaveProfile`: creates a trainer or updates their saved names.

The Twitch user ID identifies the trainer permanently. Name changes
update the existing record and preserve its creation timestamp.

Saving an unchanged profile preserves both timestamps. Changing either
name updates `updated_at_utc`.

The repository rejects missing values, surrounding whitespace, and
control characters. It preserves capitalization and international text.
It does not attempt to reproduce Twitch's complete naming rules.

SQL parameters separate profile values from SQL instructions.

The lookup and save run within one immediate write transaction.
Database errors propagate to the caller rather than being reported
as successful saves or missing trainers.

The repository requires an existing, initialized database with the
expected application marker and current schema version. It does not
create databases or run migrations.

Call `DatabaseInitializer.Initialize` during setup before using the
repository.

### Season tables

`seasons` stores the season ID, display name, UTC boundaries, and
registration timestamp.

`season_generations` stores one row for each generation unlocked
within a saved season.

Its combined primary key prevents duplicate generation entries within
the same season. A foreign key requires each generation record to
reference an existing season.

The foreign key restricts deletion or renaming of a parent season while
generation records reference it.

`StoredSeason.cs` represents an immutable snapshot. Its generation list
is copied into a read-only collection.

Configuration remains responsible for active-season selection.
The database provides persistent season references for future catches.

Season registration, configuration-change handling, and retrieval are
the next implementation steps.

### Rejection rules

The initializer rejects:

- Negative or newer unsupported schema versions.
- Version-0 databases containing application objects.
- Version-0 databases with an existing application marker.
- Initialized databases with an unexpected application marker.
- Migration histories with missing, unexpected, or mismatched records.

History validation checks ordered versions, expected names, and
nonempty timestamps. It is not a complete database integrity audit.

### Verified manual checks

`DatabaseInitializationCheck.cs` verifies:

- Existing migration timestamps survive initialization or upgrade.
- Repeated initialization preserves all three migration records.
- A fresh database reaches version 3.
- Trainer and season tables contain their expected columns.

The persistent test database has successfully upgraded from version 1
to 2 and subsequently from version 2 to 3. Later runs reuse version 3;
they do not recreate those earlier upgrade scenarios.

The column checks do not yet verify season foreign-key or uniqueness
constraint behaviour.

The persistent test file is:

`runtime/database-initialization-test.db`

Fresh initialization uses a separate temporary database.

`DatabaseRejectionCheck.cs` verifies:

- A database marked one version newer than the current build is rejected.
- An unrelated database containing data is rejected.
- Both rejected files remain byte-for-byte unchanged.

Temporary test directories are removed after execution when possible.

Neither action uses the planned live database, `runtime/pokehunter.db`.

`TrainerStorageCheck.cs` verifies:

- An unknown trainer returns `null`.
- A saved trainer can be read through a separate repository instance.
- New trainer timestamps use UTC and initially match.
- Saving an unchanged profile preserves both timestamps.
- Renaming preserves identity and creation time while updating the profile.
- Different trainers remain separate.
- Repeated saves do not create duplicate trainer rows.
- A blank login name is rejected without changing the saved profile.

The action uses synthetic profiles in a fresh temporary database and
attempts to remove its temporary directory afterward.

A fixed old update timestamp is injected into the test fixture so
timestamp checks do not depend on execution speed or artificial delays.
This fixture modification is not part of normal trainer storage.

Rollback after a partially executed migration, concurrent initialization,
concurrent trainer writes, and the remaining input-validation cases
have not yet been explicitly tested.

## Where the database lives

The planned game database location is:

`runtime/pokehunter.db`

SQLite runs inside the application using a library. It does not require
a separate database server or a background service.

The existing `runtime/sqlite-persistence-test.db` is a development test
database. It will not become the game database.

Runtime databases and their accompanying files must remain excluded
from Git. They contain local progress and are not project source files.

## Responsibilities

C# coordinates the application:

- Loads configuration.
- Calculates encounter results.
- Opens database connections.
- Executes SQL with parameters.
- Handles transactions and errors.

SQL describes database operations:

- Creating tables and indexes.
- Inserting successful catches.
- Reading collections and catch history.
- Updating redemption progress.

JSON remains responsible for editable configuration and generated
Pokémon catalogue data.

## Successful catches are permanent records

Every successful catch receives its own record.

Catching the same entry in different seasons creates separate catch
records. Earlier records remain available when a new season begins.

The Seasonal and National Pokédex are derived from these records.
They are not two independently maintained copies of a collection.

### Seasonal Pokédex

The Seasonal Pokédex includes catches belonging to one season.

A trainer may catch each collectible entry only once per season.
The database will enforce this rule as well as the catching logic.

### National Pokédex

The National Pokédex includes catches from every season.

It supports two different measurements:

- Total catches: every successful catch across all seasons.
- Unique entries: each distinct collectible entry counted once.

Catching the same entry again in another season increases total
catches, but does not increase unique entries.

Unique shiny entries are also counted separately for the additional
shiny collection bonus.

## Collectible entry identity

Every collectible entry needs a stable identifier.

Its identity distinguishes:

- Pokémon species.
- Supported regional or other forms.
- Supported costumes.
- Visible gender variants where they count as separate entries.
- Normal and shiny versions.

Gender does not create separate collection entries for Pokémon
without a supported visible gender difference.

Display names and sprite URLs are not identifiers. They may change
without changing which entry a trainer owns.

The catalogue work will define the exact identifier format. Once
catch records reference an identifier, it must not be casually renamed.

## Planned records

### Trainers

A trainer record identifies a viewer using their Twitch user ID.

Twitch usernames and display names are presentation information.
Changing a name must not create a new trainer or reset progress.

### Seasons

A stored season record gives catches a permanent season reference.

Season IDs must remain stable after they are used. Ending a season
must not delete its records or its catches.

Configuration determines the active season. The database preserves
the season references needed for collection history.

### Catches

A successful catch record will identify:

- The trainer.
- The season.
- The collectible entry.
- Whether the entry is shiny.
- When the catch happened in UTC.
- The redemption that produced it.
- The active event, when applicable.

The record must retain enough catalogue information to interpret
historical catches if presentation data changes later.

The exact historical snapshot fields will be defined alongside the
catalogue and catch-storage implementation.

### Trainer progress

Trainer progress will track the consecutive failed-redemption count
used to calculate attempts on the next redemption.

A successful redemption resets that count. A failed redemption
increases it once, regardless of how many attempts it contained.

Shiny protection belongs to one redemption session. It must not carry
over as a shiny guarantee on the next redemption.

### Redemptions

A redemption record identifies a channel-point redemption and its
processing result.

The redemption ID will prevent the same Twitch redemption from
awarding another catch or changing the failure streak twice if it is
delivered or processed again.

Failed redemptions still need records even though they create no catch.

## Database integrity

The database will enforce important rules alongside the application:

- Twitch user IDs are unique.
- Season IDs are unique.
- Redemption IDs are unique.
- A trainer cannot own the same collectible entry twice in one season.
- A redemption cannot award more than one catch.
- Catch records must reference existing trainers and seasons.

Foreign-key enforcement will be enabled on each database connection.

## Transactions

A transaction groups related database changes into one operation.

Recording a redemption result, inserting any successful catch, and
updating the trainer's failure streak must succeed together.

If an operation fails, its changes must be rolled back together.
This prevents partially recorded results.

Chat messages and overlay notifications happen after the database
transaction succeeds.

Different viewers may redeem concurrently. Database writes must remain
short, and processing must protect each viewer from conflicting updates.

## Schema versions and migrations

The database schema is the structure of its tables, indexes, and rules.

A migration is a numbered change to that structure.

The initialization process will:

1. Open or create the database.
2. Read its schema version.
3. Apply missing migrations in order.
4. Record each completed migration.
5. Leave an already-current database unchanged.

Each migration and its version update must complete within the same
transaction.

A database created by a newer, unsupported application version must
be rejected rather than modified.

Existing player data must not be deleted simply to make a schema
change easier.

## Backups

Live progress must be backed up before applying future migrations.

The backup procedure will be documented and implemented before the
database stores real viewer catches.

## Implementation order

1. Add versioned database initialization.
2. Verify first-run creation and repeated initialization.
3. Add trainer, season, and catch storage.
4. Add redemption records and failure-streak updates.
5. Verify uniqueness rules and transaction rollback.
6. Connect storage to the encounter and redemption logic.

## Season repository

`streamerbot/storage/SeasonRepository.cs` provides two operations:

- `FindById` returns a stored season or `null` when the ID is unknown.
- `Register` saves a new season or returns an equivalent existing record.

The database must already be initialized. The repository does not
create databases or run migrations.

### Identity and equivalent registrations

Season IDs are compared using `OrdinalIgnoreCase` in C#.
Original ID capitalization is preserved.

The repository performs this comparison explicitly because SQLite's
built-in `NOCASE` collation only handles ASCII case differences.

Registration considers definitions equivalent when:

- Their IDs match without regard to capitalization.
- Their names match exactly.
- Their starts and exclusive ends represent the same instants.
- Their unlocked-generation sets are identical.

Generation order does not matter. Timestamps are stored in UTC.

Equivalent registration preserves the original creation timestamp
and does not insert duplicate records.

### Conflicting definitions

Registration rejects an existing ID with a different name, start,
exclusive end, or generation set.

Stored seasons are not automatically overwritten when configuration
changes. An explicit season-editing workflow is not implemented.

Schedule continuity remains the configuration validator's responsibility.
The repository validates the individual season being registered.

### Atomic registration

The season and its generation rows are inserted in one transaction.

`BEGIN IMMEDIATE` reserves the write transaction before checking
whether the season exists. A failure rolls back the registration.

The manual `SeasonStorageCheck.cs` action has verified creation,
retrieval, equivalent registrations, conflict rejection, invalid
generation lists, international IDs, and rollback after a deliberately
failed generation insert.

Concurrent registration from multiple actions has not yet been tested.

This functionality uses schema version 3 and requires no new migration.
