using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS
{
    /// <summary>
    /// Phase 6 tests: BotFieldState fields embedded on BotEntity.
    /// Verifies zone movement field state can be stored/read on entities.
    /// </summary>
    [TestFixture]
    public class BotFieldStateOnEntityTests
    {
        private BotRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _registry = new BotRegistry(16);
        }

        [Test]
        public void FieldNoiseSeed_DefaultIsZero()
        {
            var entity = _registry.Add();
            Assert.That(entity.FieldNoiseSeed, Is.EqualTo(0));
        }

        [Test]
        public void FieldNoiseSeed_SetAndReadBack()
        {
            var entity = _registry.Add();
            entity.FieldNoiseSeed = 42;
            Assert.That(entity.FieldNoiseSeed, Is.EqualTo(42));
        }

        [Test]
        public void FieldNoiseSeed_DifferentPerBot()
        {
            var e1 = _registry.Add();
            e1.FieldNoiseSeed = "bot-abc".GetHashCode();
            var e2 = _registry.Add();
            e2.FieldNoiseSeed = "bot-xyz".GetHashCode();

            Assert.That(e1.FieldNoiseSeed, Is.Not.EqualTo(e2.FieldNoiseSeed));
        }

        [Test]
        public void HasFieldState_DefaultIsFalse()
        {
            var entity = _registry.Add();
            Assert.That(entity.HasFieldState, Is.False);
        }

        [Test]
        public void HasFieldState_SetToTrue()
        {
            var entity = _registry.Add();
            entity.HasFieldState = true;
            Assert.That(entity.HasFieldState, Is.True);
        }

        [Test]
        public void FieldState_ClearedOnDeactivation()
        {
            var entity = _registry.Add();
            entity.FieldNoiseSeed = 123;
            entity.HasFieldState = true;

            // Simulate deactivation
            entity.IsActive = false;
            // Manual cleanup: reset field state when entity dies
            entity.HasFieldState = false;
            entity.FieldNoiseSeed = 0;

            Assert.That(entity.HasFieldState, Is.False);
            Assert.That(entity.FieldNoiseSeed, Is.EqualTo(0));
        }

        [Test]
        public void FieldState_IndependentPerBot()
        {
            var e1 = _registry.Add();
            e1.FieldNoiseSeed = 100;
            e1.HasFieldState = true;

            var e2 = _registry.Add();
            e2.FieldNoiseSeed = 200;
            e2.HasFieldState = true;

            var e3 = _registry.Add();
            // e3 has no field state

            Assert.That(e1.FieldNoiseSeed, Is.EqualTo(100));
            Assert.That(e2.FieldNoiseSeed, Is.EqualTo(200));
            Assert.That(e3.HasFieldState, Is.False);
        }

        [Test]
        public void FieldState_SurvivesSensorUpdates()
        {
            var entity = _registry.Add();
            entity.FieldNoiseSeed = 42;
            entity.HasFieldState = true;

            // Sensor updates shouldn't affect field state
            entity.SetSensor(BotSensor.InCombat, true);
            entity.SetSensor(BotSensor.CanQuest, false);

            Assert.That(entity.FieldNoiseSeed, Is.EqualTo(42));
            Assert.That(entity.HasFieldState, Is.True);
        }

        [Test]
        public void FieldState_SurvivesBossAssignment()
        {
            var boss = _registry.Add();
            boss.FieldNoiseSeed = 10;
            boss.HasFieldState = true;

            var follower = _registry.Add();
            follower.FieldNoiseSeed = 20;
            follower.HasFieldState = true;

            HiveMindSystem.AssignBoss(follower, boss);

            // Field state is per-bot, not affected by group operations
            Assert.That(boss.FieldNoiseSeed, Is.EqualTo(10));
            Assert.That(follower.FieldNoiseSeed, Is.EqualTo(20));
        }

        [Test]
        public void FieldState_NotResetByInactiveSensorReset_ActiveEntity()
        {
            // ResetInactiveEntitySensors resets sensor bools but must NOT
            // touch field state on active entities.
            var entity = _registry.Add();
            entity.FieldNoiseSeed = 999;
            entity.HasFieldState = true;
            entity.SetSensor(BotSensor.InCombat, true);

            HiveMindSystem.ResetInactiveEntitySensors(_registry.Entities);

            // Active entity: sensors untouched, field state untouched
            Assert.That(entity.IsInCombat, Is.True);
            Assert.That(entity.FieldNoiseSeed, Is.EqualTo(999));
            Assert.That(entity.HasFieldState, Is.True);
        }

        [Test]
        public void FieldState_UntouchedByInactiveSensorReset_InactiveEntity()
        {
            // When inactive, ResetInactiveEntitySensors resets sensors to defaults
            // but FieldNoiseSeed and HasFieldState are NOT sensor fields — they
            // should remain untouched by the sensor reset system.
            var entity = _registry.Add();
            entity.FieldNoiseSeed = 42;
            entity.HasFieldState = true;
            entity.SetSensor(BotSensor.InCombat, true);

            entity.IsActive = false;
            HiveMindSystem.ResetInactiveEntitySensors(_registry.Entities);

            // Sensors reset to defaults
            Assert.That(entity.IsInCombat, Is.False);
            // Field state NOT reset by sensor system
            Assert.That(entity.FieldNoiseSeed, Is.EqualTo(42));
            Assert.That(entity.HasFieldState, Is.True);
        }

        [Test]
        public void FieldState_AfterClearAndReAdd_FreshDefaults()
        {
            var entity = _registry.Add();
            entity.FieldNoiseSeed = 77;
            entity.HasFieldState = true;

            _registry.Clear();

            // New entity after clear gets fresh defaults
            var fresh = _registry.Add();
            Assert.That(fresh.FieldNoiseSeed, Is.EqualTo(0));
            Assert.That(fresh.HasFieldState, Is.False);
        }
    }
}
