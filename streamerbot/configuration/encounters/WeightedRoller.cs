using System;
using System.Collections.Generic;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Connects a random-number source to deterministic weighted selection.
    /// Does not handle ownership, retries, or catch storage.
    /// </summary>
    public sealed class WeightedRoller
    {
        private readonly IRandomSource randomSource;

        public WeightedRoller(IRandomSource randomSource)
        {
            if (randomSource == null)
            {
                throw new ArgumentNullException(nameof(randomSource));
            }

            this.randomSource = randomSource;
        }

        /// <summary>
        /// Uses one random value to select one weighted item.
        /// Empty pools are rejected; gameplay must handle them before rolling.
        /// </summary>
        public T Roll<T>(
            IReadOnlyList<T> items,
            Func<T, double> getWeight
        )
        {
            if (items == null)
            {
                throw new ArgumentNullException(nameof(items));
            }

            if (getWeight == null)
            {
                throw new ArgumentNullException(nameof(getWeight));
            }

            if (items.Count == 0)
            {
                throw new ArgumentException(
                    "Cannot roll an empty collection.",
                    nameof(items)
                );
            }

            // WeightedSelector validates the supplied number and every weight.
            // Invalid sources or weights fail instead of awarding an item.
            return WeightedSelector.Select(
                items,
                getWeight,
                randomSource.NextDouble()
            );
        }
    }
}