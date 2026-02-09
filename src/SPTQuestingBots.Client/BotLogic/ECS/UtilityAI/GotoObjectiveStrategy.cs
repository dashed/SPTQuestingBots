using System;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI
{
    /// <summary>
    /// Primary squad strategy: move the squad toward the leader's current quest objective.
    /// Assigns tactical roles and positions to followers based on quest action type,
    /// tracks arrivals, and adjusts hold duration.
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public class GotoObjectiveStrategy : SquadStrategy
    {
        private readonly SquadStrategyConfig _config;
        private readonly System.Random _rng;

        // Reusable buffers (max 6 followers)
        private readonly SquadRole[] _roleBuffer = new SquadRole[SquadObjective.MaxMembers];
        private readonly float[] _positionBuffer = new float[SquadObjective.MaxMembers * 3];

        public GotoObjectiveStrategy(SquadStrategyConfig config, float hysteresis = 0.25f)
            : base(hysteresis)
        {
            _config = config;
            _rng = new System.Random();
        }

        /// <summary>
        /// Constructor with seeded RNG for deterministic testing.
        /// </summary>
        public GotoObjectiveStrategy(SquadStrategyConfig config, int seed, float hysteresis = 0.25f)
            : base(hysteresis)
        {
            _config = config;
            _rng = new System.Random(seed);
        }

        /// <summary>
        /// Score 0.5 for squads with an active leader who has an objective; 0 otherwise.
        /// </summary>
        public override void ScoreSquad(int ordinal, SquadEntity squad)
        {
            if (squad.Leader != null && squad.Leader.IsActive && squad.Leader.HasActiveObjective)
                squad.StrategyScores[ordinal] = 0.5f;
            else
                squad.StrategyScores[ordinal] = 0f;
        }

        public override void Update()
        {
            var squads = ActiveSquads;
            for (int i = 0; i < squads.Count; i++)
            {
                UpdateSquad(squads[i]);
            }
        }

        private void UpdateSquad(SquadEntity squad)
        {
            if (squad.Leader == null || !squad.Leader.IsActive)
                return;

            var obj = squad.Objective;

            // If no objective or leader's objective changed, assign new objective
            if (!obj.HasObjective || HasLeaderObjectiveChanged(squad))
            {
                AssignNewObjective(squad);
                return;
            }

            // Check member arrivals
            if (obj.State == ObjectiveState.Active)
            {
                CheckArrivals(squad);
            }
        }

        private bool HasLeaderObjectiveChanged(SquadEntity squad)
        {
            return squad.Leader.HasActiveObjective && squad.Leader.LastSeenObjectiveVersion != squad.Objective.Version;
        }

        internal void AssignNewObjective(SquadEntity squad)
        {
            var leader = squad.Leader;
            if (!leader.HasActiveObjective)
                return;

            var obj = squad.Objective;

            // Approach vector from leader position to objective
            float approachX = leader.CurrentPositionX;
            float approachZ = leader.CurrentPositionZ;

            int followerCount = 0;
            for (int i = 0; i < squad.Members.Count; i++)
            {
                if (squad.Members[i] != leader && squad.Members[i].IsActive)
                    followerCount++;
            }

            if (followerCount == 0)
                return;

            int clampedCount = Math.Min(followerCount, SquadObjective.MaxMembers);

            // Assign roles based on leader's quest type
            if (_config.UseQuestTypeRoles)
                TacticalPositionCalculator.AssignRoles(leader.CurrentQuestAction, clampedCount, _roleBuffer);
            else
                TacticalPositionCalculator.AssignRoles(0, clampedCount, _roleBuffer);

            // Compute positions
            TacticalPositionCalculator.ComputePositions(
                obj.ObjectiveX,
                obj.ObjectiveY,
                obj.ObjectiveZ,
                approachX,
                approachZ,
                _roleBuffer,
                clampedCount,
                _positionBuffer,
                _config
            );

            // Distribute to followers
            int posIdx = 0;
            for (int i = 0; i < squad.Members.Count; i++)
            {
                var member = squad.Members[i];
                if (member == leader || !member.IsActive)
                    continue;
                if (posIdx >= clampedCount)
                    break;

                member.SquadRole = _roleBuffer[posIdx];
                member.TacticalPositionX = _positionBuffer[posIdx * 3];
                member.TacticalPositionY = _positionBuffer[posIdx * 3 + 1];
                member.TacticalPositionZ = _positionBuffer[posIdx * 3 + 2];
                member.HasTacticalPosition = true;
                member.LastSeenObjectiveVersion = obj.Version;

                obj.SetTacticalPosition(
                    posIdx,
                    _positionBuffer[posIdx * 3],
                    _positionBuffer[posIdx * 3 + 1],
                    _positionBuffer[posIdx * 3 + 2],
                    _roleBuffer[posIdx]
                );

                posIdx++;
            }

            obj.MemberCount = posIdx;
            obj.State = ObjectiveState.Active;
            leader.LastSeenObjectiveVersion = obj.Version;

            // Set duration with Gaussian sampling (60-180 seconds)
            obj.Duration = SampleGaussian(60f, 180f);
            obj.DurationAdjusted = false;
        }

        internal void CheckArrivals(SquadEntity squad)
        {
            var obj = squad.Objective;
            float arrivalSqr = _config.ArrivalRadius * _config.ArrivalRadius;

            int arrived = 0;
            int totalFollowers = 0;

            for (int i = 0; i < squad.Members.Count; i++)
            {
                var member = squad.Members[i];
                if (member == squad.Leader || !member.IsActive || !member.HasTacticalPosition)
                    continue;

                totalFollowers++;
                float dx = member.CurrentPositionX - member.TacticalPositionX;
                float dy = member.CurrentPositionY - member.TacticalPositionY;
                float dz = member.CurrentPositionZ - member.TacticalPositionZ;
                float sqrDist = dx * dx + dy * dy + dz * dz;

                if (sqrDist <= arrivalSqr)
                    arrived++;
            }

            // When first member arrives, switch to Wait
            if (arrived > 0 && obj.State == ObjectiveState.Active)
            {
                obj.State = ObjectiveState.Wait;
            }

            // When all arrived and not yet adjusted, cut duration
            if (arrived >= totalFollowers && totalFollowers > 0 && !obj.DurationAdjusted)
            {
                float factor = SampleGaussian(0.2f, 0.5f);
                obj.Duration *= factor;
                obj.DurationAdjusted = true;
            }
        }

        /// <summary>
        /// Box-Muller Gaussian sampling clamped to [min, max].
        /// </summary>
        internal float SampleGaussian(float min, float max)
        {
            float mean = (min + max) / 2f;
            float stddev = (max - min) / 4f; // ~95% within range

            float u1 = 1f - (float)_rng.NextDouble();
            float u2 = (float)_rng.NextDouble();
            float normal = (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
            float value = mean + stddev * normal;

            return Math.Max(min, Math.Min(max, value));
        }
    }
}
