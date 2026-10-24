using System.Collections.Generic;

namespace PokeHunter.Storage
{
    /// <summary>
    /// Immutable collection state read at one database snapshot.
    /// It may become stale after another redemption commits; the future
    /// save workflow must guard against using stale state.
    /// </summary>
    public sealed class CollectionSnapshot
    {
        public string TwitchUserId { get; private set; }
        public string SeasonId { get; private set; }

        /// <summary>Successful collection entry IDs in this season only.</summary>
        public IReadOnlyList<string> SeasonalEntryIds { get; private set; }

        public int ConsecutiveFailedRedemptions { get; private set; }
        public int UniqueNormalEntries { get; private set; }
        public int UniqueShinyEntries { get; private set; }

        /// <summary>Includes shiny entries; repeats across seasons count once.</summary>
        public long TotalUniqueEntries
        {
            get { return (long)UniqueNormalEntries + UniqueShinyEntries; }
        }

        internal CollectionSnapshot(
            string userId, string seasonId, List<string> entries,
            int streak, int normal, int shiny)
        {
            TwitchUserId = userId;
            SeasonId = seasonId;
            SeasonalEntryIds = new List<string>(entries).AsReadOnly();
            ConsecutiveFailedRedemptions = streak;
            UniqueNormalEntries = normal;
            UniqueShinyEntries = shiny;
        }
    }
}