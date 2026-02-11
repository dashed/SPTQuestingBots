using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Speed decision for followers in formation movement.
/// </summary>
public enum FormationSpeedDecision : byte
{
    /// <summary>Match the boss's current movement speed.</summary>
    MatchBoss,

    /// <summary>Sprint to catch up when too far from boss.</summary>
    Sprint,

    /// <summary>Walk at normal pace within comfortable distance.</summary>
    Walk,

    /// <summary>Slow approach when close to tactical position.</summary>
    SlowApproach,
}

/// <summary>
/// Pre-computed squared distance thresholds for formation speed decisions.
/// Avoids per-frame sqrt calls.
/// </summary>
public readonly struct FormationConfig
{
    /// <summary>Squared distance beyond which followers sprint to catch up.</summary>
    public readonly float CatchUpDistanceSqr;

    /// <summary>Squared distance beyond which followers walk at normal pace.</summary>
    public readonly float MatchSpeedDistanceSqr;

    /// <summary>Squared distance to tactical position within which followers slow down.</summary>
    public readonly float SlowApproachDistanceSqr;

    /// <summary>Whether formation speed control is enabled.</summary>
    public readonly bool Enabled;

    public FormationConfig(float catchUpDistance, float matchSpeedDistance, float slowApproachDistance, bool enabled)
    {
        CatchUpDistanceSqr = catchUpDistance * catchUpDistance;
        MatchSpeedDistanceSqr = matchSpeedDistance * matchSpeedDistance;
        SlowApproachDistanceSqr = slowApproachDistance * slowApproachDistance;
        Enabled = enabled;
    }

    /// <summary>Default configuration: catch-up at 30m, match at 15m, slow at 5m.</summary>
    public static FormationConfig Default
    {
        get { return new FormationConfig(30f, 15f, 5f, true); }
    }
}

/// <summary>
/// Pure-logic static class for computing follower speed decisions based on
/// distance to boss and distance to tactical position.
/// <para>Pure C# — no Unity or EFT dependencies — fully testable.</para>
/// </summary>
public static class FormationSpeedController
{
    /// <summary>
    /// Compute the speed decision for a follower based on distances.
    /// </summary>
    public static FormationSpeedDecision ComputeSpeedDecision(
        bool bossIsSprinting,
        float distToBossSqr,
        float distToTacticalSqr,
        in FormationConfig config
    )
    {
        if (!config.Enabled)
        {
            return FormationSpeedDecision.MatchBoss;
        }

        FormationSpeedDecision decision;
        if (distToBossSqr > config.CatchUpDistanceSqr)
        {
            decision = FormationSpeedDecision.Sprint;
        }
        else if (distToBossSqr > config.MatchSpeedDistanceSqr)
        {
            decision = FormationSpeedDecision.Walk;
        }
        else if (distToTacticalSqr < config.SlowApproachDistanceSqr)
        {
            decision = FormationSpeedDecision.SlowApproach;
        }
        else
        {
            decision = FormationSpeedDecision.MatchBoss;
        }

        LoggingController.LogDebug(
            "[FormationSpeedController] Decision=" + decision + " (bossDist2=" + distToBossSqr + ", tacDist2=" + distToTacticalSqr + ")"
        );
        return decision;
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
