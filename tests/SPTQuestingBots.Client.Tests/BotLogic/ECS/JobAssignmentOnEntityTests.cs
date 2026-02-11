using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

/// <summary>
/// Phase 8 tests: Job assignment state embedded on BotEntity.
/// Verifies per-bot quest assignment tracking via entity fields.
/// </summary>
[TestFixture]
public class JobAssignmentOnEntityTests
{
    private BotRegistry _registry;

    [SetUp]
    public void SetUp()
    {
        _registry = new BotRegistry(16);
    }

    [Test]
    public void ConsecutiveFailedAssignments_DefaultIsZero()
    {
        var entity = _registry.Add();
        Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));
    }

    [Test]
    public void ConsecutiveFailedAssignments_IncrementAndReset()
    {
        var entity = _registry.Add();

        entity.ConsecutiveFailedAssignments++;
        entity.ConsecutiveFailedAssignments++;
        entity.ConsecutiveFailedAssignments++;
        Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(3));

        // Reset on successful assignment
        entity.ConsecutiveFailedAssignments = 0;
        Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));
    }

    [Test]
    public void ConsecutiveFailedAssignments_IndependentPerBot()
    {
        var e1 = _registry.Add();
        e1.BotType = BotType.PMC;
        var e2 = _registry.Add();
        e2.BotType = BotType.PMC;

        e1.ConsecutiveFailedAssignments = 5;
        e2.ConsecutiveFailedAssignments = 2;

        Assert.That(e1.ConsecutiveFailedAssignments, Is.EqualTo(5));
        Assert.That(e2.ConsecutiveFailedAssignments, Is.EqualTo(2));
    }

    [Test]
    public void ConsecutiveFailedAssignments_SurvivesSensorChanges()
    {
        var entity = _registry.Add();
        entity.ConsecutiveFailedAssignments = 3;

        entity.SetSensor(BotSensor.InCombat, true);
        entity.SetSensor(BotSensor.CanQuest, false);

        Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(3));
    }

    [Test]
    public void ConsecutiveFailedAssignments_SurvivesBossAssignment()
    {
        var boss = _registry.Add();
        boss.ConsecutiveFailedAssignments = 1;
        var follower = _registry.Add();
        follower.ConsecutiveFailedAssignments = 4;

        HiveMindSystem.AssignBoss(follower, boss);

        Assert.That(boss.ConsecutiveFailedAssignments, Is.EqualTo(1));
        Assert.That(follower.ConsecutiveFailedAssignments, Is.EqualTo(4));
    }

    [Test]
    public void ConsecutiveFailedAssignments_ResetOnDeactivation()
    {
        var entity = _registry.Add();
        entity.ConsecutiveFailedAssignments = 10;

        entity.IsActive = false;
        // Manual cleanup: reset assignment state when entity dies
        entity.ConsecutiveFailedAssignments = 0;

        Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));
    }

    [Test]
    public void IterateEntities_CountHighFailureBots()
    {
        var e1 = _registry.Add();
        e1.BotType = BotType.PMC;
        e1.ConsecutiveFailedAssignments = 3;

        var e2 = _registry.Add();
        e2.BotType = BotType.PMC;
        e2.ConsecutiveFailedAssignments = 0;

        var e3 = _registry.Add();
        e3.BotType = BotType.Scav;
        e3.ConsecutiveFailedAssignments = 5;

        var e4 = _registry.Add();
        e4.BotType = BotType.PMC;
        e4.ConsecutiveFailedAssignments = 2;
        e4.IsActive = false;

        // Count active bots with high failure count (>= 3)
        int highFailureCount = 0;
        for (int i = 0; i < _registry.Entities.Count; i++)
        {
            var e = _registry.Entities[i];
            if (e.IsActive && e.ConsecutiveFailedAssignments >= 3)
            {
                highFailureCount++;
            }
        }

        Assert.That(highFailureCount, Is.EqualTo(2)); // e1 (3) and e3 (5), not e4 (inactive)
    }

    [Test]
    public void FullLifecycle_AssignFailResetCycle()
    {
        var entity = _registry.Add();
        entity.BotType = BotType.PMC;

        // First quest succeeds
        entity.ConsecutiveFailedAssignments = 0;
        Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));

        // Three failed attempts
        entity.ConsecutiveFailedAssignments++;
        entity.ConsecutiveFailedAssignments++;
        entity.ConsecutiveFailedAssignments++;
        Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(3));

        // New quest succeeds, reset
        entity.ConsecutiveFailedAssignments = 0;
        Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(0));

        // Two more failures
        entity.ConsecutiveFailedAssignments++;
        entity.ConsecutiveFailedAssignments++;
        Assert.That(entity.ConsecutiveFailedAssignments, Is.EqualTo(2));
    }
}
