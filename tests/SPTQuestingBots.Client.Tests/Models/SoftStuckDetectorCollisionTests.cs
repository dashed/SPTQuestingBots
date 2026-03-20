using NUnit.Framework;
using SPTQuestingBots.Models;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Models;

[TestFixture]
public class SoftStuckDetectorCollisionTests
{
    private const float VaultDelay = 1.5f;
    private const float JumpDelay = 3f;
    private const float FailDelay = 6f;

    [Test]
    public void Update_CollisionFlag_AcceleratesStuckTimerVsNoCollision()
    {
        // Two detectors: one with collision, one without
        var withCollision = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        var withoutCollision = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        // Initialize both
        withCollision.Update(pos, speed, 0f, null, false);
        withoutCollision.Update(pos, speed, 0f, null, false);

        // Feed identical stuck frames, but one has collision flag
        float time = 0f;
        float dt = 0.1f;
        for (int i = 0; i < 10; i++)
        {
            time += dt;
            withCollision.Update(pos, speed, time, null, hasRecentCollision: true);
            withoutCollision.Update(pos, speed, time, null, hasRecentCollision: false);
        }

        Assert.Greater(withCollision.Timer, withoutCollision.Timer, "Collision flag should accelerate stuck timer");
    }

    [Test]
    public void Update_CollisionFlagStacksWithNotMoving()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        // Initialize
        detector.Update(pos, speed, 0f, false, false);

        // Both not-moving AND collision → should be 2.0 * 1.5 = 3.0x multiplier
        float time = 0f;
        float dt = 0.1f;
        for (int i = 0; i < 5; i++)
        {
            time += dt;
            detector.Update(pos, speed, time, isMoving: false, hasRecentCollision: true);
        }

        // With 3.0x multiplier, 5 ticks * 0.1s * 3.0 = 1.5s → should have hit VaultDelay (1.5s)
        Assert.AreEqual(SoftStuckStatus.Vaulting, detector.Status, "Double multiplier should reach Vaulting faster");
    }

    [Test]
    public void Update_NoCollision_DefaultBehavior()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        // Initialize
        detector.Update(pos, speed, 0f);

        // Feed stuck frames without collision flag (backwards compatible)
        float time = 0f;
        float dt = 0.1f;
        for (int i = 0; i < 14; i++)
        {
            time += dt;
            detector.Update(pos, speed, time);
        }

        // 14 * 0.1 = 1.4s, just under VaultDelay (1.5s)
        Assert.AreEqual(SoftStuckStatus.None, detector.Status, "Should not have reached Vaulting yet");

        // One more tick pushes past 1.5s
        time += dt;
        detector.Update(pos, speed, time);
        Assert.AreEqual(SoftStuckStatus.Vaulting, detector.Status, "Should have reached Vaulting now");
    }

    [Test]
    public void Update_CollisionOnly_EscalatesFaster()
    {
        // With collision but no notMoving flag, multiplier = 1.5x
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, speed, 0f, null, false);

        float time = 0f;
        float dt = 0.1f;

        // 1.5s / (0.1 * 1.5) = 10 ticks to reach VaultDelay
        for (int i = 0; i < 9; i++)
        {
            time += dt;
            detector.Update(pos, speed, time, null, hasRecentCollision: true);
        }
        Assert.AreEqual(SoftStuckStatus.None, detector.Status, "Should not be Vaulting yet at 9 ticks");

        time += dt;
        detector.Update(pos, speed, time, null, hasRecentCollision: true);
        Assert.AreEqual(SoftStuckStatus.Vaulting, detector.Status, "Should be Vaulting at 10 ticks with collision");
    }

    [Test]
    public void Update_BackwardsCompatible_DefaultParameter()
    {
        // The new parameter has a default value, so old call sites still work
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        detector.Update(pos, speed, 0f);

        float time = 0.1f;
        bool transitioned = detector.Update(pos, speed, time, isMoving: null);
        // No crash, backwards compatible
        Assert.IsFalse(transitioned);
    }

    [Test]
    public void Update_CollisionWithMovement_ResetsProperly()
    {
        var detector = new SoftStuckDetector(VaultDelay, JumpDelay, FailDelay);
        float speed = 3.5f;
        var pos = new Vector3(0, 0, 0);

        detector.Update(pos, speed, 0f, null, true);

        // Bot moves normally despite collision flag → should reset
        float time = 0.1f;
        var movedPos = new Vector3(speed * time, 0, 0); // moved at full speed
        detector.Update(movedPos, speed, time, null, hasRecentCollision: true);

        Assert.AreEqual(SoftStuckStatus.None, detector.Status);
        Assert.AreEqual(0f, detector.Timer);
    }
}
