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

            // Each loader reads the file and validates its contents.
            GameConfig game = GameConfigLoader.Load(gamePath);
            SeasonsConfig seasons = SeasonsConfigLoader.Load(seasonsPath);

            // Capture one instant for this execution.
            DateTimeOffset checkedAt = DateTimeOffset.UtcNow;

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

            CPH.LogInfo(
                "[PokéHunter] Checked at: " +
                checkedAt.ToString("O")
            );

            CPH.LogInfo(
                "[PokéHunter] Game configuration file: " + gamePath
            );

            CPH.LogInfo(
                "[PokéHunter] Season configuration file: " + seasonsPath
            );

            // Valid configuration can still describe a schedule that
            // has not started yet or has already ended.
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

            // Round-trip formatting includes the timestamp's UTC offset.
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
            // Include the complete exception for troubleshooting.
            CPH.LogError(
                "[PokéHunter] Configuration check failed: " +
                exception
            );

            return false;
        }
    }
}