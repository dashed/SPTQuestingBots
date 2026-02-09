using System;
using System.Collections.Generic;

namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Per-bot data container with a stable recycled ID.
    /// Pure C# — no Unity or EFT dependencies — fully testable in net9.0.
    /// Phase 1: minimal fields (Id, IsActive, Boss/Followers hierarchy).
    /// Later phases add BotType, IsSleeping, sensor state, etc.
    /// </summary>
    public sealed class BotEntity : IEquatable<BotEntity>
    {
        /// <summary>Stable slot ID assigned by BotRegistry. Recycled on removal.</summary>
        public readonly int Id;

        /// <summary>Whether this entity is currently active in the world.</summary>
        public bool IsActive;

        /// <summary>
        /// Boss entity for followers, null for bosses and solo bots.
        /// Replaces BotHiveMindMonitor.botBosses dictionary lookup.
        /// </summary>
        public BotEntity Boss;

        /// <summary>
        /// Direct followers of this entity (empty for non-bosses).
        /// Replaces BotHiveMindMonitor.botFollowers dictionary lookup.
        /// </summary>
        public readonly List<BotEntity> Followers;

        public BotEntity(int id)
        {
            Id = id;
            IsActive = true;
            Followers = new List<BotEntity>(4);
        }

        public bool Equals(BotEntity other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return Id == other.Id;
        }

        public override bool Equals(object obj) => Equals(obj as BotEntity);

        public override int GetHashCode() => Id;

        public override string ToString() =>
            $"BotEntity(Id={Id}, Active={IsActive}, Boss={Boss?.Id.ToString() ?? "none"}, Followers={Followers.Count})";
    }
}
