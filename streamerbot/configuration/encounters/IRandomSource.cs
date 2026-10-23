namespace PokeHunter.Encounters
{
    /// <summary>
    /// Supplies numbers for encounter decisions.
    /// Tests can supply predetermined values instead of real randomness.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>
        /// Returns a value greater than or equal to zero and less than one.
        /// </summary>
        double NextDouble();
    }
}