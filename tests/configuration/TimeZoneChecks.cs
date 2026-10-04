using System;
using System.Collections.Generic;
using PokeHunter.Configuration;

/// <summary>
/// Checks configured time-zone resolution and local-time conversion.
/// Uses fixed dates rather than the computer's current date.
/// </summary>
internal static class TimeZoneChecks
{
    public static void Run()
    {
        TimeZoneInfo zone = ConfigurationTimeZone.Resolve("Europe/Oslo");

        Console.WriteLine(
            "PASS: Europe/Oslo resolved successfully."
        );

        CheckWinterConversion(zone);
        CheckSummerConversion(zone);
        CheckUnknownZoneRejected();
    }

    private static void CheckWinterConversion(TimeZoneInfo zone)
    {
        // In this winter example, noon UTC should become 13:00 in Oslo.
        var utcInstant = new DateTimeOffset(
            2026, 1, 15, 12, 0, 0, TimeSpan.Zero
        );

        var expected = new DateTimeOffset(
            2026, 1, 15, 13, 0, 0, TimeSpan.FromHours(1)
        );

        ExpectConversion(zone, utcInstant, expected, "Winter");

        Console.WriteLine(
            "PASS: Oslo winter conversion uses UTC+01:00."
        );
    }

    private static void CheckSummerConversion(TimeZoneInfo zone)
    {
        // In this summer example, noon UTC should become 14:00 in Oslo.
        var utcInstant = new DateTimeOffset(
            2026, 7, 15, 12, 0, 0, TimeSpan.Zero
        );

        var expected = new DateTimeOffset(
            2026, 7, 15, 14, 0, 0, TimeSpan.FromHours(2)
        );

        ExpectConversion(zone, utcInstant, expected, "Summer");

        Console.WriteLine(
            "PASS: Oslo summer conversion uses UTC+02:00."
        );
    }

    /// <summary>
    /// Verifies both the displayed local time and its offset.
    /// Also confirms that conversion preserves the original instant.
    /// </summary>
    private static void ExpectConversion(
        TimeZoneInfo zone,
        DateTimeOffset utcInstant,
        DateTimeOffset expected,
        string scenario
    )
    {
        DateTimeOffset actual = TimeZoneInfo.ConvertTime(
            utcInstant,
            zone
        );

        // DateTimeOffset equality alone compares instants.
        // Check the local clock value and offset separately as well.
        if (actual.DateTime != expected.DateTime ||
            actual.Offset != expected.Offset)
        {
            throw new InvalidOperationException(
                scenario + " conversion: expected " +
                expected.ToString("O") +
                ", but received " +
                actual.ToString("O") + "."
            );
        }

        if (actual != utcInstant)
        {
            throw new InvalidOperationException(
                scenario + " conversion changed the represented instant."
            );
        }
    }

    private static void CheckUnknownZoneRejected()
    {
        // Everything except the time-zone name is valid.
        // Calling the validator verifies that resolution is connected,
        // rather than only testing the resolver in isolation.
        var config = new SeasonsConfig
        {
            SchemaVersion = 1,
            TimeZone = "Invalid/PokeHunter-Test-Zone",
            Seasons = new List<SeasonDefinition>
            {
                new SeasonDefinition
                {
                    Id = "test-season",
                    Name = "Test Season",
                    StartsAt = new DateTimeOffset(
                        2026, 1, 1, 0, 0, 0, TimeSpan.Zero
                    ),
                    EndsAtExclusive = new DateTimeOffset(
                        2027, 1, 1, 0, 0, 0, TimeSpan.Zero
                    ),
                    UnlockedGenerations = new List<int> { 1 }
                }
            }
        };

        CheckAssert.Rejected(
            () => { SeasonsConfigValidator.Validate(config); },
            "timeZone",
            "Invalid/PokeHunter-Test-Zone",
            "could not be resolved"
        );

        Console.WriteLine(
            "PASS: Season validation rejects an unknown time zone."
        );
    }
}