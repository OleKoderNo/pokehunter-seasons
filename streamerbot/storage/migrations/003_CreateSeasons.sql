-- Migration 3: preserve season metadata and generation unlocks.
--
-- Configuration determines the active season.
-- These records provide persistent references for collection history.
--
-- The migration runner owns the transaction and version updates.

CREATE TABLE seasons (
    -- Match the configuration validator's case-insensitive identity rule
    -- for ordinary ASCII season IDs such as "season-1".
    season_id TEXT NOT NULL COLLATE NOCASE PRIMARY KEY
        CHECK (length(trim(season_id)) > 0),

    name TEXT NOT NULL
        CHECK (length(trim(name)) > 0),

    -- Storage code writes normalized UTC round-trip timestamps.
    -- It also validates that the end is later than the start.
    starts_at_utc TEXT NOT NULL
        CHECK (length(trim(starts_at_utc)) > 0),

    ends_at_exclusive_utc TEXT NOT NULL
        CHECK (length(trim(ends_at_exclusive_utc)) > 0),

    created_at_utc TEXT NOT NULL
        CHECK (length(trim(created_at_utc)) > 0)
);

CREATE TABLE season_generations (
    season_id TEXT NOT NULL COLLATE NOCASE,

    generation INTEGER NOT NULL
        CHECK (
            typeof(generation) = 'integer'
            AND generation > 0
        ),

    -- A generation can appear only once within a particular season.
    PRIMARY KEY (season_id, generation),

    -- Generation records must belong to an existing season.
    -- Prevent accidental deletion or renaming of their parent season.
    FOREIGN KEY (season_id)
        REFERENCES seasons (season_id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
);