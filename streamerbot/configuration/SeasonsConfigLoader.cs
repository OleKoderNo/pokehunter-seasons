using System;
using System.IO;
using Newtonsoft.Json;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Reads seasons.json and validates its structure and scheduling values.
    /// Named time-zone resolution is handled separately.
    /// </summary>
    public static class SeasonsConfigLoader
    {
        /// <summary>
        /// Loads season configuration from the supplied file path.
        /// Throws when the file cannot be read or its contents are invalid.
        /// </summary>
        public static SeasonsConfig Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "A season configuration file path is required.",
                    nameof(filePath)
                );
            }

            // File access errors propagate to the caller.
            string json = File.ReadAllText(filePath);

            var settings = new JsonSerializerSettings
            {
                // Require the properties described by our C# model.
                ContractResolver = new RequiredConfigContractResolver(),

                // Reject misspelled or unsupported property names.
                MissingMemberHandling = MissingMemberHandling.Error,

                // Reject extra content after the configuration object.
                CheckAdditionalContent = true,

                // JSON must not choose which C# types are instantiated.
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,

                MaxDepth = 32,

                // Keep timestamp strings unchanged until our converter
                // checks them for an explicit UTC offset.
                DateParseHandling = DateParseHandling.None
            };

            settings.Converters.Add(
                new ExplicitOffsetDateTimeConverter()
            );

            try
            {
                SeasonsConfig config =
                    JsonConvert.DeserializeObject<SeasonsConfig>(
                        json,
                        settings
                    );

                if (config == null)
                {
                    throw new JsonSerializationException(
                        "The season configuration must contain a JSON object."
                    );
                }

                // Check IDs, generation lists, and season boundaries.
                SeasonsConfigValidator.Validate(config);

                return config;
            }
            catch (JsonException exception)
            {
                // Preserve the original parsing error while adding
                // the filename to help identify the failing configuration.
                throw new InvalidDataException(
                    "Could not load season configuration from '" +
                    filePath + "': " + exception.Message,
                    exception
                );
            }
        }
    }
}