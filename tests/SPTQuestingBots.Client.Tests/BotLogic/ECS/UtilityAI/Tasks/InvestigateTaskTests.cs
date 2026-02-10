using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks
{
    [TestFixture]
    public class InvestigateTaskTests
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
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_InCombat_ReturnsZero()
        {
            SetupValidState();
            _entity.IsInCombat = true;
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_AlreadyVulturing_ReturnsZero()
        {
            SetupValidState();
            _entity.VulturePhase = VulturePhase.Approach;
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_VulturePhaseNone_ScoresNormally()
        {
            SetupValidState();
            _entity.VulturePhase = VulturePhase.None;
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.GreaterThan(0f));
        }

        [Test]
        public void Score_VulturePhaseComplete_ScoresNormally()
        {
            SetupValidState();
            _entity.VulturePhase = VulturePhase.Complete;
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.GreaterThan(0f));
        }

        [Test]
        public void Score_BelowIntensityThreshold_ReturnsZero()
        {
            SetupValidState();
            _entity.CombatIntensity = 3;
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.EqualTo(0f));
        }

        // ── Already investigating ────────────────────────────

        [Test]
        public void Score_AlreadyInvestigating_ReturnsMaxBaseScore()
        {
            SetupValidState();
            _entity.IsInvestigating = true;
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.EqualTo(InvestigateTask.MaxBaseScore));
        }

        [Test]
        public void Score_AlreadyInvestigating_IgnoresLowIntensity()
        {
            _entity.HasNearbyEvent = true;
            _entity.IsInCombat = false;
            _entity.VulturePhase = VulturePhase.None;
            _entity.IsInvestigating = true;
            _entity.CombatIntensity = 1; // Below threshold
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.EqualTo(InvestigateTask.MaxBaseScore));
        }

        // ── Scoring ───────────────────────────────────────────

        [Test]
        public void Score_AtThreshold_ReturnsPositive()
        {
            SetupValidState();
            _entity.CombatIntensity = 5;
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.GreaterThan(0f));
        }

        [Test]
        public void Score_HighIntensity_ScoresHigher()
        {
            SetupValidState();
            _entity.CombatIntensity = 5;
            float scoreLow = InvestigateTask.Score(_entity, 5, 120f);

            _entity.CombatIntensity = 10;
            float scoreHigh = InvestigateTask.Score(_entity, 5, 120f);

            Assert.That(scoreHigh, Is.GreaterThan(scoreLow));
        }

        [Test]
        public void Score_CloserEvent_ScoresHigher()
        {
            SetupValidState();
            _entity.NearbyEventX = 10f;
            _entity.NearbyEventZ = 10f;
            _entity.CombatIntensity = 8;
            float scoreClose = InvestigateTask.Score(_entity, 5, 120f);

            _entity.NearbyEventX = 80f;
            _entity.NearbyEventZ = 80f;
            float scoreFar = InvestigateTask.Score(_entity, 5, 120f);

            Assert.That(scoreClose, Is.GreaterThan(scoreFar));
        }

        [Test]
        public void Score_EventBeyondRange_ZeroProximity()
        {
            SetupValidState();
            _entity.NearbyEventX = 200f;
            _entity.NearbyEventZ = 200f;
            _entity.CombatIntensity = 10;
            float score = InvestigateTask.Score(_entity, 5, 120f);
            // Beyond range — proximity=0, but intensity still contributes
            Assert.That(score, Is.GreaterThan(0f));
        }

        [Test]
        public void Score_CappedAtMaxBaseScore()
        {
            SetupValidState();
            _entity.CombatIntensity = 1000;
            _entity.NearbyEventX = 1f;
            _entity.NearbyEventZ = 1f;
            float score = InvestigateTask.Score(_entity, 5, 120f);
            Assert.That(score, Is.LessThanOrEqualTo(InvestigateTask.MaxBaseScore));
        }

        [Test]
        public void Score_LowerThanVulture()
        {
            Assert.That(InvestigateTask.MaxBaseScore, Is.LessThan(VultureTask.MaxBaseScore));
        }

        // ── ScoreEntity integration ───────────────────────────

        [Test]
        public void ScoreEntity_WritesToTaskScoresAtOrdinal()
        {
            SetupValidState();
            var task = new InvestigateTask();
            task.ScoreEntity(11, _entity);
            Assert.That(_entity.TaskScores[11], Is.GreaterThan(0f));
        }

        [Test]
        public void ScoreEntity_UsesConfiguredThreshold()
        {
            SetupValidState();
            _entity.CombatIntensity = 3; // Below default 5

            var task = new InvestigateTask();
            task.IntensityThreshold = 2; // Lower threshold
            task.ScoreEntity(11, _entity);
            Assert.That(_entity.TaskScores[11], Is.GreaterThan(0f));
        }

        // ── Constants ─────────────────────────────────────────

        [Test]
        public void BotActionTypeId_IsInvestigate()
        {
            var task = new InvestigateTask();
            Assert.That(task.BotActionTypeId, Is.EqualTo(BotActionTypeId.Investigate));
        }

        [Test]
        public void ActionReason_IsInvestigate()
        {
            var task = new InvestigateTask();
            Assert.That(task.ActionReason, Is.EqualTo("Investigate"));
        }

        [Test]
        public void BotActionTypeId_Investigate_Is16()
        {
            Assert.That(BotActionTypeId.Investigate, Is.EqualTo(16));
        }

        // ── QuestTaskFactory ──────────────────────────────────

        [Test]
        public void QuestTaskFactory_TaskCount_IsTwelve()
        {
            Assert.That(QuestTaskFactory.TaskCount, Is.EqualTo(12));
        }

        [Test]
        public void QuestTaskFactory_Create_IncludesInvestigateTask()
        {
            var manager = QuestTaskFactory.Create();
            var entity = new BotEntity(1);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasNearbyEvent = true;
            entity.NearbyEventX = 10f;
            entity.NearbyEventZ = 10f;
            entity.CombatIntensity = 8;
            entity.VulturePhase = VulturePhase.None;
            entity.VultureCooldownUntil = 0f;
            entity.IsInBossZone = false;
            entity.IsInCombat = false;
            entity.IsInvestigating = false;

            manager.ScoreAndPick(entity);
            // InvestigateTask is at index 11 (0-indexed)
            Assert.That(entity.TaskScores[11], Is.GreaterThan(0f));
        }

        // ── BotEntity fields ────────────────────────────────

        [Test]
        public void BotEntity_IsInvestigating_DefaultsFalse()
        {
            var entity = new BotEntity(99);
            Assert.That(entity.IsInvestigating, Is.False);
        }

        [Test]
        public void BotEntity_InvestigateTimeoutAt_DefaultsZero()
        {
            var entity = new BotEntity(99);
            Assert.That(entity.InvestigateTimeoutAt, Is.EqualTo(0f));
        }

        // ── Helper ────────────────────────────────────────────

        private void SetupValidState()
        {
            _entity.HasNearbyEvent = true;
            _entity.NearbyEventX = 50f;
            _entity.NearbyEventY = 0f;
            _entity.NearbyEventZ = 50f;
            _entity.NearbyEventTime = 1f;
            _entity.CombatIntensity = 8;
            _entity.IsInCombat = false;
            _entity.VulturePhase = VulturePhase.None;
            _entity.IsInvestigating = false;
        }
    }
}
