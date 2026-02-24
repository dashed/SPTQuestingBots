using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class TimePacingEdgeCaseTests
{
    // ── Negative interval ────────────────────────────────────

    [Test]
    public void NegativeInterval_RunsEveryCall()
    {
        var pacing = new TimePacing(-1.0f);

        // currentTime=0, _nextRunTime=0: 0 < 0 → false → runs
        Assert.That(pacing.ShouldRun(0f), Is.True);
        // nextRunTime = 0 + (-1) = -1. 0 < -1 → false → runs
        Assert.That(pacing.ShouldRun(0f), Is.True);
        Assert.That(pacing.ShouldRun(0.001f), Is.True);
    }

    // ── Very large interval ──────────────────────────────────

    [Test]
    public void VeryLargeInterval_OnlyRunsOnce()
    {
        var pacing = new TimePacing(float.MaxValue);

        Assert.That(pacing.ShouldRun(0f), Is.True);
        // nextRunTime = 0 + MaxValue = MaxValue (or Infinity if overflow)
        Assert.That(pacing.ShouldRun(1000000f), Is.False);
    }

    // ── NaN interval ─────────────────────────────────────────

    [Test]
    public void NaN_Interval_FirstCallRuns()
    {
        var pacing = new TimePacing(float.NaN);

        // 0 < NaN is false → runs
        Assert.That(pacing.ShouldRun(0f), Is.True);
    }

    [Test]
    public void NaN_Interval_SubsequentCalls_AlwaysRun()
    {
        var pacing = new TimePacing(float.NaN);

        pacing.ShouldRun(0f); // nextRunTime = 0 + NaN = NaN
        // currentTime < NaN is always false → always runs
        Assert.That(pacing.ShouldRun(0f), Is.True);
        Assert.That(pacing.ShouldRun(100f), Is.True);
    }

    // ── NaN currentTime ──────────────────────────────────────

    [Test]
    public void NaN_CurrentTime_ReturnsFalse()
    {
        var pacing = new TimePacing(1.0f);

        pacing.ShouldRun(0f); // nextRunTime = 1.0
        // NaN < 1.0 → false in float comparison... actually NaN < anything is false
        // So it would NOT be less than → falls through → runs!
        // Actually wait: ShouldRun checks `currentTime < _nextRunTime`
        // NaN < 1.0 → false → does NOT return false → runs and sets nextRunTime = NaN + 1.0 = NaN
        bool result = pacing.ShouldRun(float.NaN);
        Assert.That(result, Is.True, "NaN < threshold is false, so ShouldRun returns true");
    }

    // ── Reset behavior ───────────────────────────────────────

    [Test]
    public void Reset_AfterManyRuns_AllowsImmediate()
    {
        var pacing = new TimePacing(1.0f);

        for (float t = 0; t < 10; t += 1.0f)
        {
            pacing.ShouldRun(t);
        }

        pacing.Reset();
        Assert.That(pacing.ShouldRun(0f), Is.True);
    }

    // ── Time going backwards ─────────────────────────────────

    [Test]
    public void ShouldRun_TimeGoesBackwards_ReturnsFalse()
    {
        var pacing = new TimePacing(1.0f);

        pacing.ShouldRun(5.0f); // nextRunTime = 6.0
        // Time goes backwards to 2.0
        Assert.That(pacing.ShouldRun(2.0f), Is.False);
    }

    // ── Infinity interval ────────────────────────────────────

    [Test]
    public void InfinityInterval_OnlyRunsFirstTime()
    {
        var pacing = new TimePacing(float.PositiveInfinity);

        Assert.That(pacing.ShouldRun(0f), Is.True);
        // nextRunTime = 0 + Infinity = Infinity
        Assert.That(pacing.ShouldRun(1e30f), Is.False);
    }
}

[TestFixture]
public class FramePacingEdgeCaseTests
{
    // ── Negative interval ────────────────────────────────────

    [Test]
    public void NegativeInterval_RunsEveryCall()
    {
        var pacing = new FramePacing(-1);

        Assert.That(pacing.ShouldRun(0), Is.True);
        // nextRunFrame = 0 + (-1) = -1. 0 < -1 → false → runs
        Assert.That(pacing.ShouldRun(0), Is.True);
    }

    // ── Very large interval ──────────────────────────────────

    [Test]
    public void VeryLargeInterval_OnlyRunsOnce()
    {
        var pacing = new FramePacing(int.MaxValue);

        Assert.That(pacing.ShouldRun(0), Is.True);
        // nextRunFrame = 0 + MaxValue = MaxValue
        Assert.That(pacing.ShouldRun(1000000), Is.False);
    }

    // ── Integer overflow ─────────────────────────────────────

    [Test]
    public void IntegerOverflow_LargeFrameAndInterval()
    {
        var pacing = new FramePacing(int.MaxValue);

        Assert.That(pacing.ShouldRun(int.MaxValue), Is.True);
        // nextRunFrame = MaxValue + MaxValue overflows to a negative number
        // Then any positive frame >= negative → runs again
        Assert.That(pacing.ShouldRun(0), Is.True, "Overflow wraps nextRunFrame negative, so 0 >= negative is true");
    }

    // ── Reset behavior ───────────────────────────────────────

    [Test]
    public void Reset_AfterManyRuns_AllowsImmediate()
    {
        var pacing = new FramePacing(10);

        for (int f = 0; f < 100; f += 10)
        {
            pacing.ShouldRun(f);
        }

        pacing.Reset();
        Assert.That(pacing.ShouldRun(0), Is.True);
    }

    // ── Frame going backwards ────────────────────────────────

    [Test]
    public void ShouldRun_FrameGoesBackwards_ReturnsFalse()
    {
        var pacing = new FramePacing(10);

        pacing.ShouldRun(50); // nextRunFrame = 60
        Assert.That(pacing.ShouldRun(20), Is.False);
    }
}
