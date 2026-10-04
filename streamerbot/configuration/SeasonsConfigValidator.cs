using System;
using System.Collections.Generic;
using System.IO;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Checks season configuration values and schedule continuity.
    /// Does not modify the supplied configuration.
    /// </summary>
    public static class SeasonsConfigValidator
    {
        /// <summary>
        /// Throws InvalidDataException when a configuration rule fails.
        /// Reports the first problem found.
        /// </summary>
        public static void Validate(SeasonsConfig config)
        {
            Require(config != null, "The configuration must not be null.");

            Require(
                config.SchemaVersion == 1,
                "schemaVersion must be 1."
            );

            // Require a valid identifier that can be resolved on this computer.
            // Explicit timestamp offsets still determine the season boundaries.
            RequireText(config.TimeZone, "timeZone");
            ConfigurationTimeZone.Resolve(config.TimeZone);

            Require(
                config.Seasons != null && config.Seasons.Count > 0,
                "seasons must contain at least one season."
            );

            // Treat IDs that differ only by capitalization as duplicates.
            // This prevents confusing pairs such as season-1 and Season-1.
            var seasonIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

            for (int index = 0; index < config.Seasons.Count; index++)
            {
                SeasonDefinition season = config.Seasons[index];
                string path = "seasons[" + index + "]";

                Require(
                    season != null,
                    path + " must not be null."
                );

                RequireText(season.Id, path + ".id");
                RequireText(season.Name, path + ".name");

                // HashSet.Add returns false when the ID already exists.
                Require(
                    seasonIds.Add(season.Id),
                    path + ".id duplicates another season ID: " +
                    season.Id + "."
                );

                Require(
                    season.StartsAt != default(DateTimeOffset),
                    path + ".startsAt must contain a starting timestamp."
                );

                Require(
                    season.EndsAtExclusive > season.StartsAt,
                    path + ".endsAtExclusive must be later than startsAt."
                );

                ValidateGenerations(
                    season.UnlockedGenerations,
                    path + ".unlockedGenerations"
                );
            }

            // Only check schedule continuity after every season is valid.
            ValidateContinuousSchedule(config.Seasons);
        }

        /// <summary>
        /// Requires a nonempty list of distinct, positive generation numbers.
        /// This does not verify availability in the Pokémon catalogue.
        /// </summary>
        private static void ValidateGenerations(
            List<int> generations,
            string path
        )
        {
            Require(
                generations != null && generations.Count > 0,
                path + " must contain at least one generation."
            );

            var seenGenerations = new HashSet<int>();

            for (int index = 0; index < generations.Count; index++)
            {
                int generation = generations[index];

                Require(
                    generation > 0,
                    path + "[" + index + "] must be greater than zero."
                );

                Require(
                    seenGenerations.Add(generation),
                    path + " contains duplicate generation " +
                    generation + "."
                );
            }
        }

        /// <summary>
        /// Requires consecutive seasons to meet at exactly the same instant.
        /// Rejects overlaps and gaps between configured seasons.
        /// Does not create seasons beyond the configured schedule.
        /// </summary>
        private static void ValidateContinuousSchedule(
            List<SeasonDefinition> seasons
        )
        {
            // Sort a copy so the original configuration order is preserved.
            var orderedSeasons = new List<SeasonDefinition>(seasons);

            orderedSeasons.Sort(
                (left, right) => left.StartsAt.CompareTo(right.StartsAt)
            );

            for (int index = 1; index < orderedSeasons.Count; index++)
            {
                SeasonDefinition previous = orderedSeasons[index - 1];
                SeasonDefinition current = orderedSeasons[index];

                // A start before the previous end creates an overlap.
                // Keep this error separate so the problem is clear.
                Require(
                    current.StartsAt >= previous.EndsAtExclusive,
                    "Season '" + current.Id +
                    "' overlaps season '" + previous.Id + "'."
                );

                // Once overlaps are excluded, anything other than equality
                // means there is a gap between these two seasons.
                Require(
                    current.StartsAt == previous.EndsAtExclusive,
                    "Gap between season '" + previous.Id +
                    "' and season '" + current.Id +
                    "'. Each season must start exactly when the previous " +
                    "season ends."
                );
            }
        }

        /// <summary>
        /// Rejects missing text and accidental surrounding whitespace.
        /// Values are rejected rather than silently trimmed.
        /// </summary>
        private static void RequireText(string value, string path)
        {
            Require(
                !string.IsNullOrWhiteSpace(value),
                path + " must not be empty."
            );

            Require(
                value == value.Trim(),
                path + " must not contain leading or trailing whitespace."
            );
        }

        /// <summary>
        /// Gives every validation failure a consistent exception and prefix.
        /// </summary>
        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(
                    "Season configuration error: " + message
                );
            }
        }
    }
}