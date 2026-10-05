using System;
using System.IO;
using Newtonsoft.Json;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Reads events.json and validates its structure and event definitions.
    /// Does not resolve Pokémon identifiers or construct encounter pools.
    /// </summary>
    public static class EventsConfigLoader
    {
        /// <summary>
        /// Loads event configuration from the supplied file path.
        /// Throws when the file cannot be read or its contents are invalid.
        /// </summary>
        public static EventsConfig Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "An event configuration file path is required.",
                    nameof(filePath)
                );
            }

            // File access errors propagate to the calling code.
            string json = File.ReadAllText(filePath);

            var settings = new JsonSerializerSettings
            {
                // Missing properties must not silently become defaults.
                // This also requires explicit true/false values in the JSON.
                ContractResolver = new RequiredConfigContractResolver(),

                // Reject unknown properties, including misspelled names.
                MissingMemberHandling = MissingMemberHandling.Error,

                // Only one root configuration value is allowed.
                CheckAdditionalContent = true,

                // Configuration must not choose which C# types are created.
                TypeNameHandling = TypeNameHandling.None,
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,

                MaxDepth = 32,

                // Preserve timestamp strings until our converter checks
                // that an explicit UTC offset or Z was supplied.
                DateParseHandling = DateParseHandling.None
            };

            settings.Converters.Add(
                new ExplicitOffsetDateTimeConverter()
            );

            try
            {
                EventsConfig config =
                    JsonConvert.DeserializeObject<EventsConfig>(
                        json,
                        settings
                    );

                if (config == null)
                {
                    throw new JsonSerializationException(
                        "The event configuration must contain a JSON object."
                    );
                }

                // Check values, time-zone resolution, inclusion lists,
                // and overlaps between enabled events.
                EventsConfigValidator.Validate(config);

                return config;
            }
            catch (JsonException exception)
            {
                // Add the filename while preserving the parsing error.
                throw new InvalidDataException(
                    "Could not load event configuration from '" +
                    filePath + "': " + exception.Message,
                    exception
                );
            }
        }
    }
}