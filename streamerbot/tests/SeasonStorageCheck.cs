using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class SeasonStorageCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        // Every run gets its own database. The real game database and
        // the persistent initialization-test database are not used.
        string temporaryFolder = Path.Combine(
            Path.GetTempPath(),
            "PokeHunter-SeasonStorage-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            Directory.CreateDirectory(temporaryFolder);

            string databasePath = Path.Combine(
                temporaryFolder,
                "season-storage-test.db"
            );

            DatabaseInitializer.Initialize(databasePath);

            var repository = new SeasonRepository(databasePath);

            // Fixed timestamps keep these checks independent of today's
            // date, the computer's time zone, and the real season schedule.
            var start = new DateTimeOffset(
                2030, 1, 1, 0, 0, 0,
                TimeSpan.FromHours(1)
            );

            var end = new DateTimeOffset(
                2031, 1, 1, 0, 0, 0,
                TimeSpan.FromHours(1)
            );

            CheckRegistration(repository, databasePath, start, end);
            CheckConflicts(repository, databasePath, start, end);
            CheckInvalidGenerations(repository, databasePath, start, end);
            CheckUnicodeIdentity(repository, databasePath, start, end);
            CheckRollback(repository, databasePath, start, end);

            CPH.LogInfo(
                "[PokéHunter] All season storage checks passed."
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[PokéHunter] Season storage check failed: " +
                exception
            );

            return false;
        }
        finally
        {
            // Cleanup is attempted even when a check fails.
            // A cleanup failure must not hide the original test result.
            try
            {
                if (Directory.Exists(temporaryFolder))
                {
                    Directory.Delete(temporaryFolder, true);
                }
            }
            catch (Exception exception)
            {
                CPH.LogWarn(
                    "[PokéHunter] Could not remove temporary season " +
                    "check folder '" + temporaryFolder + "': " +
                    exception.Message
                );
            }
        }
    }

    /// <summary>
    /// Checks creation, persistence, UTC conversion, and repeat registration.
    /// </summary>
    private void CheckRegistration(
        SeasonRepository repository,
        string databasePath,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        Require(
            repository.FindById("missing-season") == null,
            "An unknown season should return null."
        );

        Pass("An unknown season returned null.");

        int[] inputGenerations = { 2, 1 };

        DateTimeOffset beforeRegistration = DateTimeOffset.UtcNow;

        StoredSeason saved = repository.Register(
            "test-season",
            "Trainer's Test Season",
            start,
            end,
            inputGenerations
        );

        DateTimeOffset afterRegistration = DateTimeOffset.UtcNow;

        Require(saved.Id == "test-season", "The season ID changed.");
        Require(
            saved.Name == "Trainer's Test Season",
            "The season name was not preserved."
        );

        Require(
            saved.StartsAtUtc == start &&
            saved.EndsAtExclusiveUtc == end,
            "Stored boundaries represent different instants."
        );

        Require(
            saved.StartsAtUtc.Offset == TimeSpan.Zero &&
            saved.EndsAtExclusiveUtc.Offset == TimeSpan.Zero &&
            saved.CreatedAtUtc.Offset == TimeSpan.Zero,
            "Stored timestamps should use UTC."
        );

        Require(
            saved.CreatedAtUtc >= beforeRegistration &&
            saved.CreatedAtUtc <= afterRegistration,
            "The creation timestamp should describe registration time."
        );

        RequireGenerations(saved, 1, 2);

        // Changing the caller's array must not change the saved snapshot.
        inputGenerations[0] = 99;
        RequireGenerations(saved, 1, 2);

        var anotherRepository = new SeasonRepository(databasePath);

        StoredSeason reread = anotherRepository.FindById("TEST-SEASON");

        RequireSameSeason(saved, reread);

        Pass(
            "A season was saved in UTC and read through another " +
            "repository using a case-insensitive ID."
        );

        // The same instants expressed with other offsets are equivalent.
        // Generation order and ID capitalization are also insignificant.
        StoredSeason repeated = repository.Register(
            "TEST-SEASON",
            saved.Name,
            start.ToOffset(TimeSpan.FromHours(-4)),
            end.ToUniversalTime(),
            new[] { 1, 2 }
        );

        RequireSameSeason(saved, repeated);
        RequireCounts(databasePath, 1, 2);

        Pass(
            "Equivalent registration preserved the original record " +
            "and creation timestamp without duplicate rows."
        );
    }

    /// <summary>
    /// Checks each protected field independently.
    /// Every rejected registration must leave the saved record unchanged.
    /// </summary>
    private void CheckConflicts(
        SeasonRepository repository,
        string databasePath,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        StoredSeason original = repository.FindById("test-season");

        ExpectConflict(
            () => repository.Register(
                original.Id,
                "Changed name",
                start,
                end,
                new[] { 1, 2 }
            )
        );

        RequireSameSeason(original, repository.FindById(original.Id));

        ExpectConflict(
            () => repository.Register(
                original.Id,
                original.Name,
                start.AddDays(1),
                end,
                new[] { 1, 2 }
            )
        );

        RequireSameSeason(original, repository.FindById(original.Id));

        ExpectConflict(
            () => repository.Register(
                original.Id,
                original.Name,
                start,
                end.AddDays(1),
                new[] { 1, 2 }
            )
        );

        RequireSameSeason(original, repository.FindById(original.Id));

        ExpectConflict(
            () => repository.Register(
                original.Id,
                original.Name,
                start,
                end,
                new[] { 1, 3 }
            )
        );

        RequireSameSeason(original, repository.FindById(original.Id));
        RequireCounts(databasePath, 1, 2);

        Pass(
            "Conflicting names, boundaries, and generation lists " +
            "were rejected without changing the stored season."
        );
    }

    /// <summary>
    /// Invalid lists must be rejected before any season is inserted.
    /// </summary>
    private void CheckInvalidGenerations(
        SeasonRepository repository,
        string databasePath,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        int[][] invalidLists =
        {
            null,
            new int[0],
            new[] { 0 },
            new[] { -1 },
            new[] { 1, 1 }
        };

        foreach (int[] generations in invalidLists)
        {
            bool rejected = false;

            try
            {
                repository.Register(
                    "invalid-season",
                    "Invalid Season",
                    start,
                    end,
                    generations
                );
            }
            catch (ArgumentException exception)
            {
                if (exception.ParamName != "unlockedGenerations")
                {
                    throw;
                }

                rejected = true;
            }

            Require(
                rejected,
                "An invalid generation list was accepted."
            );

            Require(
                repository.FindById("invalid-season") == null,
                "Rejected input left a season record behind."
            );
        }

        RequireCounts(databasePath, 1, 2);

        Pass(
            "Missing, empty, nonpositive, and duplicate generation " +
            "lists were rejected without inserting records."
        );
    }

    /// <summary>
    /// Exercises the C# identity comparison beyond SQLite's ASCII NOCASE.
    /// Also verifies that a different season remains a separate record.
    /// </summary>
    private void CheckUnicodeIdentity(
        SeasonRepository repository,
        string databasePath,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        StoredSeason original = repository.FindById("test-season");

        StoredSeason international = repository.Register(
            "sæson-ø",
            "International Test Season",
            start,
            end,
            new[] { 3 }
        );

        StoredSeason repeated = repository.Register(
            "SÆSON-Ø",
            international.Name,
            start,
            end,
            new[] { 3 }
        );

        RequireSameSeason(international, repeated);
        RequireSameSeason(
            international,
            repository.FindById("SÆSON-Ø")
        );

        RequireSameSeason(original, repository.FindById(original.Id));
        RequireCounts(databasePath, 2, 3);

        Pass(
            "International IDs used case-insensitive identity while " +
            "different seasons remained separate."
        );
    }

    /// <summary>
    /// Forces a database failure after the parent and first generation
    /// have been inserted. The transaction must undo both earlier writes.
    /// </summary>
    private void CheckRollback(
        SeasonRepository repository,
        string databasePath,
        DateTimeOffset start,
        DateTimeOffset end
    )
    {
        // This trigger belongs only to our temporary test database.
        // It deliberately rejects generation 2 for one test season.
        using (var connection = OpenTestConnection(databasePath))
        {
            Execute(
                connection,
                @"CREATE TRIGGER fail_test_generation
                  BEFORE INSERT ON season_generations
                  WHEN NEW.season_id = 'rollback-season'
                       AND NEW.generation = 2
                  BEGIN
                    SELECT RAISE(
                        ABORT,
                        'Deliberate season storage check failure'
                    );
                  END;"
            );
        }

        bool rejected = false;

        try
        {
            repository.Register(
                "rollback-season",
                "Rollback Test Season",
                start,
                end,
                new[] { 1, 2 }
            );
        }
        catch (SQLiteException exception)
        {
            // An unrelated SQLite error must not count as a passing test.
            if (exception.Message.IndexOf(
                "Deliberate season storage check failure",
                StringComparison.Ordinal
            ) < 0)
            {
                throw;
            }

            rejected = true;
        }
        finally
        {
            using (var connection = OpenTestConnection(databasePath))
            {
                Execute(
                    connection,
                    "DROP TRIGGER IF EXISTS fail_test_generation;"
                );
            }
        }

        Require(rejected, "The deliberate insert failure did not occur.");

        Require(
            repository.FindById("rollback-season") == null,
            "A failed registration left its parent season behind."
        );

        // Checking both tables catches orphaned generation rows as well
        // as a parent season that incorrectly survived rollback.
        RequireCounts(databasePath, 2, 3);

        Pass(
            "A failed generation insert rolled back the season " +
            "and its earlier generation inserts."
        );

        // Once the deliberate failure is removed, the same registration
        // must succeed. This also checks that no transaction remains open.
        StoredSeason recovered = repository.Register(
            "rollback-season",
            "Rollback Test Season",
            start,
            end,
            new[] { 1, 2 }
        );

        RequireGenerations(recovered, 1, 2);
        RequireCounts(databasePath, 3, 5);

        Pass("Registration succeeded after the deliberate failure was removed.");
    }

    private static void ExpectConflict(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidDataException exception)
        {
            if (exception.Message.IndexOf(
                "already registered with different settings",
                StringComparison.Ordinal
            ) < 0)
            {
                throw;
            }

            return;
        }

        throw new InvalidOperationException(
            "A conflicting season registration was accepted."
        );
    }

    /// <summary>
    /// Compares all persisted values, including original ID capitalization
    /// and the creation timestamp.
    /// </summary>
    private static void RequireSameSeason(
        StoredSeason expected,
        StoredSeason actual
    )
    {
        Require(expected != null, "The expected season is missing.");
        Require(actual != null, "The saved season could not be retrieved.");

        Require(
            expected.Id == actual.Id &&
            expected.Name == actual.Name &&
            expected.StartsAtUtc == actual.StartsAtUtc &&
            expected.EndsAtExclusiveUtc == actual.EndsAtExclusiveUtc &&
            expected.CreatedAtUtc == actual.CreatedAtUtc,
            "The stored season values changed unexpectedly."
        );

        Require(
            actual.StartsAtUtc.Offset == TimeSpan.Zero &&
            actual.EndsAtExclusiveUtc.Offset == TimeSpan.Zero &&
            actual.CreatedAtUtc.Offset == TimeSpan.Zero,
            "The retrieved timestamps should use UTC."
        );

        Require(
            expected.UnlockedGenerations.Count ==
                actual.UnlockedGenerations.Count,
            "The generation count changed."
        );

        for (int index = 0;
            index < expected.UnlockedGenerations.Count;
            index++)
        {
            Require(
                expected.UnlockedGenerations[index] ==
                    actual.UnlockedGenerations[index],
                "The stored generations changed."
            );
        }
    }

    private static void RequireGenerations(
        StoredSeason season,
        params int[] expected
    )
    {
        Require(season != null, "The season is missing.");

        Require(
            season.UnlockedGenerations.Count == expected.Length,
            "Unexpected number of unlocked generations."
        );

        for (int index = 0; index < expected.Length; index++)
        {
            Require(
                season.UnlockedGenerations[index] == expected[index],
                "Unexpected unlocked generation or ordering."
            );
        }
    }

    /// <summary>
    /// Counts actual rows rather than relying only on repository results.
    /// This helps detect duplicate or orphaned records.
    /// </summary>
    private static void RequireCounts(
        string databasePath,
        int expectedSeasons,
        int expectedGenerations
    )
    {
        using (var connection = OpenTestConnection(databasePath))
        {
            Require(
                ReadCount(
                    connection,
                    "SELECT COUNT(*) FROM seasons;"
                ) == expectedSeasons,
                "Unexpected season row count."
            );

            Require(
                ReadCount(
                    connection,
                    "SELECT COUNT(*) FROM season_generations;"
                ) == expectedGenerations,
                "Unexpected season-generation row count."
            );
        }
    }

    /// <summary>
    /// Opens only the temporary database created by this check.
    /// Pooling is disabled so disposed connections release their files.
    /// </summary>
    private static SQLiteConnection OpenTestConnection(string databasePath)
    {
        var settings = new SQLiteConnectionStringBuilder
        {
            DataSource = databasePath,
            Version = 3,
            FailIfMissing = true,
            Pooling = false,
            DefaultTimeout = 5
        };

        var connection = new SQLiteConnection(settings.ConnectionString);

        try
        {
            connection.Open();
            Execute(connection, "PRAGMA foreign_keys = ON;");
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    private static long ReadCount(
        SQLiteConnection connection,
        string sql
    )
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;

            return Convert.ToInt64(
                command.ExecuteScalar(),
                CultureInfo.InvariantCulture
            );
        }
    }

    private static void Execute(SQLiteConnection connection, string sql)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private void Pass(string message)
    {
        CPH.LogInfo("[PokéHunter] PASS: " + message);
    }
}