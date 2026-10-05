using System;
using System.Collections.Generic;
using PokeHunter.Configuration;

/// <summary>
/// Creates fresh event configurations for standalone checks.
/// These examples do not depend on the real event roster or today's date.
/// </summary>
internal static class EventTestFixtures
{
    public static EventsConfig Create()
    {
        return new EventsConfig
        {
            SchemaVersion = 1,
            TimeZone = "Europe/Oslo",
            Events = new List<EventDefinition>
            {
                CreateEvent("test-event-1", 1, 2),
                CreateEvent("test-event-2", 2, 3)
            }
        };
    }

    private static EventDefinition CreateEvent(
        string id,
        int startMonth,
        int endMonth
    )
    {
        return new EventDefinition
        {
            Id = id,
            Name = id,
            Enabled = true,
            StartsAt = new DateTimeOffset(
                2026, startMonth, 1, 0, 0, 0, TimeSpan.Zero
            ),
            EndsAtExclusive = new DateTimeOffset(
                2026, endMonth, 1, 0, 0, 0, TimeSpan.Zero
            ),
            CategoryWeight = 4,
            AllowLockedGenerations = true,
            RequireUnlockedGenerationForLegendary = true,
            RequireUnlockedGenerationForMythical = true,
            Include = new EventInclusionConfig
            {
                Types = new List<string> { "ghost" },
                EvolutionFamilies = new List<string>(),
                Forms = new List<string>(),
                Costumes = new List<string>()
            }
        };
    }
}