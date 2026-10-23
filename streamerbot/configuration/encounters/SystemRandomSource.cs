using System;

namespace PokeHunter.Encounters
{
    /// <summary>
    /// Provides pseudorandom values using one retained generator.
    /// Access is synchronized because System.Random is not thread-safe
    /// on the project's .NET Framework runtime.
    /// </summary>
    public sealed class SystemRandomSource : IRandomSource
    {
        private readonly Random generator = new Random();
        private readonly object syncRoot = new object();

        /// <summary>
        /// Shared gameplay source. Do not create a new generator per roll.
        /// </summary>
        public static SystemRandomSource Shared { get; } =
            new SystemRandomSource();

        private SystemRandomSource()
        {
        }

        public double NextDouble()
        {
            lock (syncRoot)
            {
                return generator.NextDouble();
            }
        }
    }
}