using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Tests for InvestigateTask VisibleDist gating — bots should not investigate events
/// beyond their effective vision range.
/// </summary>
[TestFixture]
public class InvestigateTaskVisibleDistTests
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

    [Test]
    public void Score_EventWithinVisibleDist_ReturnsPositive()
    {
        SetupValidState();
        _entity.NearbyEventX = 50f;
        _entity.NearbyEventZ = 0f;
        _entity.VisibleDist = 100f; // Event at 50m, vision at 100m
        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_EventBeyondVisibleDist_ReturnsZero()
    {
        SetupValidState();
        _entity.NearbyEventX = 100f;
        _entity.NearbyEventZ = 0f;
        _entity.VisibleDist = 50f; // Event at 100m, vision at 50m
        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_EventExactlyAtVisibleDist_ReturnsPositive()
    {
        SetupValidState();
        _entity.NearbyEventX = 50f;
        _entity.NearbyEventZ = 0f;
        _entity.VisibleDist = 50f; // Event at exactly vision range
        float score = InvestigateTask.Score(_entity, 5, 120f);
        // distSqr == visibleDistSqr, so not > visibleDistSqr — should score
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_EventJustBeyondVisibleDist_ReturnsZero()
    {
        SetupValidState();
        _entity.NearbyEventX = 51f;
        _entity.NearbyEventZ = 0f;
        _entity.VisibleDist = 50f; // Event at 51m, vision at 50m
        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_LowVisibleDist_BlocksFarEvents()
    {
        SetupValidState();
        _entity.NearbyEventX = 30f;
        _entity.NearbyEventZ = 30f;
        _entity.VisibleDist = 20f; // Poor visibility (nighttime/fog)
        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_HighVisibleDist_AllowsFarEvents()
    {
        SetupValidState();
        _entity.NearbyEventX = 100f;
        _entity.NearbyEventZ = 100f;
        _entity.VisibleDist = 500f; // Clear day, long sight range
        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_DefaultVisibleDist_AllowsNearEvents()
    {
        SetupValidState();
        // Default VisibleDist is 150, event at ~70m
        _entity.NearbyEventX = 50f;
        _entity.NearbyEventZ = 50f;
        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_AlreadyInvestigating_IgnoresVisibleDist()
    {
        // When already investigating, the bot should maintain score
        // regardless of VisibleDist — it committed to the investigation
        _entity.HasNearbyEvent = true;
        _entity.IsInCombat = false;
        _entity.VulturePhase = VulturePhase.None;
        _entity.IsInvestigating = true;
        _entity.NearbyEventX = 200f;
        _entity.NearbyEventZ = 0f;
        _entity.VisibleDist = 50f; // Event way beyond vision
        _entity.CombatIntensity = 8;
        float score = InvestigateTask.Score(_entity, 5, 120f);
        // Already investigating — returns MaxBaseScore regardless
        Assert.That(score, Is.EqualTo(InvestigateTask.MaxBaseScore));
    }

    [Test]
    public void Score_ZeroVisibleDist_DoesNotBlock()
    {
        // VisibleDist of 0 would make visibleDistSqr = 0,
        // but we guard with visibleDistSqr > 0f
        SetupValidState();
        _entity.VisibleDist = 0f;
        float score = InvestigateTask.Score(_entity, 5, 120f);
        // Zero VisibleDist means the check is skipped (guard clause)
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_DiagonalEvent_ChecksActualDistance()
    {
        SetupValidState();
        // (70, 70) => distance = sqrt(70^2 + 70^2) = ~99m
        _entity.NearbyEventX = 70f;
        _entity.NearbyEventZ = 70f;
        _entity.VisibleDist = 95f; // Vision at 95m, event at ~99m
        float score = InvestigateTask.Score(_entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f));
    }

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
