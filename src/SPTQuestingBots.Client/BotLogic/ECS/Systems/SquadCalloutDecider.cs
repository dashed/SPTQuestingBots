using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Pure-logic static class that decides which voice callout a boss or follower should make
/// based on squad events (objective changes, arrivals, enemy direction).
/// <para>
/// Pure C# — no Unity or EFT dependencies — fully testable.
/// </para>
/// </summary>
public static class SquadCalloutDecider
{
    /// <summary>
    /// Decide the boss's callout based on objective and arrival state.
    /// Returns Gogogo when the objective changed, HoldPosition when the boss arrived, or None.
    /// </summary>
    public static int DecideBossCallout(bool objectiveChanged, bool bossArrived)
    {
        int callout;
        if (objectiveChanged)
        {
            callout = SquadCalloutId.Gogogo;
        }
        else if (bossArrived)
        {
            callout = SquadCalloutId.HoldPosition;
        }
        else
        {
            callout = SquadCalloutId.None;
        }

        if (callout != SquadCalloutId.None)
        {
            LoggingController.LogDebug(
                "[SquadCalloutDecider] Boss callout=" + callout + " (objChanged=" + objectiveChanged + ", arrived=" + bossArrived + ")"
            );
        }

        return callout;
    }

    /// <summary>
    /// Decide a follower's response callout to the boss's callout.
    /// Alternates between Roger/Going or Roger/OnPosition based on follower ordinal.
    /// </summary>
    public static int DecideFollowerResponse(int bossCalloutId, int followerOrdinal)
    {
        bool isEven = (followerOrdinal & 1) == 0;

        int response;
        switch (bossCalloutId)
        {
            case SquadCalloutId.FollowMe:
            case SquadCalloutId.Gogogo:
                response = isEven ? SquadCalloutId.Roger : SquadCalloutId.Going;
                break;
            case SquadCalloutId.HoldPosition:
                response = isEven ? SquadCalloutId.Roger : SquadCalloutId.OnPosition;
                break;
            default:
                response = SquadCalloutId.None;
                break;
        }

        if (response != SquadCalloutId.None)
        {
            LoggingController.LogDebug(
                "[SquadCalloutDecider] Follower " + followerOrdinal + " response=" + response + " to boss callout=" + bossCalloutId
            );
        }

        return response;
    }

    /// <summary>
    /// Returns OnPosition if the follower just arrived at its tactical position, None otherwise.
    /// </summary>
    public static int DecideArrivalCallout(bool justArrived)
    {
        return justArrived ? SquadCalloutId.OnPosition : SquadCalloutId.None;
    }

    /// <summary>
    /// Decide which directional callout to use based on the dot products of the enemy direction
    /// relative to the bot's forward and right vectors.
    /// </summary>
    public static int DecideEnemyDirectionCallout(float dotForward, float dotRight)
    {
        int callout;
        if (dotForward < -0.5f)
        {
            callout = SquadCalloutId.OnSix;
        }
        else if (dotRight < -0.5f)
        {
            callout = SquadCalloutId.LeftFlank;
        }
        else if (dotRight > 0.5f)
        {
            callout = SquadCalloutId.RightFlank;
        }
        else if (dotForward > 0.5f)
        {
            callout = SquadCalloutId.InTheFront;
        }
        else
        {
            callout = SquadCalloutId.OnFirstContact;
        }

        LoggingController.LogDebug(
            "[SquadCalloutDecider] Enemy direction callout=" + callout + " (dotFwd=" + dotForward + ", dotRight=" + dotRight + ")"
        );
        return callout;
    }

    /// <summary>
    /// Returns true if the cooldown has not yet elapsed since lastTime.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsOnCooldown(float lastTime, float currentTime, float cooldown)
    {
        return currentTime - lastTime < cooldown;
    }
}
