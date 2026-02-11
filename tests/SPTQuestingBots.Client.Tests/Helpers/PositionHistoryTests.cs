using NUnit.Framework;
using SPTQuestingBots.Helpers;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class PositionHistoryTests
{
    [Test]
    public void GetDistanceSqr_EmptyBuffer_ReturnsZero()
    {
        var history = new PositionHistory(10);
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));
    }

    [Test]
    public void GetDistanceSqr_SingleSample_ReturnsZero()
    {
        var history = new PositionHistory(10);
        history.Update(new Vector3(1, 2, 3));
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));
    }

    [Test]
    public void GetDistanceSqr_TwoSamples_ReturnsCorrectScaledDistance()
    {
        // 10 segments -> buffer size 11. With 2 samples, scaleFactor = 10/1 = 10
        var history = new PositionHistory(10);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(1, 0, 0));

        // observedDistSqr = 1, scaleFactor = 10, result = 1 * 10 * 10 = 100
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(100f));
    }

    [Test]
    public void GetDistanceSqr_ThreeSamples_WarmupProjection()
    {
        // 4 segments -> buffer size 5. With 3 samples, scaleFactor = 4/2 = 2
        var history = new PositionHistory(4);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(1, 0, 0));
        history.Update(new Vector3(3, 0, 0));

        // oldest=0, most recent=3. observedDistSqr = 9
        // scaleFactor = 4/2 = 2, result = 9 * 2 * 2 = 36
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(36f));
    }

    [Test]
    public void GetDistanceSqr_FullBuffer_ReturnsExactDistance()
    {
        // 3 segments -> buffer size 4. Need 4 samples to fill.
        var history = new PositionHistory(3);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(1, 0, 0));
        history.Update(new Vector3(2, 0, 0));
        history.Update(new Vector3(5, 0, 0));

        // Full buffer: oldest=index 0 (0,0,0), newest=index 3 (5,0,0)
        // distSqr = 25, no scaling since buffer is full
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(25f));
    }

    [Test]
    public void GetDistanceSqr_WraparoundWorks()
    {
        // 2 segments -> buffer size 3. Fill with 3, then add 2 more.
        var history = new PositionHistory(2);

        // Fill the buffer (3 samples)
        history.Update(new Vector3(0, 0, 0)); // index 0
        history.Update(new Vector3(1, 0, 0)); // index 1
        history.Update(new Vector3(2, 0, 0)); // index 2, writeIndex wraps to 0

        // Buffer full. oldest=writeIndex=0, newest=2
        // dist = (2-0)^2 = 4
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(4f));

        // Add one more - overwrites index 0
        history.Update(new Vector3(10, 0, 0)); // index 0, writeIndex=1

        // oldest=writeIndex=1 -> (1,0,0), newest=index 0 -> (10,0,0)
        // dist = (10-1)^2 = 81
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(81f));
    }

    [Test]
    public void GetDistanceSqr_MultipleWraparounds_MaintainsCorrectness()
    {
        // 2 segments -> buffer size 3
        var history = new PositionHistory(2);

        // Wrap around multiple times
        for (int i = 0; i < 10; i++)
        {
            history.Update(new Vector3(i, 0, 0));
        }

        // After 10 updates with buffer size 3:
        // writeIndex = 10 % 3 = 1, buffer = [9, 7, 8]
        // oldest = writeIndex = 1 -> (7,0,0)
        // newest = (writeIndex-1+3)%3 = 0 -> (9,0,0)
        // distSqr = (9-7)^2 = 4
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(4f));
    }

    [Test]
    public void Reset_ClearsState()
    {
        var history = new PositionHistory(5);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(10, 0, 0));

        history.Reset();

        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));
    }

    [Test]
    public void Reset_AllowsReuseAfterReset()
    {
        var history = new PositionHistory(2);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(100, 0, 0));
        history.Reset();

        history.Update(new Vector3(0, 0, 0));
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));

        history.Update(new Vector3(3, 4, 0));
        // 2 segments, buffer size 3, 2 samples. scaleFactor = 2/1 = 2
        // distSqr = 9+16 = 25, scaled = 25 * 4 = 100
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(100f));
    }

    [Test]
    public void GetDistanceSqr_3DVector_AllAxesContribute()
    {
        // 1 segment -> buffer size 2. Full at 2 samples.
        var history = new PositionHistory(1);
        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(1, 2, 3));

        // distSqr = 1 + 4 + 9 = 14, buffer is full so no scaling
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(14f));
    }

    [Test]
    public void GetDistanceSqr_StationaryBot_ReturnsZero()
    {
        var history = new PositionHistory(5);
        var pos = new Vector3(5, 10, 15);

        for (int i = 0; i < 10; i++)
        {
            history.Update(pos);
        }

        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f));
    }
}
