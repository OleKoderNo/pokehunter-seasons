-- Migration 2: create permanent trainer identities.
--
-- The migration runner owns the transaction and records completion.
-- Do not add BEGIN, COMMIT, or schema-version updates to this file.

CREATE TABLE trainers (
    -- Stable account identity. Names must never be used as this key.
    twitch_user_id TEXT NOT NULL PRIMARY KEY
        CHECK (length(trim(twitch_user_id)) > 0),

    -- Mutable profile information.
    login_name TEXT NOT NULL
        CHECK (length(trim(login_name)) > 0),

    display_name TEXT NOT NULL
        CHECK (length(trim(display_name)) > 0),

    -- Written by C# as UTC timestamps using the round-trip "O" format.
    -- Parsing and timestamp validation belong to the storage code.
    created_at_utc TEXT NOT NULL
        CHECK (length(trim(created_at_utc)) > 0),

    updated_at_utc TEXT NOT NULL
        CHECK (length(trim(updated_at_utc)) > 0)
);