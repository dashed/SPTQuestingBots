using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when the bot detects nearby combat events worth investigating.
/// Bots hear gunfire/explosions and move to ambush weakened survivors.
/// Ported from Vulture mod's activation logic.
/// <para>
/// Gating: not in combat, has nearby event, intensity exceeds courage threshold,
/// not in boss zone, not on cooldown.
/// </para>
/// </summary>
public sealed class VultureTask : QuestUtilityTask
{
    /// <summary>Maximum base score for vulture behavior.</summary>
    public const float MaxBaseScore = 0.60f;

    /// <summary>Score contribution from combat intensity relative to courage threshold.</summary>
    private const float IntensityWeight = 0.30f;

    /// <summary>Score contribution from event proximity (closer = higher).</summary>
    private const float ProximityWeight = 0.30f;

    /// <summary>Default courage threshold if not configured.</summary>
    public const int DefaultCourageThreshold = 15;

    /// <summary>Default detection range for proximity scoring.</summary>
    public const float DefaultDetectionRange = 150f;

    /// <summary>Default cooldown on reject (seconds).</summary>
    public const float DefaultCooldownOnReject = 180f;

    /// <summary>
    /// Score multiplier when BSG Mind says HOW_WORK_OVER_DEAD_BODY == 0 (ignore dead bodies).
    /// Heavily penalizes vulture scoring for bots that don't interact with corpses.
    /// </summary>
    public const float DeadBodyIgnorePenalty = 0.3f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.Vulture; }
    }

    public override string ActionReason
    {
        get { return "Vulture"; }
    }

    /// <summary>Courage threshold — minimum combat intensity to trigger vulturing.</summary>
    public int CourageThreshold { get; set; } = DefaultCourageThreshold;

    /// <summary>Detection range for proximity scoring (squared internally).</summary>
    public float DetectionRange { get; set; } = DefaultDetectionRange;

    public VultureTask(float hysteresis = 0.20f)
        : base(hysteresis) { }

    public override void ScoreEntity(int ordinal, BotEntity entity)
    {
        float score = Score(entity, CourageThreshold, DetectionRange);
        float coverInfluence = ScoringModifiers.ComputeCoverInfluence(entity.IsInCover, entity.HasEnemyInfo);
        entity.TaskScores[ordinal] =
            score
            * ScoringModifiers.CombinedModifier(
                entity.Aggression,
                entity.RaidTimeNormalized,
                entity.HumanPlayerProximity,
                coverInfluence,
                entity.IsInDogFight,
                BotActionTypeId
            );
    }

    internal static float Score(BotEntity entity, int courageThreshold, float detectionRange)
    {
        // In combat → don't vulture (already fighting). Cancels even active phases.
        if (entity.IsInCombat)
        {
            LoggingController.LogDebug("[VultureTask] Entity " + entity.Id + ": in combat, score=0");
            return 0f;
        }

        // Already vulturing → maintain current phase with high score.
        // Must check BEFORE HasNearbyEvent: the nearby event may expire while
        // the bot is mid-approach, but the committed phase should persist.
        if (entity.VulturePhase != Systems.VulturePhase.None && entity.VulturePhase != Systems.VulturePhase.Complete)
        {
            LoggingController.LogDebug(
                "[VultureTask] Entity "
                    + entity.Id
                    + ": active phase "
                    + entity.VulturePhase
                    + ", maintaining score="
                    + MaxBaseScore.ToString("F2")
            );
            return MaxBaseScore;
        }

        // No nearby combat event → no score (only blocks NEW activation)
        if (!entity.HasNearbyEvent)
        {
            LoggingController.LogDebug("[VultureTask] Entity " + entity.Id + ": no nearby event, score=0");
            return 0f;
        }

        // In a boss zone → too dangerous (only blocks NEW activation)
        if (entity.IsInBossZone)
        {
            LoggingController.LogDebug("[VultureTask] Entity " + entity.Id + ": in boss zone, score=0");
            return 0f;
        }

        // On cooldown → recently rejected (only blocks NEW activation)
        if (entity.VultureCooldownUntil > entity.CurrentGameTime)
        {
            LoggingController.LogDebug(
                "[VultureTask] Entity " + entity.Id + ": on cooldown until " + entity.VultureCooldownUntil.ToString("F1") + ", score=0"
            );
            return 0f;
        }

        // Intensity must meet courage threshold
        if (entity.CombatIntensity < courageThreshold)
        {
            LoggingController.LogDebug(
                "[VultureTask] Entity "
                    + entity.Id
                    + ": intensity "
                    + entity.CombatIntensity
                    + " below courage threshold "
                    + courageThreshold
                    + ", score=0"
            );
            return 0f;
        }

        // Intensity component: how much over threshold, capped at 2×
        int safeCourageThreshold = System.Math.Max(1, courageThreshold);
        float intensityRatio = (float)entity.CombatIntensity / safeCourageThreshold;
        if (intensityRatio > 2f)
        {
            intensityRatio = 2f;
        }

        float intensityScore = (intensityRatio - 1f) * IntensityWeight;

        // Proximity component: closer events score higher
        float dx = entity.CurrentPositionX - entity.NearbyEventX;
        float dz = entity.CurrentPositionZ - entity.NearbyEventZ;
        float distSqr = dx * dx + dz * dz;

        // Don't vulture toward events beyond the bot's effective vision range
        float visibleDistSqr = entity.VisibleDist * entity.VisibleDist;
        if (visibleDistSqr > 0f && distSqr > visibleDistSqr)
        {
            LoggingController.LogDebug(
                "[VultureTask] Entity " + entity.Id + ": event beyond VisibleDist (" + entity.VisibleDist.ToString("F0") + "m), score=0"
            );
            return 0f;
        }

        float rangeSqr = detectionRange * detectionRange;
        float proximityScore;
        if (distSqr >= rangeSqr)
        {
            proximityScore = 0f;
        }
        else
        {
            proximityScore = (1f - distSqr / rangeSqr) * ProximityWeight;
        }

        float score = intensityScore + proximityScore;

        // Penalty when BSG Mind says bot ignores dead bodies
        if (entity.MindHowWorkOverDeadBody == 0)
        {
            score *= DeadBodyIgnorePenalty;
        }

        // Clamp
        if (score < 0f)
        {
            return 0f;
        }

        if (score > MaxBaseScore)
        {
            return MaxBaseScore;
        }

        LoggingController.LogDebug(
            "[VultureTask] Entity "
                + entity.Id
                + ": intensity="
                + entity.CombatIntensity
                + " ratio="
                + intensityRatio.ToString("F2")
                + " proximityScore="
                + proximityScore.ToString("F2")
                + " finalScore="
                + score.ToString("F2")
        );
        return score;
    }
}
