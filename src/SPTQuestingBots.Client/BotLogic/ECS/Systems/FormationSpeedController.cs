using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    public enum FormationSpeedDecision : byte
    {
        MatchBoss,
        Sprint,
        Walk,
        SlowApproach,
    }

    public readonly struct FormationConfig
    {
        public readonly float CatchUpDistanceSqr;
        public readonly float MatchSpeedDistanceSqr;
        public readonly float SlowApproachDistanceSqr;
        public readonly bool Enabled;

        public FormationConfig(float catchUpDistance, float matchSpeedDistance, float slowApproachDistance, bool enabled)
        {
            CatchUpDistanceSqr = catchUpDistance * catchUpDistance;
            MatchSpeedDistanceSqr = matchSpeedDistance * matchSpeedDistance;
            SlowApproachDistanceSqr = slowApproachDistance * slowApproachDistance;
            Enabled = enabled;
        }

        public static FormationConfig Default => new FormationConfig(30f, 15f, 5f, true);
    }

    public static class FormationSpeedController
    {
        public static FormationSpeedDecision ComputeSpeedDecision(
            bool bossIsSprinting,
            float distToBossSqr,
            float distToTacticalSqr,
            in FormationConfig config
        )
        {
            if (!config.Enabled)
                return FormationSpeedDecision.MatchBoss;

            if (distToBossSqr > config.CatchUpDistanceSqr)
                return FormationSpeedDecision.Sprint;

            if (distToBossSqr > config.MatchSpeedDistanceSqr)
                return FormationSpeedDecision.Walk;

            if (distToTacticalSqr < config.SlowApproachDistanceSqr)
                return FormationSpeedDecision.SlowApproach;

            return FormationSpeedDecision.MatchBoss;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ShouldSprint(FormationSpeedDecision decision, bool bossIsSprinting)
        {
            switch (decision)
            {
                case FormationSpeedDecision.Sprint:
                    return true;
                case FormationSpeedDecision.MatchBoss:
                    return bossIsSprinting;
                default:
                    return false;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SpeedMultiplier(FormationSpeedDecision decision)
        {
            return decision == FormationSpeedDecision.SlowApproach ? 0.5f : 1.0f;
        }
    }
}
