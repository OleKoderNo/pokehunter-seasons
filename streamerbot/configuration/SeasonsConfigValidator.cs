using System;
using System.Collections.Generic;
using System.IO;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Checks season configuration values and scheduling rules.
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

            RequireText(config.TimeZone, "timeZone");

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

            // Only check overlaps after every individual season is valid.
            ValidateNoOverlaps(config.Seasons);
        }

        /// <summary>
        /// Requires a nonempty list of distinct, positive generation numbers.
        /// Catalogue support for those generations is checked separately.
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
        /// Checks boundaries in chronological order without changing
        /// the order of seasons in the original configuration.
        /// </summary>
        private static void ValidateNoOverlaps(
            List<SeasonDefinition> seasons
        )
        {
            var orderedSeasons = new List<SeasonDefinition>(seasons);

            orderedSeasons.Sort(
                (left, right) => left.StartsAt.CompareTo(right.StartsAt)
            );

            for (int index = 1; index < orderedSeasons.Count; index++)
            {
                SeasonDefinition previous = orderedSeasons[index - 1];
                SeasonDefinition current = orderedSeasons[index];

                // Equality is allowed: the previous season ends exactly
                // when the next begins. An earlier start is an overlap.
                Require(
                    current.StartsAt >= previous.EndsAtExclusive,
                    "Season '" + current.Id +
                    "' overlaps season '" + previous.Id + "'."
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