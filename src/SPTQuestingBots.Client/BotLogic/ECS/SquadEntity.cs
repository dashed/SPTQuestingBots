using System;
using System.Collections.Generic;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Per-squad data container with a stable recycled ID.
    /// Holds members, a shared objective, and strategy scoring state.
    /// Pure C# — no Unity or EFT dependencies — fully testable in net9.0.
    /// </summary>
    public sealed class SquadEntity : IEquatable<SquadEntity>
    {
        // ── Identity ────────────────────────────────────────────────

        /// <summary>Stable slot ID assigned by SquadRegistry. Recycled on removal.</summary>
        public readonly int Id;

        /// <summary>Whether this squad is currently active.</summary>
        public bool IsActive;

        // ── Membership ──────────────────────────────────────────────

        /// <summary>The squad leader (first member added, or reassigned on leader removal).</summary>
        public BotEntity Leader;

        /// <summary>All members of the squad, including the leader.</summary>
        public readonly List<BotEntity> Members;

        /// <summary>Target number of members for this squad.</summary>
        public int TargetMembersCount;

        // ── Shared Objective ────────────────────────────────────────

        /// <summary>Shared objective state for the squad.</summary>
        public readonly SquadObjective Objective;

        // ── Strategy Scoring ────────────────────────────────────────

        /// <summary>
        /// Per-strategy utility scores. Sized to the number of registered strategies.
        /// Written by each strategy's UpdateScores, read by the strategy manager's PickStrategy.
        /// </summary>
        public readonly float[] StrategyScores;

        /// <summary>
        /// The currently assigned strategy and its ordinal index.
        /// Default: no strategy assigned.
        /// </summary>
        public StrategyAssignment StrategyAssignment;

        // ── Squad Personality ──────────────────────────────────────────

        /// <summary>Personality type determined from member composition.</summary>
        public SquadPersonalityType PersonalityType;

        /// <summary>Coordination level (1-5) from personality settings. Affects sharing probability.</summary>
        public float CoordinationLevel;

        /// <summary>Aggression level (1-5) from personality settings.</summary>
        public float AggressionLevel;

        // ── Combat Threat Tracking ─────────────────────────────────

        /// <summary>
        /// Normalized X component of the direction from objective toward the detected threat.
        /// Set by BotHiveMindMonitor when any squad member detects an enemy.
        /// </summary>
        public float ThreatDirectionX;

        /// <summary>
        /// Normalized Z component of the direction from objective toward the detected threat.
        /// Set by BotHiveMindMonitor when any squad member detects an enemy.
        /// </summary>
        public float ThreatDirectionZ;

        /// <summary>
        /// Whether a valid threat direction has been computed for this squad.
        /// Reset when threat clears (no members in combat).
        /// </summary>
        public bool HasThreatDirection;

        /// <summary>
        /// Monotonic counter bumped when combat state changes (threat detected or cleared).
        /// Compared by GotoObjectiveStrategy to trigger combat position re-evaluation.
        /// </summary>
        public int CombatVersion;

        /// <summary>
        /// Last CombatVersion processed by GotoObjectiveStrategy.
        /// When CombatVersion != LastProcessedCombatVersion, positions are re-computed.
        /// </summary>
        public int LastProcessedCombatVersion;

        // ── Formation Heading Tracking ──────────────────────────────

        /// <summary>Previous leader X position for heading computation.</summary>
        public float PreviousLeaderX;

        /// <summary>Previous leader Z position for heading computation.</summary>
        public float PreviousLeaderZ;

        // ── Zone Movement State ───────────────────────────────────

        /// <summary>
        /// Whether the squad's current objective is from zone-based movement (no quest assigned).
        /// Set by BotHiveMindMonitor when the boss's current quest name matches the zone movement quest.
        /// </summary>
        public bool IsZoneObjective;

        // ── Shared Loot State ────────────────────────────────────────

        /// <summary>Number of valid shared loot targets from boss scan results.</summary>
        public int SharedLootCount;

        /// <summary>Shared loot target IDs (max 8). Distributed to followers within comm range.</summary>
        public readonly int[] SharedLootIds = new int[8];

        /// <summary>Shared loot target X positions (max 8).</summary>
        public readonly float[] SharedLootX = new float[8];

        /// <summary>Shared loot target Y positions (max 8).</summary>
        public readonly float[] SharedLootY = new float[8];

        /// <summary>Shared loot target Z positions (max 8).</summary>
        public readonly float[] SharedLootZ = new float[8];

        /// <summary>Shared loot target values (max 8).</summary>
        public readonly float[] SharedLootValues = new float[8];

        /// <summary>Shared loot target types (max 8).</summary>
        public readonly byte[] SharedLootTypes = new byte[8];

        // ── Derived ─────────────────────────────────────────────────

        /// <summary>Current number of squad members.</summary>
        public int Size => Members.Count;

        // ── Constructor ─────────────────────────────────────────────

        public SquadEntity(int id, int strategyCount, int targetMembers)
        {
            Id = id;
            IsActive = true;
            Members = new List<BotEntity>(6);
            Objective = new SquadObjective();
            StrategyScores = new float[strategyCount];
            TargetMembersCount = targetMembers;

            LoggingController.LogInfo(
                "[SquadEntity] Created squad " + id + " (strategySlots=" + strategyCount + ", targetMembers=" + targetMembers + ")"
            );
        }

        // ── Equality ────────────────────────────────────────────────

        public bool Equals(SquadEntity other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return Id == other.Id;
        }

        public override bool Equals(object obj) => Equals(obj as SquadEntity);

        public override int GetHashCode() => Id;

        public override string ToString() =>
            $"SquadEntity(Id={Id}, Active={IsActive}, Size={Size}, "
            + $"Leader={Leader?.Id.ToString() ?? "none"}, Target={TargetMembersCount})";
    }
}
