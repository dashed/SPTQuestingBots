using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Tests for VultureTask VisibleDist gating — bots should not vulture toward events
/// beyond their effective vision range.
/// </summary>
[TestFixture]
public class VultureTaskVisibleDistTests
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
        _entity.VisibleDist = 100f;
        float score = VultureTask.Score(_entity, 15, 150f);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_EventBeyondVisibleDist_ReturnsZero()
    {
        SetupValidState();
        _entity.NearbyEventX = 100f;
        _entity.NearbyEventZ = 0f;
        _entity.VisibleDist = 50f;
        float score = VultureTask.Score(_entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_EventExactlyAtVisibleDist_ReturnsPositive()
    {
        SetupValidState();
        _entity.NearbyEventX = 50f;
        _entity.NearbyEventZ = 0f;
        _entity.VisibleDist = 50f;
        float score = VultureTask.Score(_entity, 15, 150f);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_EventJustBeyondVisibleDist_ReturnsZero()
    {
        SetupValidState();
        _entity.NearbyEventX = 51f;
        _entity.NearbyEventZ = 0f;
        _entity.VisibleDist = 50f;
        float score = VultureTask.Score(_entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_LowVisibleDist_BlocksFarEvents()
    {
        SetupValidState();
        _entity.NearbyEventX = 40f;
        _entity.NearbyEventZ = 40f;
        _entity.VisibleDist = 20f;
        float score = VultureTask.Score(_entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_HighVisibleDist_AllowsFarEvents()
    {
        SetupValidState();
        _entity.NearbyEventX = 100f;
        _entity.NearbyEventZ = 100f;
        _entity.VisibleDist = 500f;
        float score = VultureTask.Score(_entity, 15, 150f);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_AlreadyVulturing_IgnoresVisibleDist()
    {
        // Active vulture phase should maintain max score regardless of VisibleDist
        SetupValidState();
        _entity.VulturePhase = VulturePhase.Approach;
        _entity.NearbyEventX = 200f;
        _entity.NearbyEventZ = 0f;
        _entity.VisibleDist = 50f;
        float score = VultureTask.Score(_entity, 15, 150f);
        Assert.That(score, Is.EqualTo(VultureTask.MaxBaseScore));
    }

    [Test]
    public void Score_ZeroVisibleDist_DoesNotBlock()
    {
        SetupValidState();
        _entity.VisibleDist = 0f;
        float score = VultureTask.Score(_entity, 15, 150f);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_NightVision_ExtendedRange()
    {
        SetupValidState();
        _entity.NearbyEventX = 80f;
        _entity.NearbyEventZ = 80f;

        // Without NVG: short vision, event too far
        _entity.VisibleDist = 50f;
        float scoreShort = VultureTask.Score(_entity, 15, 150f);
        Assert.That(scoreShort, Is.EqualTo(0f));

        // With NVG: extended vision, event within range
        _entity.VisibleDist = 200f;
        float scoreLong = VultureTask.Score(_entity, 15, 150f);
        Assert.That(scoreLong, Is.GreaterThan(0f));
    }

    private void SetupValidState()
    {
        _entity.HasNearbyEvent = true;
        _entity.NearbyEventX = 50f;
        _entity.NearbyEventY = 0f;
        _entity.NearbyEventZ = 50f;
        _entity.NearbyEventTime = 1f;
        _entity.CombatIntensity = 20;
        _entity.IsInCombat = false;
        _entity.IsInBossZone = false;
        _entity.VultureCooldownUntil = 0f;
        _entity.VulturePhase = VulturePhase.None;
    }
}
