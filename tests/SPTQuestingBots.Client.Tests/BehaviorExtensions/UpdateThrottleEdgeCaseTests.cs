using System.Threading;
using NUnit.Framework;
using SPTQuestingBots.BehaviorExtensions;

namespace SPTQuestingBots.Client.Tests.BehaviorExtensions;

[TestFixture]
public class UpdateThrottleEdgeCaseTests
{
    // ── Zero interval ────────────────────────────────────────

    [Test]
    public void ZeroInterval_AlwaysAllowsUpdate()
    {
        var throttle = new UpdateThrottle(0);

        // Any elapsed time >= 0 passes the check
        Assert.That(throttle.CanUpdate(), Is.True);
        Assert.That(throttle.CanUpdate(), Is.True);
    }

    // ── Negative interval ────────────────────────────────────

    [Test]
    public void NegativeInterval_AlwaysAllowsUpdate()
    {
        var throttle = new UpdateThrottle(-100);

        // ElapsedMilliseconds (>= 0) is never < -100
        Assert.That(throttle.CanUpdate(), Is.True);
        Assert.That(throttle.CanUpdate(), Is.True);
    }

    // ── Very large interval ──────────────────────────────────

    [Test]
    public void VeryLargeInterval_NeverUpdates()
    {
        var throttle = new UpdateThrottle(int.MaxValue);
        Thread.Sleep(10);

        // Even after sleeping, 10ms < int.MaxValue
        Assert.That(throttle.CanUpdate(), Is.False);
    }

    // ── Pause with negative time ─────────────────────────────

    [Test]
    public void Pause_NegativeSeconds_DoesNotBlock()
    {
        var throttle = new UpdateThrottle(10);
        Thread.Sleep(15);

        // Consume initial update
        throttle.CanUpdate();

        // Pause with negative time
        throttle.Pause(-1.0f);
        Thread.Sleep(15);

        // Negative pause = _pauseTimer.Elapsed < 1000 * (-1) = -1000ms → always false → no block
        Assert.That(throttle.CanUpdate(), Is.True);
    }

    // ── Pause with zero ──────────────────────────────────────

    [Test]
    public void Pause_Zero_DoesNotBlockFuture()
    {
        var throttle = new UpdateThrottle(10);
        Thread.Sleep(15);
        throttle.CanUpdate();

        throttle.Pause(0f);
        Thread.Sleep(15);

        Assert.That(throttle.CanUpdate(), Is.True);
    }

    // ── Pause with very large value ──────────────────────────

    [Test]
    public void Pause_VeryLargeValue_BlocksIndefinitely()
    {
        var throttle = new UpdateThrottle(10);
        Thread.Sleep(15);
        throttle.CanUpdate();

        throttle.Pause(float.MaxValue);
        Thread.Sleep(15);

        Assert.That(throttle.CanUpdate(), Is.False);
    }

    // ── Multiple rapid CanUpdate calls ───────────────────────

    [Test]
    public void CanUpdate_RapidCalls_OnlyFirstSucceeds()
    {
        var throttle = new UpdateThrottle(50);
        Thread.Sleep(55);

        Assert.That(throttle.CanUpdate(), Is.True);
        Assert.That(throttle.CanUpdate(), Is.False);
        Assert.That(throttle.CanUpdate(), Is.False);
    }

    // ── Pause overwritten by second pause ────────────────────

    [Test]
    public void Pause_CalledTwice_SecondOverridesFirst()
    {
        var throttle = new UpdateThrottle(10);
        Thread.Sleep(15);
        throttle.CanUpdate();

        throttle.Pause(10.0f); // 10 second pause
        throttle.Pause(0.01f); // Override with 10ms pause
        Thread.Sleep(20);

        // Second pause of 10ms should have expired
        Assert.That(throttle.CanUpdate(), Is.True);
    }
}
