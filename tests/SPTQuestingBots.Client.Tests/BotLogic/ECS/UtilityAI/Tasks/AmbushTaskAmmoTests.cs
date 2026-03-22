using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class AmbushTaskAmmoTests
{
    private BotEntity _entity;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
        _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        SetupValidAmbushState();
    }

    // ── Ammo ratio affects score ──────────────────────────

    [Test]
    public void Score_FullAmmo_ReturnsBaseScore()
    {
        _entity.AmmoRatio = 1f;
        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.EqualTo(AmbushTask.BaseScore).Within(0.001f));
    }

    [Test]
    public void Score_HalfAmmo_ReturnsReducedScore()
    {
        _entity.AmmoRatio = 0.5f;
        float score = AmbushTask.Score(_entity);

        float expectedMultiplier = AmbushTask.MinAmmoMultiplier + (1f - AmbushTask.MinAmmoMultiplier) * 0.5f;
        float expected = AmbushTask.BaseScore * expectedMultiplier;
        Assert.That(score, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void Score_EmptyAmmo_ReturnsMinimumScore()
    {
        _entity.AmmoRatio = 0f;
        float score = AmbushTask.Score(_entity);

        float expected = AmbushTask.BaseScore * AmbushTask.MinAmmoMultiplier;
        Assert.That(score, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void Score_LowAmmo_LowerThanFullAmmo()
    {
        _entity.AmmoRatio = 1f;
        float fullScore = AmbushTask.Score(_entity);

        _entity.AmmoRatio = 0.2f;
        float lowScore = AmbushTask.Score(_entity);

        Assert.That(lowScore, Is.LessThan(fullScore));
    }

    [Test]
    public void Score_EmptyAmmo_StillPositive()
    {
        _entity.AmmoRatio = 0f;
        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_AmmoDoesNotAffectGating()
    {
        // Not close to objective = gated out regardless of ammo
        _entity.IsCloseToObjective = false;
        _entity.AmmoRatio = 1f;
        float score = AmbushTask.Score(_entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_AmmoAndGameAmbushBonusStack()
    {
        _entity.AmmoRatio = 0.5f;
        _entity.HasGameAmbushPoint = true;

        float score = AmbushTask.Score(_entity);

        float expectedMultiplier = AmbushTask.MinAmmoMultiplier + (1f - AmbushTask.MinAmmoMultiplier) * 0.5f;
        float baseWithBonus = AmbushTask.BaseScore + AmbushTask.GameAmbushBonus;
        float expected = baseWithBonus * expectedMultiplier;
        Assert.That(score, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void MinAmmoMultiplier_IsPositive()
    {
        Assert.That(AmbushTask.MinAmmoMultiplier, Is.GreaterThan(0f));
    }

    [Test]
    public void MinAmmoMultiplier_IsLessThanOne()
    {
        Assert.That(AmbushTask.MinAmmoMultiplier, Is.LessThan(1f));
    }

    // ── Helper ──────────────────────────────────────────

    private void SetupValidAmbushState()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Ambush;
        _entity.IsCloseToObjective = true;
        _entity.AmmoRatio = 1f;
    }
}
