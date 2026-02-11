using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class TimePacingTests
{
    [Test]
    public void FirstCall_AlwaysReturnsTrue()
    {
        var pacing = new TimePacing(1.0f);
        Assert.That(pacing.ShouldRun(0f), Is.True);
    }

    [Test]
    public void SecondCall_BeforeInterval_ReturnsFalse()
    {
        var pacing = new TimePacing(1.0f);
        pacing.ShouldRun(0f);
        Assert.That(pacing.ShouldRun(0.5f), Is.False);
    }

    [Test]
    public void SecondCall_AtInterval_ReturnsTrue()
    {
        var pacing = new TimePacing(1.0f);
        pacing.ShouldRun(0f);
        Assert.That(pacing.ShouldRun(1.0f), Is.True);
    }

    [Test]
    public void SecondCall_AfterInterval_ReturnsTrue()
    {
        var pacing = new TimePacing(1.0f);
        pacing.ShouldRun(0f);
        Assert.That(pacing.ShouldRun(2.0f), Is.True);
    }

    [Test]
    public void MultipleRuns_RespectInterval()
    {
        var pacing = new TimePacing(0.5f);

        Assert.That(pacing.ShouldRun(0f), Is.True);
        Assert.That(pacing.ShouldRun(0.3f), Is.False);
        Assert.That(pacing.ShouldRun(0.5f), Is.True);
        Assert.That(pacing.ShouldRun(0.7f), Is.False);
        Assert.That(pacing.ShouldRun(1.0f), Is.True);
    }

    [Test]
    public void Reset_AllowsImmediateRun()
    {
        var pacing = new TimePacing(10.0f);
        pacing.ShouldRun(0f);

        Assert.That(pacing.ShouldRun(1.0f), Is.False);

        pacing.Reset();
        Assert.That(pacing.ShouldRun(1.0f), Is.True);
    }

    [Test]
    public void IntervalSeconds_ReturnsConfiguredValue()
    {
        var pacing = new TimePacing(2.5f);
        Assert.That(pacing.IntervalSeconds, Is.EqualTo(2.5f));
    }

    [Test]
    public void StartTime_OffsetDelaysFirstRun()
    {
        var pacing = new TimePacing(1.0f, startTime: 5.0f);

        Assert.That(pacing.ShouldRun(0f), Is.False);
        Assert.That(pacing.ShouldRun(4.9f), Is.False);
        Assert.That(pacing.ShouldRun(5.0f), Is.True);
    }

    [Test]
    public void ZeroInterval_RunsEveryCall()
    {
        var pacing = new TimePacing(0f);

        Assert.That(pacing.ShouldRun(0f), Is.True);
        Assert.That(pacing.ShouldRun(0f), Is.True);
        Assert.That(pacing.ShouldRun(0.001f), Is.True);
    }
}
