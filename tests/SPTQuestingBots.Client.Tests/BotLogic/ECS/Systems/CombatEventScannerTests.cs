using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class CombatEventScannerTests
    {
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

        // ── UpdateEntity ──────────────────────────────────────

        [Test]
        public void UpdateEntity_NoEvents_HasNearbyEventFalse()
        {
            var entity = MakeEntity(10f, 10f);
            CombatEventScanner.UpdateEntity(entity, 1f, 300f, 150f, 150f, 15f, 75f, 120f);

            Assert.IsFalse(entity.HasNearbyEvent);
            Assert.That(entity.CombatIntensity, Is.EqualTo(0));
        }

        [Test]
        public void UpdateEntity_EventInRange_HasNearbyEventTrue()
        {
            CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1f, 100f, CombatEventType.Gunshot, false);
            var entity = MakeEntity(10f, 10f);
            CombatEventScanner.UpdateEntity(entity, 2f, 300f, 150f, 150f, 15f, 75f, 120f);

            Assert.IsTrue(entity.HasNearbyEvent);
            Assert.That(entity.NearbyEventX, Is.EqualTo(50f));
            Assert.That(entity.NearbyEventZ, Is.EqualTo(50f));
            Assert.That(entity.NearbyEventTime, Is.EqualTo(1f));
        }

        [Test]
        public void UpdateEntity_EventOutOfRange_HasNearbyEventFalse()
        {
            CombatEventRegistry.RecordEvent(500f, 0f, 500f, 1f, 100f, CombatEventType.Gunshot, false);
            var entity = MakeEntity(0f, 0f);
            CombatEventScanner.UpdateEntity(entity, 2f, 300f, 150f, 150f, 15f, 75f, 120f);

            Assert.IsFalse(entity.HasNearbyEvent);
        }

        [Test]
        public void UpdateEntity_IntensityComputed_ForNearestEventPosition()
        {
            // Multiple events near (50, 50)
            CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1f, 100f, CombatEventType.Gunshot, false);
            CombatEventRegistry.RecordEvent(52f, 0f, 52f, 2f, 100f, CombatEventType.Gunshot, false);
            CombatEventRegistry.RecordEvent(48f, 0f, 48f, 3f, 100f, CombatEventType.Gunshot, false);
            var entity = MakeEntity(10f, 10f);
            CombatEventScanner.UpdateEntity(entity, 4f, 300f, 150f, 150f, 15f, 75f, 120f);

            Assert.IsTrue(entity.HasNearbyEvent);
            Assert.That(entity.CombatIntensity, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void UpdateEntity_ExplosionIntensity_HigherThanGunshot()
        {
            CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1f, 150f, CombatEventType.Explosion, false);
            var entity = MakeEntity(10f, 10f);
            CombatEventScanner.UpdateEntity(entity, 2f, 300f, 150f, 150f, 15f, 75f, 120f);

            Assert.That(entity.CombatIntensity, Is.EqualTo(3)); // 1 base + 2 explosion bonus
        }

        [Test]
        public void UpdateEntity_BossEvent_SetsIsInBossZone()
        {
            CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1f, 100f, CombatEventType.Gunshot, true);
            var entity = MakeEntity(60f, 60f);
            CombatEventScanner.UpdateEntity(entity, 2f, 300f, 150f, 150f, 15f, 75f, 120f);

            Assert.IsTrue(entity.IsInBossZone);
        }

        [Test]
        public void UpdateEntity_NoBossEvent_IsInBossZoneFalse()
        {
            CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1f, 100f, CombatEventType.Gunshot, false);
            var entity = MakeEntity(60f, 60f);
            CombatEventScanner.UpdateEntity(entity, 2f, 300f, 150f, 150f, 15f, 75f, 120f);

            Assert.IsFalse(entity.IsInBossZone);
        }

        [Test]
        public void UpdateEntity_EventCleared_ResetsPreviousState()
        {
            CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1f, 100f, CombatEventType.Gunshot, false);
            var entity = MakeEntity(10f, 10f);
            CombatEventScanner.UpdateEntity(entity, 2f, 300f, 150f, 150f, 15f, 75f, 120f);
            Assert.IsTrue(entity.HasNearbyEvent);

            // Now clear events and re-scan
            CombatEventRegistry.Clear();
            CombatEventScanner.UpdateEntity(entity, 3f, 300f, 150f, 150f, 15f, 75f, 120f);
            Assert.IsFalse(entity.HasNearbyEvent);
            Assert.That(entity.CombatIntensity, Is.EqualTo(0));
            Assert.That(entity.NearbyEventX, Is.EqualTo(0f));
        }

        // ── UpdateEntities (batch) ────────────────────────────

        [Test]
        public void UpdateEntities_MultipleEntities_EachGetsResults()
        {
            CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1f, 100f, CombatEventType.Gunshot, false);

            var entities = new List<BotEntity>
            {
                MakeEntity(10f, 10f),
                MakeEntity(500f, 500f), // Out of range
                MakeEntity(40f, 40f),
            };

            CombatEventScanner.UpdateEntities(entities, 2f, 300f, 150f, 150f, 15f, 75f, 120f);

            Assert.IsTrue(entities[0].HasNearbyEvent);
            Assert.IsFalse(entities[1].HasNearbyEvent);
            Assert.IsTrue(entities[2].HasNearbyEvent);
        }

        [Test]
        public void UpdateEntities_InactiveEntity_Skipped()
        {
            CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1f, 100f, CombatEventType.Gunshot, false);

            var entity = MakeEntity(10f, 10f);
            entity.IsActive = false;
            var entities = new List<BotEntity> { entity };

            CombatEventScanner.UpdateEntities(entities, 2f, 300f, 150f, 150f, 15f, 75f, 120f);

            Assert.IsFalse(entity.HasNearbyEvent);
        }

        [Test]
        public void UpdateEntities_EmptyList_NoCrash()
        {
            var entities = new List<BotEntity>();
            Assert.DoesNotThrow(() => CombatEventScanner.UpdateEntities(entities, 2f, 300f, 150f, 150f, 15f, 75f, 120f));
        }

        [Test]
        public void UpdateEntity_ExpiredEvent_NotDetected()
        {
            CombatEventRegistry.RecordEvent(50f, 0f, 50f, 1f, 100f, CombatEventType.Gunshot, false);
            var entity = MakeEntity(10f, 10f);
            // maxEventAge=5, currentTime=100 → event age is 99, expired
            CombatEventScanner.UpdateEntity(entity, 100f, 5f, 150f, 150f, 15f, 75f, 120f);

            Assert.IsFalse(entity.HasNearbyEvent);
        }

        // ── Helper ────────────────────────────────────────────

        private static BotEntity MakeEntity(float x, float z)
        {
            var entity = new BotEntity(0);
            entity.IsActive = true;
            entity.CurrentPositionX = x;
            entity.CurrentPositionZ = z;
            return entity;
        }
    }
}
