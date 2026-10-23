using System;
using System.Collections.Generic;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Selects one item using positive relative weights.
    ///
    /// Randomness is supplied by the caller so selection can be tested
    /// deterministically. This class does not generate random numbers.
    /// </summary>
    public static class WeightedSelector
    {
        /// <summary>
        /// Selects an item using a value in the range [0, 1).
        ///
        /// Zero is valid; one is not. Every weight must be finite
        /// and greater than zero.
        ///
        /// The supplied list is not modified. The selected item itself
        /// is returned, not a copy.
        /// </summary>
        public static T Select<T>(
            IReadOnlyList<T> items,
            Func<T, double> getWeight,
            double randomValue
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
                    "Weighted selection requires at least one item.",
                    nameof(items)
                );
            }

            if (
                double.IsNaN(randomValue) ||
                double.IsInfinity(randomValue) ||
                randomValue < 0 ||
                randomValue >= 1
            )
            {
                throw new ArgumentOutOfRangeException(
                    nameof(randomValue),
                    "The random value must be finite and in the range [0, 1)."
                );
            }

            // Read each weight exactly once. Validate the complete pool
            // before selecting, including items after a possible winner.
            var weights = new double[items.Count];
            double totalWeight = 0;

            for (int index = 0; index < items.Count; index++)
            {
                if (ReferenceEquals(items[index], null))
                {
                    throw new ArgumentException(
                        "Weighted selection items must not be null.",
                        nameof(items)
                    );
                }

                double weight = getWeight(items[index]);

                if (
                    double.IsNaN(weight) ||
                    double.IsInfinity(weight) ||
                    weight <= 0
                )
                {
                    throw new ArgumentException(
                        "Every selection weight must be finite and " +
                        "greater than zero.",
                        nameof(getWeight)
                    );
                }

                double nextTotal = totalWeight + weight;

                if (double.IsInfinity(nextTotal))
                {
                    throw new ArgumentException(
                        "The total selection weight is too large.",
                        nameof(getWeight)
                    );
                }

                // Reject extreme weight differences that would make
                // an item's interval disappear at double precision.
                if (nextTotal <= totalWeight)
                {
                    throw new ArgumentException(
                        "Selection weights differ too greatly to represent " +
                        "every interval reliably.",
                        nameof(getWeight)
                    );
                }

                weights[index] = weight;
                totalWeight = nextTotal;
            }

            double target = randomValue * totalWeight;
            double cumulativeWeight = 0;

            for (int index = 0; index < items.Count; index++)
            {
                cumulativeWeight += weights[index];

                // Intervals include their start and exclude their end.
                // An exact boundary belongs to the following item.
                if (target < cumulativeWeight)
                {
                    return items[index];
                }
            }

            // Floating-point multiplication can round a target just
            // below the total up to the total. The final interval owns
            // that upper-edge rounding case.
            return items[items.Count - 1];
        }
    }
}