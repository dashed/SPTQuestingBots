using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class RollingAverageEdgeCaseTests
{
    // ── NaN input ────────────────────────────────────────────

    [Test]
    public void Update_NaN_CorruptsAverage()
    {
        var avg = new RollingAverage(5);
        avg.Update(1f);
        avg.Update(2f);
        avg.Update(float.NaN);

        // Once NaN enters, sum becomes NaN, Value = NaN/count = NaN
        Assert.That(float.IsNaN(avg.Value), Is.True);
    }

    [Test]
    public void Update_NaN_ThenReset_RecoversClearly()
    {
        var avg = new RollingAverage(5);
        avg.Update(float.NaN);
        Assert.That(float.IsNaN(avg.Value), Is.True);

        avg.Reset();
        avg.Update(5f);
        Assert.That(avg.Value, Is.EqualTo(5f));
    }

    // ── Infinity input ───────────────────────────────────────

    [Test]
    public void Update_PositiveInfinity_ProducesInfinity()
    {
        var avg = new RollingAverage(5);
        avg.Update(float.PositiveInfinity);
        Assert.That(float.IsPositiveInfinity(avg.Value), Is.True);
    }

    [Test]
    public void Update_MixedInfinities_ProducesNaN()
    {
        var avg = new RollingAverage(5);
        avg.Update(float.PositiveInfinity);
        avg.Update(float.NegativeInfinity);
        // Infinity + (-Infinity) = NaN
        Assert.That(float.IsNaN(avg.Value), Is.True);
    }

    // ── Very large values ────────────────────────────────────

    [Test]
    public void Update_VeryLargeValues_MaintainsAccuracy()
    {
        var avg = new RollingAverage(3);
        avg.Update(1e30f);
        avg.Update(1e30f);
        avg.Update(1e30f);

        Assert.That(avg.Value, Is.EqualTo(1e30f).Within(1e24f));
    }

    // ── Negative values ──────────────────────────────────────

    [Test]
    public void Update_NegativeValues_WorkCorrectly()
    {
        var avg = new RollingAverage(3);
        avg.Update(-10f);
        avg.Update(-20f);
        avg.Update(-30f);

        Assert.That(avg.Value, Is.EqualTo(-20f));
    }

    [Test]
    public void Update_MixedPositiveNegative_Cancels()
    {
        var avg = new RollingAverage(2);
        avg.Update(10f);
        avg.Update(-10f);

        Assert.That(avg.Value, Is.EqualTo(0f));
    }

    // ── WindowSize edge cases ────────────────────────────────

    [Test]
    public void WindowSize1_OnlyTracksLatest()
    {
        var avg = new RollingAverage(1);

        avg.Update(100f);
        Assert.That(avg.Value, Is.EqualTo(100f));

        avg.Update(200f);
        Assert.That(avg.Value, Is.EqualTo(200f));

        avg.Update(0f);
        Assert.That(avg.Value, Is.EqualTo(0f));
    }

    // ── Drift correction edge cases ──────────────────────────

    [Test]
    public void DriftCorrection_FrequentRecalc_StaysAccurate()
    {
        // recalcInterval=1 means recalculate every other update
        var avg = new RollingAverage(3, recalcInterval: 1);

        for (int i = 0; i < 100; i++)
        {
            avg.Update(i);
        }

        // Last 3: 97, 98, 99 → avg = 98
        Assert.That(avg.Value, Is.EqualTo(98f).Within(0.1f));
    }

    // ── Reset idempotency ────────────────────────────────────

    [Test]
    public void Reset_CalledMultipleTimes_NoCrash()
    {
        var avg = new RollingAverage(5);
        avg.Update(10f);
        avg.Reset();
        avg.Reset();
        avg.Reset();

        Assert.That(avg.Value, Is.EqualTo(0f));
    }

    // ── Zero values ──────────────────────────────────────────

    [Test]
    public void Update_AllZeros_ReturnsZero()
    {
        var avg = new RollingAverage(5);
        for (int i = 0; i < 10; i++)
        {
            avg.Update(0f);
        }

        Assert.That(avg.Value, Is.EqualTo(0f));
    }

    // ── Rapid eviction ───────────────────────────────────────

    [Test]
    public void Update_RapidOverwrite_CorrectAverage()
    {
        var avg = new RollingAverage(2);

        avg.Update(1f); // buffer: [1, _], count=1, avg=1
        avg.Update(2f); // buffer: [1, 2], count=2, avg=1.5
        Assert.That(avg.Value, Is.EqualTo(1.5f));

        avg.Update(3f); // evicts 1, buffer: [3, 2], count=2, avg=2.5
        Assert.That(avg.Value, Is.EqualTo(2.5f));

        avg.Update(4f); // evicts 2, buffer: [3, 4], count=2, avg=3.5
        Assert.That(avg.Value, Is.EqualTo(3.5f));
    }
}
