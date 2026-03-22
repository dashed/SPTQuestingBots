using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class InvestigateTaskAmmoTests
{
    private BotEntity _entity;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
        _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        SetupValidInvestigateState();
    }

    // ── Ammo ratio affects score ──────────────────────────

    [Test]
    public void Score_FullAmmo_ReturnsNormalScore()
    {
        _entity.AmmoRatio = 1f;
        float score = InvestigateTask.Score(_entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_HalfAmmo_ReturnsReducedScore()
    {
        _entity.AmmoRatio = 1f;
        float fullScore = InvestigateTask.Score(_entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);

        _entity.AmmoRatio = 0.5f;
        float halfScore = InvestigateTask.Score(_entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);

        Assert.That(halfScore, Is.LessThan(fullScore));
    }

    [Test]
    public void Score_EmptyAmmo_ReturnsVeryLowScore()
    {
        _entity.AmmoRatio = 1f;
        float fullScore = InvestigateTask.Score(_entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);

        _entity.AmmoRatio = 0f;
        float emptyScore = InvestigateTask.Score(_entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);

        Assert.That(emptyScore, Is.LessThan(fullScore * 0.3f));
    }

    [Test]
    public void Score_EmptyAmmo_StillNonNegative()
    {
        _entity.AmmoRatio = 0f;
        float score = InvestigateTask.Score(_entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void Score_AmmoDoesNotAffectGating()
    {
        // No nearby event = gated out regardless of ammo
        _entity.HasNearbyEvent = false;
        _entity.AmmoRatio = 1f;
        float score = InvestigateTask.Score(_entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void MinAmmoMultiplier_IsPositive()
    {
        Assert.That(InvestigateTask.MinAmmoMultiplier, Is.GreaterThan(0f));
    }

    [Test]
    public void MinAmmoMultiplier_IsLessThanOne()
    {
        Assert.That(InvestigateTask.MinAmmoMultiplier, Is.LessThan(1f));
    }

    // ── Helper ──────────────────────────────────────────

    private void SetupValidInvestigateState()
    {
        _entity.HasNearbyEvent = true;
        _entity.IsInCombat = false;
        _entity.CombatIntensity = InvestigateTask.DefaultIntensityThreshold + 5;
        _entity.NearbyEventX = 10f;
        _entity.NearbyEventZ = 10f;
        _entity.CurrentPositionX = 0f;
        _entity.CurrentPositionZ = 0f;
        _entity.VisibleDist = 200f;
        _entity.AmmoRatio = 1f;
    }
}
