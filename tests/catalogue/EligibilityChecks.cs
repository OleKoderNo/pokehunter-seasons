using System;
using System.Collections.Generic;
using PokeHunter.Catalogue;
using PokeHunter.Configuration;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class EligibilityChecks
    {
        public static void Run(CatalogueDocument document)
        {
            var catalogue = new CatalogueIndex(document);
            SeasonsConfig seasons = CreateSeasons();
            EventsConfig events = CreateEvents();

            DateTimeOffset eventStart = events.Events[0].StartsAt;
            DateTimeOffset eventEnd = events.Events[0].EndsAtExclusive;

            EligibleCataloguePool before = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                eventStart.AddTicks(-1)
            );

            Expect(before, "pikachu:male:normal", true, false);
            Expect(before, "mew:genderless:normal", true, false);
            Expect(before, "houndour:unspecified:normal", false, false);
            Expect(before, "pikachu-libre:female:normal", false, false);

            Require(
                before.EventId == null && before.EventCategoryWeight == 0,
                "An inactive event must not supply an Event category."
            );

            Console.WriteLine(
                "PASS: Without an event, generation locks and costume restrictions apply."
            );

            EligibleCataloguePool active = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                eventStart
            );

            Expect(active, "gastly:unspecified:normal", true, true);
            Expect(active, "gastly:unspecified:shiny", true, true);

            // Houndour matches through Houndoom's evolution family.
            Expect(active, "houndour:unspecified:normal", true, true);

            // A later regional form enters through its Ghost typing.
            Expect(active, "marowak-alola:unspecified:normal", true, true);

            // Explicitly included costume.
            Expect(active, "pikachu-libre:female:normal", true, true);

            // Other costumes must not be unlocked incidentally.
            Expect(active, "pikachu-belle:female:normal", false, false);

            // Generation 1 Mythical remains ordinarily available.
            Expect(active, "mew:genderless:normal", true, false);

            // Later Legendary and Mythical entries remain locked.
            Expect(active, "giratina-altered:genderless:normal", false, false);
            Expect(active, "marshadow:genderless:normal", false, false);

            Require(
                active.EventId == "test-halloween" &&
                active.EventCategoryWeight == 4,
                "The active event identity or weight was lost."
            );

            Console.WriteLine(
                "PASS: Event types, families, and explicit costumes resolve with generation restrictions."
            );

            var allIds = new HashSet<string>(
                active.EntryIds,
                StringComparer.Ordinal
            );

            var eventIds = new HashSet<string>(
                active.EventEntryIds,
                StringComparer.Ordinal
            );

            Require(
                allIds.Count == active.EntryIds.Count &&
                eventIds.Count == active.EventEntryIds.Count &&
                eventIds.IsSubsetOf(allIds),
                "Event entries must be unique and included in the general pool."
            );

            Console.WriteLine(
                "PASS: Event entries also enter the general pool without duplicate IDs."
            );

            EligibleCataloguePool ended = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                eventEnd
            );

            Expect(ended, "houndour:unspecified:normal", false, false);
            Expect(ended, "pikachu-libre:female:normal", false, false);

            Require(
                ended.EventId == null,
                "The event remained active at its exclusive end."
            );

            Console.WriteLine(
                "PASS: Event additions disappear at the exact exclusive end."
            );

            events.Events[0].Enabled = false;

            EligibleCataloguePool disabled = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                eventStart
            );

            Expect(disabled, "houndour:unspecified:normal", false, false);

            Require(
                disabled.EventId == null,
                "A disabled event was selected."
            );

            events.Events[0].Enabled = true;
            events.Events[0].AllowLockedGenerations = false;

            EligibleCataloguePool restricted = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                eventStart
            );

            Expect(restricted, "gastly:unspecified:normal", true, true);
            Expect(restricted, "houndour:unspecified:normal", false, false);
            Expect(restricted, "pikachu-libre:female:normal", false, false);

            Console.WriteLine(
                "PASS: Disabled events and disabled generation bypasses are respected."
            );

            // Unlocking a generation must permit its matching Legendary.
            seasons.Seasons[0].UnlockedGenerations.Add(4);

            EligibleCataloguePool generationFour = EligibilityPoolBuilder.Build(
                catalogue,
                seasons,
                events,
                eventStart
            );

            Expect(
                generationFour,
                "giratina-altered:genderless:normal",
                true,
                true
            );

            Console.WriteLine(
                "PASS: An unlocked Legendary can participate in a matching event."
            );

            bool noSeasonRejected = false;

            try
            {
                EligibilityPoolBuilder.Build(
                    catalogue,
                    seasons,
                    events,
                    seasons.Seasons[0].EndsAtExclusive
                );
            }
            catch (InvalidOperationException exception)
            {
                if (exception.Message.IndexOf(
                    "without an active season",
                    StringComparison.Ordinal
                ) < 0)
                {
                    throw;
                }

                noSeasonRejected = true;
            }

            Require(
                noSeasonRejected,
                "Pool construction must reject an instant without a season."
            );

            Console.WriteLine(
                "PASS: A missing active season prevents pool construction."
            );
        }

        private static void Expect(
            EligibleCataloguePool pool,
            string entryId,
            bool expectedGeneral,
            bool expectedEvent
        )
        {
            bool inGeneral = new HashSet<string>(
                pool.EntryIds,
                StringComparer.Ordinal
            ).Contains(entryId);

            bool inEvent = new HashSet<string>(
                pool.EventEntryIds,
                StringComparer.Ordinal
            ).Contains(entryId);

            Require(
                inGeneral == expectedGeneral && inEvent == expectedEvent,
                "Unexpected eligibility for " + entryId +
                ". General: " + inGeneral +
                "; Event: " + inEvent + "."
            );
        }

        private static SeasonsConfig CreateSeasons()
        {
            return new SeasonsConfig
            {
                SchemaVersion = 1,
                TimeZone = "Europe/Oslo",
                Seasons = new List<SeasonDefinition>
                {
                    new SeasonDefinition
                    {
                        Id = "test-season",
                        Name = "Test Season",
                        StartsAt = new DateTimeOffset(
                            2026, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)
                        ),
                        EndsAtExclusive = new DateTimeOffset(
                            2027, 1, 1, 0, 0, 0, TimeSpan.FromHours(1)
                        ),
                        UnlockedGenerations = new List<int> { 1 }
                    }
                }
            };
        }

        private static EventsConfig CreateEvents()
        {
            return new EventsConfig
            {
                SchemaVersion = 1,
                TimeZone = "Europe/Oslo",
                Events = new List<EventDefinition>
                {
                    new EventDefinition
                    {
                        Id = "test-halloween",
                        Name = "Test Halloween",
                        Enabled = true,
                        StartsAt = new DateTimeOffset(
                            2026, 10, 1, 0, 0, 0, TimeSpan.FromHours(2)
                        ),
                        EndsAtExclusive = new DateTimeOffset(
                            2026, 11, 1, 0, 0, 0, TimeSpan.FromHours(1)
                        ),
                        CategoryWeight = 4,
                        AllowLockedGenerations = true,
                        RequireUnlockedGenerationForLegendary = true,
                        RequireUnlockedGenerationForMythical = true,
                        Include = new EventInclusionConfig
                        {
                            Types = new List<string> { "ghost" },
                            EvolutionFamilies = new List<string> { "houndoom" },
                            Forms = new List<string>(),
                            Costumes = new List<string> { "pikachu-libre" }
                        }
                    }
                }
            };
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}