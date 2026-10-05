using System;
using System.Collections.Generic;
using System.IO;

namespace PokeHunter.Configuration
{
    /// <summary>
    /// Validates event settings and prevents overlapping enabled events.
    /// Does not modify the configuration or resolve catalogue entries.
    /// </summary>
    public static class EventsConfigValidator
    {
        // The 18 type identifiers supported by this game's configuration.
        // Lowercase spelling keeps identifiers consistent across files.
        private static readonly HashSet<string> SupportedTypes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "normal",
                "fire",
                "water",
                "electric",
                "grass",
                "ice",
                "fighting",
                "poison",
                "ground",
                "flying",
                "psychic",
                "bug",
                "rock",
                "ghost",
                "dragon",
                "dark",
                "steel",
                "fairy"
            };

        /// <summary>
        /// Throws InvalidDataException for the first invalid setting found.
        /// An empty events list is valid and means no events are scheduled.
        /// </summary>
        public static void Validate(EventsConfig config)
        {
            Require(
                config != null,
                "The configuration must not be null."
            );

            Require(
                config.SchemaVersion == 1,
                "schemaVersion must be 1."
            );

            RequireText(config.TimeZone, "timeZone");

            // Confirm that the named zone can be used on this computer.
            // Explicit offsets still define the event boundary instants.
            ConfigurationTimeZone.Resolve(config.TimeZone);

            Require(
                config.Events != null,
                "events must be an array. Use an empty array for no events."
            );

            var eventIds = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase
            );

            var enabledEvents = new List<EventDefinition>();

            for (int index = 0; index < config.Events.Count; index++)
            {
                EventDefinition definition = config.Events[index];
                string path = "events[" + index + "]";

                Require(
                    definition != null,
                    path + " must not be null."
                );

                RequireText(definition.Id, path + ".id");
                RequireText(definition.Name, path + ".name");

                // IDs identify definitions even when they are disabled.
                Require(
                    eventIds.Add(definition.Id),
                    path + ".id duplicates another event ID: " +
                    definition.Id + "."
                );

                Require(
                    definition.StartsAt != default(DateTimeOffset),
                    path + ".startsAt must contain a starting timestamp."
                );

                Require(
                    definition.EndsAtExclusive > definition.StartsAt,
                    path + ".endsAtExclusive must be later than startsAt."
                );

                Require(
                    !double.IsNaN(definition.CategoryWeight) &&
                    !double.IsInfinity(definition.CategoryWeight) &&
                    definition.CategoryWeight > 0,
                    path + ".categoryWeight must be a finite number " +
                    "greater than zero. Use enabled=false to disable an event."
                );

                ValidateInclusion(
                    definition.Include,
                    path + ".include"
                );

                // Disabled definitions remain validated, but cannot cause
                // a conflict in the active event schedule.
                if (definition.Enabled)
                {
                    enabledEvents.Add(definition);
                }
            }

            ValidateNoEnabledOverlaps(enabledEvents);
        }

        /// <summary>
        /// Requires all inclusion lists and at least one selector overall.
        /// Individual lists may be empty.
        /// </summary>
        private static void ValidateInclusion(
            EventInclusionConfig include,
            string path
        )
        {
            Require(
                include != null,
                path + " must not be null."
            );

            ValidateIdentifiers(include.Types, path + ".types");
            ValidateIdentifiers(
                include.EvolutionFamilies,
                path + ".evolutionFamilies"
            );
            ValidateIdentifiers(include.Forms, path + ".forms");
            ValidateIdentifiers(include.Costumes, path + ".costumes");

            Require(
                include.Types.Count > 0 ||
                include.EvolutionFamilies.Count > 0 ||
                include.Forms.Count > 0 ||
                include.Costumes.Count > 0,
                path + " must contain at least one inclusion selector."
            );

            for (int index = 0; index < include.Types.Count; index++)
            {
                string type = include.Types[index];

                Require(
                    SupportedTypes.Contains(type),
                    path + ".types[" + index +
                    "] contains an unsupported type: " + type + "."
                );
            }
        }

        /// <summary>
        /// Rejects missing lists, malformed identifiers, and duplicates
        /// within the same list. Does not check catalogue existence.
        /// </summary>
        private static void ValidateIdentifiers(
            List<string> identifiers,
            string path
        )
        {
            Require(
                identifiers != null,
                path + " must be an array. Use an empty array when unused."
            );

            var seen = new HashSet<string>(StringComparer.Ordinal);

            for (int index = 0; index < identifiers.Count; index++)
            {
                string identifier = identifiers[index];
                string itemPath = path + "[" + index + "]";

                RequireText(identifier, itemPath);

                Require(
                    IsValidIdentifier(identifier),
                    itemPath + " must use lowercase letters, digits, " +
                    "and single hyphens between nonempty parts."
                );

                Require(
                    seen.Add(identifier),
                    path + " contains duplicate identifier: " +
                    identifier + "."
                );
            }
        }

        /// <summary>
        /// Accepts identifiers such as ghost, houndoom, and pikachu-phd.
        /// Rejects spaces, uppercase letters, and empty hyphenated parts.
        /// </summary>
        private static bool IsValidIdentifier(string value)
        {
            if (value[0] == '-' || value[value.Length - 1] == '-')
            {
                return false;
            }

            bool previousWasHyphen = false;

            foreach (char character in value)
            {
                if (character == '-')
                {
                    if (previousWasHyphen)
                    {
                        return false;
                    }

                    previousWasHyphen = true;
                    continue;
                }

                bool isLowercaseLetter =
                    character >= 'a' && character <= 'z';

                bool isDigit =
                    character >= '0' && character <= '9';

                if (!isLowercaseLetter && !isDigit)
                {
                    return false;
                }

                previousWasHyphen = false;
            }

            return true;
        }

        /// <summary>
        /// Allows gaps and touching boundaries, but rejects overlaps
        /// between enabled events.
        /// </summary>
        private static void ValidateNoEnabledOverlaps(
            List<EventDefinition> enabledEvents
        )
        {
            // This list was created during validation.
            // Sorting it does not reorder the original configuration.
            enabledEvents.Sort(
                (left, right) => left.StartsAt.CompareTo(right.StartsAt)
            );

            for (int index = 1; index < enabledEvents.Count; index++)
            {
                EventDefinition previous = enabledEvents[index - 1];
                EventDefinition current = enabledEvents[index];

                Require(
                    current.StartsAt >= previous.EndsAtExclusive,
                    "Enabled event '" + current.Id +
                    "' overlaps enabled event '" + previous.Id + "'."
                );
            }
        }

        /// <summary>
        /// Requires text without silently trimming or replacing it.
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

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidDataException(
                    "Event configuration error: " + message
                );
            }
        }
    }
}