using System;
using System.IO;
using PokeHunter.Configuration;

// Our development project supplies the Streamer.bot base class.
// Inside Streamer.bot, the action uses its expected CPHInline name.
#if EXTERNAL_EDITOR
public class GameConfigurationCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
    // Change this path to the folder containing your project.
    // Keep the @ prefix so Windows backslashes are treated literally.
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
            // Construct the path without relying on Streamer.bot's
            // current working directory.
            string configurationPath = Path.Combine(
                ProjectFolder,
                "config",
                "game.json"
            );

            // The shared library reads the file and validates both
            // its expected structure and the configured values.
            GameConfig config = GameConfigLoader.Load(configurationPath);

            CPH.LogInfo(
                "[PokéHunter] Game configuration check passed."
            );

            CPH.LogInfo(
                "[PokéHunter] Reward cost: " +
                config.Redemption.Cost +
                " | Cooldown: " +
                config.Redemption.PerUserCooldownSeconds +
                " seconds"
            );

            CPH.LogInfo(
                "[PokéHunter] Base shiny odds: 1 in " +
                config.Shiny.BaseOddsDenominator
            );

            CPH.LogInfo(
                "[PokéHunter] Configuration file: " +
                configurationPath
            );

            return true;
        }
        catch (Exception exception)
        {
            // At the action boundary, report the complete exception.
            // This includes any underlying error preserved by the loader.
            CPH.LogError(
                "[PokéHunter] Game configuration check failed: " +
                exception
            );

            return false;
        }
    }
}