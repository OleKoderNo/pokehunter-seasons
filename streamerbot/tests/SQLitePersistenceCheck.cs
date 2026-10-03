using System;
using System.Data.SQLite;
using System.IO;

// VS Code needs an explicit base class and a unique action class name.
// Streamer.bot uses its standard CPHInline declaration.
#if EXTERNAL_EDITOR
public class SQLitePersistenceCheck
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
    // Change this to the folder containing your project's README.
    private const string ProjectFolder =
        @"C:\Users\ohfb9\Documents\Coding\Private\Streaming\pokehunter-seasons";

    // Explicitly hide the base method when compiling in the external editor.
    // Keep Streamer.bot's standard method declaration when pasted there.
#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        try
        {
            // Keep generated databases in the Git-ignored runtime folder.
            // This test uses its own database, separate from future player data.
            string runtimeFolder = Path.Combine(ProjectFolder, "runtime");
            Directory.CreateDirectory(runtimeFolder);

            string databasePath = Path.Combine(
                runtimeFolder,
                "sqlite-persistence-test.db"
            );

            // The builder safely handles spaces and punctuation in paths.
            var settings = new SQLiteConnectionStringBuilder
            {
                DataSource = databasePath,
                Version = 3,
                Pooling = false
            };

            long savedCount;

            using (var connection = new SQLiteConnection(settings.ConnectionString))
            {
                connection.Open();

                // Create one counter that survives between action executions.
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        CREATE TABLE IF NOT EXISTS persistence_check (
                            id INTEGER PRIMARY KEY CHECK (id = 1),
                            run_count INTEGER NOT NULL
                        );";
                    command.ExecuteNonQuery();
                }

                // Commit the counter update as one transaction.
                using (var transaction = connection.BeginTransaction())
                using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;

                    command.CommandText = @"
                        INSERT INTO persistence_check (id, run_count)
                        VALUES (1, 0)
                        ON CONFLICT(id) DO NOTHING;";
                    command.ExecuteNonQuery();

                    command.CommandText = @"
                        UPDATE persistence_check
                        SET run_count = run_count + 1
                        WHERE id = 1;";
                    command.ExecuteNonQuery();

                    command.CommandText = @"
                        SELECT run_count
                        FROM persistence_check
                        WHERE id = 1;";
                    savedCount = Convert.ToInt64(command.ExecuteScalar());

                    transaction.Commit();
                }
            }

            // Open a fresh connection to verify the committed value.
            using (var connection = new SQLiteConnection(settings.ConnectionString))
            {
                connection.Open();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = @"
                        SELECT run_count
                        FROM persistence_check
                        WHERE id = 1;";

                    long loadedCount = Convert.ToInt64(command.ExecuteScalar());

                    if (loadedCount != savedCount)
                    {
                        throw new InvalidOperationException(
                            "The saved counter did not match the reopened database."
                        );
                    }

                    CPH.LogInfo(
                        "[PokéHunter] Persistence check passed. " +
                        "Saved run count: " + loadedCount
                    );
                }
            }

            CPH.LogInfo("[PokéHunter] Test database: " + databasePath);
            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[PokéHunter] Persistence check failed: " + exception
            );

            return false;
        }
    }
}