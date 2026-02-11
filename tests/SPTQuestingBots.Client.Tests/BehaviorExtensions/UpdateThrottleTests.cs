using System.Threading;
using NUnit.Framework;
using SPTQuestingBots.BehaviorExtensions;

namespace SPTQuestingBots.Client.Tests.BehaviorExtensions;

[TestFixture]
public class UpdateThrottleTests
{
    [Test]
    public void DefaultInterval_Is100Ms()
    {
        Assert.That(UpdateThrottle.DefaultIntervalMs, Is.EqualTo(100));
    }

    [Test]
    public void Constructor_Default_UsesDefaultInterval()
    {
        var throttle = new UpdateThrottle();

        Assert.That(throttle.IntervalMs, Is.EqualTo(UpdateThrottle.DefaultIntervalMs));
    }

    [Test]
    public void Constructor_CustomInterval_StoresValue()
    {
        var throttle = new UpdateThrottle(250);

        Assert.That(throttle.IntervalMs, Is.EqualTo(250));
    }

    [Test]
    public void TwoInstances_HaveIndependentIntervals()
    {
        var fast = new UpdateThrottle(25);
        var slow = new UpdateThrottle(250);

        Assert.Multiple(() =>
        {
            Assert.That(fast.IntervalMs, Is.EqualTo(25));
            Assert.That(slow.IntervalMs, Is.EqualTo(250));
        });
    }

    [Test]
    public void CanUpdate_ReturnsTrueOnFirstCall_AfterIntervalElapsed()
    {
        // Stopwatch starts on construction, so after waiting the interval it should be ready
        var throttle = new UpdateThrottle(10);
        Thread.Sleep(15);

        Assert.That(throttle.CanUpdate(), Is.True);
    }

    [Test]
    public void CanUpdate_ReturnsFalseImmediatelyAfterUpdate()
    {
        var throttle = new UpdateThrottle(50);
        Thread.Sleep(55);

        // First call succeeds and resets the timer
        throttle.CanUpdate();

        // Immediate second call should fail (< 50ms have passed)
        Assert.That(throttle.CanUpdate(), Is.False);
    }

    [Test]
    public void CanUpdate_ReturnsTrueAgainAfterIntervalElapses()
    {
        var throttle = new UpdateThrottle(10);
        Thread.Sleep(15);

        throttle.CanUpdate(); // consume
        Thread.Sleep(15);

        Assert.That(throttle.CanUpdate(), Is.True);
    }

    [Test]
    public void Pause_BlocksUpdatesForDuration()
    {
        var throttle = new UpdateThrottle(10);
        Thread.Sleep(15);

        // Consume initial update
        throttle.CanUpdate();

        // Pause for 100ms — even though the 10ms interval elapses, pause blocks
        throttle.Pause(0.1f);
        Thread.Sleep(15); // past interval but not past pause

        Assert.That(throttle.CanUpdate(), Is.False);
    }

    [Test]
    public void Pause_AllowsUpdatesAfterDuration()
    {
        var throttle = new UpdateThrottle(10);
        Thread.Sleep(15);

        throttle.CanUpdate();

        throttle.Pause(0.01f); // 10ms pause
        Thread.Sleep(20); // past both interval and pause

        Assert.That(throttle.CanUpdate(), Is.True);
    }

    [Test]
    public void Pause_WithZero_DoesNotBlockBeyondInterval()
    {
        var throttle = new UpdateThrottle(10);
        Thread.Sleep(15);

        throttle.CanUpdate();

        throttle.Pause(0f); // zero-second pause
        Thread.Sleep(15); // past interval

        Assert.That(throttle.CanUpdate(), Is.True);
    }

    [Test]
    public void MultipleInstances_UpdateIndependently()
    {
        var fast = new UpdateThrottle(10);
        var slow = new UpdateThrottle(200);

        Thread.Sleep(15);

        // Fast should be ready, slow should not (only 15ms elapsed of 200ms)
        Assert.Multiple(() =>
        {
            Assert.That(fast.CanUpdate(), Is.True);
            Assert.That(slow.CanUpdate(), Is.False);
        });
    }

    [Test]
    public void Pause_OnOneInstance_DoesNotAffectAnother()
    {
        var a = new UpdateThrottle(10);
        var b = new UpdateThrottle(10);

        Thread.Sleep(15);

        a.CanUpdate();
        b.CanUpdate();

        // Pause only 'a'
        a.Pause(1.0f);

        Thread.Sleep(15);

        Assert.Multiple(() =>
        {
            Assert.That(a.CanUpdate(), Is.False, "Paused instance should not update");
            Assert.That(b.CanUpdate(), Is.True, "Non-paused instance should update");
        });
    }
}
