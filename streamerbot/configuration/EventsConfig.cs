using System;
using System.Collections.Generic;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Describes config/events.json.
    /// Loading, validation, and event selection are handled separately.
    /// </summary>
    public sealed class EventsConfig
    {
        /// <summary>
        /// Identifies the supported configuration format.
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Named time zone used for local-time display.
        /// Explicit timestamp offsets determine event boundaries.
        /// </summary>
        public string TimeZone { get; set; }

        /// <summary>
        /// Configured event definitions.
        /// An empty list represents a game with no scheduled events.
        /// </summary>
        public List<EventDefinition> Events { get; set; }
    }

    /// <summary>
    /// Describes an event's schedule, encounter weight, and inclusion rules.
    /// These settings do not themselves construct an encounter pool.
    /// </summary>
    public sealed class EventDefinition
    {
        /// <summary>
        /// Stable identifier for this event occurrence.
        /// Keep it unchanged once game records refer to it.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Human-readable event name for announcements and displays.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Allows an event to be disabled without deleting its definition.
        /// Disabled events will not be selected as active.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Inclusive starting instant.
        /// </summary>
        public DateTimeOffset StartsAt { get; set; }

        /// <summary>
        /// Exclusive ending instant.
        /// The event is no longer active at this exact instant.
        /// </summary>
        public DateTimeOffset EndsAtExclusive { get; set; }

        /// <summary>
        /// Relative weight of the Event encounter category.
        /// This is a weight, not a percentage or a per-Pokémon multiplier.
        /// </summary>
        public double CategoryWeight { get; set; }

        /// <summary>
        /// Permits included Pokémon from generations not normally unlocked
        /// by the active season, subject to the restrictions below.
        /// </summary>
        public bool AllowLockedGenerations { get; set; }

        /// <summary>
        /// When true, Legendary Pokémon must belong to an unlocked
        /// generation even when the event permits other locked generations.
        /// </summary>
        public bool RequireUnlockedGenerationForLegendary { get; set; }

        /// <summary>
        /// When true, Mythical Pokémon must belong to an unlocked
        /// generation even when the event permits other locked generations.
        /// </summary>
        public bool RequireUnlockedGenerationForMythical { get; set; }

        /// <summary>
        /// Selectors identifying the event's themed Pokémon and costumes.
        /// Matching entries will be combined without duplication.
        /// </summary>
        public EventInclusionConfig Include { get; set; }
    }

    /// <summary>
    /// Describes which catalogue entries should be considered for an event.
    /// Catalogue resolution and generation filtering happen separately.
    /// </summary>
    public sealed class EventInclusionConfig
    {
        /// <summary>
        /// Pokémon type identifiers, such as ghost.
        /// A dual-type Pokémon can match through either of its types.
        /// </summary>
        public List<string> Types { get; set; }

        /// <summary>
        /// Species identifiers used to locate complete evolution families.
        /// The named species may be any member of the family.
        /// </summary>
        public List<string> EvolutionFamilies { get; set; }

        /// <summary>
        /// Specific form identifiers, such as morpeko-hangry.
        /// These select forms rather than automatically selecting a family.
        /// </summary>
        public List<string> Forms { get; set; }

        /// <summary>
        /// Specific costume identifiers available through this event.
        /// Costume ownership and gender selection are handled by gameplay.
        /// </summary>
        public List<string> Costumes { get; set; }
    }
}