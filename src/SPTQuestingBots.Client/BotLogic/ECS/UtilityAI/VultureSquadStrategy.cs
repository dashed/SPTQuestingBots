using System;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI
{
    /// <summary>
    /// Squad strategy for vulture behavior: when the leader enters vulture mode,
    /// followers fan out around the combat event position for a coordinated ambush.
    /// Scores high only when the leader has an active vulture phase.
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public class VultureSquadStrategy : SquadStrategy
    {
        public const float BaseScore = 0.75f;

        private readonly float[] _positionBuffer = new float[SquadObjective.MaxMembers * 3];

        public VultureSquadStrategy(float hysteresis = 0.20f)
            : base(hysteresis) { }

        public override void ScoreSquad(int ordinal, SquadEntity squad)
        {
            squad.StrategyScores[ordinal] = Score(squad);
        }

        internal static float Score(SquadEntity squad)
        {
            // Require a leader in active vulture mode
            var leader = squad.Leader;
            if (leader == null)
                return 0f;

            if (leader.VulturePhase == VulturePhase.None || leader.VulturePhase == VulturePhase.Complete)
                return 0f;

            if (!leader.HasNearbyEvent)
                return 0f;

            return BaseScore;
        }

        public override void Update()
        {
            for (int i = 0; i < ActiveSquads.Count; i++)
            {
                UpdateSquad(ActiveSquads[i]);
            }
        }

        private void UpdateSquad(SquadEntity squad)
        {
            var leader = squad.Leader;
            if (leader == null || !leader.HasNearbyEvent)
                return;

            float targetX = leader.NearbyEventX;
            float targetZ = leader.NearbyEventZ;
            float targetY = leader.NearbyEventY;

            int followerCount = squad.Size - 1;
            if (followerCount <= 0)
                return;

            // Compute direction from leader to target for fan-out
            float dirX = targetX - leader.CurrentPositionX;
            float dirZ = targetZ - leader.CurrentPositionZ;
            float dirLen = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            if (dirLen < 0.01f)
            {
                dirX = 0f;
                dirZ = 1f;
            }
            else
            {
                dirX /= dirLen;
                dirZ /= dirLen;
            }

            // Perpendicular for spread
            float perpX = -dirZ;
            float perpZ = dirX;

            // Assign positions: fan out perpendicular to approach direction
            // at ambush distance behind the target
            float ambushOffset = 30f;
            float spread = 15f;
            int idx = 0;

            for (int m = 0; m < squad.Size; m++)
            {
                var member = squad.Members[m];
                if (member == null || member == leader)
                    continue;

                float lateralOffset;
                if (followerCount == 1)
                {
                    lateralOffset = spread;
                }
                else
                {
                    float t = followerCount > 1 ? (float)idx / (followerCount - 1) - 0.5f : 0f;
                    lateralOffset = t * 2f * spread;
                }

                float posX = targetX - dirX * ambushOffset + perpX * lateralOffset;
                float posZ = targetZ - dirZ * ambushOffset + perpZ * lateralOffset;

                member.TacticalPositionX = posX;
                member.TacticalPositionY = targetY;
                member.TacticalPositionZ = posZ;
                member.HasTacticalPosition = true;

                // Assign roles based on position
                if (idx == 0)
                    member.SquadRole = SquadRole.Flanker;
                else if (idx == followerCount - 1)
                    member.SquadRole = SquadRole.Flanker;
                else
                    member.SquadRole = SquadRole.Guard;

                idx++;
            }
        }
    }
}
