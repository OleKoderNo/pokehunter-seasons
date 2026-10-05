# Database Design

PokéHunter Seasons will use SQLite to store viewer progress locally.

Database initialization is implemented and verified inside Streamer.bot
using dedicated test databases.

Schema version 1 creates the migration-history table. Trainer records,
catch storage, redemption processing, and live game integration are
not implemented yet.

The collection design below describes the planned storage behaviour.

## Implemented initialization

The storage library is defined by:

`streamerbot/storage/PokeHunter.Storage.csproj`

Its initialization entry point is:

`DatabaseInitializer.Initialize(databasePath)`

The initializer requires a full, normalized absolute file path.
It creates missing parent directories and opens or creates the database.

Initialization then:

1. Enables and verifies foreign-key enforcement for its connection.
2. Starts an immediate write transaction.
3. Reads the application identifier and schema version.
4. Initializes an unmarked, empty database or checks an existing one.
5. Commits the transaction and returns the installed schema version.

If initialization fails, it attempts to roll back the transaction.
Errors are reported to the caller.

Foreign-key enforcement must also be enabled on future connections
used for game storage; enabling it here does not configure all connections.

### Schema version 1

The first migration creates `schema_migrations` with these columns:

| Column           | Purpose                                      |
| ---------------- | -------------------------------------------- |
| `version`        | Unique, positive migration number.           |
| `name`           | Description of the migration.                |
| `applied_at_utc` | UTC timestamp recording when it was applied. |

Version 1 contains one migration named `Create migration history`.

SQLite's `user_version` stores the installed schema version.
Its `application_id` stores the fixed marker `0x50485331`, identifying
this database family as PokéHunter Seasons.

The application marker remains stable when future schema versions
are introduced.

The migration table, history row, application marker, and schema
version are written within the same transaction.

Repeated initialization checks an existing version-1 database without
replacing its migration record.

### Rejection rules

The initializer rejects:

- Negative schema versions or versions newer than this build supports.
- Version-0 databases containing application objects.
- Version-0 databases with an existing application marker.
- Version-1 databases with an unexpected application marker.
- Version-1 databases whose migration history fails the expected checks.

This is a compatibility and migration-history check, not a complete
database integrity audit.

### Verified manual checks

`streamerbot/tests/DatabaseInitializationCheck.cs` verifies that:

- A fresh test database initializes successfully.
- The installed schema version is 1.
- Exactly one version-1 migration record exists.
- Repeated initialization preserves its original timestamp.
- The same database can be reused across separate action executions.

It uses `runtime/database-initialization-test.db`.

`streamerbot/tests/DatabaseRejectionCheck.cs` verifies that:

- A database marked as schema version 2 is rejected.
- An unrelated database containing existing data is rejected.
- Both rejected database files remain byte-for-byte unchanged.

The rejection checks create isolated temporary databases and attempt
to remove their temporary directory afterward.

These checks currently target schema version 1. Their fixtures and
expectations must be reviewed when another migration is introduced.

Neither check uses the planned live database, `runtime/pokehunter.db`.

Migration rollback after a partially executed migration and concurrent
initialization have not yet been explicitly tested.

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
