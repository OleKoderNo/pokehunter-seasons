-- Migration 1: create the migration-history table.
--
-- Keep this migration compatible with databases already initialized
-- by the original C# implementation.
--
-- The initializer owns the transaction, inserts the history record,
-- and updates the application identifier and schema version.

CREATE TABLE schema_migrations (
    version INTEGER NOT NULL PRIMARY KEY
        CHECK (version > 0),
    name TEXT NOT NULL,
    applied_at_utc TEXT NOT NULL
);