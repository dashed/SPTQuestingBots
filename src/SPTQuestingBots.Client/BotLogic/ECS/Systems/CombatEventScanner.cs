using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Pure-logic system that queries <see cref="CombatEventRegistry"/> for each active entity
/// and writes results to <see cref="BotEntity"/> vulture fields.
/// Called once per HiveMind tick.
/// </summary>
public static class CombatEventScanner
{
    /// <summary>
    /// Update vulture-related fields on all active entities by querying the combat event registry.
    /// </summary>
    /// <param name="entities">Dense entity list from BotRegistry.</param>
    /// <param name="currentTime">Current game time (Time.time).</param>
    /// <param name="maxEventAge">Maximum age for events to be considered (seconds).</param>
    /// <param name="detectionRange">Maximum detection range for nearby events.</param>
    /// <param name="intensityRadius">Radius for intensity counting.</param>
    /// <param name="intensityWindow">Time window for intensity counting.</param>
    /// <param name="bossAvoidanceRadius">Radius for boss zone detection.</param>
    /// <param name="bossZoneDecay">Decay time for boss zone events (seconds).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateEntities(
        List<BotEntity> entities,
        float currentTime,
        float maxEventAge,
        float detectionRange,
        float intensityRadius,
        float intensityWindow,
        float bossAvoidanceRadius,
        float bossZoneDecay
    )
    {
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            if (!entity.IsActive)
            {
                continue;
            }

            UpdateEntity(
                entity,
                currentTime,
                maxEventAge,
                detectionRange,
                intensityRadius,
                intensityWindow,
                bossAvoidanceRadius,
                bossZoneDecay
            );
        }
    }

    /// <summary>
    /// Update vulture fields for a single entity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void UpdateEntity(
        BotEntity entity,
        float currentTime,
        float maxEventAge,
        float detectionRange,
        float intensityRadius,
        float intensityWindow,
        float bossAvoidanceRadius,
        float bossZoneDecay
    )
    {
        float botX = entity.CurrentPositionX;
        float botZ = entity.CurrentPositionZ;

        // Query nearest event
        bool found = CombatEventRegistry.GetNearestEvent(botX, botZ, detectionRange, currentTime, maxEventAge, out var nearest);

        entity.HasNearbyEvent = found;
        if (found)
        {
            entity.NearbyEventX = nearest.X;
            entity.NearbyEventY = nearest.Y;
            entity.NearbyEventZ = nearest.Z;
            entity.NearbyEventTime = nearest.Time;

            // Query intensity around the event position (not the bot)
            entity.CombatIntensity = CombatEventRegistry.GetIntensity(nearest.X, nearest.Z, intensityRadius, intensityWindow, currentTime);

            float dx = botX - nearest.X;
            float dz = botZ - nearest.Z;
            float dist = (float)System.Math.Sqrt(dx * dx + dz * dz);
            LoggingController.LogDebug(
                "[CombatEventScanner] Entity "
                    + entity.Id
                    + ": nearest event at "
                    + dist.ToString("F0")
                    + "m, intensity="
                    + entity.CombatIntensity
            );
        }
        else
        {
            entity.NearbyEventX = 0f;
            entity.NearbyEventY = 0f;
            entity.NearbyEventZ = 0f;
            entity.NearbyEventTime = 0f;
            entity.CombatIntensity = 0;
        }

        // Query boss zone
        entity.IsInBossZone = CombatEventRegistry.IsInBossZone(botX, botZ, bossAvoidanceRadius, bossZoneDecay, currentTime);
        if (entity.IsInBossZone)
        {
            LoggingController.LogDebug(
                "[CombatEventScanner] Entity " + entity.Id + ": in boss zone (radius=" + bossAvoidanceRadius.ToString("F0") + ")"
            );
        }
    }
}
