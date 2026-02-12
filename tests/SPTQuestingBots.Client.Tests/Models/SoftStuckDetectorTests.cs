using NUnit.Framework;
using SPTQuestingBots.Models;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Models;

[TestFixture]
public class SoftStuckDetectorTests
{
    private const float VaultDelay = 1.5f;
    private const float JumpDelay = 3f;
    private const float FailDelay = 6f;

    [Test]
    public void Update_FirstCall_NeverTransitions()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        bool transitioned = detector.Update(new Vector3(0, 0, 0), 0.5f, 0f);

        Assert.That(transitioned, Is.False);
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
    }

    [Test]
    public void Update_BotMovingNormally_StaysNone()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;

        // Initialize
        detector.Update(new Vector3(0, 0, 0), speed, time);

        // Bot moves 0.5 m/s * 0.1s = 0.05m per tick. Threshold is ~1.75*0.5*0.1 = 0.0875m
        // So 0.05m < 0.0875m won't clear. Let's move faster.
        // At speed=0.5, threshold = 1.75*0.5*0.1 = 0.0875
        // Move 0.1m per tick (> 0.0875)
        for (int i = 0; i < 50; i++)
        {
            time += dt;
            detector.Update(new Vector3(time, 0, 0), speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void Update_BotStationary_ProgressesToVault()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        // Initialize
        detector.Update(pos, speed, time);

        // Feed same position for 1.5s+ to reach Vaulting
        bool sawVaultTransition = false;
        for (int i = 0; i < 20; i++)
        {
            time += dt;
            if (detector.Update(pos, speed, time))
            {
                if (detector.Status == SoftStuckStatus.Vaulting)
                {
                    sawVaultTransition = true;
                }
            }
        }

        Assert.That(sawVaultTransition, Is.True, "Should have transitioned to Vaulting");
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Vaulting));
    }

    [Test]
    public void Update_BotStationary_ProgressesToJump()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, speed, time);

        bool sawJumpTransition = false;
        for (int i = 0; i < 40; i++)
        {
            time += dt;
            if (detector.Update(pos, speed, time))
            {
                if (detector.Status == SoftStuckStatus.Jumping)
                {
                    sawJumpTransition = true;
                }
            }
        }

        Assert.That(sawJumpTransition, Is.True, "Should have transitioned to Jumping");
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Jumping));
    }

    [Test]
    public void Update_BotStationary_ProgressesToFailed()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, speed, time);

        bool sawFailedTransition = false;
        for (int i = 0; i < 70; i++)
        {
            time += dt;
            if (detector.Update(pos, speed, time))
            {
                if (detector.Status == SoftStuckStatus.Failed)
                {
                    sawFailedTransition = true;
                }
            }
        }

        Assert.That(sawFailedTransition, Is.True, "Should have transitioned to Failed");
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Failed));
    }

    [Test]
    public void Update_BotMovesAfterBeingStuck_Resets()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, speed, time);

        // Get to Vaulting state
        for (int i = 0; i < 20; i++)
        {
            time += dt;
            detector.Update(pos, speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Vaulting));

        // Bot starts moving significantly
        time += dt;
        detector.Update(new Vector3(50, 0, 50), speed, time);

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void Update_ZeroSpeed_ResetsImmediately()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.5f, time);

        // Update with zero speed
        time += 0.1f;
        detector.Update(pos, 0f, time);

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void Update_LargeDeltaTime_ResetsAsDormancy()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, speed, time);

        // Get stuck
        for (int i = 0; i < 20; i++)
        {
            time += 0.1f;
            detector.Update(pos, speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Vaulting));

        // Long dormancy gap (> 0.2s stale threshold)
        time += 1.0f;
        detector.Update(pos, speed, time);

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
    }

    [Test]
    public void Update_IgnoresVerticalMovement()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;

        // Bot at (5,0,5)
        detector.Update(new Vector3(5, 0, 5), speed, time);

        // Bot jumps straight up - only Y changes, horizontal is the same
        // This should NOT count as movement, bot should still be detected as stuck
        for (int i = 0; i < 20; i++)
        {
            time += dt;
            detector.Update(new Vector3(5, i * 0.5f, 5), speed, time);
        }

        // Should have progressed to at least Vaulting since horizontal movement is zero
        Assert.That(detector.Status, Is.Not.EqualTo(SoftStuckStatus.None));
    }

    [Test]
    public void Reset_ClearsAllState()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, 0.5f, time);
        for (int i = 0; i < 20; i++)
        {
            time += 0.1f;
            detector.Update(pos, 0.5f, time);
        }

        Assert.That(detector.Status, Is.Not.EqualTo(SoftStuckStatus.None));

        detector.Reset();

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void Update_AsymmetricSpeedBuffering_SlowerSpeedUsedImmediately()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        var pos = new Vector3(5, 0, 5);

        // Initialize with high speed
        detector.Update(pos, 1.0f, time);

        // Drop to zero speed immediately - should reset (stationary)
        time += 0.1f;
        detector.Update(pos, 0.0f, time);

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
    }

    [Test]
    public void Update_TransitionReturnsTrueOnlyOnce()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, speed, time);

        int transitionCount = 0;
        for (int i = 0; i < 70; i++)
        {
            time += dt;
            if (detector.Update(pos, speed, time))
            {
                transitionCount++;
            }
        }

        // Should transition exactly 3 times: None->Vaulting, Vaulting->Jumping, Jumping->Failed
        Assert.That(transitionCount, Is.EqualTo(3));
    }

    [Test]
    public void Update_IsMovingFalse_EscalatesToVaultFaster()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        // Initialize
        detector.Update(pos, speed, time);

        // With isMoving=false and 2x multiplier, effective time accumulates at 0.2s per tick.
        // VaultDelay=1.5s should be reached in ~8 ticks (8 * 0.2 = 1.6) instead of ~16.
        int ticksToVault = 0;
        for (int i = 0; i < 20; i++)
        {
            time += dt;
            detector.Update(pos, speed, time, isMoving: false);
            ticksToVault++;
            if (detector.Status == SoftStuckStatus.Vaulting)
            {
                break;
            }
        }

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Vaulting));
        // With 2x multiplier, should reach vault in roughly half the ticks vs normal (16 ticks)
        Assert.That(ticksToVault, Is.LessThanOrEqualTo(10));
    }

    [Test]
    public void Update_IsMovingNull_PreservesExistingBehavior()
    {
        var detectorWithNull = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        var detectorWithout = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        // Initialize both
        detectorWithNull.Update(pos, speed, time);
        detectorWithout.Update(pos, speed, time);

        // Run both for same duration
        for (int i = 0; i < 20; i++)
        {
            time += dt;
            detectorWithNull.Update(pos, speed, time, isMoving: null);
            detectorWithout.Update(pos, speed, time);
        }

        Assert.That(detectorWithNull.Status, Is.EqualTo(detectorWithout.Status));
        Assert.That(detectorWithNull.Timer, Is.EqualTo(detectorWithout.Timer).Within(0.001f));
    }

    [Test]
    public void Update_IsMovingTrue_DoesNotEscalateFaster()
    {
        var detectorMoving = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        var detectorDefault = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        // Initialize both
        detectorMoving.Update(pos, speed, time);
        detectorDefault.Update(pos, speed, time);

        // Run both for same duration
        for (int i = 0; i < 20; i++)
        {
            time += dt;
            detectorMoving.Update(pos, speed, time, isMoving: true);
            detectorDefault.Update(pos, speed, time);
        }

        Assert.That(detectorMoving.Status, Is.EqualTo(detectorDefault.Status));
        Assert.That(detectorMoving.Timer, Is.EqualTo(detectorDefault.Timer).Within(0.001f));
    }
}
