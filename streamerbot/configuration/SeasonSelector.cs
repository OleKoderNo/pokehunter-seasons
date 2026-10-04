using System;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Finds the configured season containing a supplied instant.
    /// Does not modify configuration or collection records.
    /// </summary>
    public static class SeasonSelector
    {
        /// <summary>
        /// Returns the matching season, or null when the instant is
        /// outside the configured schedule.
        /// Throws if the configuration is invalid.
        /// </summary>
        public static SeasonDefinition FindActive(
            SeasonsConfig config,
            DateTimeOffset instant
        )
        {
            // Reject invalid schedules even when the configuration was
            // constructed directly in code instead of loaded from JSON.
            SeasonsConfigValidator.Validate(config);

            foreach (SeasonDefinition season in config.Seasons)
            {
                // Starts are inclusive; ends are exclusive.
                // At a shared boundary, the next season becomes active.
                if (instant >= season.StartsAt &&
                    instant < season.EndsAtExclusive)
                {
                    return season;
                }
            }

            // Never silently reuse an expired season.
            return null;
        }
    }
}