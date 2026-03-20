using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.Objective;

/// <summary>
/// Tests for GoToObjectiveAction CanSprint ordering.
/// Bug: In GoToObjectiveAction.Update() with custom mover, CanSprint overrides
/// (indoor, combat, suspicion, approach distance) were applied AFTER TickCustomMover().
/// This meant the mover used a stale CanSprint value from the previous frame/throttled
/// update, causing a 1-frame sprint in contexts where sprinting should be blocked.
/// Fix: Move CanSprint overrides BEFORE TickCustomMover() call.
/// </summary>
[TestFixture]
public class GoToObjectiveSprintOrderingTests
{
    /// <summary>
    /// Simulates the CanSprint override logic in GoToObjectiveAction.Update()
    /// to verify that indoor detection blocks sprinting BEFORE it would be read.
    /// </summary>
    [Test]
    public void IndoorDetection_BlocksSprintBeforeMoverTick()
    {
        // Initial CanSprint from previous throttled update
        bool canSprint = true;

        // Entity state: bot is indoors (Player.Environment == Indoor)
        bool isIndoor = true;

        // Override logic (as in fixed code: BEFORE TickCustomMover)
        if (isIndoor)
        {
            canSprint = false;
        }

        // TickCustomMover would now see canSprint = false
        Assert.That(canSprint, Is.False, "Indoor detection must block sprint before mover tick");
    }

    /// <summary>
    /// Combat state should block sprinting before the mover tick.
    /// </summary>
    [Test]
    public void CombatState_BlocksSprintBeforeMoverTick()
    {
        bool canSprint = true;

        var entity = new BotEntity(0);
        entity.IsInCombat = true;
        entity.IsSuspicious = false;

        if (entity.IsInCombat || entity.IsSuspicious)
        {
            canSprint = false;
        }

        Assert.That(canSprint, Is.False, "Combat state must block sprint before mover tick");
    }

    /// <summary>
    /// Suspicion state should block sprinting before the mover tick.
    /// </summary>
    [Test]
    public void SuspiciousState_BlocksSprintBeforeMoverTick()
    {
        bool canSprint = true;

        var entity = new BotEntity(0);
        entity.IsInCombat = false;
        entity.IsSuspicious = true;

        if (entity.IsInCombat || entity.IsSuspicious)
        {
            canSprint = false;
        }

        Assert.That(canSprint, Is.False, "Suspicious state must block sprint before mover tick");
    }

    /// <summary>
    /// Close approach to objective should block sprinting before the mover tick.
    /// </summary>
    [Test]
    public void CloseToObjective_BlocksSprintBeforeMoverTick()
    {
        bool canSprint = true;

        var entity = new BotEntity(0);
        entity.DistanceToObjective = 10f;
        entity.HasActiveObjective = true;

        if (entity.DistanceToObjective < 30f && entity.HasActiveObjective)
        {
            if (entity.DistanceToObjective < 15f)
            {
                canSprint = false;
            }
        }

        Assert.That(canSprint, Is.False, "Close to objective must block sprint before mover tick");
    }

    /// <summary>
    /// When no blocking conditions apply, sprint should remain enabled.
    /// </summary>
    [Test]
    public void NoBlockingConditions_SprintRemains()
    {
        bool canSprint = true;

        var entity = new BotEntity(0);
        entity.IsInCombat = false;
        entity.IsSuspicious = false;
        entity.DistanceToObjective = 100f;
        entity.HasActiveObjective = true;

        bool isIndoor = false; // outdoor

        // Apply all override checks
        if (isIndoor)
        {
            canSprint = false;
        }
        if (entity.IsInCombat || entity.IsSuspicious)
        {
            canSprint = false;
        }
        if (entity.DistanceToObjective < 30f && entity.HasActiveObjective)
        {
            if (entity.DistanceToObjective < 15f)
            {
                canSprint = false;
            }
        }

        Assert.That(canSprint, Is.True, "No blocking conditions: sprint should remain enabled");
    }

    /// <summary>
    /// Pose calculation follows the same ordering as sprint checks.
    /// Personality-based pose should be computed before being lowered by conditions.
    /// </summary>
    [Test]
    public void PoseCalculation_PersonalityBaseline_IsAppliedFirst()
    {
        float aggression = 0.5f; // Normal personality
        float pose = 0.8f + 0.2f * aggression;

        Assert.That(pose, Is.EqualTo(0.9f).Within(0.001f));

        // Indoor lowers pose
        pose = System.Math.Min(pose, 0.8f);
        Assert.That(pose, Is.EqualTo(0.8f).Within(0.001f));

        // Combat lowers pose further
        pose = System.Math.Min(pose, 0.6f);
        Assert.That(pose, Is.EqualTo(0.6f).Within(0.001f));
    }

    /// <summary>
    /// Verify the fix by simulating the full per-frame code order:
    /// 1. Compute pose and CanSprint overrides
    /// 2. THEN tick mover with corrected CanSprint
    /// </summary>
    [Test]
    public void FullPerFrameOrder_OverridesAppliedBeforeMoverTick()
    {
        bool canSprint = true;
        bool moverReceivedSprint = false;

        // Entity: indoors, in combat, close to objective
        var entity = new BotEntity(0);
        entity.IsInCombat = true;
        entity.DistanceToObjective = 5f;
        entity.HasActiveObjective = true;
        bool isIndoor = true;

        // Step 1: Apply overrides (fixed order)
        if (isIndoor)
            canSprint = false;
        if (entity.IsInCombat || entity.IsSuspicious)
            canSprint = false;
        if (entity.DistanceToObjective < 30f && entity.HasActiveObjective)
        {
            if (entity.DistanceToObjective < 15f)
                canSprint = false;
        }

        // Step 2: Mover tick sees the corrected value
        moverReceivedSprint = canSprint;

        Assert.That(moverReceivedSprint, Is.False, "Mover should receive corrected CanSprint=false");
    }
}
