using System;
using System.IO;
using Newtonsoft.Json;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Reads game.json and converts it into configuration objects.
    /// Numerical game rules are validated separately.
    /// </summary>
    public static class GameConfigLoader
    {
        /// <summary>
        /// Loads the game configuration from an absolute file path.
        /// Throws an exception when the file cannot be read or
        /// its contents cannot be mapped to the expected structure.
        /// </summary>
        public static GameConfig Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "A configuration file path is required.",
                    nameof(filePath)
                );
            }

            // Reading the file produces text, not a GameConfig object.
            // File access errors propagate to the calling action.
            string json = File.ReadAllText(filePath);

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new RequiredConfigContractResolver(),

                // Reject JSON properties that have no matching C# property.
                // This catches mistakes such as "costt" instead of "cost".
                MissingMemberHandling = MissingMemberHandling.Error,

                // Reject additional content after the configuration object.
                CheckAdditionalContent = true,

                // Configuration files do not choose which C# types to create.
                TypeNameHandling = TypeNameHandling.None,

                // Treat metadata-like names as ordinary properties.
                // Unknown properties will then be rejected.
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,

                // Our configuration is shallow; excessive nesting is invalid.
                MaxDepth = 32
            };

            try
            {
                // <GameConfig> specifies the type we want to receive.
                GameConfig config =
                    JsonConvert.DeserializeObject<GameConfig>(
                        json,
                        settings
                    );

                // A document containing only "null" must also be rejected.
                if (config == null)
                {
                    throw new JsonSerializationException(
                        "The configuration must contain a JSON object."
                    );
                }

                // Only return configuration that passes the game's value checks.
                GameConfigValidator.Validate(config);

                return config;
            }
            catch (JsonException exception)
            {
                // Add the filename while preserving the original error.
                // The JSON library normally includes the relevant property
                // path and location in its message.
                throw new InvalidDataException(
                    "Could not load game configuration from '" +
                    filePath + "': " + exception.Message,
                    exception
                );
            }
        }
    }
}