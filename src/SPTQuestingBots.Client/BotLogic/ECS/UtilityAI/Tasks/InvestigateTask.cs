using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks
{
    /// <summary>
    /// Scores high when the bot detects nearby combat events worth checking out.
    /// Lighter-weight than Vulture: lower intensity threshold, lower score,
    /// simple approach-then-look-around behavior.
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

        /// <summary>Default intensity threshold if not configured.</summary>
        public const int DefaultIntensityThreshold = 5;

        /// <summary>Default detection range for proximity scoring.</summary>
        public const float DefaultDetectionRange = 120f;

        public override int BotActionTypeId => UtilityAI.BotActionTypeId.Investigate;
        public override string ActionReason => "Investigate";

        /// <summary>Minimum combat intensity to trigger investigation.</summary>
        public int IntensityThreshold { get; set; } = DefaultIntensityThreshold;

        /// <summary>Detection range for proximity scoring (squared internally).</summary>
        public float DetectionRange { get; set; } = DefaultDetectionRange;

        public InvestigateTask(float hysteresis = 0.15f)
            : base(hysteresis) { }

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            float score = Score(entity, IntensityThreshold, DetectionRange);
            entity.TaskScores[ordinal] =
                score * ScoringModifiers.CombinedModifier(entity.Aggression, entity.RaidTimeNormalized, BotActionTypeId);
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
            float intensityRatio = (float)entity.CombatIntensity / intensityThreshold;
            if (intensityRatio > 2f)
                intensityRatio = 2f;
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
                return 0f;
            if (score > MaxBaseScore)
                return MaxBaseScore;

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
}
