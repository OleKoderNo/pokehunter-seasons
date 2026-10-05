using System;
using System.IO;

/// <summary>
/// Runs the standalone checks and manages their temporary directory.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length != 3)
        {
            Console.Error.WriteLine(
                "Usage: ConfigurationChecks.exe " +
                "<game.json> <seasons.json> <events.json>"
            );

            return 1;
        }

        string temporaryFolder = Path.Combine(
            Path.GetTempPath(),
            "pokehunter-config-check-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            Directory.CreateDirectory(temporaryFolder);

            // Preserve existing checks when adding new groups.
            GameConfigurationChecks.Run(args[0], temporaryFolder);
            SeasonConfigurationChecks.Run(args[1], temporaryFolder);
            SeasonSelectionChecks.Run();
            TimeZoneChecks.Run();

            EventConfigurationChecks.Run(args[2], temporaryFolder);

            Console.WriteLine("All configuration checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
        finally
        {
            RemoveTemporaryFolder(temporaryFolder);
        }
    }

    private static void RemoveTemporaryFolder(string temporaryFolder)
    {
        try
        {
            if (Directory.Exists(temporaryFolder))
            {
                Directory.Delete(temporaryFolder, true);
            }
        }
        catch (Exception exception)
        {
            // Cleanup failure must not hide the original check result.
            Console.Error.WriteLine(
                "Warning: Could not remove temporary test files at '" +
                temporaryFolder + "': " + exception.Message
            );
        }
    }
}