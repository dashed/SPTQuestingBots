using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers
{
    [TestFixture]
    public class HumanPlayerCacheTests
    {
        [SetUp]
        public void SetUp()
        {
            HumanPlayerCache.Clear();
        }

        [Test]
        public void SetPositions_SinglePlayer_SetsCountAndHasPlayers()
        {
            HumanPlayerCache.SetPositions(new float[] { 1f }, new float[] { 2f }, new float[] { 3f }, 1);

            Assert.That(HumanPlayerCache.PlayerCount, Is.EqualTo(1));
            Assert.That(HumanPlayerCache.HasPlayers, Is.True);
        }

        [Test]
        public void SetPositions_MultiplePlayers_SetsCorrectCount()
        {
            HumanPlayerCache.SetPositions(new float[] { 1f, 4f, 7f }, new float[] { 2f, 5f, 8f }, new float[] { 3f, 6f, 9f }, 3);

            Assert.That(HumanPlayerCache.PlayerCount, Is.EqualTo(3));
        }

        [Test]
        public void SetPositions_ClampsToMax()
        {
            var x = new float[10];
            var y = new float[10];
            var z = new float[10];

            HumanPlayerCache.SetPositions(x, y, z, 10);

            Assert.That(HumanPlayerCache.PlayerCount, Is.EqualTo(HumanPlayerCache.MaxPlayers));
        }

        [Test]
        public void SetPositions_NegativeCount_ClampsToZero()
        {
            HumanPlayerCache.SetPositions(new float[0], new float[0], new float[0], -5);

            Assert.That(HumanPlayerCache.PlayerCount, Is.EqualTo(0));
            Assert.That(HumanPlayerCache.HasPlayers, Is.False);
        }

        [Test]
        public void ComputeMinSqrDistance_SinglePlayer_ReturnsCorrectDistance()
        {
            // Player at (3, 0, 4), query at origin → sqr distance = 9 + 0 + 16 = 25
            HumanPlayerCache.SetPositions(new float[] { 3f }, new float[] { 0f }, new float[] { 4f }, 1);

            float result = HumanPlayerCache.ComputeMinSqrDistance(0f, 0f, 0f);

            Assert.That(result, Is.EqualTo(25f));
        }

        [Test]
        public void ComputeMinSqrDistance_ReturnsClosest()
        {
            // Players at distances squared: 100, 4, 25 from origin
            HumanPlayerCache.SetPositions(new float[] { 10f, 2f, 5f }, new float[] { 0f, 0f, 0f }, new float[] { 0f, 0f, 0f }, 3);

            float result = HumanPlayerCache.ComputeMinSqrDistance(0f, 0f, 0f);

            Assert.That(result, Is.EqualTo(4f));
        }

        [Test]
        public void ComputeMinSqrDistance_NoPlayers_ReturnsMaxValue()
        {
            float result = HumanPlayerCache.ComputeMinSqrDistance(5f, 5f, 5f);

            Assert.That(result, Is.EqualTo(float.MaxValue));
        }

        [Test]
        public void ComputeMinSqrDistance_ExactPosition_ReturnsZero()
        {
            HumanPlayerCache.SetPositions(new float[] { 7f }, new float[] { 3f }, new float[] { -2f }, 1);

            float result = HumanPlayerCache.ComputeMinSqrDistance(7f, 3f, -2f);

            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void ComputeMinSqrDistance_IncludesYAxis()
        {
            // Player at (0, 10, 0), query at (0, 0, 0) → sqr distance = 100
            HumanPlayerCache.SetPositions(new float[] { 0f }, new float[] { 10f }, new float[] { 0f }, 1);

            float result = HumanPlayerCache.ComputeMinSqrDistance(0f, 0f, 0f);

            Assert.That(result, Is.EqualTo(100f));
        }

        [Test]
        public void Clear_ResetsState()
        {
            HumanPlayerCache.SetPositions(new float[] { 1f, 2f }, new float[] { 1f, 2f }, new float[] { 1f, 2f }, 2);

            HumanPlayerCache.Clear();

            Assert.That(HumanPlayerCache.PlayerCount, Is.EqualTo(0));
            Assert.That(HumanPlayerCache.HasPlayers, Is.False);
            Assert.That(HumanPlayerCache.ComputeMinSqrDistance(0f, 0f, 0f), Is.EqualTo(float.MaxValue));
        }

        [Test]
        public void SetPositions_OverwritesPrevious_OldDataDoesNotLeak()
        {
            // First: 3 players, one close at distance 1
            HumanPlayerCache.SetPositions(new float[] { 1f, 100f, 200f }, new float[] { 0f, 0f, 0f }, new float[] { 0f, 0f, 0f }, 3);

            // Second: 1 player far away — old close player should not persist
            HumanPlayerCache.SetPositions(new float[] { 50f }, new float[] { 0f }, new float[] { 0f }, 1);

            Assert.That(HumanPlayerCache.PlayerCount, Is.EqualTo(1));

            float result = HumanPlayerCache.ComputeMinSqrDistance(0f, 0f, 0f);

            Assert.That(result, Is.EqualTo(2500f));
        }
    }
}
