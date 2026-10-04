using System;
using System.IO;
using Newtonsoft.Json.Linq;
using PokeHunter.Configuration;

internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Usage: ConfigurationChecks.exe <game.json> <seasons.json>"
            );

            return 1;
        }

        string temporaryFolder = Path.Combine(
            Path.GetTempPath(),
            "pokehunter-config-check-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            Directory.CreateDirectory(temporaryFolder);

            CheckGameConfiguration(args[0], temporaryFolder);
            CheckSeasonConfiguration(args[1], temporaryFolder);

            Console.WriteLine("All configuration checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
        finally
        {
            // Remove only the temporary folder created by this run.
            // Cleanup failure must not hide the original test result.
            try
            {
                if (Directory.Exists(temporaryFolder))
                {
                    Directory.Delete(temporaryFolder, true);
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(
                    "Warning: Could not remove temporary test files at '" +
                    temporaryFolder + "': " + exception.Message
                );
            }
        }
    }

    private static void CheckGameConfiguration(
        string filePath,
        string temporaryFolder
    )
    {
        GameConfig config = GameConfigLoader.Load(filePath);

        Console.WriteLine("PASS: The real game configuration loaded.");
        Console.WriteLine(
            "Reward cost: " + config.Redemption.Cost +
            " | Cooldown: " +
            config.Redemption.PerUserCooldownSeconds +
            " seconds"
        );

        // Modify an in-memory copy, then save it to a temporary file.
        JObject invalidGame = JObject.Parse(File.ReadAllText(filePath));

        invalidGame["shiny"]["collectionBonus"]
            ["uniqueEntriesPerMilestone"] = 0;

        string invalidPath = WriteTemporaryJson(
            temporaryFolder,
            "invalid-game-milestone.json",
            invalidGame
        );

        ExpectRejected(
            () => { GameConfigLoader.Load(invalidPath); },
            "uniqueEntriesPerMilestone",
            "must be greater than zero"
        );

        Console.WriteLine("PASS: A zero milestone size was rejected.");
    }

    private static void CheckSeasonConfiguration(
        string filePath,
        string temporaryFolder
    )
    {
        SeasonsConfig config = SeasonsConfigLoader.Load(filePath);

        Console.WriteLine("PASS: The real season configuration loaded.");
        Console.WriteLine(
            "Configured seasons: " + config.Seasons.Count +
            " | Time zone: " + config.TimeZone
        );

        // First confirm that our controlled example is valid.
        // Its adjoining boundaries use different offsets but represent
        // exactly the same instant.
        string adjoiningPath = WriteTemporaryJson(
            temporaryFolder,
            "adjoining-seasons.json",
            CreateSeasonFixture()
        );

        SeasonsConfig adjoining = SeasonsConfigLoader.Load(adjoiningPath);

        if (adjoining.Seasons[0].EndsAtExclusive !=
            adjoining.Seasons[1].StartsAt)
        {
            throw new InvalidOperationException(
                "Adjoining season boundaries did not represent " +
                "the same instant."
            );
        }

        if (adjoining.Seasons[0].EndsAtExclusive.Offset !=
            TimeSpan.FromHours(1))
        {
            throw new InvalidOperationException(
                "The timestamp converter did not preserve the UTC offset."
            );
        }

        Console.WriteLine(
            "PASS: Adjoining seasons with equivalent timestamps were accepted."
        );

        // Start the second season one second before the first ends.
        JObject overlapping = CreateSeasonFixture();

        overlapping["seasons"][1]["startsAt"] =
            "2026-12-31T22:59:59Z";

        string overlappingPath = WriteTemporaryJson(
            temporaryFolder,
            "overlapping-seasons.json",
            overlapping
        );

        ExpectRejected(
            () => { SeasonsConfigLoader.Load(overlappingPath); },
            "overlaps season"
        );

        Console.WriteLine("PASS: Overlapping seasons were rejected.");

        // Keep the date and time valid, but remove the required offset.
        JObject missingOffset = CreateSeasonFixture();

        missingOffset["seasons"][0]["startsAt"] =
            "2026-01-01T00:00:00";

        string missingOffsetPath = WriteTemporaryJson(
            temporaryFolder,
            "missing-timestamp-offset.json",
            missingOffset
        );

        ExpectRejected(
            () => { SeasonsConfigLoader.Load(missingOffsetPath); },
            "seasons[0].startsAt",
            "explicit UTC offset"
        );

        Console.WriteLine(
            "PASS: A timestamp without an explicit offset was rejected."
        );
    }

    /// <summary>
    /// Creates a fresh, predictable schedule for each scenario.
    /// Timestamp values remain strings so the loader tests their parsing.
    /// </summary>
    private static JObject CreateSeasonFixture()
    {
        return JObject.FromObject(new
        {
            schemaVersion = 1,
            timeZone = "Europe/Oslo",
            seasons = new[]
            {
                new
                {
                    id = "test-season-1",
                    name = "Test Season 1",
                    startsAt = "2026-01-01T00:00:00+01:00",
                    endsAtExclusive = "2027-01-01T00:00:00+01:00",
                    unlockedGenerations = new[] { 1 }
                },
                new
                {
                    id = "test-season-2",
                    name = "Test Season 2",
                    startsAt = "2026-12-31T23:00:00Z",
                    endsAtExclusive = "2028-01-01T00:00:00+01:00",
                    unlockedGenerations = new[] { 1, 2 }
                }
            }
        });
    }

    private static string WriteTemporaryJson(
        string folder,
        string filename,
        JObject document
    )
    {
        string path = Path.Combine(folder, filename);
        File.WriteAllText(path, document.ToString());
        return path;
    }

    /// <summary>
    /// Requires the operation to reject its input for the expected reason.
    /// An unrelated exception does not count as a passing check.
    /// </summary>
    private static void ExpectRejected(
        Action operation,
        params string[] expectedMessageParts
    )
    {
        try
        {
            operation();
        }
        catch (InvalidDataException exception)
        {
            foreach (string expectedPart in expectedMessageParts)
            {
                if (exception.Message.IndexOf(
                    expectedPart,
                    StringComparison.OrdinalIgnoreCase
                ) < 0)
                {
                    throw new InvalidOperationException(
                        "Configuration was rejected for an unexpected reason. " +
                        "Expected the message to contain '" +
                        expectedPart + "'. Actual message: " +
                        exception.Message,
                        exception
                    );
                }
            }

            return;
        }

        throw new InvalidOperationException(
            "Invalid configuration was accepted."
        );
    }
}