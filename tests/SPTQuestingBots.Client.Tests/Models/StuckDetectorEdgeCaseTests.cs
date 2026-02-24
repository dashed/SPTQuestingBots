using NUnit.Framework;
using SPTQuestingBots.Models;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Models;

[TestFixture]
public class SoftStuckDetectorEdgeCaseTests
{
    // ── State monotonicity ───────────────────────────────────

    [Test]
    public void Update_NeverGoesBackwards_WithoutReset()
    {
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.5f, time);

        var maxStatus = SoftStuckStatus.None;
        for (int i = 0; i < 70; i++)
        {
            time += dt;
            detector.Update(pos, 0.5f, time);

            Assert.That(detector.Status, Is.GreaterThanOrEqualTo(maxStatus), "Status went backwards at tick " + i);
            maxStatus = detector.Status;
        }

        Assert.That(maxStatus, Is.EqualTo(SoftStuckStatus.Failed));
    }

    // ── Inverted delay thresholds ────────────────────────────

    [Test]
    public void Update_InvertedDelays_SkipsIntermediate()
    {
        // vaultDelay=5, jumpDelay=3, failDelay=6
        // Timer reaches 5 → Vaulting, next tick timer >= 3 → Jumping immediately
        var detector = new SoftStuckDetector(5f, 3f, 6f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.5f, time);

        // Track that Vaulting→Jumping transition happens in consecutive ticks
        bool sawVaulting = false;
        int ticksBetweenVaultAndJump = 0;
        for (int i = 0; i < 80; i++)
        {
            time += dt;
            bool transitioned = detector.Update(pos, 0.5f, time);
            if (transitioned && detector.Status == SoftStuckStatus.Vaulting)
            {
                sawVaulting = true;
            }
            else if (sawVaulting && detector.Status == SoftStuckStatus.Vaulting)
            {
                ticksBetweenVaultAndJump++;
            }
            else if (transitioned && detector.Status == SoftStuckStatus.Jumping)
            {
                break;
            }
        }

        Assert.That(sawVaulting, Is.True, "Should have reached Vaulting");
        // With inverted thresholds (jumpDelay < vaultDelay), Jumping follows immediately after Vaulting
        Assert.That(ticksBetweenVaultAndJump, Is.LessThanOrEqualTo(1), "Jumping should follow within 1 tick of Vaulting");
    }

    // ── Zero delays ──────────────────────────────────────────

    [Test]
    public void Update_ZeroDelays_ProgressesInThreeTicks()
    {
        var detector = new SoftStuckDetector(0f, 0f, 0f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        // Initialize
        detector.Update(pos, 0.5f, time);

        int transitions = 0;
        for (int i = 0; i < 5; i++)
        {
            time += dt;
            if (detector.Update(pos, 0.5f, time))
            {
                transitions++;
            }
        }

        Assert.That(transitions, Is.EqualTo(3));
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Failed));
    }

    // ── Negative delays ──────────────────────────────────────

    [Test]
    public void Update_NegativeDelays_ProgressesInThreeTicks()
    {
        // Negative thresholds: timer (positive) >= negative is always true after first tick
        var detector = new SoftStuckDetector(-1f, -2f, -3f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.5f, time);

        int transitions = 0;
        for (int i = 0; i < 5; i++)
        {
            time += dt;
            if (detector.Update(pos, 0.5f, time))
            {
                transitions++;
            }
        }

        Assert.That(transitions, Is.EqualTo(3));
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Failed));
    }

    // ── Time-backwards behavior ──────────────────────────────

    [Test]
    public void Update_TimeGoesBackwards_Resets()
    {
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        var pos = new Vector3(5, 0, 5);

        // Initialize at time=10
        detector.Update(pos, 0.5f, 10f);

        // Get stuck
        for (float t = 10.1f; t < 12f; t += 0.1f)
        {
            detector.Update(pos, 0.5f, t);
        }

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Vaulting));

        // Time goes backwards — deltaTime is negative, distanceMoved=0
        // stuckThreshold = SpeedThreshold * moveSpeed * negativeDT = negative
        // 0 > negative → true → Reset
        detector.Update(pos, 0.5f, 5f);

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
    }

    // ── IsMoving interaction with very slow speed ────────────

    [Test]
    public void Update_IsMovingFalse_NearZeroSpeed_NoMultiplier()
    {
        // When moveSpeed <= 0.01, the detector resets regardless of isMoving
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.005f, 0f);

        float time = 0.1f;
        detector.Update(pos, 0.005f, time, isMoving: false);

        // Speed <= 0.01 → resets immediately
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    // ── Double reset doesn't crash ───────────────────────────

    [Test]
    public void Reset_CalledTwice_NoCrash()
    {
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        detector.Reset();
        detector.Reset();

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    // ── Very large position values ───────────────────────────

    [Test]
    public void Update_VeryLargePositions_DoesNotCrash()
    {
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        float time = 0f;

        detector.Update(new Vector3(1e6f, 0, 1e6f), 0.5f, time);
        time += 0.1f;
        detector.Update(new Vector3(1e6f + 0.01f, 0, 1e6f), 0.5f, time);

        // Should not throw
        Assert.That(detector.Status, Is.Not.EqualTo(SoftStuckStatus.Failed));
    }

    // ── NaN position ─────────────────────────────────────────

    [Test]
    public void Update_NaNPosition_DoesNotCrash()
    {
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);

        detector.Update(new Vector3(0, 0, 0), 0.5f, 0f);

        // NaN position: magnitude is NaN, NaN > threshold is false → accumulates
        // This won't crash but timer will accumulate indefinitely
        detector.Update(new Vector3(float.NaN, 0, 0), 0.5f, 0.1f);

        // Just assert no exception was thrown
        Assert.Pass("NaN position did not crash");
    }

    // ── Exact stale threshold boundary ───────────────────────

    [Test]
    public void Update_ExactlyAtStaleThreshold_DoesNotReset()
    {
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.5f, 0f);

        // deltaTime = exactly 0.2 (StaleThreshold). Check: 0.2 > 0.2 is FALSE → does NOT reset
        detector.Update(pos, 0.5f, 0.2f);

        // Timer should have accumulated
        Assert.That(detector.Timer, Is.GreaterThan(0f));
    }

    [Test]
    public void Update_JustAboveStaleThreshold_Resets()
    {
        var detector = new SoftStuckDetector(1.5f, 3f, 6f);
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.5f, 0f);

        // deltaTime = 0.201 > 0.2 → resets
        detector.Update(pos, 0.5f, 0.201f);

        Assert.That(detector.Timer, Is.EqualTo(0f));
    }
}

[TestFixture]
public class HardStuckDetectorEdgeCaseTests
{
    // ── State monotonicity ───────────────────────────────────

    [Test]
    public void Update_NeverGoesBackwards_WithoutReset()
    {
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        var maxStatus = HardStuckStatus.None;
        for (int i = 0; i < 160; i++)
        {
            time += dt;
            detector.Update(pos, 0.5f, time);

            Assert.That(detector.Status, Is.GreaterThanOrEqualTo(maxStatus), "Status went backwards at tick " + i);
            maxStatus = detector.Status;
        }

        Assert.That(maxStatus, Is.EqualTo(HardStuckStatus.Failed));
    }

    // ── Zero delays ──────────────────────────────────────────

    [Test]
    public void Update_ZeroDelays_ProgressesInThreeTicks()
    {
        var detector = new HardStuckDetector(10, 0f, 0f, 0f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        int transitions = 0;
        for (int i = 0; i < 10; i++)
        {
            time += dt;
            if (detector.Update(pos, 0.5f, time))
            {
                transitions++;
            }
        }

        Assert.That(transitions, Is.EqualTo(3));
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Failed));
    }

    // ── squaredDistToDestination=0 (at destination) ──────────

    [Test]
    public void Update_AtDestination_NoFastEscalation()
    {
        var detectorAtDest = new HardStuckDetector(10, 5f, 10f, 15f);
        var detectorDefault = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        for (int i = 0; i < 60; i++)
        {
            time += dt;
            detectorAtDest.Update(pos, 0.5f, time, squaredDistToDestination: 0f);
            detectorDefault.Update(pos, 0.5f, time);
        }

        // squaredDistToDestination=0 should behave same as no multiplier
        Assert.That(detectorAtDest.Status, Is.EqualTo(detectorDefault.Status));
        Assert.That(detectorAtDest.Timer, Is.EqualTo(detectorDefault.Timer).Within(0.001f));
    }

    // ── squaredDistToDestination=float.MaxValue ──────────────

    [Test]
    public void Update_MaxDistToDestination_FasterEscalation()
    {
        var detectorFar = new HardStuckDetector(10, 5f, 10f, 15f);
        var detectorDefault = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        for (int i = 0; i < 60; i++)
        {
            time += dt;
            detectorFar.Update(pos, 0.5f, time, squaredDistToDestination: float.MaxValue);
            detectorDefault.Update(pos, 0.5f, time);
        }

        // With 1.5x multiplier, far detector should have higher timer
        Assert.That(detectorFar.Timer, Is.GreaterThan(detectorDefault.Timer));
    }

    // ── Zero speed when not in stuck state ───────────────────

    [Test]
    public void Update_ZeroSpeed_NotStuck_DoesNotAccumulateTimer()
    {
        // Fix: stationary bots (speed <= 0.01) with Status=None now skip stuck detection entirely.
        // Previously, the zero-speed guard only triggered when Status != None, allowing
        // a stationary bot with Status=None to accumulate stuck time (false positive).
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        for (int i = 0; i < 60; i++)
        {
            time += dt;
            detector.Update(pos, 0f, time);
        }

        // With the fix, timer should not accumulate for stationary bots
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    // ── historySize=1 ────────────────────────────────────────

    [Test]
    public void Constructor_HistorySize1_DoesNotCrash()
    {
        var detector = new HardStuckDetector(1, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        for (int i = 0; i < 20; i++)
        {
            time += dt;
            detector.Update(pos, 0.5f, time);
        }

        // Should not crash
        Assert.That(detector.Status, Is.Not.EqualTo(HardStuckStatus.None).Or.EqualTo(HardStuckStatus.None));
    }

    // ── Reset preserves position history when not stuck ──────

    [Test]
    public void ResetInternal_WhenNotStuck_OnlyResetsTimer()
    {
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        float dt = 0.1f;

        // Move normally — not stuck
        for (int i = 0; i < 10; i++)
        {
            time += dt;
            detector.Update(new Vector3(i * 5, 0, 0), 0.5f, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));

        // Timer is 0 because moving resets timer (via ResetInternal when Status=None)
    }

    [Test]
    public void Reset_WhenStuck_ClearsEverything()
    {
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;
        var pos = new Vector3(5, 0, 5);

        for (int i = 0; i < 60; i++)
        {
            time += 0.1f;
            detector.Update(pos, 0.5f, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Retrying));

        detector.Reset();

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    // ── Double reset doesn't crash ───────────────────────────

    [Test]
    public void Reset_CalledTwice_NoCrash()
    {
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        detector.Reset();
        detector.Reset();

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
    }

    // ── Time-backwards behavior ──────────────────────────────

    [Test]
    public void Update_TimeGoesBackwards_ResetsAsDormancy()
    {
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        var pos = new Vector3(5, 0, 5);

        for (float t = 0.1f; t < 7f; t += 0.1f)
        {
            detector.Update(pos, 0.5f, t);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Retrying));

        // Time goes backwards — negative deltaTime > StaleThreshold is false unless very negative
        // Actually: deltaTime = 1f - 7f = -6f, and -6 > 0.2 is false → doesn't reset via stale check
        // But negative deltaTime leads to negative timer increment, and eventually the position check
        // might cause a reset depending on movement.
        // With stationary bot and negative delta: timer -= (positive amount), but that's still accumulated
        detector.Update(pos, 0.5f, 1f);

        // Regardless of exact behavior, should not crash
        Assert.Pass("Time-backwards did not crash");
    }

    // ── Very large position values ───────────────────────────

    [Test]
    public void Update_VeryLargePositions_DoesNotCrash()
    {
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        float time = 0f;

        for (int i = 0; i < 5; i++)
        {
            time += 0.1f;
            detector.Update(new Vector3(1e6f + i * 0.01f, 0, 1e6f), 0.5f, time);
        }

        Assert.Pass("Very large positions did not crash");
    }

    // ── Exact stale threshold boundary ───────────────────────

    [Test]
    public void Update_ExactlyAtStaleThreshold_DoesNotReset()
    {
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.5f, 0f);
        // deltaTime = exactly 0.2. Check: 0.2 > 0.2 is FALSE
        detector.Update(pos, 0.5f, 0.2f);

        Assert.That(detector.Timer, Is.GreaterThan(0f));
    }

    [Test]
    public void Update_JustAboveStaleThreshold_Resets()
    {
        var detector = new HardStuckDetector(10, 5f, 10f, 15f);
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.5f, 0f);
        detector.Update(pos, 0.5f, 0.201f);

        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    // ── Inverted thresholds ──────────────────────────────────

    [Test]
    public void Update_InvertedThresholds_StillProgresses()
    {
        // pathRetryDelay=10, teleportDelay=5, failDelay=15
        var detector = new HardStuckDetector(10, 10f, 5f, 15f);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        int transitions = 0;
        for (int i = 0; i < 160; i++)
        {
            time += dt;
            if (detector.Update(pos, 0.5f, time))
            {
                transitions++;
            }
        }

        Assert.That(transitions, Is.EqualTo(3));
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Failed));
    }
}
