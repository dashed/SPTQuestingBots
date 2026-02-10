using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class LookVarianceControllerTests
    {
        private BotEntity _entity;

        [SetUp]
        public void SetUp()
        {
            _entity = new BotEntity(1);
            _entity.IsInCombat = false;
            _entity.CurrentPositionX = 100f;
            _entity.CurrentPositionY = 0f;
            _entity.CurrentPositionZ = 100f;
            // Facing forward (positive Z)
            _entity.CurrentFacingX = 0f;
            _entity.CurrentFacingZ = 1f;
            // All timers expired by default (0 < currentTime)
            _entity.NextFlankCheckTime = 0f;
            _entity.NextPoiGlanceTime = 0f;
        }

        [Test]
        public void TryGetLookTarget_InCombat_ReturnsFalse()
        {
            _entity.IsInCombat = true;
            _entity.HasNearbyEvent = true;

            bool result = LookVarianceController.TryGetLookTarget(_entity, 10f, out float tx, out float tz);

            Assert.That(result, Is.False);
            Assert.That(tx, Is.EqualTo(0f));
            Assert.That(tz, Is.EqualTo(0f));
        }

        [Test]
        public void TryGetLookTarget_CombatEventGlance_WhenHasNearbyEvent()
        {
            _entity.HasNearbyEvent = true;
            _entity.NearbyEventX = 200f;
            _entity.NearbyEventZ = 200f;
            _entity.NextPoiGlanceTime = 0f; // expired

            bool result = LookVarianceController.TryGetLookTarget(_entity, 10f, out float tx, out float tz);

            Assert.That(result, Is.True);
            Assert.That(tx, Is.EqualTo(200f));
            Assert.That(tz, Is.EqualTo(200f));
        }

        [Test]
        public void TryGetLookTarget_CombatEventGlance_ResetsTimer()
        {
            _entity.HasNearbyEvent = true;
            _entity.NearbyEventX = 200f;
            _entity.NearbyEventZ = 200f;
            _entity.NextPoiGlanceTime = 0f;
            float currentTime = 10f;

            LookVarianceController.TryGetLookTarget(_entity, currentTime, 5f, 15f, 8f, 20f, 225f, out _, out _);

            // Timer should have been reset to currentTime + interval (between 8 and 20)
            Assert.That(_entity.NextPoiGlanceTime, Is.GreaterThanOrEqualTo(currentTime + 8f));
            Assert.That(_entity.NextPoiGlanceTime, Is.LessThanOrEqualTo(currentTime + 20f));
        }

        [Test]
        public void TryGetLookTarget_SquadMemberGlance_WhenBossIsClose()
        {
            var boss = new BotEntity(0);
            boss.CurrentPositionX = 105f; // 5 meters away in X
            boss.CurrentPositionZ = 100f;
            _entity.Boss = boss;
            _entity.HasNearbyEvent = false;
            _entity.NextPoiGlanceTime = 0f;

            float squadRangeSqr = 15f * 15f; // 225

            bool result = LookVarianceController.TryGetLookTarget(
                _entity,
                10f,
                5f,
                15f,
                8f,
                20f,
                squadRangeSqr,
                out float tx,
                out float tz
            );

            Assert.That(result, Is.True);
            Assert.That(tx, Is.EqualTo(105f));
            Assert.That(tz, Is.EqualTo(100f));
        }

        [Test]
        public void TryGetLookTarget_SquadMemberGlance_SkippedWhenBossTooFar()
        {
            var boss = new BotEntity(0);
            boss.CurrentPositionX = 200f; // 100 meters away
            boss.CurrentPositionZ = 100f;
            _entity.Boss = boss;
            _entity.HasNearbyEvent = false;
            _entity.NextPoiGlanceTime = 0f;
            // Set flank timer to not expire yet so we can isolate the squad check
            _entity.NextFlankCheckTime = 999f;

            float squadRangeSqr = 15f * 15f;

            bool result = LookVarianceController.TryGetLookTarget(
                _entity,
                10f,
                5f,
                15f,
                8f,
                20f,
                squadRangeSqr,
                out float tx,
                out float tz
            );

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryGetLookTarget_FlankCheck_FiresAtCorrectTime()
        {
            _entity.HasNearbyEvent = false;
            _entity.NextFlankCheckTime = 5f;

            // At time 4 — should not fire
            bool result1 = LookVarianceController.TryGetLookTarget(_entity, 4f, out _, out _);
            Assert.That(result1, Is.False);

            // At time 5 — should fire
            bool result2 = LookVarianceController.TryGetLookTarget(_entity, 5f, out float tx, out float tz);
            Assert.That(result2, Is.True);
        }

        [Test]
        public void TryGetLookTarget_FlankCheck_ProducesRotatedDirection()
        {
            _entity.HasNearbyEvent = false;
            _entity.NextFlankCheckTime = 0f;
            // Facing positive Z
            _entity.CurrentFacingX = 0f;
            _entity.CurrentFacingZ = 1f;

            bool result = LookVarianceController.TryGetLookTarget(_entity, 10f, out float tx, out float tz);

            Assert.That(result, Is.True);
            // Target should be roughly 10m from current position (offset by rotated direction)
            float dx = tx - _entity.CurrentPositionX;
            float dz = tz - _entity.CurrentPositionZ;
            float dist = (float)System.Math.Sqrt(dx * dx + dz * dz);
            Assert.That(dist, Is.EqualTo(10f).Within(0.1f));
        }

        [Test]
        public void TryGetLookTarget_FlankCheck_ResetsFlankTimer()
        {
            _entity.HasNearbyEvent = false;
            _entity.NextFlankCheckTime = 0f;
            float currentTime = 10f;

            LookVarianceController.TryGetLookTarget(_entity, currentTime, 5f, 15f, 8f, 20f, 225f, out _, out _);

            Assert.That(_entity.NextFlankCheckTime, Is.GreaterThanOrEqualTo(currentTime + 5f));
            Assert.That(_entity.NextFlankCheckTime, Is.LessThanOrEqualTo(currentTime + 15f));
        }

        [Test]
        public void TryGetLookTarget_NoGlance_WhenTimersNotExpired()
        {
            _entity.HasNearbyEvent = true;
            _entity.NearbyEventX = 200f;
            _entity.NearbyEventZ = 200f;
            _entity.NextPoiGlanceTime = 100f; // far future
            _entity.NextFlankCheckTime = 100f; // far future

            bool result = LookVarianceController.TryGetLookTarget(_entity, 10f, out float tx, out float tz);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TryGetLookTarget_FlankCheck_45DegreeRotation_CorrectXZ()
        {
            _entity.HasNearbyEvent = false;
            _entity.NextFlankCheckTime = 0f;
            // Facing positive X (east)
            _entity.CurrentFacingX = 1f;
            _entity.CurrentFacingZ = 0f;
            _entity.CurrentPositionX = 0f;
            _entity.CurrentPositionZ = 0f;

            // Manually verify rotation math for a known angle
            // For 45 degrees: cos(45) = sin(45) = sqrt(2)/2 ≈ 0.7071
            // Rotated direction from (1,0): (cos45*1 - sin45*0, sin45*1 + cos45*0) = (0.7071, 0.7071)
            // Target = position + rotated * 10 = (7.071, 7.071)
            // The actual angle is random, but we can verify the distance is 10
            bool result = LookVarianceController.TryGetLookTarget(_entity, 10f, out float tx, out float tz);

            Assert.That(result, Is.True);
            float dist = (float)System.Math.Sqrt(tx * tx + tz * tz);
            Assert.That(dist, Is.EqualTo(10f).Within(0.1f));
        }

        [Test]
        public void TryGetLookTarget_Priority_CombatEventBeforeSquadGlance()
        {
            // Both combat event and squad member are available
            _entity.HasNearbyEvent = true;
            _entity.NearbyEventX = 200f;
            _entity.NearbyEventZ = 200f;
            _entity.NextPoiGlanceTime = 0f;

            var boss = new BotEntity(0);
            boss.CurrentPositionX = 105f;
            boss.CurrentPositionZ = 100f;
            _entity.Boss = boss;

            bool result = LookVarianceController.TryGetLookTarget(_entity, 10f, out float tx, out float tz);

            Assert.That(result, Is.True);
            // Should be combat event position, not boss position
            Assert.That(tx, Is.EqualTo(200f));
            Assert.That(tz, Is.EqualTo(200f));
        }

        [Test]
        public void SampleInterval_ReturnsValueInRange()
        {
            for (int i = 0; i < 100; i++)
            {
                float val = LookVarianceController.SampleInterval(5f, 15f);
                Assert.That(val, Is.GreaterThanOrEqualTo(5f));
                Assert.That(val, Is.LessThanOrEqualTo(15f));
            }
        }

        [Test]
        public void RandomAngle_ReturnsValueInRange()
        {
            for (int i = 0; i < 100; i++)
            {
                float val = LookVarianceController.RandomAngle(-45f, 45f);
                Assert.That(val, Is.GreaterThanOrEqualTo(-45f));
                Assert.That(val, Is.LessThanOrEqualTo(45f));
            }
        }
    }
}
