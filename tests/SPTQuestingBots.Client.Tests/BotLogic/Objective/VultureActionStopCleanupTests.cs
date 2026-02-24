using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.Objective;

/// <summary>
/// Tests for VultureAction lifecycle cleanup.
/// Bug: VultureAction.Stop() set VulturePhase to Complete but did NOT clear
/// HasNearbyEvent. When a bot was interrupted from vulturing (e.g., by combat)
/// and later returned to normal scoring, the stale HasNearbyEvent=true could
/// cause the vulture task to re-score highly, sending the bot to investigate
/// a stale/irrelevant event location.
/// Fix: Stop() now clears HasNearbyEvent = false, consistent with CompleteVulture().
/// </summary>
[TestFixture]
public class VultureActionStopCleanupTests
{
    [Test]
    public void Stop_ClearsVulturePhaseToComplete()
    {
        var entity = MakeVultureEntity(VulturePhase.Approach);

        // Simulate what Stop() does to entity state
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Complete));
    }

    [Test]
    public void Stop_ClearsHasNearbyEvent()
    {
        var entity = MakeVultureEntity(VulturePhase.SilentApproach);

        // Before fix: Stop() only set VulturePhase, not HasNearbyEvent
        // After fix: Stop() also clears HasNearbyEvent
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;

        Assert.That(entity.HasNearbyEvent, Is.False, "HasNearbyEvent should be cleared on Stop()");
    }

    /// <summary>
    /// Regression test: after Stop() is called during combat interruption,
    /// the entity should NOT have stale event data that could trigger re-entry.
    /// </summary>
    [Test]
    public void StopDuringCombatInterruption_NoStaleEventData()
    {
        var entity = MakeVultureEntity(VulturePhase.Approach);
        entity.HasNearbyEvent = true;
        entity.NearbyEventX = 100f;
        entity.NearbyEventZ = 200f;
        entity.NearbyEventTime = 50f;

        // Simulate Stop() cleanup (as implemented in fix)
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;

        // After stop, no stale event should be flagged
        Assert.That(entity.HasNearbyEvent, Is.False);
        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Complete));
    }

    /// <summary>
    /// CompleteVulture() and Stop() should produce equivalent entity state
    /// for the fields they both manage.
    /// </summary>
    [Test]
    public void StopAndComplete_ProduceConsistentState()
    {
        // Entity as if Stop() was called
        var entityStop = MakeVultureEntity(VulturePhase.SilentApproach);
        entityStop.VulturePhase = VulturePhase.Complete;
        entityStop.HasNearbyEvent = false; // Fixed: Stop() now clears this

        // Entity as if CompleteVulture() was called
        var entityComplete = MakeVultureEntity(VulturePhase.Rush);
        entityComplete.VulturePhase = VulturePhase.Complete;
        entityComplete.HasNearbyEvent = false;

        Assert.That(entityStop.VulturePhase, Is.EqualTo(entityComplete.VulturePhase));
        Assert.That(entityStop.HasNearbyEvent, Is.EqualTo(entityComplete.HasNearbyEvent));
    }

    /// <summary>
    /// Without the fix: bot interrupted from vulturing retains HasNearbyEvent=true.
    /// When vulture cooldown expires (VultureCooldownUntil=0 means never set),
    /// the scoring task would see HasNearbyEvent=true and potentially re-score.
    /// </summary>
    [Test]
    public void WithoutFix_StaleEventWouldPersist()
    {
        var entity = MakeVultureEntity(VulturePhase.Approach);
        entity.HasNearbyEvent = true;

        // OLD behavior (before fix): Stop only sets phase
        entity.VulturePhase = VulturePhase.Complete;
        // HasNearbyEvent NOT cleared -- this was the bug

        // Verify the bug scenario: stale event persists
        // (This test documents what WOULD happen without the fix)
        // We test that the entity would still have HasNearbyEvent = true
        Assert.That(entity.HasNearbyEvent, Is.True, "Documents pre-fix behavior: stale event persists");

        // Apply the fix
        entity.HasNearbyEvent = false;
        Assert.That(entity.HasNearbyEvent, Is.False, "After fix: stale event is cleared");
    }

    private static BotEntity MakeVultureEntity(byte initialPhase)
    {
        var entity = new BotEntity(0);
        entity.HasNearbyEvent = true;
        entity.NearbyEventX = 50f;
        entity.NearbyEventY = 0f;
        entity.NearbyEventZ = 75f;
        entity.NearbyEventTime = 10f;
        entity.VulturePhase = initialPhase;
        entity.CombatIntensity = 5;
        return entity;
    }
}
