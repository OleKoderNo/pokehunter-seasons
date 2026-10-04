using System;
using System.Collections.Generic;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Describes the structure of config/seasons.json.
    /// Loading, validation, and active-season selection are handled separately.
    /// </summary>
    public sealed class SeasonsConfig
    {
        /// <summary>
        /// Identifies the configuration format understood by the loader.
        /// This is not the current season number.
        /// </summary>
        public int SchemaVersion { get; set; }

        /// <summary>
        /// Named time zone used for the season schedule, such as Europe/Oslo.
        /// Each boundary also contains an explicit UTC offset.
        /// </summary>
        public string TimeZone { get; set; }

        /// <summary>
        /// Season definitions, including past, current, and future seasons.
        /// Collection records are stored separately from these definitions.
        /// </summary>
        public List<SeasonDefinition> Seasons { get; set; }
    }

    /// <summary>
    /// Describes one season's identity, schedule, and generation unlocks.
    /// </summary>
    public sealed class SeasonDefinition
    {
        /// <summary>
        /// Stable identifier used to associate catches with this season.
        /// Do not rename it after catches have been recorded against it.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Human-readable season name.
        /// This can change without changing the season's identifier.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Inclusive starting instant.
        /// The season is active at this exact instant.
        /// </summary>
        public DateTimeOffset StartsAt { get; set; }

        /// <summary>
        /// Exclusive ending instant.
        /// The season is no longer active at this exact instant.
        /// </summary>
        public DateTimeOffset EndsAtExclusive { get; set; }

        /// <summary>
        /// Complete list of generations normally available in this season.
        /// Include earlier generations explicitly when unlocks are cumulative.
        /// Event exceptions will be handled separately.
        /// </summary>
        public List<int> UnlockedGenerations { get; set; }
    }
}