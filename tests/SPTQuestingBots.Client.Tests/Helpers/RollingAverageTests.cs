using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class RollingAverageTests
{
    [Test]
    public void Value_EmptyBuffer_ReturnsZero()
    {
        var avg = new RollingAverage(5);
        Assert.That(avg.Value, Is.EqualTo(0f));
    }

    [Test]
    public void Value_SingleValue_ReturnsThatValue()
    {
        var avg = new RollingAverage(5);
        avg.Update(3.5f);
        Assert.That(avg.Value, Is.EqualTo(3.5f));
    }

    [Test]
    public void Value_MultipleValues_ReturnsCorrectAverage()
    {
        var avg = new RollingAverage(5);
        avg.Update(1f);
        avg.Update(2f);
        avg.Update(3f);
        Assert.That(avg.Value, Is.EqualTo(2f));
    }

    [Test]
    public void Value_FullBuffer_ReturnsCorrectAverage()
    {
        var avg = new RollingAverage(3);
        avg.Update(1f);
        avg.Update(2f);
        avg.Update(3f);
        Assert.That(avg.Value, Is.EqualTo(2f));
    }

    [Test]
    public void Value_WindowEviction_OldestValueReplaced()
    {
        var avg = new RollingAverage(3);
        avg.Update(10f);
        avg.Update(20f);
        avg.Update(30f);
        // Buffer full: [10, 20, 30], avg = 20

        avg.Update(40f);
        // Evicts 10, buffer: [40, 20, 30], avg = 30
        Assert.That(avg.Value, Is.EqualTo(30f));

        avg.Update(50f);
        // Evicts 20, buffer: [40, 50, 30], avg = 40
        Assert.That(avg.Value, Is.EqualTo(40f));
    }

    [Test]
    public void Value_MultipleWraparounds_RemainsAccurate()
    {
        var avg = new RollingAverage(3);
        // Fill buffer twice over
        for (int i = 1; i <= 9; i++)
            avg.Update(i);

        // Last 3 values: 7, 8, 9 -> avg = 8
        Assert.That(avg.Value, Is.EqualTo(8f));
    }

    [Test]
    public void Reset_ClearsState()
    {
        var avg = new RollingAverage(5);
        avg.Update(10f);
        avg.Update(20f);
        avg.Update(30f);

        avg.Reset();

        Assert.That(avg.Value, Is.EqualTo(0f));
    }

    [Test]
    public void Reset_AllowsReuseAfterReset()
    {
        var avg = new RollingAverage(3);
        avg.Update(100f);
        avg.Update(200f);
        avg.Reset();

        avg.Update(5f);
        Assert.That(avg.Value, Is.EqualTo(5f));
    }

    [Test]
    public void DriftCorrection_ProducesSameResultAsManualSum()
    {
        // Use recalcInterval=5 so drift correction triggers frequently
        var avg = new RollingAverage(4, recalcInterval: 5);

        // Feed enough values to trigger multiple recalculations
        float[] values = { 1.1f, 2.2f, 3.3f, 4.4f, 5.5f, 6.6f, 7.7f, 8.8f, 9.9f, 1.0f, 2.0f, 3.0f, 4.0f, 5.0f, 6.0f, 7.0f };

        foreach (var v in values)
            avg.Update(v);

        // Last 4 values: 4.0, 5.0, 6.0, 7.0 -> avg = 5.5
        float expected = (4.0f + 5.0f + 6.0f + 7.0f) / 4f;
        Assert.That(avg.Value, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void Value_AccurateAfterManyUpdates()
    {
        var avg = new RollingAverage(10);

        // Push 1000 values through
        for (int i = 0; i < 1000; i++)
            avg.Update(i);

        // Last 10 values: 990..999, avg = 994.5
        float expected = (990 + 991 + 992 + 993 + 994 + 995 + 996 + 997 + 998 + 999) / 10f;
        Assert.That(avg.Value, Is.EqualTo(expected).Within(0.1f));
    }

    [Test]
    public void WindowSize_One_AlwaysReturnsLastValue()
    {
        var avg = new RollingAverage(1);
        avg.Update(5f);
        Assert.That(avg.Value, Is.EqualTo(5f));

        avg.Update(10f);
        Assert.That(avg.Value, Is.EqualTo(10f));

        avg.Update(15f);
        Assert.That(avg.Value, Is.EqualTo(15f));
    }
}
