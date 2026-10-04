using System;
using System.Collections.Generic;
using PokeHunter.Configuration;

/// <summary>
/// Checks active-season selection and the continuous-schedule rule.
/// Uses fixed instants so results do not depend on today's date.
/// </summary>
internal static class SeasonSelectionChecks
{
    public static void Run()
    {
        SeasonsConfig config = CreateSchedule();

        DateTimeOffset firstStart = config.Seasons[0].StartsAt;
        DateTimeOffset transition = config.Seasons[0].EndsAtExclusive;
        DateTimeOffset finalEnd = config.Seasons[1].EndsAtExclusive;

        ExpectSeason(
            config,
            firstStart.AddTicks(-1),
            null,
            "Before the first season"
        );

        ExpectSeason(
            config,
            firstStart,
            "test-season-1",
            "At the first season's exact start"
        );

        ExpectSeason(
            config,
            transition.AddTicks(-1),
            "test-season-1",
            "Immediately before the transition"
        );

        ExpectSeason(
            config,
            transition,
            "test-season-2",
            "At the exact season transition"
        );

        ExpectSeason(
            config,
            transition.ToOffset(TimeSpan.FromHours(-5)),
            "test-season-2",
            "At the same transition expressed with another offset"
        );

        ExpectSeason(
            config,
            finalEnd.AddTicks(-1),
            "test-season-2",
            "Immediately before the final end"
        );

        ExpectSeason(
            config,
            finalEnd,
            null,
            "At the final configured end"
        );

        ExpectSeason(
            config,
            finalEnd.AddDays(1),
            null,
            "After the configured schedule"
        );

        Console.WriteLine(
            "PASS: Season selection respects inclusive starts " +
            "and exclusive ends."
        );

        // File order must not determine which season is selected.
        SeasonsConfig reversed = CreateSchedule();
        reversed.Seasons.Reverse();

        ExpectSeason(
            reversed,
            transition,
            "test-season-2",
            "With season definitions in reverse order"
        );

        Console.WriteLine(
            "PASS: Season selection is independent of definition order."
        );

        CheckGapRejected();
    }

    private static void CheckGapRejected()
    {
        SeasonsConfig config = CreateSchedule();

        // Introduce a one-second gap without making either season's
        // individual start/end range invalid.
        config.Seasons[1].StartsAt =
            config.Seasons[1].StartsAt.AddSeconds(1);

        // Use the public selector to verify that it rejects an invalid
        // schedule rather than silently treating the gap as normal.
        CheckAssert.Rejected(
            () =>
            {
                SeasonSelector.FindActive(
                    config,
                    config.Seasons[0].StartsAt
                );
            },
            "Gap between season"
        );

        Console.WriteLine("PASS: A gap between seasons was rejected.");
    }

    /// <summary>
    /// Checks the selected season ID. A null expected ID means that
    /// no season should cover the supplied instant.
    /// </summary>
    private static void ExpectSeason(
        SeasonsConfig config,
        DateTimeOffset instant,
        string expectedId,
        string scenario
    )
    {
        SeasonDefinition selected =
            SeasonSelector.FindActive(config, instant);

        string actualId = selected == null ? null : selected.Id;

        if (!string.Equals(
            actualId,
            expectedId,
            StringComparison.Ordinal
        ))
        {
            throw new InvalidOperationException(
                scenario + ": expected " +
                (expectedId ?? "no season") +
                ", but received " +
                (actualId ?? "no season") + "."
            );
        }
    }

    /// <summary>
    /// Creates a continuous schedule directly as C# objects.
    /// JSON parsing is covered by SeasonConfigurationChecks.
    /// </summary>
    private static SeasonsConfig CreateSchedule()
    {
        TimeSpan offset = TimeSpan.FromHours(1);

        var start = new DateTimeOffset(
            2026, 1, 1, 0, 0, 0, offset
        );

        var transition = new DateTimeOffset(
            2027, 1, 1, 0, 0, 0, offset
        );

        var end = new DateTimeOffset(
            2028, 1, 1, 0, 0, 0, offset
        );

        return new SeasonsConfig
        {
            SchemaVersion = 1,
            TimeZone = "Europe/Oslo",
            Seasons = new List<SeasonDefinition>
            {
                new SeasonDefinition
                {
                    Id = "test-season-1",
                    Name = "Test Season 1",
                    StartsAt = start,
                    EndsAtExclusive = transition,
                    UnlockedGenerations = new List<int> { 1 }
                },
                new SeasonDefinition
                {
                    Id = "test-season-2",
                    Name = "Test Season 2",
                    StartsAt = transition,
                    EndsAtExclusive = end,
                    UnlockedGenerations = new List<int> { 1, 2 }
                }
            }
        };
    }
}