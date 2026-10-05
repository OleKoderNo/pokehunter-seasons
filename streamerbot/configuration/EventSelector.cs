using System;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Selects the enabled event containing a supplied instant.
    /// Does not modify configuration or determine Pokémon eligibility.
    /// </summary>
    public static class EventSelector
    {
        /// <summary>
        /// Returns the active event, or null when no enabled event covers
        /// the supplied instant.
        /// Throws if the configuration is invalid.
        /// </summary>
        public static EventDefinition FindActive(
            EventsConfig config,
            DateTimeOffset instant
        )
        {
            // Validate directly constructed configurations as well as
            // loaded ones. Overlaps must not produce an arbitrary winner.
            EventsConfigValidator.Validate(config);

            foreach (EventDefinition definition in config.Events)
            {
                // A disabled event never becomes active, even when
                // the supplied instant falls within its schedule.
                if (!definition.Enabled)
                {
                    continue;
                }

                // Starts are inclusive and ends are exclusive.
                // DateTimeOffset comparisons account for UTC offsets.
                if (instant >= definition.StartsAt &&
                    instant < definition.EndsAtExclusive)
                {
                    return definition;
                }
            }

            // No scheduled event, a gap, or only disabled events.
            // All are normal reasons to have no active event.
            return null;
        }
    }
}