using System;
using System.IO;
using Newtonsoft.Json;

namespace PokeHunter.Catalogue
{
    /// <summary>
    /// Reads and validates a generated intermediate catalogue.
    ///
    /// Does not download Pokémon data, update player storage,
    /// or calculate season and event eligibility.
    /// </summary>
    public static class CatalogueLoader
    {
        /// <summary>
        /// Loads the catalogue from an absolute file path.
        ///
        /// Returns only after structural deserialization and catalogue
        /// validation succeed. The caller decides how long to retain
        /// the loaded document.
        /// </summary>
        public static CatalogueDocument Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException(
                    "A catalogue file path is required.",
                    nameof(filePath)
                );
            }

            if (!Path.IsPathRooted(filePath))
            {
                throw new ArgumentException(
                    "The catalogue file path must be absolute.",
                    nameof(filePath)
                );
            }

            // File access errors propagate to the caller. They must not
            // be mistaken for an empty or successfully loaded catalogue.
            string json = File.ReadAllText(filePath);

            var settings = new JsonSerializerSettings
            {
                // Reject misspelled or unsupported JSON properties.
                MissingMemberHandling = MissingMemberHandling.Error,

                // Reject another JSON value after the document.
                CheckAdditionalContent = true,

                // The catalogue cannot choose which C# types to create.
                TypeNameHandling = TypeNameHandling.None,

                // Treat metadata-looking names as ordinary properties.
                // Unknown names will therefore be rejected.
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,

                // Catalogue text fields should remain text.
                DateParseHandling = DateParseHandling.None,

                // This format is shallow. Excessive nesting is invalid.
                MaxDepth = 32
            };

            try
            {
                CatalogueDocument document =
                    JsonConvert.DeserializeObject<CatalogueDocument>(
                        json,
                        settings
                    );

                if (document == null)
                {
                    throw new InvalidDataException(
                        "The catalogue must contain a JSON object."
                    );
                }

                CatalogueValidator.Validate(document);

                return document;
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "Could not load catalogue from '" + filePath +
                    "': " + exception.Message,
                    exception
                );
            }
            catch (InvalidDataException exception)
            {
                throw new InvalidDataException(
                    "Could not validate catalogue from '" + filePath +
                    "': " + exception.Message,
                    exception
                );
            }
        }
    }
}