using System;
using Newtonsoft.Json.Linq;
using PokeHunter.Configuration;

/// <summary>
/// Checks event loading, validation, and active-event selection.
/// Never modifies the supplied events.json file.
/// </summary>
internal static class EventConfigurationChecks
{
    public static void Run(string filePath, string temporaryFolder)
    {
        EventsConfig realConfig = EventsConfigLoader.Load(filePath);

        Console.WriteLine("PASS: The real event configuration loaded.");
        Console.WriteLine(
            "Configured events: " + realConfig.Events.Count +
            " | Time zone: " + realConfig.TimeZone
        );

        CheckBoundaries(temporaryFolder);
        CheckDisabledEvents();
        CheckGapsAndEmptySchedule();
        CheckInvalidInputs(temporaryFolder);
    }

    private static void CheckBoundaries(string temporaryFolder)
    {
        // Exercise the loader with a predictable, valid schedule.
        string path = TestJsonFiles.Write(
            temporaryFolder,
            "adjoining-events.json",
            CreateJsonFixture()
        );

        EventsConfig config = EventsConfigLoader.Load(path);

        DateTimeOffset start = config.Events[0].StartsAt;
        DateTimeOffset transition = config.Events[0].EndsAtExclusive;
        DateTimeOffset end = config.Events[1].EndsAtExclusive;

        ExpectEvent(config, start.AddTicks(-1), null);
        ExpectEvent(config, start, "test-event-1");
        ExpectEvent(config, transition.AddTicks(-1), "test-event-1");
        ExpectEvent(config, transition, "test-event-2");

        // The same instant expressed with another offset must select
        // the same event.
        ExpectEvent(
            config,
            transition.ToOffset(TimeSpan.FromHours(2)),
            "test-event-2"
        );

        ExpectEvent(config, end.AddTicks(-1), "test-event-2");
        ExpectEvent(config, end, null);
        ExpectEvent(config, end.AddDays(1), null);

        // Definition order must not affect selection.
        config.Events.Reverse();
        ExpectEvent(config, transition, "test-event-2");

        Console.WriteLine(
            "PASS: Event selection respects boundaries, offsets, " +
            "and definition order."
        );
    }

    private static void CheckDisabledEvents()
    {
        EventsConfig config = EventTestFixtures.Create();
        config.Events[0].Enabled = false;

        ExpectEvent(config, config.Events[0].StartsAt, null);

        // Overlap the disabled event with the enabled one.
        // The disabled definition must not block the enabled event.
        config.Events[0].EndsAtExclusive =
            config.Events[1].EndsAtExclusive;

        ExpectEvent(
            config,
            config.Events[1].StartsAt,
            "test-event-2"
        );

        Console.WriteLine(
            "PASS: Disabled events are ignored during selection " +
            "and overlap checks."
        );
    }

    private static void CheckGapsAndEmptySchedule()
    {
        EventsConfig config = EventTestFixtures.Create();

        DateTimeOffset gapStart = config.Events[0].EndsAtExclusive;

        // Unlike seasons, events may have unscheduled periods.
        config.Events[1].StartsAt =
            config.Events[1].StartsAt.AddDays(1);

        ExpectEvent(config, gapStart, null);
        ExpectEvent(config, gapStart.AddHours(12), null);
        ExpectEvent(
            config,
            config.Events[1].StartsAt,
            "test-event-2"
        );

        config.Events.Clear();
        ExpectEvent(config, gapStart, null);

        Console.WriteLine(
            "PASS: Event gaps and an empty event schedule are accepted."
        );
    }

    private static void CheckInvalidInputs(string temporaryFolder)
    {
        // Each scenario starts with a fresh valid configuration.
        JObject overlap = CreateJsonFixture();
        overlap["events"][1]["startsAt"] = "2026-01-31T23:59:59Z";

        ExpectInvalidJson(
            temporaryFolder,
            "overlapping-events.json",
            overlap,
            "overlaps enabled event"
        );

        Console.WriteLine(
            "PASS: Overlapping enabled events were rejected."
        );

        JObject missingOffset = CreateJsonFixture();
        missingOffset["events"][0]["startsAt"] = "2026-01-01T00:00:00";

        ExpectInvalidJson(
            temporaryFolder,
            "event-missing-offset.json",
            missingOffset,
            "events[0].startsAt",
            "explicit UTC offset"
        );

        Console.WriteLine(
            "PASS: An event timestamp without an offset was rejected."
        );

        JObject invalidWeight = CreateJsonFixture();
        invalidWeight["events"][0]["categoryWeight"] = 0;

        ExpectInvalidJson(
            temporaryFolder,
            "event-zero-weight.json",
            invalidWeight,
            "events[0].categoryWeight",
            "greater than zero"
        );

        Console.WriteLine(
            "PASS: A zero event category weight was rejected."
        );

        JObject invalidType = CreateJsonFixture();
        invalidType["events"][0]["include"]["types"][0] = "spooky";

        ExpectInvalidJson(
            temporaryFolder,
            "event-unknown-type.json",
            invalidType,
            "unsupported type",
            "spooky"
        );

        Console.WriteLine(
            "PASS: An unsupported event type was rejected."
        );

        JObject missingRestriction = CreateJsonFixture();

        ((JObject)missingRestriction["events"][0]).Remove(
            "requireUnlockedGenerationForLegendary"
        );

        ExpectInvalidJson(
            temporaryFolder,
            "event-missing-restriction.json",
            missingRestriction,
            "requireUnlockedGenerationForLegendary",
            "not found"
        );

        Console.WriteLine(
            "PASS: A missing Legendary restriction setting was rejected."
        );
    }

    /// <summary>
    /// Uses the same property-name mapping as the configuration loaders.
    /// DateTimeOffset values are written with explicit offsets.
    /// </summary>
    private static JObject CreateJsonFixture()
    {
        var serializer = new Newtonsoft.Json.JsonSerializer
        {
            ContractResolver =
                new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver()
        };

        return JObject.FromObject(
            EventTestFixtures.Create(),
            serializer
        );
    }

    private static void ExpectInvalidJson(
        string temporaryFolder,
        string filename,
        JObject document,
        params string[] expectedMessageParts
    )
    {
        string path = TestJsonFiles.Write(
            temporaryFolder,
            filename,
            document
        );

        CheckAssert.Rejected(
            () => { EventsConfigLoader.Load(path); },
            expectedMessageParts
        );
    }

    private static void ExpectEvent(
        EventsConfig config,
        DateTimeOffset instant,
        string expectedId
    )
    {
        EventDefinition selected = EventSelector.FindActive(config, instant);
        string actualId = selected == null ? null : selected.Id;

        if (!string.Equals(
            actualId,
            expectedId,
            StringComparison.Ordinal
        ))
        {
            throw new InvalidOperationException(
                "At " + instant.ToString("O") +
                ", expected " + (expectedId ?? "no event") +
                ", but received " + (actualId ?? "no event") + "."
            );
        }
    }
}