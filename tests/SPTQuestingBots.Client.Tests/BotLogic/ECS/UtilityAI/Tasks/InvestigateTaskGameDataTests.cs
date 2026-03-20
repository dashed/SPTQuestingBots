using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class InvestigateTaskGameDataTests
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

    // ── Game search bonus ──────────────────────────────

    [Test]
    public void Score_WithGameSearchTarget_PlayerPosition_IncludesBonus()
    {
        SetupValidState();
        _entity.HasGameSearchTarget = true;
        _entity.GameSearchTargetType = 0; // playerPosition

        float score = InvestigateTask.Score(_entity, 5, 120f);

        // Score should include the GameSearchBonus
        _entity.HasGameSearchTarget = false;
        float scoreWithout = InvestigateTask.Score(_entity, 5, 120f);

        Assert.That(score, Is.GreaterThan(scoreWithout));
    }

    [Test]
    public void Score_WithGameSearchTarget_MapPosition_NoBonus()
    {
        SetupValidState();
        _entity.HasGameSearchTarget = true;
        _entity.GameSearchTargetType = 1; // mapPosition — not as precise

        float scoreWithMap = InvestigateTask.Score(_entity, 5, 120f);

        _entity.HasGameSearchTarget = false;
        float scoreWithout = InvestigateTask.Score(_entity, 5, 120f);

        // Map position type should not get the bonus
        Assert.That(scoreWithMap, Is.EqualTo(scoreWithout));
    }

    [Test]
    public void Score_NoGameSearchTarget_NoBonus()
    {
        SetupValidState();
        _entity.HasGameSearchTarget = false;

        float score = InvestigateTask.Score(_entity, 5, 120f);

        // Still scores based on intensity/proximity
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_GameSearchBonus_StillCappedAtMax()
    {
        SetupValidState();
        _entity.CombatIntensity = 1000;
        _entity.NearbyEventX = 1f;
        _entity.NearbyEventZ = 1f;
        _entity.HasGameSearchTarget = true;
        _entity.GameSearchTargetType = 0; // playerPosition

        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.LessThanOrEqualTo(InvestigateTask.MaxBaseScore));
    }

    [Test]
    public void Score_GameSearchBonus_Constant_IsPositive()
    {
        Assert.That(InvestigateTask.GameSearchBonus, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_GameSearchBonus_IsSmall()
    {
        // The bonus should be smaller than the base scoring weights
        Assert.That(InvestigateTask.GameSearchBonus, Is.LessThan(0.20f));
    }

    // ── Gating still works with game data ──────────────

    [Test]
    public void Score_InCombat_GameSearchIgnored()
    {
        SetupValidState();
        _entity.IsInCombat = true;
        _entity.HasGameSearchTarget = true;
        _entity.GameSearchTargetType = 0;

        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_NoNearbyEvent_GameSearchIgnored()
    {
        _entity.HasNearbyEvent = false;
        _entity.HasGameSearchTarget = true;
        _entity.GameSearchTargetType = 0;

        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_AlreadyInvestigating_GameSearchDoesNotChangeMaxScore()
    {
        SetupValidState();
        _entity.IsInvestigating = true;
        _entity.HasGameSearchTarget = true;
        _entity.GameSearchTargetType = 0;

        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.EqualTo(InvestigateTask.MaxBaseScore));
    }

    // ── BotEntity game search defaults ──────────────────

    [Test]
    public void BotEntity_HasGameSearchTarget_DefaultsFalse()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.HasGameSearchTarget, Is.False);
    }

    [Test]
    public void BotEntity_GameSearchTargetX_DefaultsZero()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.GameSearchTargetX, Is.EqualTo(0f));
    }

    [Test]
    public void BotEntity_GameSearchTargetY_DefaultsZero()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.GameSearchTargetY, Is.EqualTo(0f));
    }

    [Test]
    public void BotEntity_GameSearchTargetZ_DefaultsZero()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.GameSearchTargetZ, Is.EqualTo(0f));
    }

    [Test]
    public void BotEntity_GameSearchTargetType_DefaultsZero()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.GameSearchTargetType, Is.EqualTo(0));
    }

    // ── ScoreEntity integration ─────────────────────────

    [Test]
    public void ScoreEntity_WithGameSearch_WritesHigherScore()
    {
        SetupValidState();
        _entity.Aggression = 0.5f;
        _entity.RaidTimeNormalized = 0.5f;
        _entity.HumanPlayerProximity = 0f;

        var task = new InvestigateTask();

        _entity.HasGameSearchTarget = false;
        task.ScoreEntity(11, _entity);
        float baseResult = _entity.TaskScores[11];

        _entity.HasGameSearchTarget = true;
        _entity.GameSearchTargetType = 0; // playerPosition
        task.ScoreEntity(11, _entity);
        float boostedResult = _entity.TaskScores[11];

        Assert.That(boostedResult, Is.GreaterThan(baseResult));
    }

    // ── Helper ──────────────────────────────────────────

    private void SetupValidState()
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
    }
}
