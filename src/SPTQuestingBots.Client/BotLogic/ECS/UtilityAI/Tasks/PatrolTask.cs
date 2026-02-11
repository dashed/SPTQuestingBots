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
            score * ScoringModifiers.CombinedModifier(entity.Aggression, entity.RaidTimeNormalized, BotActionTypeId);
    }

    internal static float Score(BotEntity entity, PatrolRoute[] routes)
    {
        // In combat — don't patrol
        if (entity.IsInCombat)
        {
            return 0f;
        }

        // Has active quest objective — quest takes priority
        if (entity.HasActiveObjective)
        {
            return 0f;
        }

        // No routes available for this map
        if (routes == null || routes.Length == 0)
        {
            return 0f;
        }

        // Lazy route assignment: if no route assigned, try to pick one
        if (entity.PatrolRouteIndex < 0)
        {
            int selected = Systems.PatrolRouteSelector.SelectRoute(
                entity.CurrentPositionX,
                entity.CurrentPositionZ,
                entity.Aggression,
                entity.RaidTimeNormalized,
                routes,
                entity.Id
            );

            if (selected < 0)
            {
                return 0f;
            }

            entity.PatrolRouteIndex = selected;
            entity.PatrolWaypointIndex = 0;
            LoggingController.LogDebug(
                "[PatrolTask] Entity " + entity.Id + ": assigned route " + selected + " (" + routes[selected].Name + ")"
            );
        }

        // Validate route index
        if (entity.PatrolRouteIndex >= routes.Length)
        {
            entity.PatrolRouteIndex = -1;
            return 0f;
        }

        // On cooldown
        if (entity.PatrolCooldownUntil > entity.CurrentGameTime)
        {
            return 0f;
        }

        // Proximity factor: closer to current waypoint = higher urgency
        var route = routes[entity.PatrolRouteIndex];
        if (route.Waypoints == null || route.Waypoints.Length == 0)
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

        float score = MaxBaseScore * (0.4f + 0.6f * proximityFactor);

        if (score > MaxBaseScore)
        {
            score = MaxBaseScore;
        }

        if (score < 0f)
        {
            score = 0f;
        }

        LoggingController.LogDebug(
            "[PatrolTask] Entity "
                + entity.Id
                + ": route="
                + routes[entity.PatrolRouteIndex].Name
                + " dist="
                + dist.ToString("F0")
                + " score="
                + score.ToString("F2")
        );

        return score;
    }
}
