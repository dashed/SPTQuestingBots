using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    public static class SquadCalloutDecider
    {
        public static int DecideBossCallout(bool objectiveChanged, bool bossArrived)
        {
            if (objectiveChanged)
                return SquadCalloutId.Gogogo;
            if (bossArrived)
                return SquadCalloutId.HoldPosition;
            return SquadCalloutId.None;
        }

        public static int DecideFollowerResponse(int bossCalloutId, int followerOrdinal)
        {
            bool isEven = (followerOrdinal & 1) == 0;

            switch (bossCalloutId)
            {
                case SquadCalloutId.FollowMe:
                case SquadCalloutId.Gogogo:
                    return isEven ? SquadCalloutId.Roger : SquadCalloutId.Going;
                case SquadCalloutId.HoldPosition:
                    return isEven ? SquadCalloutId.Roger : SquadCalloutId.OnPosition;
                default:
                    return SquadCalloutId.None;
            }
        }

        public static int DecideArrivalCallout(bool justArrived)
        {
            return justArrived ? SquadCalloutId.OnPosition : SquadCalloutId.None;
        }

        public static int DecideEnemyDirectionCallout(float dotForward, float dotRight)
        {
            if (dotForward < -0.5f)
                return SquadCalloutId.OnSix;
            if (dotRight < -0.5f)
                return SquadCalloutId.LeftFlank;
            if (dotRight > 0.5f)
                return SquadCalloutId.RightFlank;
            if (dotForward > 0.5f)
                return SquadCalloutId.InTheFront;
            return SquadCalloutId.OnFirstContact;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsOnCooldown(float lastTime, float currentTime, float cooldown)
        {
            return currentTime - lastTime < cooldown;
        }
    }
}
