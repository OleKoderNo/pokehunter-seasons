using System;
using System.IO;
using PokeHunter.Configuration;

#if EXTERNAL_EDITOR
public class GameConfigurationCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
    // Change this to your own repository folder when setting up the action.
    // The @ prefix lets Windows paths contain ordinary backslashes.
    private const string ProjectFolder =
        @"C:\Users\ohfb9\Documents\Coding\Private\Streaming\pokehunter-seasons";

#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        try
        {
            // Build paths from one project location so setup only requires
            // changing ProjectFolder above.
            string gamePath = Path.Combine(
                ProjectFolder,
                "config",
                "game.json"
            );

            string seasonsPath = Path.Combine(
                ProjectFolder,
                "config",
                "seasons.json"
            );

            string eventsPath = Path.Combine(
                ProjectFolder,
                "config",
                "events.json"
            );

            // Each loader checks both the JSON structure and the
            // configuration rules before returning an object.
            GameConfig game = GameConfigLoader.Load(gamePath);
            SeasonsConfig seasons = SeasonsConfigLoader.Load(seasonsPath);
            EventsConfig events = EventsConfigLoader.Load(eventsPath);

            // Capture the time once. Both selectors must evaluate the
            // same instant, including at exact schedule boundaries.
            DateTimeOffset checkedAtUtc = DateTimeOffset.UtcNow;

            SeasonDefinition activeSeason = SeasonSelector.FindActive(
                seasons,
                checkedAtUtc
            );

            EventDefinition activeEvent = EventSelector.FindActive(
                events,
                checkedAtUtc
            );

            // Named time zones are used for readable local-time logging.
            // Explicit offsets in the JSON still define boundary instants.
            TimeZoneInfo seasonTimeZone =
                ConfigurationTimeZone.Resolve(seasons.TimeZone);

            TimeZoneInfo eventTimeZone =
                ConfigurationTimeZone.Resolve(events.TimeZone);

            DateTimeOffset seasonLocalTime = TimeZoneInfo.ConvertTime(
                checkedAtUtc,
                seasonTimeZone
            );

            DateTimeOffset eventLocalTime = TimeZoneInfo.ConvertTime(
                checkedAtUtc,
                eventTimeZone
            );

            CPH.LogInfo(
                "[PokéHunter] Game configuration check passed."
            );

            CPH.LogInfo(
                "[PokéHunter] Reward cost: " + game.Redemption.Cost +
                " | Cooldown: " +
                game.Redemption.PerUserCooldownSeconds + " seconds"
            );

            CPH.LogInfo(
                "[PokéHunter] Base shiny odds: 1 in " +
                game.Shiny.BaseOddsDenominator
            );

            CPH.LogInfo(
                "[PokéHunter] Season configuration check passed. " +
                "Configured seasons: " + seasons.Seasons.Count
            );

            CPH.LogInfo(
                "[PokéHunter] Event configuration check passed. " +
                "Configured events: " + events.Events.Count
            );

            CPH.LogInfo(
                "[PokéHunter] Checked at (UTC): " +
                checkedAtUtc.ToString("O")
            );

            CPH.LogInfo(
                "[PokéHunter] Season local time (" +
                seasons.TimeZone + "): " +
                seasonLocalTime.ToString("O")
            );

            CPH.LogInfo(
                "[PokéHunter] Event local time (" +
                events.TimeZone + "): " +
                eventLocalTime.ToString("O")
            );

            CPH.LogInfo(
                "[PokéHunter] Game configuration file: " + gamePath
            );

            CPH.LogInfo(
                "[PokéHunter] Season configuration file: " + seasonsPath
            );

            CPH.LogInfo(
                "[PokéHunter] Event configuration file: " + eventsPath
            );

            if (activeSeason == null)
            {
                CPH.LogWarn(
                    "[PokéHunter] No season is currently active. " +
                    "Check the configured season schedule."
                );
            }
            else
            {
                CPH.LogInfo(
                    "[PokéHunter] Active season: " +
                    activeSeason.Name + " (" + activeSeason.Id + ")"
                );

                CPH.LogInfo(
                    "[PokéHunter] Unlocked generations: " +
                    string.Join(", ", activeSeason.UnlockedGenerations)
                );

                CPH.LogInfo(
                    "[PokéHunter] Season starts: " +
                    activeSeason.StartsAt.ToString("O") +
                    " | Ends exclusively: " +
                    activeSeason.EndsAtExclusive.ToString("O")
                );
            }

            // An event is optional. A valid schedule can have gaps,
            // disabled events, or no events at all.
            if (activeEvent == null)
            {
                CPH.LogInfo(
                    "[PokéHunter] No event is currently active."
                );
            }
            else
            {
                CPH.LogInfo(
                    "[PokéHunter] Active event: " +
                    activeEvent.Name + " (" + activeEvent.Id + ")"
                );

                CPH.LogInfo(
                    "[PokéHunter] Event starts: " +
                    activeEvent.StartsAt.ToString("O") +
                    " | Ends exclusively: " +
                    activeEvent.EndsAtExclusive.ToString("O")
                );

                CPH.LogInfo(
                    "[PokéHunter] Event category weight: " +
                    activeEvent.CategoryWeight
                );

                CPH.LogInfo(
                    "[PokéHunter] Event allows locked generations: " +
                    activeEvent.AllowLockedGenerations +
                    " | Legendary requires unlocked generation: " +
                    activeEvent.RequireUnlockedGenerationForLegendary +
                    " | Mythical requires unlocked generation: " +
                    activeEvent.RequireUnlockedGenerationForMythical
                );

                // These are selector counts, not counts of eligible Pokémon.
                // Resolving selectors against the catalogue comes later.
                CPH.LogInfo(
                    "[PokéHunter] Event inclusion selectors: " +
                    activeEvent.Include.Types.Count + " types, " +
                    activeEvent.Include.EvolutionFamilies.Count +
                    " evolution families, " +
                    activeEvent.Include.Forms.Count + " forms, " +
                    activeEvent.Include.Costumes.Count + " costumes"
                );
            }

            // A season is required for catching, but an event is optional.
            // This action only checks configuration; it awards no catches.
            return activeSeason != null;
        }
        catch (Exception exception)
        {
            // Include the full exception to preserve useful troubleshooting
            // details, including any underlying loader error.
            CPH.LogError(
                "[PokéHunter] Configuration check failed: " +
                exception
            );

            return false;
        }
    }
}