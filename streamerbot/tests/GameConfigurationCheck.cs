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
    // Change this to the repository root containing the config folder.
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

            // Read and validate both configuration files.
            // Season validation also verifies that its time zone resolves.
            GameConfig game = GameConfigLoader.Load(gamePath);
            SeasonsConfig seasons = SeasonsConfigLoader.Load(seasonsPath);

            // Capture one instant for selection and both time displays.
            DateTimeOffset checkedAt = DateTimeOffset.UtcNow;

            TimeZoneInfo configuredZone =
                ConfigurationTimeZone.Resolve(seasons.TimeZone);

            // Conversion changes the clock representation, not the instant.
            DateTimeOffset localCheckedAt = TimeZoneInfo.ConvertTime(
                checkedAt,
                configuredZone
            );

            SeasonDefinition activeSeason =
                SeasonSelector.FindActive(seasons, checkedAt);

            CPH.LogInfo(
                "[PokéHunter] Game configuration check passed."
            );

            CPH.LogInfo(
                "[PokéHunter] Reward cost: " +
                game.Redemption.Cost +
                " | Cooldown: " +
                game.Redemption.PerUserCooldownSeconds +
                " seconds"
            );

            CPH.LogInfo(
                "[PokéHunter] Base shiny odds: 1 in " +
                game.Shiny.BaseOddsDenominator
            );

            CPH.LogInfo(
                "[PokéHunter] Season configuration check passed. " +
                "Configured seasons: " + seasons.Seasons.Count
            );

            // O uses round-trip formatting, including the UTC offset.
            CPH.LogInfo(
                "[PokéHunter] Checked at (UTC): " +
                checkedAt.ToString("O")
            );

            CPH.LogInfo(
                "[PokéHunter] Checked at (" +
                seasons.TimeZone + "): " +
                localCheckedAt.ToString("O")
            );

            CPH.LogInfo(
                "[PokéHunter] Game configuration file: " + gamePath
            );

            CPH.LogInfo(
                "[PokéHunter] Season configuration file: " + seasonsPath
            );

            // A valid schedule may not cover the current instant.
            // Never silently select an expired or future season.
            if (activeSeason == null)
            {
                CPH.LogWarn(
                    "[PokéHunter] No season is active at the checked time. " +
                    "Check the first start and final end in seasons.json."
                );

                return false;
            }

            CPH.LogInfo(
                "[PokéHunter] Active season: " +
                activeSeason.Name +
                " (" + activeSeason.Id + ")"
            );

            CPH.LogInfo(
                "[PokéHunter] Unlocked generations: " +
                string.Join(", ", activeSeason.UnlockedGenerations)
            );

            // Keep the original boundary offsets visible for troubleshooting.
            CPH.LogInfo(
                "[PokéHunter] Season starts: " +
                activeSeason.StartsAt.ToString("O") +
                " | Ends exclusively: " +
                activeSeason.EndsAtExclusive.ToString("O")
            );

            return true;
        }
        catch (Exception exception)
        {
            // Preserve the full exception details in the log.
            CPH.LogError(
                "[PokéHunter] Configuration check failed: " +
                exception
            );

            return false;
        }
    }
}