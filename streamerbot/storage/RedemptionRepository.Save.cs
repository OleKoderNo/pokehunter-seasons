using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PokeHunter.Catalogue;

namespace PokeHunter.Storage
{
    public sealed partial class RedemptionRepository
    {
        /// <summary>
        /// Saves a completed redemption and its next failure streak
        /// in one transaction.
        ///
        /// Previously saved redemption IDs return their original result
        /// without applying progress changes again.
        ///
        /// The supplied catalogue should be the validated catalogue
        /// used to produce the encounter.
        /// </summary>
        public RedemptionSaveResult Save(
            RedemptionSaveRequest request,
            CatalogueIndex catalogue
        )
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (catalogue == null)
            {
                throw new ArgumentNullException(nameof(catalogue));
            }

            using (var connection = OpenConnection(false))
            {
                // Reserve the writer before checking for an existing result
                // or comparing collection state.
                Execute(connection, "BEGIN IMMEDIATE;");

                try
                {
                    StoredRedemption existing = ReadRedemption(
                        connection,
                        request.RedemptionId
                    );

                    if (existing != null)
                    {
                        RequireSameOwnerAndSeason(existing, request);

                        // A retry must not replace the original outcome
                        // or reset/increment the streak a second time.
                        Execute(connection, "COMMIT;");

                        return new RedemptionSaveResult(
                            existing,
                            false
                        );
                    }

                    CollectionSnapshot current =
                        CollectionRepository.ReadSnapshot(
                            connection,
                            request.ExpectedCollection.TwitchUserId,
                            request.ExpectedCollection.SeasonId
                        );

                    RequireUnchangedCollection(
                        request.ExpectedCollection,
                        current
                    );

                    RequireAcceptedDuringSeason(
                        connection,
                        current.SeasonId,
                        request.AcceptedAtUtc
                    );

                    string snapshotJson = PrepareCatchSnapshot(
                        request,
                        catalogue,
                        current
                    );

                    InsertResult(
                        connection,
                        request,
                        current.SeasonId,
                        snapshotJson
                    );

                    WriteProgress(
                        connection,
                        request,
                        current.SeasonId
                    );

                    StoredRedemption saved = ReadRedemption(
                        connection,
                        request.RedemptionId
                    );

                    RequireStored(
                        saved != null,
                        "The saved redemption could not be read."
                    );

                    Execute(connection, "COMMIT;");

                    return new RedemptionSaveResult(saved, true);
                }
                catch (Exception saveException)
                {
                    try
                    {
                        Execute(connection, "ROLLBACK;");
                    }
                    catch (Exception rollbackException)
                    {
                        throw new AggregateException(
                            "Saving the redemption failed, and rollback " +
                            "also reported an error.",
                            saveException,
                            rollbackException
                        );
                    }

                    throw;
                }
            }
        }

        /// <summary>
        /// Prevents an existing redemption ID being reused for another
        /// trainer or season. Its original saved outcome remains authoritative.
        /// </summary>
        private static void RequireSameOwnerAndSeason(
            StoredRedemption existing,
            RedemptionSaveRequest request
        )
        {
            bool matches =
                string.Equals(
                    existing.TwitchUserId,
                    request.ExpectedCollection.TwitchUserId,
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    existing.SeasonId,
                    request.ExpectedCollection.SeasonId,
                    StringComparison.OrdinalIgnoreCase
                );

            RequireStored(
                matches,
                "The redemption ID is already associated with " +
                "a different trainer or season."
            );
        }

        /// <summary>
        /// Detects changes that affect ownership, retry budgets,
        /// or lifetime collection bonuses.
        ///
        /// A stale proposal is a processing conflict, not a failed catch.
        /// Do not automatically record failure or silently reroll here.
        /// </summary>
        private static void RequireUnchangedCollection(
            CollectionSnapshot expected,
            CollectionSnapshot current
        )
        {
            bool matches =
                string.Equals(
                    expected.TwitchUserId,
                    current.TwitchUserId,
                    StringComparison.Ordinal
                ) &&
                string.Equals(
                    expected.SeasonId,
                    current.SeasonId,
                    StringComparison.Ordinal
                ) &&
                expected.ConsecutiveFailedRedemptions ==
                    current.ConsecutiveFailedRedemptions &&
                expected.UniqueNormalEntries ==
                    current.UniqueNormalEntries &&
                expected.UniqueShinyEntries ==
                    current.UniqueShinyEntries &&
                expected.SeasonalEntryIds.Count ==
                    current.SeasonalEntryIds.Count;

            if (matches)
            {
                // CollectionRepository returns IDs in a stable order.
                for (int index = 0;
                    index < expected.SeasonalEntryIds.Count;
                    index++)
                {
                    if (!string.Equals(
                        expected.SeasonalEntryIds[index],
                        current.SeasonalEntryIds[index],
                        StringComparison.Ordinal
                    ))
                    {
                        matches = false;
                        break;
                    }
                }
            }

            if (!matches)
            {
                throw new InvalidOperationException(
                    "Collection state changed after the redemption " +
                    "was rolled. The result was not saved."
                );
            }
        }

        /// <summary>
        /// Uses acceptance time to associate the result with a season.
        /// Completion may occur after the season's exclusive end.
        /// </summary>
        private static void RequireAcceptedDuringSeason(
            SQLiteConnection connection,
            string seasonId,
            DateTimeOffset acceptedAtUtc
        )
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    SELECT starts_at_utc,
                           ends_at_exclusive_utc
                    FROM seasons
                    WHERE season_id = @seasonId;";

                command.Parameters.AddWithValue(
                    "@seasonId",
                    seasonId
                );

                using (var reader = command.ExecuteReader())
                {
                    RequireStored(
                        reader.Read(),
                        "The redemption's season was not found."
                    );

                    DateTimeOffset start = ParseUtcTimestamp(
                        reader.GetString(0),
                        "starts_at_utc"
                    );

                    DateTimeOffset end = ParseUtcTimestamp(
                        reader.GetString(1),
                        "ends_at_exclusive_utc"
                    );

                    RequireStored(
                        end > start &&
                        acceptedAtUtc >= start &&
                        acceptedAtUtc < end,
                        "The redemption was not accepted during " +
                        "the specified season."
                    );
                }
            }
        }

        /// <summary>
        /// Checks that catch fields and supplied snapshot match the
        /// validated catalogue entry. Stores canonical serialized metadata.
        ///
        /// Encounter eligibility and the random roll remain responsibilities
        /// of the encounter engine, not this storage method.
        /// </summary>
        private static string PrepareCatchSnapshot(
            RedemptionSaveRequest request,
            CatalogueIndex catalogue,
            CollectionSnapshot current
        )
        {
            if (request.Outcome == StoredRedemptionOutcome.Failed)
            {
                return null;
            }

            CatalogueEntry entry = catalogue.FindEntry(
                request.EntryId
            );

            RequireStored(
                entry != null,
                "The caught entry does not exist in the supplied catalogue."
            );

            RequireStored(
                request.IsShiny.HasValue &&
                request.IsShiny.Value == entry.IsShiny &&
                string.Equals(
                    request.GroupId,
                    entry.EncounterGroupId,
                    StringComparison.Ordinal
                ),
                "Catch fields disagree with the catalogue entry."
            );

            foreach (string ownedId in current.SeasonalEntryIds)
            {
                RequireStored(
                    !string.Equals(
                        ownedId,
                        entry.Id,
                        StringComparison.Ordinal
                    ),
                    "The caught entry is already owned in this season."
                );
            }

            // Ordinary categories must be one of this form's types.
            // Event membership itself is checked by the encounter engine.
            bool categoryMatches = request.CategoryId == "event";

            foreach (string type in entry.Types)
            {
                if (string.Equals(
                    type,
                    request.CategoryId,
                    StringComparison.Ordinal
                ))
                {
                    categoryMatches = true;
                    break;
                }
            }

            RequireStored(
                categoryMatches,
                "The catch category does not match the entry's types."
            );

            RequireStored(
                request.CategoryId != "event" ||
                request.EventId != null,
                "An Event-category catch requires an event ID."
            );

            var settings = new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error,
                CheckAdditionalContent = true,
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                DateParseHandling = DateParseHandling.None,
                MaxDepth = 32
            };

            CatalogueEntry supplied;

            try
            {
                supplied = JsonConvert.DeserializeObject<CatalogueEntry>(
                    request.EntrySnapshotJson,
                    settings
                );
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "The caught-entry snapshot is invalid.",
                    exception
                );
            }

            RequireStored(
                supplied != null &&
                JToken.DeepEquals(
                    JToken.FromObject(entry),
                    JToken.FromObject(supplied)
                ),
                "The caught-entry snapshot does not match " +
                "the supplied catalogue."
            );

            // Store the validated entry rather than the caller's raw text.
            return JsonConvert.SerializeObject(
                entry,
                Formatting.None
            );
        }

        private static void InsertResult(
            SQLiteConnection connection,
            RedemptionSaveRequest request,
            string seasonId,
            string snapshotJson
        )
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    INSERT INTO redemption_results (
                        redemption_id,
                        twitch_user_id,
                        season_id,
                        accepted_at_utc,
                        completed_at_utc,
                        event_id,
                        outcome,
                        previous_failure_streak,
                        available_attempts,
                        attempts_used,
                        next_failure_streak,
                        entry_id,
                        is_shiny,
                        category_id,
                        group_id,
                        entry_snapshot_json
                    )
                    VALUES (
                        @id,
                        @user,
                        @season,
                        @accepted,
                        @completed,
                        @event,
                        @outcome,
                        @previous,
                        @available,
                        @used,
                        @next,
                        @entry,
                        @shiny,
                        @category,
                        @group,
                        @snapshot
                    );";

                command.Parameters.AddWithValue(
                    "@id", request.RedemptionId
                );
                command.Parameters.AddWithValue(
                    "@user", request.ExpectedCollection.TwitchUserId
                );
                command.Parameters.AddWithValue(
                    "@season", seasonId
                );
                command.Parameters.AddWithValue(
                    "@accepted", FormatTimestamp(request.AcceptedAtUtc)
                );
                command.Parameters.AddWithValue(
                    "@completed", FormatTimestamp(request.CompletedAtUtc)
                );
                command.Parameters.AddWithValue(
                    "@event", (object)request.EventId ?? DBNull.Value
                );
                command.Parameters.AddWithValue(
                    "@outcome",
                    request.Outcome == StoredRedemptionOutcome.Caught
                        ? "caught"
                        : "failed"
                );
                command.Parameters.AddWithValue(
                    "@previous", request.PreviousFailureStreak
                );
                command.Parameters.AddWithValue(
                    "@available", request.AvailableAttempts
                );
                command.Parameters.AddWithValue(
                    "@used", request.AttemptsUsed
                );
                command.Parameters.AddWithValue(
                    "@next", request.NextFailureStreak
                );
                command.Parameters.AddWithValue(
                    "@entry", (object)request.EntryId ?? DBNull.Value
                );
                command.Parameters.AddWithValue(
                    "@shiny",
                    request.IsShiny.HasValue
                        ? (object)(request.IsShiny.Value ? 1 : 0)
                        : DBNull.Value
                );
                command.Parameters.AddWithValue(
                    "@category", (object)request.CategoryId ?? DBNull.Value
                );
                command.Parameters.AddWithValue(
                    "@group", (object)request.GroupId ?? DBNull.Value
                );
                command.Parameters.AddWithValue(
                    "@snapshot", (object)snapshotJson ?? DBNull.Value
                );

                RequireStored(
                    command.ExecuteNonQuery() == 1,
                    "Saving the redemption did not insert exactly one row."
                );
            }
        }

        /// <summary>
        /// Inserts first-time seasonal progress or updates the existing row.
        /// Called inside the same transaction as the redemption insert.
        /// </summary>
        private static void WriteProgress(
            SQLiteConnection connection,
            RedemptionSaveRequest request,
            string seasonId
        )
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                    INSERT INTO trainer_season_progress (
                        twitch_user_id,
                        season_id,
                        consecutive_failed_redemptions,
                        updated_at_utc
                    )
                    VALUES (
                        @user,
                        @season,
                        @streak,
                        @updated
                    )
                    ON CONFLICT(twitch_user_id, season_id)
                    DO UPDATE SET
                        consecutive_failed_redemptions =
                            excluded.consecutive_failed_redemptions,
                        updated_at_utc = excluded.updated_at_utc;";

                command.Parameters.AddWithValue(
                    "@user", request.ExpectedCollection.TwitchUserId
                );
                command.Parameters.AddWithValue(
                    "@season", seasonId
                );
                command.Parameters.AddWithValue(
                    "@streak", request.NextFailureStreak
                );
                command.Parameters.AddWithValue(
                    "@updated", FormatTimestamp(request.CompletedAtUtc)
                );

                RequireStored(
                    command.ExecuteNonQuery() == 1,
                    "Saving seasonal progress did not affect exactly one row."
                );
            }
        }

        private static string FormatTimestamp(DateTimeOffset value)
        {
            return value.ToUniversalTime().ToString(
                "O",
                CultureInfo.InvariantCulture
            );
        }
    }
}