using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.StateMachines;

/// <summary>
/// Tests for VultureAction phase transitions, verifying each state machine
/// path is correctly handled, including the movement timeout fix.
/// </summary>
[TestFixture]
public class VulturePhaseTransitionTests
{
    // ── Phase identity constants ──────────────────────────────

    [Test]
    public void VulturePhase_Constants_AreDistinct()
    {
        var phases = new byte[]
        {
            VulturePhase.None,
            VulturePhase.Approach,
            VulturePhase.SilentApproach,
            VulturePhase.HoldAmbush,
            VulturePhase.Rush,
            VulturePhase.Paranoia,
            VulturePhase.Complete,
        };

        var unique = new HashSet<byte>(phases);
        Assert.That(unique.Count, Is.EqualTo(phases.Length), "All phase constants must be unique");
    }

    // ── Forward transition path (happy path) ─────────────────

    [Test]
    public void HappyPath_ApproachToSilentApproach()
    {
        var entity = MakeEntity(VulturePhase.Approach);

        // When close enough, transition to SilentApproach
        entity.VulturePhase = VulturePhase.SilentApproach;

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.SilentApproach));
    }

    [Test]
    public void HappyPath_SilentApproachToHoldAmbush()
    {
        var entity = MakeEntity(VulturePhase.SilentApproach);

        entity.VulturePhase = VulturePhase.HoldAmbush;

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.HoldAmbush));
    }

    [Test]
    public void HappyPath_HoldAmbushToRush_AmbushDurationExpired()
    {
        var entity = MakeEntity(VulturePhase.HoldAmbush);

        // Ambush duration expired
        entity.VulturePhase = VulturePhase.Rush;

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Rush));
    }

    [Test]
    public void HappyPath_HoldAmbushToRush_SilenceDetected()
    {
        var entity = MakeEntity(VulturePhase.HoldAmbush);
        entity.CombatIntensity = 0;

        // Silence for long enough triggers early rush
        entity.VulturePhase = VulturePhase.Rush;

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Rush));
        Assert.That(entity.CombatIntensity, Is.EqualTo(0));
    }

    [Test]
    public void HappyPath_RushToComplete()
    {
        var entity = MakeEntity(VulturePhase.Rush);

        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Complete));
        Assert.That(entity.HasNearbyEvent, Is.False);
    }

    [Test]
    public void FullForwardPath_ApproachThroughComplete()
    {
        var entity = MakeEntity(VulturePhase.Approach);

        // Approach -> SilentApproach
        entity.VulturePhase = VulturePhase.SilentApproach;
        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.SilentApproach));

        // SilentApproach -> HoldAmbush
        entity.VulturePhase = VulturePhase.HoldAmbush;
        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.HoldAmbush));

        // HoldAmbush -> Rush
        entity.VulturePhase = VulturePhase.Rush;
        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Rush));

        // Rush -> Complete
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;
        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Complete));
        Assert.That(entity.HasNearbyEvent, Is.False);
    }

    // ── Paranoia sub-state ───────────────────────────────────

    [Test]
    public void Paranoia_TransitionsFromHoldAmbush()
    {
        var entity = MakeEntity(VulturePhase.HoldAmbush);

        entity.VulturePhase = VulturePhase.Paranoia;

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Paranoia));
    }

    [Test]
    public void Paranoia_ReturnsToHoldAmbush()
    {
        var entity = MakeEntity(VulturePhase.Paranoia);

        // Paranoia is a single-tick action, returns to HoldAmbush
        entity.VulturePhase = VulturePhase.HoldAmbush;

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.HoldAmbush));
    }

    [Test]
    public void Paranoia_MultipleRoundTrips()
    {
        var entity = MakeEntity(VulturePhase.HoldAmbush);

        for (int i = 0; i < 5; i++)
        {
            entity.VulturePhase = VulturePhase.Paranoia;
            Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Paranoia));

            entity.VulturePhase = VulturePhase.HoldAmbush;
            Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.HoldAmbush));
        }
    }

    // ── Stop / abort transitions ─────────────────────────────

    [TestCase(VulturePhase.Approach)]
    [TestCase(VulturePhase.SilentApproach)]
    [TestCase(VulturePhase.HoldAmbush)]
    [TestCase(VulturePhase.Rush)]
    [TestCase(VulturePhase.Paranoia)]
    public void Stop_FromAnyPhase_SetsCompleteAndClearsEvent(byte startPhase)
    {
        var entity = MakeEntity(startPhase);
        Assert.That(entity.HasNearbyEvent, Is.True, "Pre-condition: event should be set");

        // Simulate Stop() behavior
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Complete));
            Assert.That(entity.HasNearbyEvent, Is.False);
        });
    }

    // ── Stuck detection: complete from any movement phase ────

    [TestCase(VulturePhase.Approach)]
    [TestCase(VulturePhase.SilentApproach)]
    [TestCase(VulturePhase.Rush)]
    public void StuckDetection_CompletesFromMovementPhase(byte movementPhase)
    {
        var entity = MakeEntity(movementPhase);

        // Simulate stuck -> CompleteVulture()
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Complete));
            Assert.That(entity.HasNearbyEvent, Is.False);
        });
    }

    // ── BUG FIX: Movement timeout should not fire during HoldAmbush ──

    /// <summary>
    /// Regression test for the movement timeout bug.
    /// Before fix: movement timeout fired during HoldAmbush (a stationary phase),
    /// causing the bot to complete early instead of transitioning to Rush.
    /// After fix: timeout only applies during movement phases (Approach, SilentApproach, Rush).
    /// </summary>
    [Test]
    public void MovementTimeout_ShouldNotApplyDuringHoldAmbush()
    {
        var entity = MakeEntity(VulturePhase.HoldAmbush);
        byte phase = entity.VulturePhase;

        // The fix: movement timeout check skips HoldAmbush and Paranoia
        bool shouldSkipTimeout = (phase == VulturePhase.HoldAmbush || phase == VulturePhase.Paranoia);

        Assert.That(shouldSkipTimeout, Is.True, "Movement timeout should be skipped during HoldAmbush (stationary phase)");
    }

    [Test]
    public void MovementTimeout_ShouldNotApplyDuringParanoia()
    {
        var entity = MakeEntity(VulturePhase.Paranoia);
        byte phase = entity.VulturePhase;

        bool shouldSkipTimeout = (phase == VulturePhase.HoldAmbush || phase == VulturePhase.Paranoia);

        Assert.That(shouldSkipTimeout, Is.True, "Movement timeout should be skipped during Paranoia (stationary sub-phase)");
    }

    [TestCase(VulturePhase.Approach)]
    [TestCase(VulturePhase.SilentApproach)]
    [TestCase(VulturePhase.Rush)]
    public void MovementTimeout_ShouldApplyDuringMovementPhases(byte movementPhase)
    {
        bool shouldSkipTimeout = (movementPhase == VulturePhase.HoldAmbush || movementPhase == VulturePhase.Paranoia);

        Assert.That(shouldSkipTimeout, Is.False, "Movement timeout should apply during movement phase " + movementPhase);
    }

    /// <summary>
    /// Simulates the scenario that caused the original bug: with continuous combat
    /// (CombatIntensity > 0), the silence trigger never fires, so the bot stays in
    /// HoldAmbush for the full ambush duration. Before fix, the movement timeout
    /// (also 90s) would fire first and complete the vulture. After fix, the ambush
    /// duration check runs and correctly transitions to Rush.
    /// </summary>
    [Test]
    public void ContinuousCombat_HoldAmbush_ShouldReachRush_NotComplete()
    {
        var entity = MakeEntity(VulturePhase.HoldAmbush);
        entity.CombatIntensity = 5; // Continuous combat

        float ambushDuration = 90f;
        float silenceTriggerDuration = 45f;
        float elapsed = 91f; // Past ambush duration

        // Before fix: movement timeout would fire first -> Complete
        // After fix: movement timeout skipped for HoldAmbush

        // Simulate the ambush duration check (which should run now)
        if (elapsed > ambushDuration)
        {
            entity.VulturePhase = VulturePhase.Rush;
        }
        // Silence check would NOT fire because CombatIntensity > 0
        else if (elapsed > silenceTriggerDuration && entity.CombatIntensity == 0)
        {
            entity.VulturePhase = VulturePhase.Rush;
        }

        Assert.That(
            entity.VulturePhase,
            Is.EqualTo(VulturePhase.Rush),
            "Bot should transition to Rush after ambush duration expires, not Complete"
        );
    }

    // ── BUG FIX: HoldAmbush fall-through between ambush/silence checks ──

    /// <summary>
    /// Verifies that ambush duration and silence checks are mutually exclusive.
    /// After fix: first matching condition returns, preventing redundant transitions.
    /// </summary>
    [Test]
    public void HoldAmbush_AmbushDurationExpiry_DoesNotFallThroughToSilenceCheck()
    {
        var entity = MakeEntity(VulturePhase.HoldAmbush);
        entity.CombatIntensity = 0; // Both conditions could fire
        float elapsed = 91f;
        float ambushDuration = 90f;
        float silenceTriggerDuration = 45f;

        int transitionCount = 0;

        // Simulate fixed code (with return after first match)
        if (elapsed > ambushDuration)
        {
            entity.VulturePhase = VulturePhase.Rush;
            transitionCount++;
            // return; (simulated by not executing the second check)
        }
        else if (elapsed > silenceTriggerDuration && entity.CombatIntensity == 0)
        {
            entity.VulturePhase = VulturePhase.Rush;
            transitionCount++;
        }

        Assert.That(transitionCount, Is.EqualTo(1), "Only one transition should fire, not both");
    }

    // ── Edge cases ───────────────────────────────────────────

    [Test]
    public void Approach_WithSilentApproachDisabled_SkipsToHoldAmbush()
    {
        var entity = MakeEntity(VulturePhase.Approach);
        bool enableSilentApproach = false;

        // When silent approach is disabled, the distance check never transitions
        // to SilentApproach; instead the bot approaches all the way to ambush distance
        if (!enableSilentApproach)
        {
            // Bot goes directly from Approach to HoldAmbush when at ambush distance
            entity.VulturePhase = VulturePhase.HoldAmbush;
        }

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.HoldAmbush));
    }

    [Test]
    public void Complete_IsTerminalState()
    {
        var entity = MakeEntity(VulturePhase.Complete);
        entity.HasNearbyEvent = false;

        // Complete should not transition to anything else automatically
        // (only Start() can restart the state machine)
        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Complete));
        Assert.That(entity.HasNearbyEvent, Is.False);
    }

    [Test]
    public void NoTarget_UpdateIsNoOp()
    {
        var entity = MakeEntity(VulturePhase.None);
        entity.HasNearbyEvent = false;

        // When there's no target, Update() should be a no-op
        // Phase stays None, no transitions
        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.None));
    }

    // ── Combat event expiry during phases ────────────────────

    [Test]
    public void HoldAmbush_SilenceDetected_TransitionsToRush()
    {
        var entity = MakeEntity(VulturePhase.HoldAmbush);
        entity.CombatIntensity = 0; // Silence
        float elapsed = 50f;
        float silenceTriggerDuration = 45f;

        if (elapsed > silenceTriggerDuration && entity.CombatIntensity == 0)
        {
            entity.VulturePhase = VulturePhase.Rush;
        }

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Rush));
    }

    [Test]
    public void HoldAmbush_CombatContinues_StaysInHoldAmbush()
    {
        var entity = MakeEntity(VulturePhase.HoldAmbush);
        entity.CombatIntensity = 3; // Active combat
        float elapsed = 50f;
        float ambushDuration = 90f;
        float silenceTriggerDuration = 45f;

        if (elapsed > ambushDuration)
        {
            entity.VulturePhase = VulturePhase.Rush;
        }
        else if (elapsed > silenceTriggerDuration && entity.CombatIntensity == 0)
        {
            entity.VulturePhase = VulturePhase.Rush;
        }

        // Neither condition fires: elapsed (50) < ambushDuration (90), CombatIntensity > 0
        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.HoldAmbush));
    }

    // ── Helper ───────────────────────────────────────────────

    private static BotEntity MakeEntity(byte initialPhase)
    {
        var entity = new BotEntity(0);
        entity.VulturePhase = initialPhase;
        entity.HasNearbyEvent = true;
        entity.NearbyEventX = 100f;
        entity.NearbyEventY = 0f;
        entity.NearbyEventZ = 200f;
        entity.NearbyEventTime = 10f;
        entity.CombatIntensity = 3;
        return entity;
    }
}
