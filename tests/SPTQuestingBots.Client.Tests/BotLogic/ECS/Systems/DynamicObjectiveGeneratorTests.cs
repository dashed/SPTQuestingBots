using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class DynamicObjectiveGeneratorTests
    {
        // ── ClusterEvents ───────────────────────────────────────────

        [Test]
        public void ClusterEvents_NullInput_ReturnsZero()
        {
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(null, 0, 10f, 120f, 2500f, output, 10);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void ClusterEvents_EmptyInput_ReturnsZero()
        {
            var events = new CombatEvent[0];
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 0, 10f, 120f, 2500f, output, 10);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void ClusterEvents_SingleGunshot_OneCluster()
        {
            var events = new[] { MakeEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot) };
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 1, 10f, 120f, 2500f, output, 10);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(100f));
            Assert.That(output[0].Z, Is.EqualTo(200f));
            Assert.That(output[0].Intensity, Is.EqualTo(1));
        }

        [Test]
        public void ClusterEvents_TwoCloseEvents_MergedIntoOneCluster()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot),
                MakeEvent(110f, 0f, 210f, 6f, 100f, CombatEventType.Gunshot),
            };
            float clusterRadiusSqr = 50f * 50f; // 2500
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 2, 10f, 120f, clusterRadiusSqr, output, 10);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(105f).Within(0.1f)); // Average of 100 and 110
            Assert.That(output[0].Z, Is.EqualTo(205f).Within(0.1f)); // Average of 200 and 210
            Assert.That(output[0].Intensity, Is.EqualTo(2));
        }

        [Test]
        public void ClusterEvents_TwoFarEvents_TwoClusters()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot),
                MakeEvent(500f, 0f, 600f, 6f, 100f, CombatEventType.Gunshot),
            };
            float clusterRadiusSqr = 50f * 50f;
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 2, 10f, 120f, clusterRadiusSqr, output, 10);

            Assert.That(count, Is.EqualTo(2));
            Assert.That(output[0].X, Is.EqualTo(100f));
            Assert.That(output[1].X, Is.EqualTo(500f));
        }

        [Test]
        public void ClusterEvents_ExplosionCountsAsThreeIntensity()
        {
            var events = new[] { MakeEvent(100f, 0f, 200f, 5f, 150f, CombatEventType.Explosion) };
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 1, 10f, 120f, 2500f, output, 10);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].Intensity, Is.EqualTo(3)); // 1 base + 2 explosion bonus
        }

        [Test]
        public void ClusterEvents_MixedExplosionAndGunshots_CorrectIntensity()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot),
                MakeEvent(110f, 0f, 210f, 6f, 150f, CombatEventType.Explosion),
                MakeEvent(105f, 0f, 205f, 7f, 100f, CombatEventType.Gunshot),
            };
            float clusterRadiusSqr = 50f * 50f;
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 3, 10f, 120f, clusterRadiusSqr, output, 10);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].Intensity, Is.EqualTo(5)); // 1 + 3 + 1
        }

        [Test]
        public void ClusterEvents_ExpiredEvents_Skipped()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 1f, 100f, CombatEventType.Gunshot), // age = 99, expired
                MakeEvent(300f, 0f, 400f, 95f, 100f, CombatEventType.Gunshot), // age = 5, fresh
            };
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 2, 100f, 30f, 2500f, output, 10);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(300f));
        }

        [Test]
        public void ClusterEvents_InactiveEvents_Skipped()
        {
            var events = new[]
            {
                new CombatEvent
                {
                    X = 100f,
                    Y = 0f,
                    Z = 200f,
                    Time = 5f,
                    Power = 100f,
                    Type = CombatEventType.Gunshot,
                    IsActive = false,
                },
                MakeEvent(300f, 0f, 400f, 6f, 100f, CombatEventType.Gunshot),
            };
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 2, 10f, 120f, 2500f, output, 10);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(300f));
        }

        [Test]
        public void ClusterEvents_DeathEvents_Excluded()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 5f, 50f, CombatEventType.Death),
                MakeEvent(300f, 0f, 400f, 6f, 100f, CombatEventType.Gunshot),
            };
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 2, 10f, 120f, 2500f, output, 10);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(300f));
        }

        [Test]
        public void ClusterEvents_MaxClustersRespected()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 100f, 5f, 100f, CombatEventType.Gunshot),
                MakeEvent(500f, 0f, 500f, 6f, 100f, CombatEventType.Gunshot),
                MakeEvent(900f, 0f, 900f, 7f, 100f, CombatEventType.Gunshot),
            };
            float clusterRadiusSqr = 50f * 50f;
            var output = new CombatEventClustering.ClusterResult[2]; // Only room for 2
            int count = CombatEventClustering.ClusterEvents(events, 3, 10f, 120f, clusterRadiusSqr, output, 2);

            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void ClusterEvents_YPositionAveraged()
        {
            var events = new[]
            {
                MakeEvent(100f, 10f, 200f, 5f, 100f, CombatEventType.Gunshot),
                MakeEvent(110f, 20f, 210f, 6f, 100f, CombatEventType.Gunshot),
            };
            float clusterRadiusSqr = 50f * 50f;
            var output = new CombatEventClustering.ClusterResult[10];
            int count = CombatEventClustering.ClusterEvents(events, 2, 10f, 120f, clusterRadiusSqr, output, 10);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].Y, Is.EqualTo(15f).Within(0.1f)); // Average of 10 and 20
        }

        // ── FilterDeathEvents ───────────────────────────────────────

        [Test]
        public void FilterDeathEvents_NullInput_ReturnsZero()
        {
            var output = new CombatEvent[10];
            int count = CombatEventClustering.FilterDeathEvents(null, 0, 10f, 120f, output);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void FilterDeathEvents_NoDeathEvents_ReturnsZero()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot),
                MakeEvent(300f, 0f, 400f, 6f, 150f, CombatEventType.Explosion),
            };
            var output = new CombatEvent[10];
            int count = CombatEventClustering.FilterDeathEvents(events, 2, 10f, 120f, output);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void FilterDeathEvents_OneDeathEvent_ReturnsOne()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot),
                MakeEvent(300f, 5f, 400f, 6f, 50f, CombatEventType.Death),
            };
            var output = new CombatEvent[10];
            int count = CombatEventClustering.FilterDeathEvents(events, 2, 10f, 120f, output);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(300f));
            Assert.That(output[0].Y, Is.EqualTo(5f));
            Assert.That(output[0].Z, Is.EqualTo(400f));
            Assert.That(output[0].Type, Is.EqualTo(CombatEventType.Death));
        }

        [Test]
        public void FilterDeathEvents_MultipleDeaths_ReturnsAll()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 5f, 50f, CombatEventType.Death),
                MakeEvent(300f, 0f, 400f, 6f, 50f, CombatEventType.Death),
                MakeEvent(500f, 0f, 600f, 7f, 50f, CombatEventType.Death),
            };
            var output = new CombatEvent[10];
            int count = CombatEventClustering.FilterDeathEvents(events, 3, 10f, 120f, output);
            Assert.That(count, Is.EqualTo(3));
        }

        [Test]
        public void FilterDeathEvents_ExpiredDeaths_Excluded()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 1f, 50f, CombatEventType.Death), // age = 99, expired
                MakeEvent(300f, 0f, 400f, 95f, 50f, CombatEventType.Death), // age = 5, fresh
            };
            var output = new CombatEvent[10];
            int count = CombatEventClustering.FilterDeathEvents(events, 2, 100f, 30f, output);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(300f));
        }

        [Test]
        public void FilterDeathEvents_InactiveDeaths_Excluded()
        {
            var events = new[]
            {
                new CombatEvent
                {
                    X = 100f,
                    Y = 0f,
                    Z = 200f,
                    Time = 5f,
                    Power = 50f,
                    Type = CombatEventType.Death,
                    IsActive = false,
                },
                MakeEvent(300f, 0f, 400f, 6f, 50f, CombatEventType.Death),
            };
            var output = new CombatEvent[10];
            int count = CombatEventClustering.FilterDeathEvents(events, 2, 10f, 120f, output);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(output[0].X, Is.EqualTo(300f));
        }

        [Test]
        public void FilterDeathEvents_OutputBufferLimit_Respected()
        {
            var events = new[]
            {
                MakeEvent(100f, 0f, 200f, 5f, 50f, CombatEventType.Death),
                MakeEvent(300f, 0f, 400f, 6f, 50f, CombatEventType.Death),
                MakeEvent(500f, 0f, 600f, 7f, 50f, CombatEventType.Death),
            };
            var output = new CombatEvent[2]; // Only room for 2
            int count = CombatEventClustering.FilterDeathEvents(events, 3, 10f, 120f, output);
            Assert.That(count, Is.EqualTo(2));
        }

        // ── GatherActiveEvents (CombatEventRegistry) ────────────────

        [SetUp]
        public void SetUp()
        {
            CombatEventRegistry.Initialize(128);
        }

        [TearDown]
        public void TearDown()
        {
            CombatEventRegistry.Clear();
        }

        [Test]
        public void GatherActiveEvents_EmptyRegistry_ReturnsZero()
        {
            var buffer = new CombatEvent[128];
            int count = CombatEventRegistry.GatherActiveEvents(buffer, 10f, 120f);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void GatherActiveEvents_ActiveEvents_ReturnsAll()
        {
            CombatEventRegistry.RecordEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot, false);
            CombatEventRegistry.RecordEvent(300f, 0f, 400f, 6f, 50f, CombatEventType.Death, false);

            var buffer = new CombatEvent[128];
            int count = CombatEventRegistry.GatherActiveEvents(buffer, 10f, 120f);
            Assert.That(count, Is.EqualTo(2));
        }

        [Test]
        public void GatherActiveEvents_ExpiredEvents_Excluded()
        {
            CombatEventRegistry.RecordEvent(100f, 0f, 200f, 1f, 100f, CombatEventType.Gunshot, false);
            CombatEventRegistry.RecordEvent(300f, 0f, 400f, 95f, 100f, CombatEventType.Gunshot, false);

            var buffer = new CombatEvent[128];
            int count = CombatEventRegistry.GatherActiveEvents(buffer, 100f, 30f);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(buffer[0].X, Is.EqualTo(300f));
        }

        [Test]
        public void GatherActiveEvents_NullBuffer_ReturnsZero()
        {
            CombatEventRegistry.RecordEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot, false);
            int count = CombatEventRegistry.GatherActiveEvents(null, 10f, 120f);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void GatherActiveEvents_ZeroMaxAge_ReturnsZero()
        {
            CombatEventRegistry.RecordEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot, false);
            var buffer = new CombatEvent[128];
            int count = CombatEventRegistry.GatherActiveEvents(buffer, 10f, 0f);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void GatherActiveEvents_InactiveEvents_Excluded()
        {
            CombatEventRegistry.RecordEvent(100f, 0f, 200f, 5f, 100f, CombatEventType.Gunshot, false);
            CombatEventRegistry.CleanupExpired(1000f, 5f); // Expire event

            var buffer = new CombatEvent[128];
            int count = CombatEventRegistry.GatherActiveEvents(buffer, 1000f, 5000f);
            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void GatherActiveEvents_IncludesDeathEvents()
        {
            CombatEventRegistry.RecordEvent(100f, 0f, 200f, 5f, 50f, CombatEventType.Death, false);

            var buffer = new CombatEvent[128];
            int count = CombatEventRegistry.GatherActiveEvents(buffer, 10f, 120f);
            Assert.That(count, Is.EqualTo(1));
            Assert.That(buffer[0].Type, Is.EqualTo(CombatEventType.Death));
        }

        // ── CombatEventType.Death constant ──────────────────────────

        [Test]
        public void CombatEventType_Death_HasExpectedValue()
        {
            Assert.That(CombatEventType.Death, Is.EqualTo((byte)4));
        }

        // ── Helper ──────────────────────────────────────────────────

        private static CombatEvent MakeEvent(float x, float y, float z, float time, float power, byte type)
        {
            return new CombatEvent
            {
                X = x,
                Y = y,
                Z = z,
                Time = time,
                Power = power,
                Type = type,
                IsBoss = false,
                IsActive = true,
            };
        }
    }
}
