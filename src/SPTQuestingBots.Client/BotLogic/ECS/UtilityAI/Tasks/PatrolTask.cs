using System.Collections.Generic;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when a bot has no active quest objective and a patrol route is assigned.
/// Bots follow named patrol routes per map, filling idle time between objectives.
/// <para>
/// Gating: not in combat, no active objective, route assigned, not on cooldown.
/// Route assignment is lazy — done in ScoreEntity when first needed.
/// </para>
/// </summary>
public sealed class PatrolTask : QuestUtilityTask
{
    /// <summary>Maximum base score for patrol behavior.</summary>
    public const float MaxBaseScore = 0.50f;

    /// <summary>Falloff distance for proximity scoring (meters).</summary>
    private const float ProximityFalloff = 200f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.Patrol; }
    }

    public override string ActionReason
    {
        get { return "Patrol"; }
    }

    /// <summary>
    /// Cached patrol routes for the current map. Loaded once on first access.
    /// Static so all PatrolTask instances share the same route data.
    /// </summary>
    internal static PatrolRoute[] CurrentMapRoutes = System.Array.Empty<PatrolRoute>();

    /// <summary>Whether routes have been loaded for the current map session.</summary>
    internal static bool RoutesLoaded;

    public PatrolTask(float hysteresis = 0.15f)
        : base(hysteresis) { }

    /// <summary>
    /// Load patrol routes for a map. Call once per raid from BotQuestBuilder or similar.
    /// </summary>
    public static void LoadRoutesForMap(string mapId, Dictionary<string, PatrolRouteEntry[]> overrides)
    {
        CurrentMapRoutes = PatrolRouteConfig.GetRoutesForMap(mapId, overrides);
        RoutesLoaded = true;
        LoggingController.LogInfo("[PatrolTask] Loaded " + CurrentMapRoutes.Length + " patrol routes for map " + mapId);
    }

    /// <summary>Reset route state between raids.</summary>
    public static void Reset()
    {
        CurrentMapRoutes = System.Array.Empty<PatrolRoute>();
        RoutesLoaded = false;
    }

    public override void ScoreEntity(int ordinal, BotEntity entity)
    {
        float score = Score(entity, CurrentMapRoutes);
        entity.TaskScores[ordinal] =
            score
            * ScoringModifiers.CombinedModifier(entity.Aggression, entity.RaidTimeNormalized, entity.HumanPlayerProximity, BotActionTypeId);
    }

    internal static float Score(BotEntity entity, PatrolRoute[] routes)
    {
        // In combat — don't patrol
        if (entity.IsInCombat)
        {
            return 0f;
        }

        // On cooldown
        if (entity.PatrolCooldownUntil > entity.CurrentGameTime)
        {
            return 0f;
        }

        bool hasCustomRoutes = routes != null && routes.Length > 0;

        // Lazy route assignment: if no route assigned, try to pick one from custom routes
        if (entity.PatrolRouteIndex < 0 && hasCustomRoutes)
        {
            int selected = Systems.PatrolRouteSelector.SelectRoute(
                entity.CurrentPositionX,
                entity.CurrentPositionZ,
                entity.Aggression,
                entity.RaidTimeNormalized,
                routes,
                entity.Id
            );

            if (selected >= 0)
            {
                entity.PatrolRouteIndex = selected;
                entity.PatrolWaypointIndex = 0;
                LoggingController.LogDebug(
                    "[PatrolTask] Entity " + entity.Id + ": assigned route " + selected + " (" + routes[selected].Name + ")"
                );
            }
        }

        // Validate route index: reset if out of range
        if (entity.PatrolRouteIndex >= 0 && hasCustomRoutes && entity.PatrolRouteIndex >= routes.Length)
        {
            entity.PatrolRouteIndex = -1;
        }

        // Determine which route to score: custom route or native fallback
        PatrolRoute route = null;
        bool usingNativeRoute = false;

        if (entity.PatrolRouteIndex >= 0 && hasCustomRoutes && entity.PatrolRouteIndex < routes.Length)
        {
            route = routes[entity.PatrolRouteIndex];
        }
        else if (entity.NativePatrolRoute != null)
        {
            // Fallback to BSG's native patrol route
            route = entity.NativePatrolRoute;
            usingNativeRoute = true;
        }

        if (route == null || route.Waypoints == null || route.Waypoints.Length == 0)
        {
            return 0f;
        }

        int wpIndex = entity.PatrolWaypointIndex;
        if (wpIndex < 0 || wpIndex >= route.Waypoints.Length)
        {
            wpIndex = 0;
        }

        float dx = entity.CurrentPositionX - route.Waypoints[wpIndex].X;
        float dz = entity.CurrentPositionZ - route.Waypoints[wpIndex].Z;
        float dist = (float)System.Math.Sqrt(dx * dx + dz * dz);

        // 1 - e^(-dist/falloff): far away = higher urgency to start moving
        float proximityFactor = 1f - (float)System.Math.Exp(-dist / ProximityFalloff);

        // Native routes score slightly lower than custom routes to prefer custom when available
        float baseScore = usingNativeRoute ? MaxBaseScore * 0.8f : MaxBaseScore;
        float score = baseScore * (0.4f + 0.6f * proximityFactor);

        if (score > baseScore)
        {
            score = baseScore;
        }

        if (score < 0f)
        {
            score = 0f;
        }

        LoggingController.LogDebug(
            "[PatrolTask] Entity "
                + entity.Id
                + ": route="
                + route.Name
                + (usingNativeRoute ? " (native)" : "")
                + " dist="
                + dist.ToString("F0")
                + " score="
                + score.ToString("F2")
        );

        return score;
    }
}
