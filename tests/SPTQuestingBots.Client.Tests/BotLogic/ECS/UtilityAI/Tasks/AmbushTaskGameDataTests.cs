using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class AmbushTaskGameDataTests
{
    private BotEntity _entity;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
        _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
    }

    // ── Game ambush point bonus ──────────────────────────

    [Test]
    public void Score_WithGameAmbushPoint_ReturnsBoostedScore()
    {
        SetupValidAmbushState();
        _entity.HasGameAmbushPoint = true;

        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.EqualTo(AmbushTask.BaseScore + AmbushTask.GameAmbushBonus));
    }

    [Test]
    public void Score_WithoutGameAmbushPoint_ReturnsBaseScore()
    {
        SetupValidAmbushState();
        _entity.HasGameAmbushPoint = false;

        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.EqualTo(AmbushTask.BaseScore));
    }

    [Test]
    public void Score_GameAmbushPointBonus_IsPositive()
    {
        Assert.That(AmbushTask.GameAmbushBonus, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_BoostedScore_IsHigherThanBase()
    {
        SetupValidAmbushState();

        float baseScore = AmbushTask.Score(_entity);

        _entity.HasGameAmbushPoint = true;
        float boostedScore = AmbushTask.Score(_entity);

        Assert.That(boostedScore, Is.GreaterThan(baseScore));
    }

    // ── Gating still works ──────────────────────────────

    [Test]
    public void Score_NoActiveObjective_GameAmbushIgnored()
    {
        _entity.HasActiveObjective = false;
        _entity.CurrentQuestAction = QuestActionId.Ambush;
        _entity.IsCloseToObjective = true;
        _entity.HasGameAmbushPoint = true;

        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_WrongQuestAction_GameAmbushIgnored()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        _entity.IsCloseToObjective = true;
        _entity.HasGameAmbushPoint = true;

        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_NotCloseToObjective_GameAmbushIgnored()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Ambush;
        _entity.IsCloseToObjective = false;
        _entity.HasGameAmbushPoint = true;

        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── BotEntity defaults ──────────────────────────────

    [Test]
    public void BotEntity_HasGameAmbushPoint_DefaultsFalse()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.HasGameAmbushPoint, Is.False);
    }

    // ── ScoreEntity integration ─────────────────────────

    [Test]
    public void ScoreEntity_WithGameAmbush_WritesHigherScore()
    {
        SetupValidAmbushState();
        _entity.Aggression = 0.5f;
        _entity.RaidTimeNormalized = 0.5f;
        _entity.HumanPlayerProximity = 0f;

        var task = new AmbushTask();

        _entity.HasGameAmbushPoint = false;
        task.ScoreEntity(2, _entity);
        float baseResult = _entity.TaskScores[2];

        _entity.HasGameAmbushPoint = true;
        task.ScoreEntity(2, _entity);
        float boostedResult = _entity.TaskScores[2];

        Assert.That(boostedResult, Is.GreaterThan(baseResult));
    }

    // ── Helper ──────────────────────────────────────────

    private void SetupValidAmbushState()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Ambush;
        _entity.IsCloseToObjective = true;
        _entity.HasGameAmbushPoint = false;
    }
}
