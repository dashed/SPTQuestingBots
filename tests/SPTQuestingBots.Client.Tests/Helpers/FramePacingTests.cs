using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers
{
    [TestFixture]
    public class FramePacingTests
    {
        [Test]
        public void FirstCall_AlwaysReturnsTrue()
        {
            var pacing = new FramePacing(10);
            Assert.That(pacing.ShouldRun(0), Is.True);
        }

        [Test]
        public void SecondCall_BeforeInterval_ReturnsFalse()
        {
            var pacing = new FramePacing(10);
            pacing.ShouldRun(0);
            Assert.That(pacing.ShouldRun(5), Is.False);
        }

        [Test]
        public void SecondCall_AtInterval_ReturnsTrue()
        {
            var pacing = new FramePacing(10);
            pacing.ShouldRun(0);
            Assert.That(pacing.ShouldRun(10), Is.True);
        }

        [Test]
        public void SecondCall_AfterInterval_ReturnsTrue()
        {
            var pacing = new FramePacing(10);
            pacing.ShouldRun(0);
            Assert.That(pacing.ShouldRun(20), Is.True);
        }

        [Test]
        public void MultipleRuns_RespectInterval()
        {
            var pacing = new FramePacing(5);

            Assert.That(pacing.ShouldRun(0), Is.True);
            Assert.That(pacing.ShouldRun(3), Is.False);
            Assert.That(pacing.ShouldRun(5), Is.True);
            Assert.That(pacing.ShouldRun(7), Is.False);
            Assert.That(pacing.ShouldRun(10), Is.True);
        }

        [Test]
        public void Reset_AllowsImmediateRun()
        {
            var pacing = new FramePacing(100);
            pacing.ShouldRun(0);

            Assert.That(pacing.ShouldRun(10), Is.False);

            pacing.Reset();
            Assert.That(pacing.ShouldRun(10), Is.True);
        }

        [Test]
        public void IntervalFrames_ReturnsConfiguredValue()
        {
            var pacing = new FramePacing(25);
            Assert.That(pacing.IntervalFrames, Is.EqualTo(25));
        }

        [Test]
        public void StartFrame_OffsetDelaysFirstRun()
        {
            var pacing = new FramePacing(10, startFrame: 50);

            Assert.That(pacing.ShouldRun(0), Is.False);
            Assert.That(pacing.ShouldRun(49), Is.False);
            Assert.That(pacing.ShouldRun(50), Is.True);
        }

        [Test]
        public void SingleFrameInterval_RunsEveryFrame()
        {
            var pacing = new FramePacing(1);

            Assert.That(pacing.ShouldRun(0), Is.True);
            Assert.That(pacing.ShouldRun(1), Is.True);
            Assert.That(pacing.ShouldRun(2), Is.True);
        }

        [Test]
        public void ZeroInterval_RunsEveryCall()
        {
            var pacing = new FramePacing(0);

            Assert.That(pacing.ShouldRun(0), Is.True);
            Assert.That(pacing.ShouldRun(0), Is.True);
            Assert.That(pacing.ShouldRun(1), Is.True);
        }
    }
}
