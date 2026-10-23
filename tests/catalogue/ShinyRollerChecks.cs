using System;
using System.Collections.Generic;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class ShinyRollerChecks
    {
        public static void Run()
        {
            CheckSuccessBoundary();
            CheckProbabilityExtremes();
            CheckInvalidProbabilities();
            CheckInvalidSourceValues();
            CheckMissingSource();

            Console.WriteLine("All shiny roller checks passed.");
        }

        private static void CheckSuccessBoundary()
        {
            double probability = 1.0 / 8192;

            var source = new SequenceRandomSource(
                probability / 2,
                probability,
                probability * 2
            );

            var roller = new ShinyRoller(source);

            Require(
                roller.Roll(probability),
                "A value below the probability must produce a shiny."
            );

            Require(
                !roller.Roll(probability),
                "A value equal to the probability must not produce a shiny."
            );

            Require(
                !roller.Roll(probability),
                "A value above the probability must not produce a shiny."
            );

            Require(
                source.DrawCount == 3,
                "Three rolls must consume exactly three draws."
            );

            Console.WriteLine(
                "PASS: Shiny rolls respected the exact success boundary."
            );
        }

        private static void CheckProbabilityExtremes()
        {
            var source = new SequenceRandomSource(0.0, 0.999999);
            var roller = new ShinyRoller(source);

            Require(
                !roller.Roll(0),
                "A zero probability must fail even when the draw is zero."
            );

            Require(
                roller.Roll(1),
                "A probability of one must succeed for every valid draw."
            );

            Require(
                source.DrawCount == 2,
                "Probability extremes must still consume one draw each."
            );

            Console.WriteLine(
                "PASS: Zero always failed and 100% always succeeded."
            );
        }

        private static void CheckInvalidProbabilities()
        {
            double[] invalidValues =
            {
                -0.1,
                1.1,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity
            };

            foreach (double probability in invalidValues)
            {
                // No values: requesting randomness would fail this test.
                var source = new SequenceRandomSource();
                var roller = new ShinyRoller(source);
                bool rejected = false;

                try
                {
                    roller.Roll(probability);
                }
                catch (ArgumentOutOfRangeException exception)
                {
                    Require(
                        exception.ParamName == "probability",
                        "The error must identify probability."
                    );

                    rejected = true;
                }

                Require(rejected, "An invalid probability was accepted.");

                Require(
                    source.DrawCount == 0,
                    "Invalid probabilities must not consume randomness."
                );
            }

            Console.WriteLine(
                "PASS: Invalid probabilities were rejected before drawing."
            );
        }

        private static void CheckInvalidSourceValues()
        {
            double[] invalidValues =
            {
                -0.1,
                1.0,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity
            };

            foreach (double value in invalidValues)
            {
                var source = new SequenceRandomSource(value);
                var roller = new ShinyRoller(source);
                bool rejected = false;

                try
                {
                    roller.Roll(0.5);
                }
                catch (InvalidOperationException exception)
                {
                    Require(
                        exception.Message.StartsWith(
                            "The random source must return",
                            StringComparison.Ordinal
                        ),
                        "The failure must identify an invalid source value."
                    );

                    rejected = true;
                }

                Require(rejected, "An invalid source value was accepted.");

                Require(
                    source.DrawCount == 1,
                    "The invalid value must have come from one source draw."
                );
            }

            Console.WriteLine(
                "PASS: Invalid random-source values raised technical errors."
            );
        }

        private static void CheckMissingSource()
        {
            bool rejected = false;

            try
            {
                new ShinyRoller(null);
            }
            catch (ArgumentNullException exception)
            {
                Require(
                    exception.ParamName == "randomSource",
                    "The error must identify randomSource."
                );

                rejected = true;
            }

            Require(rejected, "A missing random source was accepted.");

            Console.WriteLine(
                "PASS: A missing shiny random source was rejected."
            );
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Supplies known values and detects unexpected extra draws.
        /// </summary>
        private sealed class SequenceRandomSource : IRandomSource
        {
            private readonly Queue<double> values;

            public int DrawCount { get; private set; }

            public SequenceRandomSource(params double[] values)
            {
                this.values = new Queue<double>(values);
            }

            public double NextDouble()
            {
                if (values.Count == 0)
                {
                    throw new InvalidOperationException(
                        "An unexpected random draw was requested."
                    );
                }

                DrawCount++;
                return values.Dequeue();
            }
        }
    }
}