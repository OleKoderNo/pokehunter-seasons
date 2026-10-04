using System.IO;
using Newtonsoft.Json.Linq;

/// <summary>
/// File-writing support for temporary configuration examples.
/// </summary>
internal static class TestJsonFiles
{
    /// <summary>
    /// Writes a JSON document into the run's existing temporary directory
    /// and returns the path used.
    /// </summary>
    /// <param name="temporaryFolder">
    /// The directory created and owned by Program.
    /// </param>
    /// <param name="filename">
    /// A simple filename chosen by the check, not a user-supplied path.
    /// </param>
    /// <param name="document">
    /// The in-memory JSON example to save.
    /// </param>
    public static string Write(
        string temporaryFolder,
        string filename,
        JObject document
    )
    {
        string path = Path.Combine(temporaryFolder, filename);

        File.WriteAllText(path, document.ToString());

        return path;
    }
}