using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks
{
    [TestFixture]
    public class VultureTaskTests
    {
        private BotEntity _entity;

        [SetUp]
        public void SetUp()
        {
            _entity = new BotEntity(0);
            _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            _entity.CurrentPositionX = 0f;
            _entity.CurrentPositionZ = 0f;
        }

        // ── Basic gating ──────────────────────────────────────

        [Test]
        public void Score_NoNearbyEvent_ReturnsZero()
        {
            _entity.HasNearbyEvent = false;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_InCombat_ReturnsZero()
        {
            SetupValidVultureState();
            _entity.IsInCombat = true;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_InBossZone_ReturnsZero()
        {
            SetupValidVultureState();
            _entity.IsInBossZone = true;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_OnCooldown_ReturnsZero()
        {
            SetupValidVultureState();
            _entity.VultureCooldownUntil = 100f;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_BelowCourageThreshold_ReturnsZero()
        {
            SetupValidVultureState();
            _entity.CombatIntensity = 10;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.EqualTo(0f));
        }

        // ── Scoring ───────────────────────────────────────────

        [Test]
        public void Score_AtThreshold_ReturnsPositive()
        {
            SetupValidVultureState();
            _entity.CombatIntensity = 15;
            float score = VultureTask.Score(_entity, 15, 150f);
            // At exact threshold, intensityRatio=1, so intensityScore=0
            // But proximity is close, so we get proximity score
            Assert.That(score, Is.GreaterThan(0f));
        }

        [Test]
        public void Score_HighIntensity_ScoresHigher()
        {
            SetupValidVultureState();
            _entity.CombatIntensity = 15;
            float scoreLow = VultureTask.Score(_entity, 15, 150f);

            _entity.CombatIntensity = 30;
            float scoreHigh = VultureTask.Score(_entity, 15, 150f);

            Assert.That(scoreHigh, Is.GreaterThan(scoreLow));
        }

        [Test]
        public void Score_CloserEvent_ScoresHigher()
        {
            SetupValidVultureState();
            _entity.NearbyEventX = 10f;
            _entity.NearbyEventZ = 10f;
            _entity.CombatIntensity = 20;
            float scoreClose = VultureTask.Score(_entity, 15, 150f);

            _entity.NearbyEventX = 100f;
            _entity.NearbyEventZ = 100f;
            float scoreFar = VultureTask.Score(_entity, 15, 150f);

            Assert.That(scoreClose, Is.GreaterThan(scoreFar));
        }

        [Test]
        public void Score_EventAtMaxRange_LowProximityScore()
        {
            SetupValidVultureState();
            _entity.NearbyEventX = 149f;
            _entity.NearbyEventZ = 0f;
            _entity.CombatIntensity = 30;
            float score = VultureTask.Score(_entity, 15, 150f);
            // Almost at edge of range — should have low proximity
            Assert.That(score, Is.GreaterThan(0f));
            Assert.That(score, Is.LessThan(VultureTask.MaxBaseScore));
        }

        [Test]
        public void Score_EventBeyondRange_ZeroProximity()
        {
            SetupValidVultureState();
            _entity.NearbyEventX = 200f;
            _entity.NearbyEventZ = 200f;
            _entity.CombatIntensity = 30;
            float score = VultureTask.Score(_entity, 15, 150f);
            // Beyond range — proximity=0, but intensity still contributes
            Assert.That(score, Is.GreaterThan(0f));
        }

        [Test]
        public void Score_CappedAtMaxBaseScore()
        {
            SetupValidVultureState();
            _entity.CombatIntensity = 1000;
            _entity.NearbyEventX = 1f;
            _entity.NearbyEventZ = 1f;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.LessThanOrEqualTo(VultureTask.MaxBaseScore));
        }

        [Test]
        public void Score_NoCooldownZero_NotBlocked()
        {
            SetupValidVultureState();
            _entity.VultureCooldownUntil = 0f;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.GreaterThan(0f));
        }

        // ── Phase continuation ────────────────────────────────

        [Test]
        public void Score_AlreadyVulturing_Approach_ReturnsMaxScore()
        {
            SetupValidVultureState();
            _entity.VulturePhase = VulturePhase.Approach;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.EqualTo(VultureTask.MaxBaseScore));
        }

        [Test]
        public void Score_AlreadyVulturing_SilentApproach_ReturnsMaxScore()
        {
            SetupValidVultureState();
            _entity.VulturePhase = VulturePhase.SilentApproach;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.EqualTo(VultureTask.MaxBaseScore));
        }

        [Test]
        public void Score_AlreadyVulturing_HoldAmbush_ReturnsMaxScore()
        {
            SetupValidVultureState();
            _entity.VulturePhase = VulturePhase.HoldAmbush;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.EqualTo(VultureTask.MaxBaseScore));
        }

        [Test]
        public void Score_PhaseComplete_ScoresNormally()
        {
            SetupValidVultureState();
            _entity.VulturePhase = VulturePhase.Complete;
            _entity.CombatIntensity = 30;
            float score = VultureTask.Score(_entity, 15, 150f);
            // Complete phase → scores normally, not max
            Assert.That(score, Is.GreaterThan(0f));
            Assert.That(score, Is.LessThan(VultureTask.MaxBaseScore));
        }

        [Test]
        public void Score_PhaseNone_ScoresNormally()
        {
            SetupValidVultureState();
            _entity.VulturePhase = VulturePhase.None;
            _entity.CombatIntensity = 30;
            float score = VultureTask.Score(_entity, 15, 150f);
            Assert.That(score, Is.GreaterThan(0f));
        }

        // ── ScoreEntity integration ───────────────────────────

        [Test]
        public void ScoreEntity_WritesToTaskScoresAtOrdinal()
        {
            SetupValidVultureState();
            var task = new VultureTask();
            task.ScoreEntity(9, _entity);
            Assert.That(_entity.TaskScores[9], Is.GreaterThan(0f));
        }

        [Test]
        public void ScoreEntity_UsesConfiguredThreshold()
        {
            SetupValidVultureState();
            _entity.CombatIntensity = 10; // Below default 15

            var task = new VultureTask();
            task.CourageThreshold = 5; // Lower threshold
            task.ScoreEntity(9, _entity);
            Assert.That(_entity.TaskScores[9], Is.GreaterThan(0f));
        }

        // ── Constants ─────────────────────────────────────────

        [Test]
        public void BotActionTypeId_IsVulture()
        {
            var task = new VultureTask();
            Assert.That(task.BotActionTypeId, Is.EqualTo(BotActionTypeId.Vulture));
        }

        [Test]
        public void ActionReason_IsVulture()
        {
            var task = new VultureTask();
            Assert.That(task.ActionReason, Is.EqualTo("Vulture"));
        }

        [Test]
        public void VulturePhaseConstants_HaveExpectedValues()
        {
            Assert.That(VulturePhase.None, Is.EqualTo((byte)0));
            Assert.That(VulturePhase.Approach, Is.EqualTo((byte)1));
            Assert.That(VulturePhase.SilentApproach, Is.EqualTo((byte)2));
            Assert.That(VulturePhase.HoldAmbush, Is.EqualTo((byte)3));
            Assert.That(VulturePhase.Rush, Is.EqualTo((byte)4));
            Assert.That(VulturePhase.Paranoia, Is.EqualTo((byte)5));
            Assert.That(VulturePhase.Complete, Is.EqualTo((byte)6));
        }

        // ── QuestTaskFactory ──────────────────────────────────

        [Test]
        public void QuestTaskFactory_TaskCount_IsFourteen()
        {
            Assert.That(QuestTaskFactory.TaskCount, Is.EqualTo(14));
        }

        [Test]
        public void QuestTaskFactory_Create_IncludesVultureTask()
        {
            var manager = QuestTaskFactory.Create();
            // VultureTask is at index 9 (0-indexed, last)
            var entity = new BotEntity(1);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasNearbyEvent = true;
            entity.NearbyEventX = 10f;
            entity.NearbyEventZ = 10f;
            entity.CombatIntensity = 30;
            entity.VulturePhase = VulturePhase.None;
            entity.VultureCooldownUntil = 0f;
            entity.IsInBossZone = false;
            entity.IsInCombat = false;

            // Score all tasks — VultureTask should produce non-zero
            manager.ScoreAndPick(entity);
            Assert.That(entity.TaskScores[9], Is.GreaterThan(0f));
        }

        // ── Helper ────────────────────────────────────────────

        private void SetupValidVultureState()
        {
            _entity.HasNearbyEvent = true;
            _entity.NearbyEventX = 50f;
            _entity.NearbyEventY = 0f;
            _entity.NearbyEventZ = 50f;
            _entity.NearbyEventTime = 1f;
            _entity.CombatIntensity = 20;
            _entity.IsInCombat = false;
            _entity.IsInBossZone = false;
            _entity.VultureCooldownUntil = 0f;
            _entity.VulturePhase = VulturePhase.None;
        }
    }
}
