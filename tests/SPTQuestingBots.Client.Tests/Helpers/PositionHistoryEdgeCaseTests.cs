using NUnit.Framework;
using SPTQuestingBots.Helpers;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class PositionHistoryEdgeCaseTests
{
    // ── segments=0 → buffer size=1 ──────────────────────────

    [Test]
    public void ZeroSegments_ClampedToOne_WorksNormally()
    {
        // segments=0 clamped to 1 → bufferSize=2, behaves like 1-segment history
        var history = new PositionHistory(0);

        history.Update(new Vector3(0, 0, 0));
        // Only 1 sample → returns 0
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));

        history.Update(new Vector3(100, 0, 0));
        // 2 samples, buffer full → distSqr = 10000
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(10000f));

        history.Update(new Vector3(200, 0, 0));
        // Buffer wraps, oldest=(100,0,0), newest=(200,0,0) → distSqr = 10000
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(10000f));
    }

    // ── NaN positions ────────────────────────────────────────

    [Test]
    public void NaN_Position_ProducesNaN_DistanceSqr()
    {
        var history = new PositionHistory(5);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(float.NaN, 0, 0));

        float result = history.GetDistanceSqr();
        Assert.That(float.IsNaN(result), Is.True);
    }

    // ── Infinity positions ───────────────────────────────────

    [Test]
    public void Infinity_Position_ProducesInfinityOrNaN()
    {
        var history = new PositionHistory(5);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(float.PositiveInfinity, 0, 0));

        float result = history.GetDistanceSqr();
        Assert.That(float.IsInfinity(result) || float.IsNaN(result), Is.True);
    }

    // ── Very large positions ─────────────────────────────────

    [Test]
    public void VeryLargePositions_DoesNotCrash()
    {
        var history = new PositionHistory(5);
        history.Update(new Vector3(1e18f, 0, 1e18f));
        history.Update(new Vector3(1e18f + 1, 0, 1e18f));

        // May lose precision at these scales but should not crash
        float result = history.GetDistanceSqr();
        Assert.That(float.IsNaN(result), Is.False);
    }

    // ── Warmup projection scaling ────────────────────────────

    [Test]
    public void WarmupProjection_TwoSamples_ScalesBySquaredFactor()
    {
        // 5 segments → buffer size 6. With 2 samples: scaleFactor = 5/1 = 5
        var history = new PositionHistory(5);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(2, 0, 0));

        // observedDistSqr = 4, scaled = 4 * 5^2 = 100
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(100f));
    }

    [Test]
    public void WarmupProjection_OneSampleShort_ScalesCorrectly()
    {
        // 3 segments → buffer size 4. With 3 samples: scaleFactor = 3/2 = 1.5
        var history = new PositionHistory(3);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(1, 0, 0));
        history.Update(new Vector3(4, 0, 0));

        // oldest=0, newest=4. observedDistSqr = 16
        // scaleFactor = 3/2 = 1.5, result = 16 * 1.5^2 = 36
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(36f));
    }

    // ── Full buffer ──────────────────────────────────────────

    [Test]
    public void FullBuffer_NoScaling()
    {
        // 2 segments → buffer size 3
        var history = new PositionHistory(2);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(1, 0, 0));
        history.Update(new Vector3(3, 0, 0)); // buffer full

        // oldest=writeIndex=0 → (0,0,0), newest=(3,0,0)
        // distSqr = 9, no scaling
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(9f));
    }

    // ── Reset followed by immediate queries ──────────────────

    [Test]
    public void Reset_ThenGetDistanceSqr_ReturnsZero()
    {
        var history = new PositionHistory(5);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(100, 0, 0));

        history.Reset();

        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));
    }

    [Test]
    public void Reset_ThenOneSample_ReturnsZero()
    {
        var history = new PositionHistory(5);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(100, 0, 0));

        history.Reset();
        history.Update(new Vector3(50, 0, 0));

        // Only 1 sample after reset → returns 0
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));
    }

    // ── Identical positions ──────────────────────────────────

    [Test]
    public void AllSamePosition_ReturnsZero()
    {
        var history = new PositionHistory(5);
        var pos = new Vector3(42, 17, -3);

        for (int i = 0; i < 20; i++)
        {
            history.Update(pos);
        }

        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));
    }

    // ── Negative positions ───────────────────────────────────

    [Test]
    public void NegativePositions_WorkCorrectly()
    {
        var history = new PositionHistory(1);
        history.Update(new Vector3(-5, -5, -5));
        history.Update(new Vector3(-8, -5, -5));

        // distSqr = (-8 - (-5))^2 = 9
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(9f));
    }

    // ── Multiple resets ──────────────────────────────────────

    [Test]
    public void MultipleResets_NoCrash()
    {
        var history = new PositionHistory(5);
        history.Reset();
        history.Reset();
        history.Update(new Vector3(0, 0, 0));
        history.Reset();

        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));
    }

    // ── Large buffer, few samples ────────────────────────────

    [Test]
    public void LargeBuffer_TwoSamples_LargeProjection()
    {
        // 100 segments → buffer size 101. With 2 samples: scaleFactor = 100/1 = 100
        var history = new PositionHistory(100);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(1, 0, 0));

        // observedDistSqr = 1, scaled = 1 * 100^2 = 10000
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(10000f));
    }
}
