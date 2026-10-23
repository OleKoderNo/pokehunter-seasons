using System;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Rolls one shiny decision using an already-calculated probability.
    /// Does not calculate bonuses or manage redemption retry protection.
    /// </summary>
    public sealed class ShinyRoller
    {
        private readonly IRandomSource randomSource;

        public ShinyRoller(IRandomSource randomSource)
        {
            if (randomSource == null)
            {
                throw new ArgumentNullException(nameof(randomSource));
            }

            this.randomSource = randomSource;
        }

        /// <summary>
        /// Consumes exactly one random value for a valid probability.
        /// Zero always fails; one always succeeds.
        /// </summary>
        public bool Roll(double probability)
        {
            if (double.IsNaN(probability) ||
                double.IsInfinity(probability) ||
                probability < 0 ||
                probability > 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(probability),
                    "Shiny probability must be between zero and one."
                );
            }

            double value = randomSource.NextDouble();

            // A broken source is a technical error, not an unlucky roll.
            if (double.IsNaN(value) ||
                double.IsInfinity(value) ||
                value < 0 ||
                value >= 1)
            {
                throw new InvalidOperationException(
                    "The random source must return a finite value " +
                    "greater than or equal to zero and less than one."
                );
            }

            return value < probability;
        }
    }
}