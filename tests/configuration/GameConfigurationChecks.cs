using System;
using System.IO;
using Newtonsoft.Json.Linq;
using PokeHunter.Configuration;

/// <summary>
/// Regression checks for game configuration loading and validation.
/// </summary>
internal static class GameConfigurationChecks
{
    /// <summary>
    /// Loads the supplied configuration and runs the game rejection checks.
    /// The supplied file is never modified.
    /// </summary>
    public static void Run(string filePath, string temporaryFolder)
    {
        GameConfig config = GameConfigLoader.Load(filePath);

        Console.WriteLine("PASS: The real game configuration loaded.");
        Console.WriteLine(
            "Reward cost: " + config.Redemption.Cost +
            " | Cooldown: " +
            config.Redemption.PerUserCooldownSeconds +
            " seconds"
        );

        CheckZeroMilestoneRejected(filePath, temporaryFolder);
    }

    private static void CheckZeroMilestoneRejected(
        string filePath,
        string temporaryFolder
    )
    {
        // Change an in-memory copy and save only to the temporary folder.
        JObject invalidGame = JObject.Parse(File.ReadAllText(filePath));

        invalidGame["shiny"]["collectionBonus"]
            ["uniqueEntriesPerMilestone"] = 0;

        string invalidPath = TestJsonFiles.Write(
            temporaryFolder,
            "invalid-game-milestone.json",
            invalidGame
        );

        CheckAssert.Rejected(
            () => { GameConfigLoader.Load(invalidPath); },
            "uniqueEntriesPerMilestone",
            "must be greater than zero"
        );

        Console.WriteLine("PASS: A zero milestone size was rejected.");
    }
}