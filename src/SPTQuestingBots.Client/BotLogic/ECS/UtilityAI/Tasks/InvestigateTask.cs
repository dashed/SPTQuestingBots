using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Scores high when the bot detects nearby combat events worth checking out.
/// Lighter-weight than Vulture: lower intensity threshold, lower score,
/// simple approach-then-look-around behavior.
/// Gets a small bonus when the game's BotSearchData has an active player-position
/// search target, indicating the game also thinks the bot should investigate.
/// <para>
/// Gating: not in combat, has nearby event, intensity exceeds threshold,
/// not already vulturing.
/// </para>
/// </summary>
public sealed class InvestigateTask : QuestUtilityTask
{
    /// <summary>Maximum base score for investigate behavior.</summary>
    public const float MaxBaseScore = 0.40f;

    /// <summary>Score contribution from combat intensity relative to threshold.</summary>
    private const float IntensityWeight = 0.20f;

    /// <summary>Score contribution from event proximity (closer = higher).</summary>
    private const float ProximityWeight = 0.20f;

    /// <summary>
    /// Score bonus when the game's SearchData has a player-position search target.
    /// This reinforces investigation when the game's AI also wants the bot to search.
    /// </summary>
    public const float GameSearchBonus = 0.05f;

    /// <summary>
    /// Score bonus when a PlaceForCheck exists from the hearing sensor.
    /// Danger/suspicious types get a higher bonus than simple.
    /// </summary>
    public const float PlaceForCheckBonus = 0.06f;

    /// <summary>Extra bonus for danger/suspicious PlaceForCheck types.</summary>
    public const float PlaceForCheckDangerExtra = 0.04f;

    /// <summary>
    /// Minimum ammo ratio multiplier. Running into a firefight with no ammo is unwise.
    /// </summary>
    public const float MinAmmoMultiplier = 0.2f;

    /// <summary>
    /// Score bonus when enemy info exists and the enemy was recently seen.
    /// Provides gradient boost: full bonus at TimeSinceEnemySeen=0, fades to 0
    /// at MindTimeToForgetEnemySec.
    /// </summary>
    public const float EnemyInfoBonus = 0.06f;

    /// <summary>Default intensity threshold if not configured.</summary>
    public const int DefaultIntensityThreshold = 5;

    /// <summary>Default detection range for proximity scoring.</summary>
    public const float DefaultDetectionRange = 120f;

    public override int BotActionTypeId
    {
        get { return UtilityAI.BotActionTypeId.Investigate; }
    }

    public override string ActionReason
    {
        get { return "Investigate"; }
    }

    /// <summary>Minimum combat intensity to trigger investigation.</summary>
    public int IntensityThreshold { get; set; } = DefaultIntensityThreshold;

    /// <summary>Detection range for proximity scoring (squared internally).</summary>
    public float DetectionRange { get; set; } = DefaultDetectionRange;

    public InvestigateTask(float hysteresis = 0.15f)
        : base(hysteresis) { }

    public override void ScoreEntity(int ordinal, BotEntity entity)
    {
        float score = Score(entity, IntensityThreshold, DetectionRange);
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

    internal static float Score(BotEntity entity, int intensityThreshold, float detectionRange)
    {
        // No nearby combat event
        if (!entity.HasNearbyEvent)
        {
            return 0f;
        }

        // In combat — already fighting
        if (entity.IsInCombat)
        {
            LoggingController.LogDebug("[InvestigateTask] Entity " + entity.Id + ": in combat, score=0");
            return 0f;
        }

        // Already vulturing — vulture takes priority
        if (entity.VulturePhase != Systems.VulturePhase.None && entity.VulturePhase != Systems.VulturePhase.Complete)
        {
            LoggingController.LogDebug("[InvestigateTask] Entity " + entity.Id + ": vulturing, score=0");
            return 0f;
        }

        // Already investigating — maintain score
        if (entity.IsInvestigating)
        {
            LoggingController.LogDebug(
                "[InvestigateTask] Entity " + entity.Id + ": already investigating, maintaining score=" + MaxBaseScore.ToString("F2")
            );
            return MaxBaseScore;
        }

        // Intensity must meet threshold
        if (entity.CombatIntensity < intensityThreshold)
        {
            LoggingController.LogDebug(
                "[InvestigateTask] Entity "
                    + entity.Id
                    + ": intensity "
                    + entity.CombatIntensity
                    + " below threshold "
                    + intensityThreshold
                    + ", score=0"
            );
            return 0f;
        }

        // Intensity component: how much over threshold, capped at 2x
        int safeIntensityThreshold = System.Math.Max(1, intensityThreshold);
        float intensityRatio = (float)entity.CombatIntensity / safeIntensityThreshold;
        if (intensityRatio > 2f)
        {
            intensityRatio = 2f;
        }

        float intensityScore = (intensityRatio - 1f) * IntensityWeight;

        // Proximity component: closer events score higher
        float dx = entity.CurrentPositionX - entity.NearbyEventX;
        float dz = entity.CurrentPositionZ - entity.NearbyEventZ;
        float distSqr = dx * dx + dz * dz;

        // Don't investigate events beyond the bot's effective vision range
        float visibleDistSqr = entity.VisibleDist * entity.VisibleDist;
        if (visibleDistSqr > 0f && distSqr > visibleDistSqr)
        {
            LoggingController.LogDebug(
                "[InvestigateTask] Entity " + entity.Id + ": event beyond VisibleDist (" + entity.VisibleDist.ToString("F0") + "m), score=0"
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

        // Bonus when game's SearchData has an active player-position search target
        if (entity.HasGameSearchTarget && entity.GameSearchTargetType == 0) // 0 = playerPosition
        {
            score += GameSearchBonus;
        }

        // Bonus when PlaceForCheck exists (hearing sensor detected sound)
        if (entity.HasPlaceForCheck)
        {
            score += PlaceForCheckBonus;
            // Danger and suspicious types are more actionable
            if (entity.PlaceForCheckTypeId >= 1) // danger=1, suspicious=2
            {
                score += PlaceForCheckDangerExtra;
            }
        }

        // Gradient bonus from enemy info — recently seen enemies boost investigation
        if (entity.HasEnemyInfo && !entity.IsEnemyVisible && entity.TimeSinceEnemySeen < entity.MindTimeToForgetEnemySec)
        {
            float forgetTime = entity.MindTimeToForgetEnemySec > 0f ? entity.MindTimeToForgetEnemySec : 60f;
            float freshness = 1f - entity.TimeSinceEnemySeen / forgetTime;
            if (freshness > 0f)
            {
                score += EnemyInfoBonus * freshness;
            }
        }

        // Ammo penalty: investigating combat with low ammo is risky
        float ammoMultiplier = MinAmmoMultiplier + (1f - MinAmmoMultiplier) * entity.AmmoRatio;
        score *= ammoMultiplier;

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
            "[InvestigateTask] Entity "
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
