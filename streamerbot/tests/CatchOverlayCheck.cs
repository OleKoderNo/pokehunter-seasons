using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#if EXTERNAL_EDITOR
public class CatchOverlayCheck
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
            // Supplied by the local Streamer.bot action.
            // Personal installation paths must not be stored in source control.
            string projectRoot;

            if (
                !CPH.TryGetArg("pokeHunterProjectRoot", out projectRoot) ||
                string.IsNullOrWhiteSpace(projectRoot) ||
                !Path.IsPathRooted(projectRoot)
            )
            {
                throw new InvalidOperationException(
                    "Set pokeHunterProjectRoot to the absolute project folder path."
                );
            }

            // Change to pikachu:male:shiny to preview a shiny announcement.
            string entryId = "pikachu:male:normal";

            string login;
            string userId;
            string displayName;
            string profileImageUrl;

            if (
                !CPH.TryGetArg("targetUserName", out login) ||
                !string.Equals(
                    login,
                    "kapteinole",
                    StringComparison.OrdinalIgnoreCase
                ) ||
                !CPH.TryGetArg("targetUserId", out userId) ||
                string.IsNullOrWhiteSpace(userId) ||
                !CPH.TryGetArg("targetUser", out displayName) ||
                string.IsNullOrWhiteSpace(displayName) ||
                !CPH.TryGetArg("targetUserProfileImageUrl", out profileImageUrl) ||
                string.IsNullOrWhiteSpace(profileImageUrl)
            )
            {
                throw new InvalidOperationException(
                    "Run Get User Info for Target with kapteinole before this code."
                );
            }

            string cataloguePath = Path.Combine(
                projectRoot,
                "data",
                "catalogue",
                "merged-entries.json"
            );

            JObject catalogue = JObject.Parse(File.ReadAllText(cataloguePath));
            JArray entries = catalogue["entries"] as JArray;

            if (entries == null)
            {
                throw new InvalidDataException("Catalogue entries are missing.");
            }

            JObject selected = null;

            foreach (JToken token in entries)
            {
                JObject entry = token as JObject;

                if (entry != null && (string)entry["id"] == entryId)
                {
                    if (selected != null)
                    {
                        throw new InvalidDataException("Duplicate test entry ID.");
                    }

                    selected = entry;
                }
            }

            if (selected == null)
            {
                throw new InvalidDataException(
                    "Test entry was not found: " + entryId
                );
            }

            string pokemonName = (string)selected["displayName"];
            string formName = (string)selected["formName"];

            if (!string.IsNullOrWhiteSpace(formName))
            {
                pokemonName += " (" + formName + ")";
            }

            JObject payload = new JObject
            {
                ["schemaVersion"] = 1,
                ["isTest"] = true,
                ["login"] = "kapteinole",
                ["userId"] = userId,
                ["displayName"] = displayName,
                ["profileImageUrl"] = profileImageUrl,
                ["entryId"] = entryId,
                ["pokemonName"] = pokemonName,
                ["isShiny"] = selected["isShiny"].DeepClone(),
                ["spriteUrl"] = selected["sprite"]["url"].DeepClone(),
                ["cries"] = selected["cries"].DeepClone()
            };

            byte[] body = Encoding.UTF8.GetBytes(
                payload.ToString(Formatting.None)
            );

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(
                "http://127.0.0.1:8787/test-catch"
            );

            request.Method = "POST";
            request.ContentType = "application/json";
            request.ContentLength = body.Length;
            request.Timeout = 5000;
            request.ReadWriteTimeout = 5000;
            request.Proxy = null;

            using (Stream stream = request.GetRequestStream())
            {
                stream.Write(body, 0, body.Length);
            }

            using (HttpWebResponse response =
                (HttpWebResponse)request.GetResponse())
            {
                CPH.LogInfo(
                    "[PokéHunter] Overlay test accepted for " +
                    displayName + " (" + userId + "): " + entryId
                );
            }

            // This action intentionally does not write catch ownership.
            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError(
                "[PokéHunter] Overlay test failed: " + exception.Message
            );

            return false;
        }
    }
}