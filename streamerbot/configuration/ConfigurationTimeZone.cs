using System;
using System.IO;
using TimeZoneConverter;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Resolves configured time-zone names using TimeZoneConverter.
    /// Does not change timestamps or the computer's time-zone settings.
    /// </summary>
    public static class ConfigurationTimeZone
    {
        /// <summary>
        /// Resolves a supported IANA or Windows time-zone identifier.
        /// Throws InvalidDataException when the value cannot be used.
        /// </summary>
        public static TimeZoneInfo Resolve(string timeZoneId)
        {
            if (string.IsNullOrWhiteSpace(timeZoneId))
            {
                throw new InvalidDataException(
                    "Configuration error: timeZone must not be empty."
                );
            }

            // Reject accidental whitespace instead of silently fixing it.
            if (timeZoneId != timeZoneId.Trim())
            {
                throw new InvalidDataException(
                    "Configuration error: timeZone must not contain " +
                    "leading or trailing whitespace."
                );
            }

            try
            {
                // Supports names such as Europe/Oslo on Windows.
                // The resulting object provides time-zone conversion rules.
                return TZConvert.GetTimeZoneInfo(timeZoneId);
            }
            catch (TimeZoneNotFoundException exception)
            {
                throw new InvalidDataException(
                    "Configuration error: timeZone '" + timeZoneId +
                    "' could not be resolved on this computer. " +
                    "Check the identifier and installed time-zone data.",
                    exception
                );
            }
            catch (InvalidTimeZoneException exception)
            {
                throw new InvalidDataException(
                    "Configuration error: timeZone '" + timeZoneId +
                    "' has invalid system time-zone data.",
                    exception
                );
            }
        }
    }
}