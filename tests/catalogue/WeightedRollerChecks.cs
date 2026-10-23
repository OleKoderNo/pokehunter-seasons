using System;
using System.Collections.Generic;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class WeightedRollerChecks
    {
        public static void Run()
        {
            CheckSeparateDraws();
            CheckSingleItem();
            CheckEmptyPool();
            CheckInvalidSourceValue();
            CheckMissingSource();

            Console.WriteLine(
                "All weighted roller checks passed."
            );
        }

        private static void CheckSeparateDraws()
        {
            // Weights 1 and 3 give intervals [0, 0.25) and [0.25, 1).
            var source = new SequenceRandomSource(0.10, 0.75);
            var roller = new WeightedRoller(source);
            var items = new[] { "first", "second" };

            string first = roller.Roll(
                items,
                item => item == "first" ? 1.0 : 3.0
            );

            string second = roller.Roll(
                items,
                item => item == "first" ? 1.0 : 3.0
            );

            Require(
                first == "first" && second == "second",
                "Successive rolls did not use their supplied values."
            );

            Require(
                source.DrawCount == 2,
                "Two rolls must consume exactly two random values."
            );

            Console.WriteLine(
                "PASS: Successive weighted rolls consumed separate values."
            );
        }

        private static void CheckSingleItem()
        {
            var source = new SequenceRandomSource(0.99);
            var roller = new WeightedRoller(source);

            string selected = roller.Roll(
                new[] { "only" },
                item => 1.0
            );

            Require(
                selected == "only" && source.DrawCount == 1,
                "A single-item roll must return its item using one draw."
            );

            Console.WriteLine(
                "PASS: A single-item pool used one draw and selected its item."
            );
        }

        private static void CheckEmptyPool()
        {
            var source = new SequenceRandomSource();
            var roller = new WeightedRoller(source);

            ExpectArgumentException(
                () => roller.Roll(
                    new string[0],
                    item => 1.0
                ),
                "items"
            );

            Require(
                source.DrawCount == 0,
                "An empty pool must be rejected before requesting randomness."
            );

            Console.WriteLine(
                "PASS: An empty pool was rejected without consuming a draw."
            );
        }

        private static void CheckInvalidSourceValue()
        {
            var source = new SequenceRandomSource(1.0);
            var roller = new WeightedRoller(source);

            ExpectArgumentException(
                () => roller.Roll(
                    new[] { "only" },
                    item => 1.0
                ),
                "randomValue"
            );

            Console.WriteLine(
                "PASS: A random source returning 1 was rejected."
            );
        }

        private static void CheckMissingSource()
        {
            ExpectArgumentException(
                () => { new WeightedRoller(null); },
                "randomSource"
            );

            Console.WriteLine(
                "PASS: A missing random source was rejected."
            );
        }

        private static void ExpectArgumentException(
            Action action,
            string expectedParameter
        )
        {
            try
            {
                action();
            }
            catch (ArgumentException exception)
            {
                Require(
                    exception.ParamName == expectedParameter,
                    "Expected an argument error for '" +
                    expectedParameter + "', but received '" +
                    exception.ParamName + "'."
                );

                return;
            }

            throw new InvalidOperationException(
                "Expected an argument error for '" +
                expectedParameter + "'."
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
        /// Test-only source. Exhaustion fails the test, revealing
        /// unexpected additional random draws.
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
                        "The test requested an unexpected random draw."
                    );
                }

                DrawCount++;
                return values.Dequeue();
            }
        }
    }
}