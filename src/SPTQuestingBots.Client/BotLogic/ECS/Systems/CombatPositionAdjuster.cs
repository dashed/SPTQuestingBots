using System;
using System.Runtime.CompilerServices;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Pure-logic combat-aware tactical positioning. When a squad detects enemies,
/// repositions followers relative to the threat direction instead of the approach vector.
/// Guard positions cluster toward threat, Flankers go perpendicular to threat,
/// Overwatch positions behind the defense line, Escort reassigned to Flanker.
/// <para>
/// Pure C# — no Unity or EFT dependencies — fully testable.
/// </para>
/// </summary>
public static class CombatPositionAdjuster
{
    private const float PI = (float)Math.PI;

    /// <summary>
    /// Reassign roles for combat: Escort becomes Flanker (more aggressive flanking),
    /// all other roles unchanged.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void ReassignRolesForCombat(SquadRole[] currentRoles, int count, SquadRole[] outRoles)
    {
        int reassigned = 0;
        for (int i = 0; i < count; i++)
        {
            if (currentRoles[i] == SquadRole.Escort)
            {
                outRoles[i] = SquadRole.Flanker;
                reassigned++;
            }
            else
            {
                outRoles[i] = currentRoles[i];
            }
        }

        if (reassigned > 0)
        {
            LoggingController.LogDebug(
                "[CombatPositionAdjuster] Reassigned " + reassigned + " Escort->Flanker for combat (total=" + count + ")"
            );
        }
    }

    /// <summary>
    /// Compute threat-oriented tactical positions.
    /// threatDirX/Z is the normalized direction FROM the objective TOWARD the threat.
    /// </summary>
    public static void ComputeCombatPositions(
        float objX,
        float objY,
        float objZ,
        float threatDirX,
        float threatDirZ,
        SquadRole[] roles,
        int count,
        SquadStrategyConfig config,
        float[] outPositions
    )
    {
        LoggingController.LogDebug(
            "[CombatPositionAdjuster] Computing combat positions for "
                + count
                + " members (threat=("
                + threatDirX
                + ", "
                + threatDirZ
                + "))"
        );

        // Degenerate threat direction: place everyone at the objective
        float lenSq = threatDirX * threatDirX + threatDirZ * threatDirZ;
        if (lenSq < 0.0001f)
        {
            LoggingController.LogWarning("[CombatPositionAdjuster] Degenerate threat direction, placing all at objective");
            for (int i = 0; i < count; i++)
            {
                int off = i * 3;
                outPositions[off] = objX;
                outPositions[off + 1] = objY;
                outPositions[off + 2] = objZ;
            }
            return;
        }

        for (int i = 0; i < count; i++)
        {
            int offset = i * 3;
            switch (roles[i])
            {
                case SquadRole.Guard:
                    ComputeGuardPosition(objX, objY, objZ, config.GuardDistance, i, count, threatDirX, threatDirZ, outPositions, offset);
                    break;
                case SquadRole.Flanker:
                    ComputeFlankerPosition(objX, objY, objZ, config.FlankDistance, i, threatDirX, threatDirZ, outPositions, offset);
                    break;
                case SquadRole.Overwatch:
                    ComputeOverwatchPosition(objX, objY, objZ, config.OverwatchDistance, threatDirX, threatDirZ, outPositions, offset);
                    break;
                default:
                    outPositions[offset] = objX;
                    outPositions[offset + 1] = objY;
                    outPositions[offset + 2] = objZ;
                    break;
            }
        }
    }

    /// <summary>
    /// Guard: threat-biased arc. Distributes guards within a 180 degree arc
    /// centered on the threat bearing.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeGuardPosition(
        float objX,
        float objY,
        float objZ,
        float radius,
        int index,
        int totalCount,
        float threatDirX,
        float threatDirZ,
        float[] outPositions,
        int offset
    )
    {
        float baseAngleDeg = (float)Math.Atan2(threatDirZ, threatDirX) * (180f / PI);
        float spread = 180f / Math.Max(totalCount, 1);
        float angleDeg = baseAngleDeg + (index - (totalCount - 1) / 2f) * spread;
        float rad = angleDeg * (PI / 180f);

        outPositions[offset] = objX + radius * (float)Math.Cos(rad);
        outPositions[offset + 1] = objY;
        outPositions[offset + 2] = objZ + radius * (float)Math.Sin(rad);
    }

    /// <summary>
    /// Flanker: perpendicular to threat direction, alternating sides.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeFlankerPosition(
        float objX,
        float objY,
        float objZ,
        float distance,
        int index,
        float threatDirX,
        float threatDirZ,
        float[] outPositions,
        int offset
    )
    {
        float perpX = -threatDirZ;
        float perpZ = threatDirX;
        float side = (index % 2 == 0) ? 1f : -1f;

        outPositions[offset] = objX + perpX * distance * side;
        outPositions[offset + 1] = objY;
        outPositions[offset + 2] = objZ + perpZ * distance * side;
    }

    /// <summary>
    /// Overwatch: behind the defense line (opposite the threat direction).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ComputeOverwatchPosition(
        float objX,
        float objY,
        float objZ,
        float distance,
        float threatDirX,
        float threatDirZ,
        float[] outPositions,
        int offset
    )
    {
        outPositions[offset] = objX - threatDirX * distance;
        outPositions[offset + 1] = objY;
        outPositions[offset + 2] = objZ - threatDirZ * distance;
    }
}
