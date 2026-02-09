using System;
using System.Collections.Generic;

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

        // ── Formation Heading Tracking ──────────────────────────────

        /// <summary>Previous leader X position for heading computation.</summary>
        public float PreviousLeaderX;

        /// <summary>Previous leader Z position for heading computation.</summary>
        public float PreviousLeaderZ;

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
