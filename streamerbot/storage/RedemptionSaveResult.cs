using System;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Result returned after the repository finishes its transaction.
    /// Contains either the newly saved record or a previously saved one.
    /// </summary>
    public sealed class RedemptionSaveResult
    {
        public StoredRedemption Redemption { get; private set; }

        /// <summary>
        /// True when this operation inserted the redemption.
        /// False when the redemption had already been saved.
        ///
        /// This distinguishes duplicate processing, but does not by itself
        /// guarantee reliable or exactly-once notification delivery.
        /// </summary>
        public bool WasCreated { get; private set; }

        internal RedemptionSaveResult(
            StoredRedemption redemption,
            bool wasCreated
        )
        {
            if (redemption == null)
            {
                throw new ArgumentNullException(nameof(redemption));
            }

            Redemption = redemption;
            WasCreated = wasCreated;
        }
    }
}