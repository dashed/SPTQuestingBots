using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Tests for BSG data integration into AI scoring:
/// - PlacesForCheck (hearing sensor) in InvestigateTask
/// - BSG Mind settings (AmbushTask, VultureTask, LingerTask)
/// - EnemyInfo gradient post-combat (InvestigateTask)
/// </summary>
[TestFixture]
public class BsgDataIntegrationTests
{
    private BotEntity _entity;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
        _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        _entity.CurrentPositionX = 0f;
        _entity.CurrentPositionZ = 0f;
    }

    // ═══════════════════════════════════════════════════════════════
    // BotEntity field defaults
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void BotEntity_HasPlaceForCheck_DefaultsFalse()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.HasPlaceForCheck, Is.False);
    }

    [Test]
    public void BotEntity_PlaceForCheckCoords_DefaultZero()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.PlaceForCheckX, Is.EqualTo(0f));
        Assert.That(entity.PlaceForCheckY, Is.EqualTo(0f));
        Assert.That(entity.PlaceForCheckZ, Is.EqualTo(0f));
    }

    [Test]
    public void BotEntity_PlaceForCheckTypeId_DefaultZero()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.PlaceForCheckTypeId, Is.EqualTo(0));
    }

    [Test]
    public void BotEntity_MindAmbushWhenUnderFire_DefaultsFalse()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.MindAmbushWhenUnderFire, Is.False);
    }

    [Test]
    public void BotEntity_MindHowWorkOverDeadBody_DefaultsOne()
    {
        // Default is 1 (interested in dead bodies) to avoid penalizing uninitialized bots
        var entity = new BotEntity(99);
        Assert.That(entity.MindHowWorkOverDeadBody, Is.EqualTo(1));
    }

    [Test]
    public void BotEntity_MindCanStandBy_DefaultsTrue()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.MindCanStandBy, Is.True);
    }

    [Test]
    public void BotEntity_MindTimeToForgetEnemySec_DefaultsZero()
    {
        // Default is 0f from struct init; SyncMindSettings sets it to a positive value
        var entity = new BotEntity(99);
        Assert.That(entity.MindTimeToForgetEnemySec, Is.EqualTo(0f));
    }

    [Test]
    public void BotEntity_HasEnemyInfo_DefaultsFalse()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.HasEnemyInfo, Is.False);
    }

    [Test]
    public void BotEntity_TimeSinceEnemySeen_DefaultsMaxValue()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.TimeSinceEnemySeen, Is.EqualTo(float.MaxValue));
    }

    [Test]
    public void BotEntity_EnemyDistance_DefaultsZero()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.EnemyDistance, Is.EqualTo(0f));
    }

    [Test]
    public void BotEntity_IsEnemyVisible_DefaultsFalse()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.IsEnemyVisible, Is.False);
    }

    [Test]
    public void BotEntity_EnemyLastKnownCoords_DefaultZero()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.EnemyLastKnownX, Is.EqualTo(0f));
        Assert.That(entity.EnemyLastKnownY, Is.EqualTo(0f));
        Assert.That(entity.EnemyLastKnownZ, Is.EqualTo(0f));
    }

    // ═══════════════════════════════════════════════════════════════
    // Item 3: PlacesForCheck in InvestigateTask
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void InvestigateScore_WithPlaceForCheck_Simple_AddsBonus()
    {
        SetupValidInvestigateState();
        float scoreWithout = InvestigateTask.Score(_entity, 5, 120f);

        _entity.HasPlaceForCheck = true;
        _entity.PlaceForCheckTypeId = 0; // simple
        float scoreWith = InvestigateTask.Score(_entity, 5, 120f);

        Assert.That(scoreWith, Is.GreaterThan(scoreWithout));
    }

    [Test]
    public void InvestigateScore_WithPlaceForCheck_Danger_AddsHigherBonus()
    {
        SetupValidInvestigateState();

        _entity.HasPlaceForCheck = true;
        _entity.PlaceForCheckTypeId = 0; // simple
        float simpleScore = InvestigateTask.Score(_entity, 5, 120f);

        _entity.PlaceForCheckTypeId = 1; // danger
        float dangerScore = InvestigateTask.Score(_entity, 5, 120f);

        Assert.That(dangerScore, Is.GreaterThan(simpleScore));
    }

    [Test]
    public void InvestigateScore_WithPlaceForCheck_Suspicious_AddsHigherBonus()
    {
        SetupValidInvestigateState();

        _entity.HasPlaceForCheck = true;
        _entity.PlaceForCheckTypeId = 0; // simple
        float simpleScore = InvestigateTask.Score(_entity, 5, 120f);

        _entity.PlaceForCheckTypeId = 2; // suspicious
        float suspiciousScore = InvestigateTask.Score(_entity, 5, 120f);

        Assert.That(suspiciousScore, Is.GreaterThan(simpleScore));
    }

    [Test]
    public void InvestigateScore_NoPlaceForCheck_NoBonus()
    {
        SetupValidInvestigateState();
        _entity.HasPlaceForCheck = false;

        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.GreaterThan(0f));

        // Re-check: adding PlaceForCheck should change the score
        _entity.HasPlaceForCheck = true;
        _entity.PlaceForCheckTypeId = 1;
        float boosted = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(boosted, Is.GreaterThan(score));
    }

    [Test]
    public void InvestigateScore_PlaceForCheck_InCombat_NoEffect()
    {
        SetupValidInvestigateState();
        _entity.IsInCombat = true;
        _entity.HasPlaceForCheck = true;
        _entity.PlaceForCheckTypeId = 1;

        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void InvestigateScore_PlaceForCheck_StillCappedAtMax()
    {
        SetupValidInvestigateState();
        _entity.CombatIntensity = 1000;
        _entity.NearbyEventX = 1f;
        _entity.NearbyEventZ = 1f;
        _entity.HasPlaceForCheck = true;
        _entity.PlaceForCheckTypeId = 2;
        _entity.HasGameSearchTarget = true;
        _entity.GameSearchTargetType = 0;

        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.LessThanOrEqualTo(InvestigateTask.MaxBaseScore));
    }

    [Test]
    public void InvestigateScore_PlaceForCheckBonus_IsPositive()
    {
        Assert.That(InvestigateTask.PlaceForCheckBonus, Is.GreaterThan(0f));
    }

    [Test]
    public void InvestigateScore_PlaceForCheckDangerExtra_IsPositive()
    {
        Assert.That(InvestigateTask.PlaceForCheckDangerExtra, Is.GreaterThan(0f));
    }

    // ═══════════════════════════════════════════════════════════════
    // Item 5: BSG Mind settings — AmbushTask
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void AmbushScore_WithMindAmbushWhenUnderFire_AddsBonus()
    {
        SetupValidAmbushState();
        float baseScore = AmbushTask.Score(_entity);

        _entity.MindAmbushWhenUnderFire = true;
        float boosted = AmbushTask.Score(_entity);

        Assert.That(boosted, Is.GreaterThan(baseScore));
        Assert.That(boosted, Is.EqualTo(AmbushTask.BaseScore + AmbushTask.MindAmbushBonus));
    }

    [Test]
    public void AmbushScore_WithoutMindAmbushWhenUnderFire_NoBonus()
    {
        SetupValidAmbushState();
        _entity.MindAmbushWhenUnderFire = false;

        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.EqualTo(AmbushTask.BaseScore));
    }

    [Test]
    public void AmbushScore_MindAmbushBonus_IsPositive()
    {
        Assert.That(AmbushTask.MindAmbushBonus, Is.GreaterThan(0f));
    }

    [Test]
    public void AmbushScore_BothGameAndMindBonus_Cumulative()
    {
        SetupValidAmbushState();
        _entity.HasGameAmbushPoint = true;
        _entity.MindAmbushWhenUnderFire = true;

        float score = AmbushTask.Score(_entity);
        float expected = AmbushTask.BaseScore + AmbushTask.GameAmbushBonus + AmbushTask.MindAmbushBonus;
        Assert.That(score, Is.EqualTo(expected));
    }

    [Test]
    public void AmbushScore_MindAmbush_NoActiveObjective_NoEffect()
    {
        _entity.HasActiveObjective = false;
        _entity.CurrentQuestAction = QuestActionId.Ambush;
        _entity.IsCloseToObjective = true;
        _entity.MindAmbushWhenUnderFire = true;

        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void AmbushScoreEntity_MindAmbush_WritesHigherScore()
    {
        SetupValidAmbushState();
        _entity.Aggression = 0.5f;
        _entity.RaidTimeNormalized = 0.5f;
        _entity.HumanPlayerProximity = 0f;

        var task = new AmbushTask();

        _entity.MindAmbushWhenUnderFire = false;
        task.ScoreEntity(1, _entity);
        float baseResult = _entity.TaskScores[1];

        _entity.MindAmbushWhenUnderFire = true;
        task.ScoreEntity(1, _entity);
        float boostedResult = _entity.TaskScores[1];

        Assert.That(boostedResult, Is.GreaterThan(baseResult));
    }

    // ═══════════════════════════════════════════════════════════════
    // Item 5: BSG Mind settings — VultureTask
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void VultureScore_DeadBodyIgnore_PenalizesScore()
    {
        SetupValidVultureState();
        _entity.MindHowWorkOverDeadBody = 1; // interested in dead bodies
        float normalScore = VultureTask.Score(_entity, 15, 150f);

        _entity.MindHowWorkOverDeadBody = 0; // ignores dead bodies
        float penalizedScore = VultureTask.Score(_entity, 15, 150f);

        Assert.That(penalizedScore, Is.LessThan(normalScore));
    }

    [Test]
    public void VultureScore_DeadBodyIgnore_MultipliesByPenalty()
    {
        SetupValidVultureState();
        _entity.MindHowWorkOverDeadBody = 0;

        float score = VultureTask.Score(_entity, 15, 150f);

        // Score should be positive but reduced
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(score, Is.LessThan(VultureTask.MaxBaseScore));
    }

    [Test]
    public void VultureScore_DeadBodyPositive_NoPenalty()
    {
        SetupValidVultureState();
        _entity.MindHowWorkOverDeadBody = 2;
        float score2 = VultureTask.Score(_entity, 15, 150f);

        _entity.MindHowWorkOverDeadBody = 1;
        float score1 = VultureTask.Score(_entity, 15, 150f);

        // Both should produce the same score (no penalty for positive values)
        Assert.That(score1, Is.EqualTo(score2));
    }

    [Test]
    public void VultureScore_DeadBodyIgnore_InCombat_StillZero()
    {
        SetupValidVultureState();
        _entity.IsInCombat = true;
        _entity.MindHowWorkOverDeadBody = 0;

        float score = VultureTask.Score(_entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void VultureScore_DeadBodyIgnorePenalty_IsBetweenZeroAndOne()
    {
        Assert.That(VultureTask.DeadBodyIgnorePenalty, Is.GreaterThan(0f));
        Assert.That(VultureTask.DeadBodyIgnorePenalty, Is.LessThan(1f));
    }

    // ═══════════════════════════════════════════════════════════════
    // Item 5: BSG Mind settings — LingerTask
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void LingerScore_MindCanStandByFalse_ReturnsZero()
    {
        SetupValidLingerState();
        _entity.MindCanStandBy = false;

        float score = LingerTask.Score(_entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerScore_MindCanStandByTrue_ReturnsPositive()
    {
        SetupValidLingerState();
        _entity.MindCanStandBy = true;

        float score = LingerTask.Score(_entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void LingerScore_MindCanStandByDefault_IsTrue()
    {
        // BotEntity default should allow lingering (conservative)
        var entity = new BotEntity(99);
        Assert.That(entity.MindCanStandBy, Is.True);
    }

    [Test]
    public void LingerScore_MindCanStandByFalse_OverridesOtherConditions()
    {
        SetupValidLingerState();
        _entity.MindCanStandBy = false;
        _entity.IsInCombat = false;
        _entity.ObjectiveCompletedTime = 100f;
        _entity.LingerDuration = 30f;
        _entity.CurrentGameTime = 105f;

        float score = LingerTask.Score(_entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerScore_InCombat_StillZero_EvenWithCanStandBy()
    {
        SetupValidLingerState();
        _entity.IsInCombat = true;
        _entity.MindCanStandBy = true;

        float score = LingerTask.Score(_entity, LingerTask.DefaultBaseScore);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ═══════════════════════════════════════════════════════════════
    // Item 10: EnemyInfo gradient in InvestigateTask
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void InvestigateScore_EnemyJustSeen_AddsFullBonus()
    {
        SetupValidInvestigateState();
        _entity.HasEnemyInfo = true;
        _entity.IsEnemyVisible = false;
        _entity.TimeSinceEnemySeen = 0f;
        _entity.MindTimeToForgetEnemySec = 60f;

        float scoreWith = InvestigateTask.Score(_entity, 5, 120f);

        _entity.HasEnemyInfo = false;
        _entity.TimeSinceEnemySeen = float.MaxValue;
        float scoreWithout = InvestigateTask.Score(_entity, 5, 120f);

        Assert.That(scoreWith, Is.GreaterThan(scoreWithout));
    }

    [Test]
    public void InvestigateScore_EnemyHalfForgotten_AddsHalfBonus()
    {
        SetupValidInvestigateState();
        _entity.HasEnemyInfo = true;
        _entity.IsEnemyVisible = false;
        _entity.MindTimeToForgetEnemySec = 60f;

        _entity.TimeSinceEnemySeen = 0f;
        float fullBonus = InvestigateTask.Score(_entity, 5, 120f);

        _entity.TimeSinceEnemySeen = 30f; // half of forget time
        float halfBonus = InvestigateTask.Score(_entity, 5, 120f);

        Assert.That(halfBonus, Is.LessThan(fullBonus));
        Assert.That(halfBonus, Is.GreaterThan(0f));
    }

    [Test]
    public void InvestigateScore_EnemyForgotten_NoBonus()
    {
        SetupValidInvestigateState();
        _entity.HasEnemyInfo = true;
        _entity.IsEnemyVisible = false;
        _entity.MindTimeToForgetEnemySec = 60f;
        _entity.TimeSinceEnemySeen = 61f; // past forget time

        float scoreWith = InvestigateTask.Score(_entity, 5, 120f);

        _entity.HasEnemyInfo = false;
        _entity.TimeSinceEnemySeen = float.MaxValue;
        float scoreWithout = InvestigateTask.Score(_entity, 5, 120f);

        Assert.That(scoreWith, Is.EqualTo(scoreWithout));
    }

    [Test]
    public void InvestigateScore_EnemyVisible_NoBonus()
    {
        SetupValidInvestigateState();
        _entity.HasEnemyInfo = true;
        _entity.IsEnemyVisible = true; // can see enemy — no investigation needed
        _entity.TimeSinceEnemySeen = 1f;
        _entity.MindTimeToForgetEnemySec = 60f;

        float scoreWith = InvestigateTask.Score(_entity, 5, 120f);

        _entity.HasEnemyInfo = false;
        _entity.TimeSinceEnemySeen = float.MaxValue;
        float scoreWithout = InvestigateTask.Score(_entity, 5, 120f);

        Assert.That(scoreWith, Is.EqualTo(scoreWithout));
    }

    [Test]
    public void InvestigateScore_NoEnemyInfo_NoBonus()
    {
        SetupValidInvestigateState();
        _entity.HasEnemyInfo = false;
        _entity.TimeSinceEnemySeen = float.MaxValue;

        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.GreaterThan(0f)); // still scores from intensity/proximity
    }

    [Test]
    public void InvestigateScore_EnemyInfo_StillCappedAtMax()
    {
        SetupValidInvestigateState();
        _entity.CombatIntensity = 1000;
        _entity.NearbyEventX = 1f;
        _entity.NearbyEventZ = 1f;
        _entity.HasPlaceForCheck = true;
        _entity.PlaceForCheckTypeId = 2;
        _entity.HasEnemyInfo = true;
        _entity.IsEnemyVisible = false;
        _entity.TimeSinceEnemySeen = 0f;
        _entity.MindTimeToForgetEnemySec = 60f;

        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.LessThanOrEqualTo(InvestigateTask.MaxBaseScore));
    }

    [Test]
    public void InvestigateScore_EnemyInfoBonus_IsPositive()
    {
        Assert.That(InvestigateTask.EnemyInfoBonus, Is.GreaterThan(0f));
    }

    [Test]
    public void InvestigateScore_EnemyGradient_LinearDecay()
    {
        SetupValidInvestigateState();
        _entity.HasEnemyInfo = true;
        _entity.IsEnemyVisible = false;
        _entity.MindTimeToForgetEnemySec = 100f;

        // Capture scores at 0%, 25%, 50%, 75% of forget time
        float[] scores = new float[4];
        float[] times = { 0f, 25f, 50f, 75f };
        for (int i = 0; i < 4; i++)
        {
            _entity.TimeSinceEnemySeen = times[i];
            scores[i] = InvestigateTask.Score(_entity, 5, 120f);
        }

        // Each later sample should be less than or equal to the previous
        for (int i = 1; i < 4; i++)
        {
            Assert.That(scores[i], Is.LessThanOrEqualTo(scores[i - 1]), $"Score at {times[i]}s should be <= score at {times[i - 1]}s");
        }
    }

    [Test]
    public void InvestigateScore_ZeroForgetTime_NoBonus()
    {
        SetupValidInvestigateState();
        _entity.HasEnemyInfo = true;
        _entity.IsEnemyVisible = false;
        _entity.TimeSinceEnemySeen = 1f;
        _entity.MindTimeToForgetEnemySec = 0f; // edge case: zero forget time

        float scoreWith = InvestigateTask.Score(_entity, 5, 120f);

        _entity.HasEnemyInfo = false;
        _entity.TimeSinceEnemySeen = float.MaxValue;
        float scoreWithout = InvestigateTask.Score(_entity, 5, 120f);

        // With zero forget time, the condition TimeSinceEnemySeen < 0 is false, so no bonus
        Assert.That(scoreWith, Is.EqualTo(scoreWithout));
    }

    // ═══════════════════════════════════════════════════════════════
    // Combined integration: all bonuses together
    // ═══════════════════════════════════════════════════════════════

    [Test]
    public void InvestigateScore_AllBonuses_HigherThanAnyIndividual()
    {
        SetupValidInvestigateState();
        _entity.Aggression = 0.5f;
        _entity.RaidTimeNormalized = 0.5f;

        // Score with no bonuses
        float bare = InvestigateTask.Score(_entity, 5, 120f);

        // Score with all bonuses
        _entity.HasPlaceForCheck = true;
        _entity.PlaceForCheckTypeId = 2;
        _entity.HasGameSearchTarget = true;
        _entity.GameSearchTargetType = 0;
        _entity.HasEnemyInfo = true;
        _entity.IsEnemyVisible = false;
        _entity.TimeSinceEnemySeen = 5f;
        _entity.MindTimeToForgetEnemySec = 60f;

        float combined = InvestigateTask.Score(_entity, 5, 120f);

        Assert.That(combined, Is.GreaterThan(bare));
    }

    [Test]
    public void InvestigateScoreEntity_Integration_AllBonuses()
    {
        SetupValidInvestigateState();
        _entity.Aggression = 0.5f;
        _entity.RaidTimeNormalized = 0.5f;
        _entity.HumanPlayerProximity = 0f;
        _entity.HasPlaceForCheck = true;
        _entity.PlaceForCheckTypeId = 1;
        _entity.HasEnemyInfo = true;
        _entity.IsEnemyVisible = false;
        _entity.TimeSinceEnemySeen = 10f;
        _entity.MindTimeToForgetEnemySec = 60f;

        var task = new InvestigateTask();
        task.ScoreEntity(11, _entity);
        float score = _entity.TaskScores[11];

        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(score, Is.LessThanOrEqualTo(InvestigateTask.MaxBaseScore * ScoringModifiers.MaxCombinedModifier));
    }

    // ═══════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════

    private void SetupValidInvestigateState()
    {
        _entity.HasNearbyEvent = true;
        _entity.NearbyEventX = 50f;
        _entity.NearbyEventY = 0f;
        _entity.NearbyEventZ = 50f;
        _entity.NearbyEventTime = 1f;
        _entity.CombatIntensity = 8;
        _entity.IsInCombat = false;
        _entity.VulturePhase = VulturePhase.None;
        _entity.IsInvestigating = false;
        _entity.MindCanStandBy = true;
    }

    private void SetupValidAmbushState()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Ambush;
        _entity.IsCloseToObjective = true;
        _entity.HasGameAmbushPoint = false;
        _entity.MindAmbushWhenUnderFire = false;
    }

    private void SetupValidVultureState()
    {
        _entity.HasNearbyEvent = true;
        _entity.NearbyEventX = 50f;
        _entity.NearbyEventY = 0f;
        _entity.NearbyEventZ = 50f;
        _entity.NearbyEventTime = 1f;
        _entity.CombatIntensity = 20;
        _entity.IsInCombat = false;
        _entity.VulturePhase = VulturePhase.None;
        _entity.IsInBossZone = false;
        _entity.VultureCooldownUntil = 0f;
        _entity.CurrentGameTime = 100f;
        _entity.MindHowWorkOverDeadBody = 1;
    }

    private void SetupValidLingerState()
    {
        _entity.ObjectiveCompletedTime = 100f;
        _entity.LingerDuration = 30f;
        _entity.CurrentGameTime = 105f;
        _entity.IsInCombat = false;
        _entity.MindCanStandBy = true;
    }
}
