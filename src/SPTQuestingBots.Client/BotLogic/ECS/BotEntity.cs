using System;
using System.Collections.Generic;

namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Per-bot data container with a stable recycled ID.
    /// Pure C# — no Unity or EFT dependencies — fully testable in net9.0.
    /// </summary>
    public sealed class BotEntity : IEquatable<BotEntity>
    {
        // ── Phase 1: Identity + Hierarchy ────────────────────────

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

        // ── Phase 2: Sensor State ───────────────────────────────

        /// <summary>Whether this bot is currently in combat. Default: false.</summary>
        public bool IsInCombat;

        /// <summary>Whether this bot has detected something suspicious. Default: false.</summary>
        public bool IsSuspicious;

        /// <summary>Whether this bot is allowed to quest. Default: false.</summary>
        public bool CanQuest;

        /// <summary>Whether this bot can sprint to its current objective. Default: true.</summary>
        public bool CanSprintToObjective;

        /// <summary>Whether this bot wants to loot. Default: false.</summary>
        public bool WantsToLoot;

        /// <summary>
        /// Last time this bot started looting. Replaces botLastLootingTime dictionary.
        /// Default: <see cref="DateTime.MinValue"/>.
        /// </summary>
        public DateTime LastLootingTime;

        // ── Phase 2: Classification + Sleep ─────────────────────

        /// <summary>
        /// Bot classification. Replaces registeredPMCs/registeredBosses HashSet lookups.
        /// Default: <see cref="BotType.Unknown"/>.
        /// </summary>
        public BotType BotType;

        /// <summary>
        /// Whether this bot is currently sleeping (AI disabled by limiter).
        /// Replaces sleepingBotIds list lookup.
        /// Default: false.
        /// </summary>
        public bool IsSleeping;

        // ── Phase 6: Zone Movement Field State ─────────────────

        /// <summary>
        /// Per-bot noise seed for zone movement field composition.
        /// Set once from bot profile ID hash. Default: 0 (uninitialized).
        /// </summary>
        public int FieldNoiseSeed;

        /// <summary>
        /// Whether this bot has an active field state for zone movement.
        /// </summary>
        public bool HasFieldState;

        // ── Movement State ─────────────────────────────────────

        /// <summary>
        /// Per-bot movement tracking for the custom path-following system.
        /// Inline value type for dense iteration and zero-allocation reads.
        /// </summary>
        public MovementState Movement;

        // ── Phase 8: Job Assignment State ─────────────────────

        /// <summary>
        /// Number of consecutive failed quest assignments for this bot.
        /// Replaces BotJobAssignmentFactory dictionary lookup.
        /// Default: 0.
        /// </summary>
        public int ConsecutiveFailedAssignments;

        // ── Constructor ─────────────────────────────────────────

        public BotEntity(int id)
        {
            Id = id;
            IsActive = true;
            Followers = new List<BotEntity>(4);

            // Sensor defaults (match original sensor constructors)
            CanSprintToObjective = true; // BotHiveMindCanSprintToObjectiveSensor default
            LastLootingTime = DateTime.MinValue;
        }

        // ── Hierarchy Queries ───────────────────────────────────

        /// <summary>Whether this entity has a boss assigned.</summary>
        public bool HasBoss => Boss != null;

        /// <summary>Whether this entity has any followers.</summary>
        public bool HasFollowers => Followers.Count > 0;

        // ── Sensor Access by Enum ───────────────────────────────

        /// <summary>
        /// Read a sensor boolean by <see cref="BotSensor"/> enum.
        /// Avoids per-sensor method duplication for group queries.
        /// </summary>
        public bool GetSensor(BotSensor sensor)
        {
            switch (sensor)
            {
                case BotSensor.InCombat:
                    return IsInCombat;
                case BotSensor.IsSuspicious:
                    return IsSuspicious;
                case BotSensor.CanQuest:
                    return CanQuest;
                case BotSensor.CanSprintToObjective:
                    return CanSprintToObjective;
                case BotSensor.WantsToLoot:
                    return WantsToLoot;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Write a sensor boolean by <see cref="BotSensor"/> enum.
        /// </summary>
        public void SetSensor(BotSensor sensor, bool value)
        {
            switch (sensor)
            {
                case BotSensor.InCombat:
                    IsInCombat = value;
                    break;
                case BotSensor.IsSuspicious:
                    IsSuspicious = value;
                    break;
                case BotSensor.CanQuest:
                    CanQuest = value;
                    break;
                case BotSensor.CanSprintToObjective:
                    CanSprintToObjective = value;
                    break;
                case BotSensor.WantsToLoot:
                    WantsToLoot = value;
                    break;
            }
        }

        // ── Group Sensor Queries (zero allocation) ──────────────

        /// <summary>
        /// Check if this bot's boss has a given sensor value set.
        /// Returns false if no boss is assigned.
        /// Replaces BotHiveMindAbstractSensor.CheckForBossOfBot.
        /// </summary>
        public bool CheckSensorForBoss(BotSensor sensor)
        {
            return Boss != null && Boss.GetSensor(sensor);
        }

        /// <summary>
        /// Check if any follower of this entity has a given sensor value set.
        /// Replaces BotHiveMindAbstractSensor.checkStateForAnyFollowers.
        /// </summary>
        public bool CheckSensorForAnyFollower(BotSensor sensor)
        {
            for (int i = 0; i < Followers.Count; i++)
            {
                if (Followers[i].GetSensor(sensor))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if any group member (boss or any of boss's followers) has a given sensor value set.
        /// The "group boss" is this entity's Boss, or this entity itself if it has no boss.
        /// Replaces BotHiveMindAbstractSensor.checkStateForAnyGroupMembers.
        /// </summary>
        public bool CheckSensorForGroup(BotSensor sensor)
        {
            var groupBoss = Boss ?? this;

            if (groupBoss.GetSensor(sensor))
                return true;

            for (int i = 0; i < groupBoss.Followers.Count; i++)
            {
                if (groupBoss.Followers[i].GetSensor(sensor))
                    return true;
            }

            return false;
        }

        // ── Equality ────────────────────────────────────────────

        public bool Equals(BotEntity other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return Id == other.Id;
        }

        public override bool Equals(object obj) => Equals(obj as BotEntity);

        public override int GetHashCode() => Id;

        public override string ToString() =>
            $"BotEntity(Id={Id}, Type={BotType}, Active={IsActive}, Sleeping={IsSleeping}, "
            + $"Boss={Boss?.Id.ToString() ?? "none"}, Followers={Followers.Count})";
    }
}
