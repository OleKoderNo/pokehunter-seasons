using System;
using Newtonsoft.Json.Linq;
using PokeHunter.Configuration;

/// <summary>
/// Checks season loading, adjoining boundaries, and selected invalid inputs.
/// </summary>
internal static class SeasonConfigurationChecks
{
    /// <summary>
    /// Loads the supplied season configuration, then checks controlled
    /// examples independently of the creator's actual schedule.
    /// </summary>
    public static void Run(string filePath, string temporaryFolder)
    {
        SeasonsConfig config = SeasonsConfigLoader.Load(filePath);

        Console.WriteLine("PASS: The real season configuration loaded.");
        Console.WriteLine(
            "Configured seasons: " + config.Seasons.Count +
            " | Time zone: " + config.TimeZone
        );

        CheckAdjoiningSeasonsAccepted(temporaryFolder);
        CheckOverlappingSeasonsRejected(temporaryFolder);
        CheckMissingOffsetRejected(temporaryFolder);
    }

    private static void CheckAdjoiningSeasonsAccepted(
        string temporaryFolder
    )
    {
        string path = TestJsonFiles.Write(
            temporaryFolder,
            "adjoining-seasons.json",
            CreateSeasonFixture()
        );

        SeasonsConfig config = SeasonsConfigLoader.Load(path);

        // Midnight at +01:00 and 23:00 UTC on the previous day
        // describe the same instant.
        if (config.Seasons[0].EndsAtExclusive !=
            config.Seasons[1].StartsAt)
        {
            throw new InvalidOperationException(
                "Adjoining season boundaries did not represent " +
                "the same instant."
            );
        }

        // Instant equality alone would not prove that parsing preserved
        // the original offset, so check that separately.
        if (config.Seasons[0].EndsAtExclusive.Offset !=
            TimeSpan.FromHours(1))
        {
            throw new InvalidOperationException(
                "The timestamp converter did not preserve the UTC offset."
            );
        }

        Console.WriteLine(
            "PASS: Adjoining seasons with equivalent timestamps were accepted."
        );
    }

    private static void CheckOverlappingSeasonsRejected(
        string temporaryFolder
    )
    {
        JObject overlapping = CreateSeasonFixture();

        // Move the second season's start to one second before the first
        // season ends. Everything else remains valid.
        overlapping["seasons"][1]["startsAt"] =
            "2026-12-31T22:59:59Z";

        string path = TestJsonFiles.Write(
            temporaryFolder,
            "overlapping-seasons.json",
            overlapping
        );

        CheckAssert.Rejected(
            () => { SeasonsConfigLoader.Load(path); },
            "overlaps season"
        );

        Console.WriteLine("PASS: Overlapping seasons were rejected.");
    }

    private static void CheckMissingOffsetRejected(
        string temporaryFolder
    )
    {
        JObject missingOffset = CreateSeasonFixture();

        // Keep the date and time valid, but omit the required UTC offset.
        missingOffset["seasons"][0]["startsAt"] =
            "2026-01-01T00:00:00";

        string path = TestJsonFiles.Write(
            temporaryFolder,
            "missing-timestamp-offset.json",
            missingOffset
        );

        CheckAssert.Rejected(
            () => { SeasonsConfigLoader.Load(path); },
            "seasons[0].startsAt",
            "explicit UTC offset"
        );

        Console.WriteLine(
            "PASS: A timestamp without an explicit offset was rejected."
        );
    }

    /// <summary>
    /// Creates a fresh, valid schedule for each scenario.
    /// Timestamp values remain strings to exercise the loader's parser.
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
}