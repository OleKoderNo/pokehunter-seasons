using System;
using System.Data.SQLite;
using System.IO;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class TrainerStorageCheck
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
        // Each run gets a fresh database containing only synthetic test data.
        string temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            "PokeHunter-TrainerStorage-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            string databasePath = Path.Combine(
                temporaryDirectory,
                "trainers-test.db"
            );

            DatabaseInitializer.Initialize(databasePath);

            var repository = new TrainerRepository(databasePath);

            const string userId = "100000001";
            const string otherUserId = "100000002";

            Require(
                repository.FindByTwitchUserId(userId) == null,
                "An unknown trainer should return null."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: An unknown trainer returned null."
            );

            Trainer created = repository.SaveProfile(
                userId,
                "test_trainer",
                "Test_Trainer"
            );

            Require(
                created.TwitchUserId == userId &&
                created.LoginName == "test_trainer" &&
                created.DisplayName == "Test_Trainer",
                "The created profile contains unexpected values."
            );

            Require(
                created.CreatedAtUtc.Offset == TimeSpan.Zero &&
                created.UpdatedAtUtc.Offset == TimeSpan.Zero,
                "Trainer timestamps must use UTC."
            );

            Require(
                created.CreatedAtUtc == created.UpdatedAtUtc,
                "A new trainer should have matching timestamps."
            );

            // A new repository instance reads through a new connection.
            // This checks persisted data rather than reusing a returned object.
            var reopenedRepository = new TrainerRepository(databasePath);

            Trainer found = reopenedRepository.FindByTwitchUserId(userId);
            RequireSameProfile(created, found);

            CPH.LogInfo(
                "[PokéHunter] PASS: A trainer was created and read " +
                "through a separate repository."
            );

            // Give the fixture an old, fixed update timestamp.
            // This makes the next assertions meaningful even when all
            // operations execute within the same system-clock tick.
            SetOldUpdateTimestamp(databasePath, userId);

            Trainer beforeUnchangedSave =
                repository.FindByTwitchUserId(userId);

            Trainer unchanged = repository.SaveProfile(
                userId,
                "test_trainer",
                "Test_Trainer"
            );

            RequireSameProfile(beforeUnchangedSave, unchanged);

            CPH.LogInfo(
                "[PokéHunter] PASS: Saving an unchanged profile " +
                "preserved both timestamps."
            );

            Trainer renamed = repository.SaveProfile(
                userId,
                "renamed_trainer",
                "Renamed_Trainer"
            );

            Require(
                renamed.TwitchUserId == userId,
                "Renaming changed the trainer identity."
            );

            Require(
                renamed.CreatedAtUtc == created.CreatedAtUtc,
                "Renaming changed the creation timestamp."
            );

            Require(
                renamed.LoginName == "renamed_trainer" &&
                renamed.DisplayName == "Renamed_Trainer",
                "The new names were not saved."
            );

            Require(
                renamed.UpdatedAtUtc > unchanged.UpdatedAtUtc,
                "Renaming did not refresh the update timestamp."
            );

            RequireSameProfile(
                renamed,
                reopenedRepository.FindByTwitchUserId(userId)
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Renaming preserved identity and " +
                "creation time while updating the profile."
            );

            // Save a separate trainer and verify the first is unaffected.
            Trainer other = repository.SaveProfile(
                otherUserId,
                "other_trainer",
                "Other_Trainer"
            );

            Require(
                other.TwitchUserId == otherUserId,
                "The second trainer has the wrong identity."
            );

            RequireSameProfile(
                renamed,
                repository.FindByTwitchUserId(userId)
            );

            Require(
                CountTrainers(databasePath) == 2,
                "Expected exactly two trainers after repeated saves."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: Different trainers remained separate " +
                "and repeated saves created no duplicate rows."
            );

            ExpectArgumentRejected(
                () => repository.SaveProfile(
                    userId,
                    " ",
                    "Invalid_Profile"
                ),
                "loginName"
            );

            RequireSameProfile(
                renamed,
                repository.FindByTwitchUserId(userId)
            );

            Require(
                CountTrainers(databasePath) == 2,
                "Rejected input changed the trainer count."
            );

            CPH.LogInfo(
                "[PokéHunter] PASS: A blank login name was rejected " +
                "without changing the saved profile."
            );

            CPH.LogInfo(
                "[PokéHunter] All trainer storage checks passed."
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[PokéHunter] Trainer storage check failed: " +
                exception
            );

            return false;
        }
        finally
        {
            // Only remove the unique directory owned by this execution.
            try
            {
                if (Directory.Exists(temporaryDirectory))
                {
                    Directory.Delete(temporaryDirectory, true);
                }
            }
            catch (Exception exception)
            {
                CPH.LogWarn(
                    "[PokéHunter] Could not remove temporary test folder '" +
                    temporaryDirectory + "': " + exception.Message
                );
            }
        }
    }

    /// <summary>
    /// Sets a deterministic timestamp in this disposable test fixture.
    /// This deliberately bypasses normal storage rules for testing only.
    /// </summary>
    private static void SetOldUpdateTimestamp(
        string databasePath,
        string userId
    )
    {
        using (var connection = OpenFixtureConnection(databasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                @"UPDATE trainers
                  SET updated_at_utc = @timestamp
                  WHERE twitch_user_id = @userId;";

            command.Parameters.AddWithValue(
                "@timestamp",
                "2000-01-01T00:00:00.0000000+00:00"
            );
            command.Parameters.AddWithValue("@userId", userId);

            Require(
                command.ExecuteNonQuery() == 1,
                "Could not prepare the timestamp fixture."
            );
        }
    }

    private static int CountTrainers(string databasePath)
    {
        using (var connection = OpenFixtureConnection(databasePath))
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM trainers;";
            return Convert.ToInt32(command.ExecuteScalar());
        }
    }

    /// <summary>
    /// Opens only an existing test database.
    /// The caller must dispose the returned connection.
    /// </summary>
    private static SQLiteConnection OpenFixtureConnection(
        string databasePath
    )
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
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Compares every value represented by the trainer snapshot.
    /// </summary>
    private static void RequireSameProfile(
        Trainer expected,
        Trainer actual
    )
    {
        Require(
            expected != null && actual != null,
            "Expected a saved trainer profile."
        );

        Require(
            expected.TwitchUserId == actual.TwitchUserId &&
            expected.LoginName == actual.LoginName &&
            expected.DisplayName == actual.DisplayName &&
            expected.CreatedAtUtc == actual.CreatedAtUtc &&
            expected.UpdatedAtUtc == actual.UpdatedAtUtc,
            "The saved trainer profile did not match the expected values."
        );
    }

    /// <summary>
    /// Requires input validation to reject the intended parameter.
    /// Other exception types fail the test.
    /// </summary>
    private static void ExpectArgumentRejected(
        Action operation,
        string parameterName
    )
    {
        try
        {
            operation();
        }
        catch (ArgumentException exception)
        {
            Require(
                exception.ParamName == parameterName,
                "Input was rejected for an unexpected parameter."
            );

            return;
        }

        throw new InvalidOperationException(
            "Invalid input was accepted."
        );
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}