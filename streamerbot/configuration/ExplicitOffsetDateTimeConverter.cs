using System;
using System.Globalization;
using Newtonsoft.Json;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Reads timestamps that explicitly specify their UTC offset.
    /// Accepts whole seconds or up to seven fractional-second digits.
    /// </summary>
    internal sealed class ExplicitOffsetDateTimeConverter : JsonConverter
    {
        // Every accepted format requires an offset.
        // A trailing Z is converted to +00:00 before parsing.
        private static readonly string[] AcceptedFormats =
        {
            "yyyy-MM-dd'T'HH:mm:sszzz",
            "yyyy-MM-dd'T'HH:mm:ss.fzzz",
            "yyyy-MM-dd'T'HH:mm:ss.ffzzz",
            "yyyy-MM-dd'T'HH:mm:ss.fffzzz",
            "yyyy-MM-dd'T'HH:mm:ss.ffffzzz",
            "yyyy-MM-dd'T'HH:mm:ss.fffffzzz",
            "yyyy-MM-dd'T'HH:mm:ss.ffffffzzz",
            "yyyy-MM-dd'T'HH:mm:ss.fffffffzzz"
        };

        /// <summary>
        /// This converter only customizes reading configuration.
        /// Normal serialization remains available through Newtonsoft.Json.
        /// </summary>
        public override bool CanWrite
        {
            get { return false; }
        }

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(DateTimeOffset);
        }

        public override object ReadJson(
            JsonReader reader,
            Type objectType,
            object existingValue,
            JsonSerializer serializer
        )
        {
            // Dates must arrive as their original JSON strings so we can
            // check that an offset was actually supplied.
            if (reader.TokenType != JsonToken.String)
            {
                throw InvalidTimestamp(reader.Path);
            }

            string timestamp = (string)reader.Value;

            if (string.IsNullOrWhiteSpace(timestamp))
            {
                throw InvalidTimestamp(reader.Path);
            }

            // Z and +00:00 both explicitly mean UTC.
            if (timestamp.EndsWith("Z", StringComparison.Ordinal))
            {
                timestamp =
                    timestamp.Substring(0, timestamp.Length - 1) +
                    "+00:00";
            }

            DateTimeOffset parsedTimestamp;

            bool parsed = DateTimeOffset.TryParseExact(
                timestamp,
                AcceptedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out parsedTimestamp
            );

            if (!parsed)
            {
                throw InvalidTimestamp(reader.Path);
            }

            return parsedTimestamp;
        }

        public override void WriteJson(
            JsonWriter writer,
            object value,
            JsonSerializer serializer
        )
        {
            // CanWrite is false, so the serializer does not call this.
            throw new NotSupportedException(
                "This converter only reads timestamps."
            );
        }

        private static JsonSerializationException InvalidTimestamp(
            string path
        )
        {
            return new JsonSerializationException(
                "Property '" + path +
                "' must contain a timestamp with seconds and an explicit " +
                "UTC offset, such as 2027-01-01T00:00:00+01:00 " +
                "or 2026-12-31T23:00:00Z."
            );
        }
    }
}