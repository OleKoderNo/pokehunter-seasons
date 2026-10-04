using System;
using System.IO;

/// <summary>
/// Entry point for the standalone configuration checks.
/// Owns the temporary directory and the process exit code.
/// </summary>
internal static class Program
{
    private static int Main(string[] args)
    {
        // Paths are supplied by the caller so the checks can run against
        // different configurations without changing the source code.
        if (args.Length != 2)
        {
            Console.Error.WriteLine(
                "Usage: ConfigurationChecks.exe <game.json> <seasons.json>"
            );

            return 1;
        }

        // Give each execution its own directory so separate runs do not
        // overwrite each other's temporary configuration files.
        string temporaryFolder = Path.Combine(
            Path.GetTempPath(),
            "pokehunter-config-check-" + Guid.NewGuid().ToString("N")
        );

        try
        {
            Directory.CreateDirectory(temporaryFolder);

            // Keep both groups active as new checks are added.
            GameConfigurationChecks.Run(args[0], temporaryFolder);
            SeasonConfigurationChecks.Run(args[1], temporaryFolder);

            Console.WriteLine("All configuration checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            // Any failed check or unexpected error makes the run fail.
            Console.Error.WriteLine("FAIL: " + exception);
            return 1;
        }
        finally
        {
            // Cleanup runs after success or failure.
            RemoveTemporaryFolder(temporaryFolder);
        }
    }

    /// <summary>
    /// Removes this run's temporary files without hiding its test result.
    /// </summary>
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
            // A cleanup problem is reported separately from test failures.
            Console.Error.WriteLine(
                "Warning: Could not remove temporary test files at '" +
                temporaryFolder + "': " + exception.Message
            );
        }
    }
}