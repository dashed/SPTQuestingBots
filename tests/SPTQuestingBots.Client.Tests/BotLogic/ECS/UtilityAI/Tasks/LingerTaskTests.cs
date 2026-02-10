using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks
{
    [TestFixture]
    public class LingerTaskTests
    {
        private BotEntity _entity;

        [SetUp]
        public void SetUp()
        {
            _entity = new BotEntity(0);
            _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        }

        // ── Basic gating ──────────────────────────────────────

        [Test]
        public void Score_NoObjectiveCompleted_ReturnsZero()
        {
            _entity.ObjectiveCompletedTime = 0f;
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_InCombat_ReturnsZero()
        {
            SetupValidLingerState();
            _entity.IsInCombat = true;
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_ZeroLingerDuration_ReturnsZero()
        {
            _entity.ObjectiveCompletedTime = 10f;
            _entity.CurrentGameTime = 12f;
            _entity.LingerDuration = 0f;
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_NegativeLingerDuration_ReturnsZero()
        {
            _entity.ObjectiveCompletedTime = 10f;
            _entity.CurrentGameTime = 12f;
            _entity.LingerDuration = -5f;
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_DurationExpired_ReturnsZero()
        {
            SetupValidLingerState();
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime + _entity.LingerDuration + 1f;
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_DurationExactlyExpired_ReturnsZero()
        {
            SetupValidLingerState();
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime + _entity.LingerDuration;
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.EqualTo(0f));
        }

        // ── Scoring ───────────────────────────────────────────

        [Test]
        public void Score_JustCompleted_ReturnsBaseScore()
        {
            SetupValidLingerState();
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime; // elapsed = 0
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.EqualTo(0.45f).Within(0.001f));
        }

        [Test]
        public void Score_HalfwayThrough_ReturnsHalfBaseScore()
        {
            SetupValidLingerState();
            _entity.LingerDuration = 20f;
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime + 10f; // halfway
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.EqualTo(0.225f).Within(0.001f));
        }

        [Test]
        public void Score_DecaysLinearly()
        {
            SetupValidLingerState();
            _entity.LingerDuration = 20f;

            // Early in linger
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime + 2f;
            float scoreEarly = LingerTask.Score(_entity, 0.45f);

            // Late in linger
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime + 18f;
            float scoreLate = LingerTask.Score(_entity, 0.45f);

            Assert.That(scoreEarly, Is.GreaterThan(scoreLate));
        }

        [Test]
        public void Score_NeverExceedsBaseScore()
        {
            SetupValidLingerState();
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime; // elapsed = 0
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.LessThanOrEqualTo(0.45f));
        }

        [Test]
        public void Score_NeverNegative()
        {
            SetupValidLingerState();
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime + 100f; // way past duration
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void Score_NegativeElapsed_ReturnsZero()
        {
            SetupValidLingerState();
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime - 5f; // before completion
            float score = LingerTask.Score(_entity, 0.45f);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_CustomBaseScore_Respected()
        {
            SetupValidLingerState();
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime;
            float score = LingerTask.Score(_entity, 0.80f);
            Assert.That(score, Is.EqualTo(0.80f).Within(0.001f));
        }

        // ── ScoreEntity integration ───────────────────────────

        [Test]
        public void ScoreEntity_WritesToTaskScoresAtOrdinal()
        {
            SetupValidLingerState();
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime;
            var task = new LingerTask();
            task.ScoreEntity(10, _entity);
            Assert.That(_entity.TaskScores[10], Is.GreaterThan(0f));
        }

        [Test]
        public void ScoreEntity_UsesConfiguredBaseScore()
        {
            SetupValidLingerState();
            _entity.CurrentGameTime = _entity.ObjectiveCompletedTime;
            var task = new LingerTask();
            task.BaseScore = 0.80f;
            task.ScoreEntity(10, _entity);
            float modifier = ScoringModifiers.CombinedModifier(_entity.Aggression, _entity.RaidTimeNormalized, BotActionTypeId.Linger);
            Assert.That(_entity.TaskScores[10], Is.EqualTo(0.80f * modifier).Within(0.001f));
        }

        // ── Constants ─────────────────────────────────────────

        [Test]
        public void BotActionTypeId_IsLinger()
        {
            var task = new LingerTask();
            Assert.That(task.BotActionTypeId, Is.EqualTo(BotActionTypeId.Linger));
        }

        [Test]
        public void ActionReason_IsLinger()
        {
            var task = new LingerTask();
            Assert.That(task.ActionReason, Is.EqualTo("Linger"));
        }

        [Test]
        public void DefaultBaseScore_IsCorrect()
        {
            Assert.That(LingerTask.DefaultBaseScore, Is.EqualTo(0.45f));
        }

        // ── QuestTaskFactory integration ────────────────────────

        [Test]
        public void QuestTaskFactory_TaskCount_IsFourteen()
        {
            Assert.That(QuestTaskFactory.TaskCount, Is.EqualTo(14));
        }

        [Test]
        public void QuestTaskFactory_Create_IncludesLingerTask()
        {
            var manager = QuestTaskFactory.Create();
            var entity = new BotEntity(1);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.ObjectiveCompletedTime = 100f;
            entity.CurrentGameTime = 100f;
            entity.LingerDuration = 20f;
            entity.IsInCombat = false;

            manager.ScoreAndPick(entity);
            // LingerTask is at index 10 (0-indexed, last)
            Assert.That(entity.TaskScores[10], Is.GreaterThan(0f));
        }

        // ── Helper ────────────────────────────────────────────

        private void SetupValidLingerState()
        {
            _entity.ObjectiveCompletedTime = 100f;
            _entity.LingerDuration = 20f;
            _entity.CurrentGameTime = 105f; // 5 seconds elapsed
            _entity.IsInCombat = false;
            _entity.IsLingering = false;
        }
    }
}
