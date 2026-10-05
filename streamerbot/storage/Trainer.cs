using System;

namespace PokeHunter.Storage
{
    /// <summary>
    /// A saved trainer's identity and profile information.
    /// Collection progress is stored separately.
    /// </summary>
    public sealed class Trainer
    {
        /// <summary>
        /// Permanent Twitch account identifier.
        /// Stored as text because it is an identifier, not a calculation.
        /// </summary>
        public string TwitchUserId { get; }

        /// <summary>
        /// Most recently saved Twitch login name.
        /// This value may change without creating another trainer.
        /// </summary>
        public string LoginName { get; }

        /// <summary>
        /// Most recently saved display name for messages and overlays.
        /// Preserves the supplied capitalization.
        /// </summary>
        public string DisplayName { get; }

        /// <summary>
        /// When this trainer was first added to this game's database.
        /// This is not the Twitch account's creation date.
        /// </summary>
        public DateTimeOffset CreatedAtUtc { get; }

        /// <summary>
        /// When the saved login or display name last changed.
        /// Initially matches CreatedAtUtc.
        /// </summary>
        public DateTimeOffset UpdatedAtUtc { get; }

        /// <summary>
        /// Storage code constructs snapshots from saved database values.
        /// Changing a snapshot does not update the database.
        /// </summary>
        internal Trainer(
            string twitchUserId,
            string loginName,
            string displayName,
            DateTimeOffset createdAtUtc,
            DateTimeOffset updatedAtUtc
        )
        {
            TwitchUserId = twitchUserId;
            LoginName = loginName;
            DisplayName = displayName;
            CreatedAtUtc = createdAtUtc;
            UpdatedAtUtc = updatedAtUtc;
        }
    }
}