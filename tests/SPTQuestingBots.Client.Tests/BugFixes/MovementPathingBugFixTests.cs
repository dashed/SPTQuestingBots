using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Models;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.BugFixes;

/// <summary>
/// Regression tests for bugs found during the movement/pathing audit.
/// Each test reproduces the bug condition and validates the fix.
/// </summary>
[TestFixture]
public class MovementPathingBugFixTests
{
    // ── BUG 1: HardStuckDetector stationary bots accumulate timer ──────

    [Test]
    public void HardStuck_StationaryBotWithNoneStatus_DoesNotAccumulateTimer()
    {
        // BUG: When moveSpeed <= 0.01 and Status == None, the guard
        // `if (moveSpeed <= 0.01f && Status != HardStuckStatus.None)` did NOT trigger,
        // allowing the code to fall through and accumulate stuck timer on a stationary bot.
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        // Initialize
        detector.Update(pos, 0f, time);

        // Feed zero speed for long enough that the old code would escalate to Retrying
        for (int i = 0; i < 100; i++)
        {
            time += dt;
            detector.Update(pos, 0f, time);
        }

        // With the fix, stationary bots should never accumulate stuck time
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void HardStuck_StationaryBotNearZeroSpeed_DoesNotAccumulateTimer()
    {
        // Edge case: speed is 0.005 (below threshold) — should not accumulate
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.005f, time);

        for (int i = 0; i < 80; i++)
        {
            time += dt;
            detector.Update(pos, 0.005f, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void HardStuck_StationaryBotWhileStuck_StillResets()
    {
        // Confirm the existing behavior: if bot is stuck and speed drops to 0, reset
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        // Get to Retrying
        for (int i = 0; i < 60; i++)
        {
            time += dt;
            detector.Update(pos, 0.5f, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Retrying));

        // Speed drops to zero
        time += dt;
        detector.Update(pos, 0f, time);

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
    }

    // ── BUG 2: BotLodCalculator division by zero with negative skip ────

    [Test]
    public void LodCalculator_NegativeReducedSkip_DoesNotCrash()
    {
        // BUG: ShouldSkipUpdate with reducedSkip=-1 caused frameCounter % 0 (DivideByZeroException).
        // Fix: clamp divisor to at least 1.
        Assert.DoesNotThrow(() =>
        {
            for (int frame = 0; frame < 10; frame++)
            {
                BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frame, -1, 4);
            }
        });
    }

    [Test]
    public void LodCalculator_NegativeMinimalSkip_DoesNotCrash()
    {
        Assert.DoesNotThrow(() =>
        {
            for (int frame = 0; frame < 10; frame++)
            {
                BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, frame, 2, -1);
            }
        });
    }

    [Test]
    public void LodCalculator_NegativeSkip_NeverSkips()
    {
        // With negative skip clamped to cycle=1, every frame should update (never skip)
        for (int frame = 0; frame < 10; frame++)
        {
            bool skipped = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frame, -5, 4);
            Assert.That(skipped, Is.False, $"Frame {frame} should not be skipped with negative reducedSkip");
        }
    }

    [Test]
    public void LodCalculator_NegativeMinimalSkip_NeverSkips()
    {
        for (int frame = 0; frame < 10; frame++)
        {
            bool skipped = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, frame, 2, -3);
            Assert.That(skipped, Is.False, $"Frame {frame} should not be skipped with negative minimalSkip");
        }
    }

    // ── BUG 3: SoftStuckDetector threshold too high ────────────────────

    [Test]
    public void SoftStuck_BotMovingAtCommandedSpeed_IsNotStuck()
    {
        // BUG: SpeedThreshold=1.75 was used as multiplier, making threshold = 1.75 * speed * dt.
        // A bot moving at exactly its commanded speed (distance = speed * dt) would always
        // appear stuck because speed*dt < 1.75*speed*dt. Fix: use 0.5 fraction instead.
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        float time = 0f;
        float dt = 0.1f;
        float speed = 3.5f; // Typical walk speed

        // Initialize
        detector.Update(new Vector3(0, 0, 0), speed, time);

        // Bot moves at full commanded speed
        for (int i = 0; i < 50; i++)
        {
            time += dt;
            float x = speed * time; // Moving at commanded speed
            detector.Update(new Vector3(x, 0, 0), speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void SoftStuck_BotMovingAt60PercentSpeed_IsNotStuck()
    {
        // A bot moving at 60% of commanded speed should NOT be stuck (threshold is 50%)
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        float time = 0f;
        float dt = 0.1f;
        float speed = 5.0f;

        detector.Update(new Vector3(0, 0, 0), speed, time);

        for (int i = 0; i < 50; i++)
        {
            time += dt;
            float x = 0.6f * speed * time; // 60% of commanded speed
            detector.Update(new Vector3(x, 0, 0), speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
    }

    [Test]
    public void SoftStuck_BotMovingAt30PercentSpeed_IsStuck()
    {
        // A bot moving at 30% of commanded speed should be stuck (threshold is 50%)
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        float time = 0f;
        float dt = 0.1f;
        float speed = 5.0f;

        detector.Update(new Vector3(0, 0, 0), speed, time);

        bool sawTransition = false;
        for (int i = 0; i < 30; i++)
        {
            time += dt;
            float x = 0.3f * speed * time; // 30% of commanded speed
            if (detector.Update(new Vector3(x, 0, 0), speed, time))
            {
                if (detector.Status == SoftStuckStatus.Vaulting)
                {
                    sawTransition = true;
                }
            }
        }

        Assert.That(sawTransition, Is.True, "Bot at 30% speed should be detected as stuck");
    }

    [Test]
    public void SoftStuck_BotCompletelyStationary_IsStuck()
    {
        // A bot not moving at all should definitely be stuck
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        float time = 0f;
        float dt = 0.1f;
        float speed = 3.5f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, speed, time);

        bool sawTransition = false;
        for (int i = 0; i < 30; i++)
        {
            time += dt;
            if (detector.Update(pos, speed, time))
            {
                if (detector.Status == SoftStuckStatus.Vaulting)
                {
                    sawTransition = true;
                }
            }
        }

        Assert.That(sawTransition, Is.True, "Completely stationary bot should be detected as stuck");
    }

    // ── BUG 4: PathDeviationForce returns unnormalized fallback ────────

    [Test]
    public void PathDeviation_BlendDegenerate_ReturnsNormalizedVector()
    {
        // BUG: When blend produces degenerate (zero) result, the fallback returned
        // the raw moveDirection without normalizing it. If the moveDirection had
        // magnitude != 1, callers expecting normalized output got wrong values.
        var moveDirection = new Vector3(10, 0, 0); // Large magnitude
        var deviation = new Vector3(-10, 0, 0); // Exactly cancels out

        var result = PathDeviationForce.BlendWithDeviation(moveDirection, deviation);

        // Result should be normalized or zero
        float mag = (float)Math.Sqrt(result.x * result.x + result.z * result.z);
        if (mag > 0.001f)
        {
            Assert.That(mag, Is.EqualTo(1f).Within(0.01f), "Non-zero result should be normalized");
        }
    }

    [Test]
    public void PathDeviation_BlendDegenerateLargeMoveDir_ReturnsUnitLength()
    {
        // moveDirection with large magnitude, zero deviation
        // The old code returned moveDirection as-is (magnitude=50), the fix normalizes it
        var moveDirection = new Vector3(30, 0, 40); // magnitude 50
        var deviation = Vector3.zero;

        var result = PathDeviationForce.BlendWithDeviation(moveDirection, deviation);

        float mag = (float)Math.Sqrt(result.x * result.x + result.z * result.z);
        // Normal path: bx=30, bz=40, len=50, should normalize to (0.6, 0, 0.8)
        Assert.That(mag, Is.EqualTo(1f).Within(0.01f));
    }

    [Test]
    public void PathDeviation_BlendBothZero_ReturnsZero()
    {
        // Both moveDirection and deviation are zero
        var result = PathDeviationForce.BlendWithDeviation(Vector3.zero, Vector3.zero);

        Assert.That(result.x, Is.EqualTo(0f).Within(0.001f));
        Assert.That(result.z, Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void PathDeviation_BlendNearCancellation_ReturnsNormalized()
    {
        // moveDirection and deviation nearly cancel but leave a tiny residual
        var moveDirection = new Vector3(1f, 0, 0);
        var deviation = new Vector3(-0.9999f, 0, 0.0001f);

        var result = PathDeviationForce.BlendWithDeviation(moveDirection, deviation);

        float mag = (float)Math.Sqrt(result.x * result.x + result.z * result.z);
        // Should be normalized (magnitude ~1) or zero if too small
        if (mag > 0.001f)
        {
            Assert.That(mag, Is.EqualTo(1f).Within(0.02f));
        }
    }

    // ── BUG 5: RoomClearController inverted duration range ─────────────

    [Test]
    public void RoomClear_DurationMaxLessThanMin_DoesNotProduceNegativeDuration()
    {
        // BUG: When durationMax < durationMin, (durationMax - durationMin) was negative,
        // producing a duration < durationMin. With fix, range is clamped to 0.
        var entity = new BotEntity(0);
        entity.LastEnvironmentId = 0; // outdoor

        // durationMax (5) < durationMin (15) — misconfigured
        var result = RoomClearController.Update(entity, true, 10f, 15f, 5f, 1.5f);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        // Duration should be exactly durationMin (range clamped to 0)
        Assert.That(entity.RoomClearUntil, Is.EqualTo(10f + 15f).Within(0.01f));
    }

    [Test]
    public void RoomClear_EqualDurations_ProducesExactDuration()
    {
        var entity = new BotEntity(0);
        entity.LastEnvironmentId = 0; // outdoor

        var result = RoomClearController.Update(entity, true, 10f, 20f, 20f, 1.5f);

        Assert.That(result, Is.EqualTo(RoomClearInstruction.SlowWalk));
        Assert.That(entity.RoomClearUntil, Is.EqualTo(30f).Within(0.01f));
    }

    [Test]
    public void RoomClear_NormalDurationRange_ProducesDurationInRange()
    {
        var entity = new BotEntity(0);
        entity.LastEnvironmentId = 0; // outdoor

        RoomClearController.Update(entity, true, 100f, 10f, 20f, 1.5f);

        // Duration should be between 10 and 20, so RoomClearUntil between 110 and 120
        Assert.That(entity.RoomClearUntil, Is.GreaterThanOrEqualTo(110f));
        Assert.That(entity.RoomClearUntil, Is.LessThanOrEqualTo(120f));
    }
}
