using NUnit.Framework;
using SPTQuestingBots.Models;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Models;

[TestFixture]
public class HardStuckDetectorTests
{
    private const float PathRetryDelay = 5f;
    private const float TeleportDelay = 10f;
    private const float FailDelay = 15f;

    [Test]
    public void Update_FirstCall_NeverTransitions()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        bool transitioned = detector.Update(new Vector3(0, 0, 0), 0.5f, 0f);

        Assert.That(transitioned, Is.False);
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
    }

    [Test]
    public void Update_BotMovingEnough_StaysNone()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;

        // Move the bot far enough to exceed the stuck radius
        for (int i = 0; i < 100; i++)
        {
            time += dt;
            // Move 1m per tick, far exceeds StuckRadiusSqr * moveSpeed threshold
            detector.Update(new Vector3(i, 0, 0), speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
    }

    [Test]
    public void Update_BotStationary_ProgressesToRetrying()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        bool sawRetryTransition = false;
        for (int i = 0; i < 60; i++)
        {
            time += dt;
            if (detector.Update(pos, speed, time))
            {
                if (detector.Status == HardStuckStatus.Retrying)
                    sawRetryTransition = true;
            }
        }

        Assert.That(sawRetryTransition, Is.True, "Should have transitioned to Retrying");
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Retrying));
    }

    [Test]
    public void Update_BotStationary_ProgressesToTeleport()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        bool sawTeleportTransition = false;
        for (int i = 0; i < 110; i++)
        {
            time += dt;
            if (detector.Update(pos, speed, time))
            {
                if (detector.Status == HardStuckStatus.Teleport)
                    sawTeleportTransition = true;
            }
        }

        Assert.That(sawTeleportTransition, Is.True, "Should have transitioned to Teleport");
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Teleport));
    }

    [Test]
    public void Update_BotStationary_ProgressesToFailed()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        bool sawFailedTransition = false;
        for (int i = 0; i < 160; i++)
        {
            time += dt;
            if (detector.Update(pos, speed, time))
            {
                if (detector.Status == HardStuckStatus.Failed)
                    sawFailedTransition = true;
            }
        }

        Assert.That(sawFailedTransition, Is.True, "Should have transitioned to Failed");
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Failed));
    }

    [Test]
    public void Update_BotMovesAfterRetrying_Resets()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        // Get to Retrying state
        for (int i = 0; i < 60; i++)
        {
            time += dt;
            detector.Update(pos, speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Retrying));

        // Bot moves far away
        for (int i = 0; i < 20; i++)
        {
            time += dt;
            detector.Update(new Vector3(5 + i * 5, 0, 5), speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void Update_ZeroSpeedWhileStuck_Resets()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        var pos = new Vector3(5, 0, 5);

        // Get stuck at normal speed
        for (int i = 0; i < 60; i++)
        {
            time += dt;
            detector.Update(pos, 0.5f, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Retrying));

        // Speed drops to zero while stuck - should reset
        time += dt;
        detector.Update(pos, 0f, time);

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
    }

    [Test]
    public void Update_ZeroSpeedWhenNotStuck_DoesNotReset()
    {
        // From Phobos: "Don't bother if we are basically stationary" only resets if Status != None
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        var pos = new Vector3(5, 0, 5);

        // First update with zero speed should be fine
        detector.Update(pos, 0f, time);
        time += 0.1f;
        detector.Update(pos, 0f, time);

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
    }

    [Test]
    public void Update_LargeDeltaTime_ResetsAsDormancy()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        // Get stuck
        for (int i = 0; i < 60; i++)
        {
            time += 0.1f;
            detector.Update(pos, speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Retrying));

        // Long dormancy gap
        time += 1.0f;
        detector.Update(pos, speed, time);

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
    }

    [Test]
    public void Reset_ClearsAllState()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        var pos = new Vector3(5, 0, 5);

        for (int i = 0; i < 60; i++)
        {
            time += 0.1f;
            detector.Update(pos, 0.5f, time);
        }

        Assert.That(detector.Status, Is.Not.EqualTo(HardStuckStatus.None));

        detector.Reset();

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void Update_TransitionReturnsTrueOnlyOnce()
    {
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;
        var pos = new Vector3(5, 0, 5);

        int transitionCount = 0;
        for (int i = 0; i < 160; i++)
        {
            time += dt;
            if (detector.Update(pos, speed, time))
                transitionCount++;
        }

        // Should transition exactly 3 times: None->Retrying, Retrying->Teleport, Teleport->Failed
        Assert.That(transitionCount, Is.EqualTo(3));
    }

    [Test]
    public void Update_ResetOnlyTimerWhenNotStuck()
    {
        // When status is None, Reset only clears the timer (keeps position history warm)
        var detector = new HardStuckDetector(10, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.5f;

        // Bot moving normally
        for (int i = 0; i < 10; i++)
        {
            time += dt;
            detector.Update(new Vector3(i * 5, 0, 0), speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void Update_SpeedScalesStuckThreshold()
    {
        // A bot moving at lower speed has a lower stuck threshold (StuckRadiusSqr * moveSpeed)
        // Bot at low speed should take longer to be detected as "moving enough"
        var detector = new HardStuckDetector(5, PathRetryDelay, TeleportDelay, FailDelay);
        float time = 0f;
        float dt = 0.1f;
        float speed = 0.1f; // Low speed
        var pos = new Vector3(5, 0, 5);

        // Even with low speed, a stationary bot should eventually be detected as stuck
        for (int i = 0; i < 60; i++)
        {
            time += dt;
            detector.Update(pos, speed, time);
        }

        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Retrying));
    }
}
