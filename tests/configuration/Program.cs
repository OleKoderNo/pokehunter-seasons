using System;
using System.IO;
using Newtonsoft.Json.Linq;
using PokeHunter.Configuration;

internal static class Program
{
    private static int Main(string[] args)
    {
        string temporaryFolder = null;

        try
        {
            // Require an explicit path so the test does not guess
            // which configuration file it should check.
            if (args.Length != 1)
            {
                throw new ArgumentException(
                    "Provide the path to config/game.json."
                );
            }

            string configurationPath = Path.GetFullPath(args[0]);

            // Check 1: load the real configuration through our loader.
            GameConfig config = GameConfigLoader.Load(configurationPath);

            Console.WriteLine("PASS: The real game configuration loaded.");
            Console.WriteLine(
                "Reward cost: " + config.Redemption.Cost +
                " | Cooldown: " +
                config.Redemption.PerUserCooldownSeconds + " seconds"
            );

            // Give this run its own temporary directory.
            temporaryFolder = Path.Combine(
                Path.GetTempPath(),
                "pokehunter-config-check-" + Guid.NewGuid().ToString("N")
            );

            Directory.CreateDirectory(temporaryFolder);

            // JObject lets the test change one JSON value in memory.
            // The original file is never overwritten.
            JObject invalidJson = JObject.Parse(
                File.ReadAllText(configurationPath)
            );

            invalidJson["shiny"]["collectionBonus"]
                ["uniqueEntriesPerMilestone"] = 0;

            string invalidPath = Path.Combine(
                temporaryFolder,
                "invalid-game.json"
            );

            File.WriteAllText(invalidPath, invalidJson.ToString());

            // Check 2: the loader must reject a zero milestone size.
            ExpectRejected(
                invalidPath,
                "shiny.collectionBonus.uniqueEntriesPerMilestone " +
                "must be greater than zero."
            );

            Console.WriteLine("PASS: A zero milestone size was rejected.");
            Console.WriteLine("All configuration checks passed.");

            // Exit code zero means the checks succeeded.
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception.Message);

            // A nonzero exit code lets scripts detect test failure.
            return 1;
        }
        finally
        {
            // Cleanup runs whether the checks succeed or fail.
            if (temporaryFolder != null && Directory.Exists(temporaryFolder))
            {
                try
                {
                    Directory.Delete(temporaryFolder, true);
                }
                catch (Exception exception)
                {
                    Console.Error.WriteLine(
                        "Could not remove temporary test folder '" +
                        temporaryFolder + "': " + exception.Message
                    );
                }
            }
        }
    }

    private static void ExpectRejected(
        string filePath,
        string expectedMessage)
    {
        try
        {
            GameConfigLoader.Load(filePath);
        }
        catch (InvalidDataException exception)
        {
            // An unrelated error must not count as a successful check.
            if (exception.Message.IndexOf(
                expectedMessage,
                StringComparison.Ordinal) >= 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "The configuration was rejected for an unexpected reason.",
                exception
            );
        }

        // Reaching here means the invalid configuration was accepted.
        throw new InvalidOperationException(
            "The loader accepted an invalid milestone size."
        );
    }
}