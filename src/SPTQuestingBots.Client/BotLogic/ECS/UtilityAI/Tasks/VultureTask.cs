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
        entity.TaskScores[ordinal] =
            score * ScoringModifiers.CombinedModifier(entity.Aggression, entity.RaidTimeNormalized, BotActionTypeId);
    }

    internal static float Score(BotEntity entity, int courageThreshold, float detectionRange)
    {
        // No nearby combat event → no score
        if (!entity.HasNearbyEvent)
        {
            LoggingController.LogDebug("[VultureTask] Entity " + entity.Id + ": no nearby event, score=0");
            return 0f;
        }

        // In combat → don't vulture (already fighting)
        if (entity.IsInCombat)
        {
            LoggingController.LogDebug("[VultureTask] Entity " + entity.Id + ": in combat, score=0");
            return 0f;
        }

        // In a boss zone → too dangerous
        if (entity.IsInBossZone)
        {
            LoggingController.LogDebug("[VultureTask] Entity " + entity.Id + ": in boss zone, score=0");
            return 0f;
        }

        // On cooldown → recently rejected
        if (entity.VultureCooldownUntil > 0f)
        {
            LoggingController.LogDebug(
                "[VultureTask] Entity " + entity.Id + ": on cooldown until " + entity.VultureCooldownUntil.ToString("F1") + ", score=0"
            );
            return 0f;
        }

        // Already vulturing → maintain current phase with high score
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
        float intensityRatio = (float)entity.CombatIntensity / courageThreshold;
        if (intensityRatio > 2f)
        {
            intensityRatio = 2f;
        }

        float intensityScore = (intensityRatio - 1f) * IntensityWeight;

        // Proximity component: closer events score higher
        float dx = entity.CurrentPositionX - entity.NearbyEventX;
        float dz = entity.CurrentPositionZ - entity.NearbyEventZ;
        float distSqr = dx * dx + dz * dz;
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
