-- Migration 4: completed redemption outcomes and seasonal retry progress.
--
-- A successful redemption row is also its catch record.
-- Failed redemptions remain recorded for duplicate-delivery detection.
--
-- Repository code must save the result and update seasonal progress
-- within the same transaction.
--
-- This migration does not implement pending-session recovery or cooldowns.
-- The migration runner owns the transaction and schema-version changes.

CREATE TABLE redemption_results (
    -- Stable Twitch redemption identity, not a newly generated ID
    -- on each delivery or processing attempt.
    redemption_id TEXT NOT NULL PRIMARY KEY
        CHECK (length(trim(redemption_id)) > 0),

    twitch_user_id TEXT NOT NULL
        CHECK (length(trim(twitch_user_id)) > 0),

    season_id TEXT NOT NULL COLLATE NOCASE
        CHECK (length(trim(season_id)) > 0),

    -- Captured acceptance time determines the season/event context.
    -- Repository code writes normalized UTC round-trip timestamps.
    accepted_at_utc TEXT NOT NULL
        CHECK (length(trim(accepted_at_utc)) > 0),

    completed_at_utc TEXT NOT NULL
        CHECK (length(trim(completed_at_utc)) > 0),

    -- The active event at acceptance, if any.
    -- An active event does not mean the catch came from the Event category.
    event_id TEXT
        CHECK (
            event_id IS NULL
            OR length(trim(event_id)) > 0
        ),

    outcome TEXT NOT NULL
        CHECK (outcome IN ('caught', 'failed')),

    previous_failure_streak INTEGER NOT NULL
        CHECK (
            typeof(previous_failure_streak) = 'integer'
            AND previous_failure_streak >= 0
            AND previous_failure_streak < 2147483647
        ),

    available_attempts INTEGER NOT NULL
        CHECK (
            typeof(available_attempts) = 'integer'
            AND available_attempts = previous_failure_streak + 1
        ),

    attempts_used INTEGER NOT NULL
        CHECK (
            typeof(attempts_used) = 'integer'
            AND attempts_used >= 1
            AND attempts_used <= available_attempts
        ),

    next_failure_streak INTEGER NOT NULL
        CHECK (
            typeof(next_failure_streak) = 'integer'
            AND next_failure_streak >= 0
            AND next_failure_streak <= 2147483647
        ),

    -- These fields describe the successful catch only.
    -- They must all be null for a fully failed redemption.
    entry_id TEXT,
    is_shiny INTEGER,
    category_id TEXT,
    group_id TEXT,

    -- Preserve the selected CatalogueEntry as JSON so historical
    -- catches retain their metadata when the current catalogue changes.
    --
    -- Repository code validates/deserializes this snapshot and checks
    -- that its identity agrees with entry_id, group_id, and is_shiny.
    entry_snapshot_json TEXT,

    -- Explicit IS NOT NULL checks matter here:
    -- a SQL CHECK expression evaluating to NULL is not a rejection.
    CHECK (
        (
            outcome = 'caught'
            AND next_failure_streak = 0

            AND entry_id IS NOT NULL
            AND length(trim(entry_id)) > 0

            AND is_shiny IS NOT NULL
            AND typeof(is_shiny) = 'integer'
            AND is_shiny IN (0, 1)

            AND category_id IS NOT NULL
            AND length(trim(category_id)) > 0

            AND group_id IS NOT NULL
            AND length(trim(group_id)) > 0

            AND entry_snapshot_json IS NOT NULL
            AND length(trim(entry_snapshot_json)) > 0
        )
        OR
        (
            outcome = 'failed'
            AND attempts_used = available_attempts
            AND next_failure_streak = previous_failure_streak + 1

            AND entry_id IS NULL
            AND is_shiny IS NULL
            AND category_id IS NULL
            AND group_id IS NULL
            AND entry_snapshot_json IS NULL
        )
    ),

    FOREIGN KEY (twitch_user_id)
        REFERENCES trainers (twitch_user_id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    FOREIGN KEY (season_id)
        REFERENCES seasons (season_id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
);

-- A normal and shiny entry have different entry IDs.
-- Another season may contain another catch of the same entry.
CREATE UNIQUE INDEX ux_redemption_results_seasonal_entry
    ON redemption_results (
        twitch_user_id,
        season_id,
        entry_id
    )
    WHERE outcome = 'caught';

-- Supports lifetime unique-entry lookups across seasons.
CREATE INDEX ix_redemption_results_lifetime_entry
    ON redemption_results (
        twitch_user_id,
        entry_id
    )
    WHERE outcome = 'caught';

CREATE TABLE trainer_season_progress (
    twitch_user_id TEXT NOT NULL
        CHECK (length(trim(twitch_user_id)) > 0),

    season_id TEXT NOT NULL COLLATE NOCASE
        CHECK (length(trim(season_id)) > 0),

    consecutive_failed_redemptions INTEGER NOT NULL
        CHECK (
            typeof(consecutive_failed_redemptions) = 'integer'
            AND consecutive_failed_redemptions >= 0
            AND consecutive_failed_redemptions <= 2147483647
        ),

    updated_at_utc TEXT NOT NULL
        CHECK (length(trim(updated_at_utc)) > 0),

    PRIMARY KEY (twitch_user_id, season_id),

    FOREIGN KEY (twitch_user_id)
        REFERENCES trainers (twitch_user_id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT,

    FOREIGN KEY (season_id)
        REFERENCES seasons (season_id)
        ON UPDATE RESTRICT
        ON DELETE RESTRICT
);