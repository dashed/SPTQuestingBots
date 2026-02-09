using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems
{
    [TestFixture]
    public class FormationSpeedControllerTests
    {
        private FormationConfig _default;

        [SetUp]
        public void SetUp()
        {
            _default = FormationConfig.Default;
        }

        // ── ComputeSpeedDecision ──────────────────────────────────

        [Test]
        public void Disabled_AlwaysReturnsMatchBoss()
        {
            var config = new FormationConfig(30f, 15f, 5f, false);
            var result = FormationSpeedController.ComputeSpeedDecision(true, 50f * 50f, 100f * 100f, in config);
            Assert.AreEqual(FormationSpeedDecision.MatchBoss, result);
        }

        [Test]
        public void VeryFarFromBoss_ReturnsSprint()
        {
            // 40m > 30m catch-up threshold
            var result = FormationSpeedController.ComputeSpeedDecision(false, 40f * 40f, 10f * 10f, in _default);
            Assert.AreEqual(FormationSpeedDecision.Sprint, result);
        }

        [Test]
        public void MediumDistance_ReturnsWalk()
        {
            // 20m: between 15m (match) and 30m (catch-up)
            var result = FormationSpeedController.ComputeSpeedDecision(false, 20f * 20f, 10f * 10f, in _default);
            Assert.AreEqual(FormationSpeedDecision.Walk, result);
        }

        [Test]
        public void CloseToBoss_ReturnsMatchBoss()
        {
            // 10m < 15m match threshold, 10m from tactical > 5m slow approach
            var result = FormationSpeedController.ComputeSpeedDecision(false, 10f * 10f, 10f * 10f, in _default);
            Assert.AreEqual(FormationSpeedDecision.MatchBoss, result);
        }

        [Test]
        public void CloseToTactical_ReturnsSlowApproach()
        {
            // 10m from boss < 15m match, 3m from tactical < 5m slow approach
            var result = FormationSpeedController.ComputeSpeedDecision(false, 10f * 10f, 3f * 3f, in _default);
            Assert.AreEqual(FormationSpeedDecision.SlowApproach, result);
        }

        [Test]
        public void SlowApproachPriorityOverMatchBoss()
        {
            // Within match range (10m < 15m) AND within slow approach (2m < 5m)
            var result = FormationSpeedController.ComputeSpeedDecision(true, 10f * 10f, 2f * 2f, in _default);
            Assert.AreEqual(FormationSpeedDecision.SlowApproach, result);
        }

        [Test]
        public void SprintPriorityOverSlowApproach()
        {
            // Far from boss (40m > 30m) but close to tactical (2m < 5m)
            var result = FormationSpeedController.ComputeSpeedDecision(false, 40f * 40f, 2f * 2f, in _default);
            Assert.AreEqual(FormationSpeedDecision.Sprint, result);
        }

        [Test]
        public void ExactlyAtCatchUpBoundary_ReturnsWalk()
        {
            // At exactly 30m: distSqr == catchUpSqr, > is false → not Sprint → Walk (30 > 15)
            float distSqr = 30f * 30f;
            var result = FormationSpeedController.ComputeSpeedDecision(false, distSqr, 10f * 10f, in _default);
            Assert.AreEqual(FormationSpeedDecision.Walk, result);
        }

        [Test]
        public void ExactlyAtMatchBoundary_ReturnsMatchBoss()
        {
            // At exactly 15m: distSqr == matchSqr, > is false → not Walk → MatchBoss
            float distSqr = 15f * 15f;
            var result = FormationSpeedController.ComputeSpeedDecision(false, distSqr, 10f * 10f, in _default);
            Assert.AreEqual(FormationSpeedDecision.MatchBoss, result);
        }

        [Test]
        public void ExactlyAtSlowApproachBoundary_ReturnsMatchBoss()
        {
            // At exactly 5m from tactical: distSqr == slowApproachSqr, < is false → MatchBoss
            float distSqr = 5f * 5f;
            var result = FormationSpeedController.ComputeSpeedDecision(false, 10f * 10f, distSqr, in _default);
            Assert.AreEqual(FormationSpeedDecision.MatchBoss, result);
        }

        [Test]
        public void BossSprintingDoesNotAffectDecision()
        {
            // Same distances, different boss sprint state — same decision
            var result1 = FormationSpeedController.ComputeSpeedDecision(true, 20f * 20f, 10f * 10f, in _default);
            var result2 = FormationSpeedController.ComputeSpeedDecision(false, 20f * 20f, 10f * 10f, in _default);
            Assert.AreEqual(result1, result2);
            Assert.AreEqual(FormationSpeedDecision.Walk, result1);
        }

        // ── ShouldSprint ──────────────────────────────────────────

        [Test]
        public void ShouldSprint_SprintDecision_AlwaysTrue()
        {
            Assert.IsTrue(FormationSpeedController.ShouldSprint(FormationSpeedDecision.Sprint, false));
            Assert.IsTrue(FormationSpeedController.ShouldSprint(FormationSpeedDecision.Sprint, true));
        }

        [Test]
        public void ShouldSprint_MatchBoss_BossSprinting_ReturnsTrue()
        {
            Assert.IsTrue(FormationSpeedController.ShouldSprint(FormationSpeedDecision.MatchBoss, true));
        }

        [Test]
        public void ShouldSprint_MatchBoss_BossWalking_ReturnsFalse()
        {
            Assert.IsFalse(FormationSpeedController.ShouldSprint(FormationSpeedDecision.MatchBoss, false));
        }

        [Test]
        public void ShouldSprint_Walk_AlwaysFalse()
        {
            Assert.IsFalse(FormationSpeedController.ShouldSprint(FormationSpeedDecision.Walk, true));
            Assert.IsFalse(FormationSpeedController.ShouldSprint(FormationSpeedDecision.Walk, false));
        }

        [Test]
        public void ShouldSprint_SlowApproach_AlwaysFalse()
        {
            Assert.IsFalse(FormationSpeedController.ShouldSprint(FormationSpeedDecision.SlowApproach, true));
            Assert.IsFalse(FormationSpeedController.ShouldSprint(FormationSpeedDecision.SlowApproach, false));
        }

        // ── SpeedMultiplier ───────────────────────────────────────

        [Test]
        public void SpeedMultiplier_SlowApproach_ReturnsHalf()
        {
            Assert.AreEqual(0.5f, FormationSpeedController.SpeedMultiplier(FormationSpeedDecision.SlowApproach));
        }

        [Test]
        public void SpeedMultiplier_MatchBoss_ReturnsOne()
        {
            Assert.AreEqual(1.0f, FormationSpeedController.SpeedMultiplier(FormationSpeedDecision.MatchBoss));
        }

        [Test]
        public void SpeedMultiplier_Sprint_ReturnsOne()
        {
            Assert.AreEqual(1.0f, FormationSpeedController.SpeedMultiplier(FormationSpeedDecision.Sprint));
        }

        [Test]
        public void SpeedMultiplier_Walk_ReturnsOne()
        {
            Assert.AreEqual(1.0f, FormationSpeedController.SpeedMultiplier(FormationSpeedDecision.Walk));
        }

        // ── FormationConfig ───────────────────────────────────────

        [Test]
        public void DefaultConfig_HasExpectedSquaredDistances()
        {
            var config = FormationConfig.Default;
            Assert.AreEqual(900f, config.CatchUpDistanceSqr);
            Assert.AreEqual(225f, config.MatchSpeedDistanceSqr);
            Assert.AreEqual(25f, config.SlowApproachDistanceSqr);
            Assert.IsTrue(config.Enabled);
        }

        [Test]
        public void CustomConfig_SquaresDistances()
        {
            var config = new FormationConfig(10f, 5f, 2f, true);
            Assert.AreEqual(100f, config.CatchUpDistanceSqr);
            Assert.AreEqual(25f, config.MatchSpeedDistanceSqr);
            Assert.AreEqual(4f, config.SlowApproachDistanceSqr);
        }
    }
}
