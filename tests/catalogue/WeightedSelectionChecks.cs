using System;
using PokeHunter.Encounters;

namespace PokeHunter.Tests.Catalogue
{
    internal static class WeightedSelectionChecks
    {
        public static void Run()
        {
            var items = new[]
            {
                new WeightedItem("first", 1),
                new WeightedItem("second", 3)
            };

            // Total weight is 4:
            // first occupies [0, 0.25), second occupies [0.25, 1).
            Expect(items, 0, "first");
            Expect(items, 0.249999, "first");
            Expect(items, 0.25, "second");
            Expect(items, 0.999999, "second");

            Console.WriteLine(
                "PASS: Weighted selection respects exact interval boundaries."
            );

            var scaled = new[]
            {
                new WeightedItem("first", 10),
                new WeightedItem("second", 30)
            };

            foreach (double value in new[] { 0.0, 0.125, 0.25, 0.75, 0.999999 })
            {
                string originalResult = WeightedSelector.Select(
                    items,
                    item => item.Weight,
                    value
                ).Id;

                string scaledResult = WeightedSelector.Select(
                    scaled,
                    item => item.Weight,
                    value
                ).Id;

                Require(
                    originalResult == scaledResult,
                    "Scaling all weights changed their relative selection."
                );
            }

            Console.WriteLine(
                "PASS: Proportionally scaled weights preserve selection."
            );

            var single = new[]
            {
                new WeightedItem("only", 6)
            };

            Expect(single, 0, "only");
            Expect(single, 0.999999, "only");

            Console.WriteLine(
                "PASS: A single eligible item is selected consistently."
            );

            // Check a complete grid of interval midpoints rather than
            // using a random simulation that could fail by chance.
            var rarityItems = new[]
            {
                new WeightedItem("common", 10),
                new WeightedItem("uncommon", 6),
                new WeightedItem("rare", 3),
                new WeightedItem("ultraRare", 1)
            };

            var counts = new int[4];

            for (int position = 0; position < 20; position++)
            {
                double value = (position + 0.5) / 20.0;

                WeightedItem selected = WeightedSelector.Select(
                    rarityItems,
                    item => item.Weight,
                    value
                );

                int selectedIndex = Array.IndexOf(rarityItems, selected);
                counts[selectedIndex]++;
            }

            Require(
                counts[0] == 10 &&
                counts[1] == 6 &&
                counts[2] == 3 &&
                counts[3] == 1,
                "The fixed interval grid did not match the configured weights."
            );

            Console.WriteLine(
                "PASS: The 10/6/3/1 intervals match the intended relative weights."
            );

            foreach (double value in new[]
            {
                -0.1,
                1.0,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity
            })
            {
                ExpectRejected(
                    () => WeightedSelector.Select(
                        items,
                        item => item.Weight,
                        value
                    ),
                    "randomValue"
                );
            }

            ExpectRejected(
                () => WeightedSelector.Select(
                    new WeightedItem[0],
                    item => item.Weight,
                    0
                ),
                "items"
            );

            ExpectRejected(
                () => WeightedSelector.Select<WeightedItem>(
                    null,
                    item => item.Weight,
                    0
                ),
                "items"
            );

            ExpectRejected(
                () => WeightedSelector.Select(
                    items,
                    null,
                    0
                ),
                "getWeight"
            );

            foreach (double weight in new[]
            {
                0.0,
                -1.0,
                double.NaN,
                double.PositiveInfinity,
                double.NegativeInfinity
            })
            {
                var invalidPool = new[]
                {
                    new WeightedItem("valid-first", 1),
                    new WeightedItem("invalid-second", weight)
                };

                // Even when zero would select the first item, an invalid
                // later weight must cause the complete pool to be rejected.
                ExpectRejected(
                    () => WeightedSelector.Select(
                        invalidPool,
                        item => item.Weight,
                        0
                    ),
                    "getWeight"
                );
            }

            ExpectRejected(
                () => WeightedSelector.Select(
                    new[] { new WeightedItem("valid", 1), null },
                    item => item.Weight,
                    0
                ),
                "items"
            );

            ExpectRejected(
                () => WeightedSelector.Select(
                    new[]
                    {
                        new WeightedItem("first", double.MaxValue),
                        new WeightedItem("second", double.MaxValue)
                    },
                    item => item.Weight,
                    0
                ),
                "getWeight"
            );

            ExpectRejected(
                () => WeightedSelector.Select(
                    new[]
                    {
                        new WeightedItem("huge", 1e300),
                        new WeightedItem("tiny", 1)
                    },
                    item => item.Weight,
                    0
                ),
                "getWeight"
            );

            Console.WriteLine(
                "PASS: Invalid random values, pools, and weights were rejected."
            );

            int weightReads = 0;

            WeightedItem result = WeightedSelector.Select(
                items,
                item =>
                {
                    weightReads++;
                    return item.Weight;
                },
                0
            );

            Require(
                weightReads == items.Length &&
                ReferenceEquals(result, items[0]) &&
                items[0].Id == "first" &&
                items[1].Id == "second",
                "Selection changed item order or read a weight repeatedly."
            );

            Console.WriteLine(
                "PASS: Weights were read once and input order was preserved."
            );
        }

        private static void Expect(
            WeightedItem[] items,
            double randomValue,
            string expectedId
        )
        {
            WeightedItem selected = WeightedSelector.Select(
                items,
                item => item.Weight,
                randomValue
            );

            Require(
                selected.Id == expectedId,
                "Unexpected weighted selection at " + randomValue + "."
            );
        }

        private static void ExpectRejected(
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
                    "Rejection identified an unexpected parameter: " +
                    exception.ParamName + "."
                );

                return;
            }

            throw new InvalidOperationException(
                "Expected invalid weighted-selection input to be rejected."
            );
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class WeightedItem
        {
            public string Id { get; private set; }
            public double Weight { get; private set; }

            public WeightedItem(string id, double weight)
            {
                Id = id;
                Weight = weight;
            }
        }
    }
}