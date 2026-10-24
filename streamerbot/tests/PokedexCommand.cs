using System;
using System.IO;
using PokeHunter.Configuration;
using PokeHunter.Storage;

#if EXTERNAL_EDITOR
public class PokedexCommand
    : Streamer.bot.Plugin.Interface.CPHInlineBase
#else
public class CPHInline
#endif
{
#if EXTERNAL_EDITOR
    public new bool Execute()
#else
    public bool Execute()
#endif
    {
        try
        {
            string projectRoot = RequireArgument("pokeHunterProjectRoot");
            string userId = RequireArgument("userId");
            string loginName = RequireArgument("userName");

            if (!Path.IsPathRooted(projectRoot))
            {
                throw new InvalidDataException(
                    "pokeHunterProjectRoot must be an absolute folder path."
                );
            }

            projectRoot = Path.GetFullPath(projectRoot);

            SeasonsConfig seasons = SeasonsConfigLoader.Load(
                Path.Combine(projectRoot, "config", "seasons.json")
            );

            SeasonDefinition season = SeasonSelector.FindActive(
                seasons,
                DateTimeOffset.UtcNow
            );

            if (season == null)
            {
                CPH.SendMessage(
                    "@" + loginName +
                    " No Pokémon season is currently active.",
                    true,
                    true
                );

                return true;
            }

            // Use the same database as the redemption action.
            // Do not initialize or create storage from this chat command.
            string databasePath = Path.Combine(
                projectRoot,
                "runtime",
                "twitch-redemption-test.db"
            );

            var repository = new CollectionRepository(databasePath);

            CollectionSnapshot snapshot = repository.ReadProgressSnapshot(
                userId,
                season.Id
            );

            int seasonalNormal = 0;
            int seasonalShiny = 0;

            foreach (string entryId in snapshot.SeasonalEntryIds)
            {
                // Shiny state is part of the permanent entry ID.
                // This does not require loading today's obtainable roster.
                if (entryId.EndsWith(":shiny", StringComparison.Ordinal))
                {
                    seasonalShiny++;
                }
                else if (entryId.EndsWith(":normal", StringComparison.Ordinal))
                {
                    seasonalNormal++;
                }
                else
                {
                    throw new InvalidDataException(
                        "Stored entry has an unsupported identity: " +
                        entryId + "."
                    );
                }
            }

            long nextAttempts =
                (long)snapshot.ConsecutiveFailedRedemptions + 1;

            CPH.SendMessage(
                "@" + loginName + " — " + season.Name + ": " +
                seasonalNormal + " normal, " +
                seasonalShiny + " shiny | Lifetime unique: " +
                snapshot.UniqueNormalEntries + " normal, " +
                snapshot.UniqueShinyEntries + " shiny | " +
                "Next redemption: " + nextAttempts +
                (nextAttempts == 1 ? " attempt" : " attempts"),
                true,
                true
            );

            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[PokéHunter] Pokédex command failed: " + exception
            );

            try
            {
                CPH.SendMessage(
                    "Pokédex progress could not be loaded right now. " +
                    "Your collection has not been changed.",
                    true,
                    true
                );
            }
            catch (Exception chatException)
            {
                CPH.LogError(
                    "[PokéHunter] Could not send Pokédex error message: " +
                    chatException.Message
                );
            }

            return false;
        }
    }

    private string RequireArgument(string name)
    {
        string value;

        if (!CPH.TryGetArg<string>(name, out value) ||
            string.IsNullOrWhiteSpace(value) ||
            value != value.Trim())
        {
            throw new InvalidDataException(
                "Missing or invalid action argument: " + name + "."
            );
        }

        foreach (char character in value)
        {
            if (char.IsControl(character))
            {
                throw new InvalidDataException(
                    "Action argument contains control characters: " +
                    name + "."
                );
            }
        }

        return value;
    }
}