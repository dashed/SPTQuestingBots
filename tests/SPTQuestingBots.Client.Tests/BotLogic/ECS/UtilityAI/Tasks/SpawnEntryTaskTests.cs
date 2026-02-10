using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks
{
    [TestFixture]
    public class SpawnEntryTaskTests
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
        public void Score_AlreadyComplete_ReturnsZero()
        {
            SetupValidSpawnState();
            _entity.IsSpawnEntryComplete = true;
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_ZeroDuration_ReturnsZero()
        {
            _entity.SpawnTime = 10f;
            _entity.CurrentGameTime = 11f;
            _entity.SpawnEntryDuration = 0f;
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_NegativeDuration_ReturnsZero()
        {
            _entity.SpawnTime = 10f;
            _entity.CurrentGameTime = 11f;
            _entity.SpawnEntryDuration = -1f;
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(0f));
        }

        // ── Scoring during spawn entry ─────────────────────────

        [Test]
        public void Score_DuringSpawnEntry_ReturnsMaxBaseScore()
        {
            SetupValidSpawnState();
            // 2 seconds into a 4-second duration
            _entity.CurrentGameTime = _entity.SpawnTime + 2f;
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore));
        }

        [Test]
        public void Score_AtSpawnTime_ReturnsMaxBaseScore()
        {
            SetupValidSpawnState();
            _entity.CurrentGameTime = _entity.SpawnTime; // elapsed = 0
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore));
        }

        [Test]
        public void Score_JustBeforeDurationExpires_ReturnsMaxBaseScore()
        {
            SetupValidSpawnState();
            _entity.CurrentGameTime = _entity.SpawnTime + _entity.SpawnEntryDuration - 0.01f;
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore));
        }

        // ── Duration expiry ────────────────────────────────────

        [Test]
        public void Score_DurationExpired_ReturnsZero()
        {
            SetupValidSpawnState();
            _entity.CurrentGameTime = _entity.SpawnTime + _entity.SpawnEntryDuration + 1f;
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_DurationExactlyExpired_ReturnsZero()
        {
            SetupValidSpawnState();
            _entity.CurrentGameTime = _entity.SpawnTime + _entity.SpawnEntryDuration;
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(0f));
        }

        [Test]
        public void Score_DurationExpired_SetsIsSpawnEntryComplete()
        {
            SetupValidSpawnState();
            Assert.That(_entity.IsSpawnEntryComplete, Is.False);

            _entity.CurrentGameTime = _entity.SpawnTime + _entity.SpawnEntryDuration;
            SpawnEntryTask.Score(_entity);

            Assert.That(_entity.IsSpawnEntryComplete, Is.True);
        }

        [Test]
        public void Score_AfterComplete_AlwaysZero()
        {
            SetupValidSpawnState();
            // First call expires and marks complete
            _entity.CurrentGameTime = _entity.SpawnTime + _entity.SpawnEntryDuration;
            SpawnEntryTask.Score(_entity);

            // Subsequent calls should always be 0
            _entity.CurrentGameTime = _entity.SpawnTime; // reset to spawn time
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(0f));
        }

        // ── Squad stagger ──────────────────────────────────────

        [Test]
        public void Score_WithSquadStagger_StaggerAddsToDuration()
        {
            SetupValidSpawnState();
            float baseDuration = 4f;
            float stagger = 3f;
            _entity.SpawnEntryDuration = baseDuration + stagger; // 7 seconds total

            // At 5 seconds — past base duration but within staggered duration
            _entity.CurrentGameTime = _entity.SpawnTime + 5f;
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore));

            // At 7 seconds — exactly at staggered duration
            _entity.CurrentGameTime = _entity.SpawnTime + 7f;
            score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(0f));
            Assert.That(_entity.IsSpawnEntryComplete, Is.True);
        }

        // ── Negative elapsed (clock safety) ────────────────────

        [Test]
        public void Score_NegativeElapsed_ReturnsMaxBaseScore()
        {
            SetupValidSpawnState();
            _entity.CurrentGameTime = _entity.SpawnTime - 5f; // before spawn
            float score = SpawnEntryTask.Score(_entity);
            Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore));
        }

        // ── Direction bias ─────────────────────────────────────

        [Test]
        public void DirectionBias_NoBias_ReturnsZero()
        {
            SetupDirectionBiasState();
            _entity.SpawnFacingBias = 0f;
            float bias = GoToObjectiveTask.DirectionBias(_entity);
            Assert.That(bias, Is.EqualTo(0f));
        }

        [Test]
        public void DirectionBias_NoSquad_ReturnsZero()
        {
            SetupDirectionBiasState();
            _entity.Squad = null;
            float bias = GoToObjectiveTask.DirectionBias(_entity);
            Assert.That(bias, Is.EqualTo(0f));
        }

        [Test]
        public void DirectionBias_AlignedObjective_ReturnsPositiveBonus()
        {
            SetupDirectionBiasState();
            // Bot at (0,0,0), facing (0,0,1), objective at (0,0,100) — perfectly aligned
            _entity.CurrentPositionX = 0f;
            _entity.CurrentPositionZ = 0f;
            _entity.SpawnFacingX = 0f;
            _entity.SpawnFacingZ = 1f;
            _entity.Squad.Objective.SetObjective(0f, 0f, 100f);

            float bias = GoToObjectiveTask.DirectionBias(_entity);
            // dot = 1.0, bonus = 1.0 * 0.05 * 1.0 = 0.05
            Assert.That(bias, Is.EqualTo(0.05f).Within(0.001f));
        }

        [Test]
        public void DirectionBias_OppositeObjective_ReturnsZero()
        {
            SetupDirectionBiasState();
            // Bot at (0,0,0), facing (0,0,1), objective at (0,0,-100) — opposite direction
            _entity.CurrentPositionX = 0f;
            _entity.CurrentPositionZ = 0f;
            _entity.SpawnFacingX = 0f;
            _entity.SpawnFacingZ = 1f;
            _entity.Squad.Objective.SetObjective(0f, 0f, -100f);

            float bias = GoToObjectiveTask.DirectionBias(_entity);
            Assert.That(bias, Is.EqualTo(0f));
        }

        [Test]
        public void DirectionBias_PerpendicularObjective_ReturnsZero()
        {
            SetupDirectionBiasState();
            // Bot at (0,0,0), facing (0,0,1), objective at (100,0,0) — perpendicular
            _entity.CurrentPositionX = 0f;
            _entity.CurrentPositionZ = 0f;
            _entity.SpawnFacingX = 0f;
            _entity.SpawnFacingZ = 1f;
            _entity.Squad.Objective.SetObjective(100f, 0f, 0f);

            float bias = GoToObjectiveTask.DirectionBias(_entity);
            Assert.That(bias, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void DirectionBias_HalfDecayedBias_ReturnsHalfBonus()
        {
            SetupDirectionBiasState();
            _entity.SpawnFacingBias = 0.5f;
            // Perfectly aligned
            _entity.CurrentPositionX = 0f;
            _entity.CurrentPositionZ = 0f;
            _entity.SpawnFacingX = 0f;
            _entity.SpawnFacingZ = 1f;
            _entity.Squad.Objective.SetObjective(0f, 0f, 100f);

            float bias = GoToObjectiveTask.DirectionBias(_entity);
            // dot = 1.0, bonus = 1.0 * 0.05 * 0.5 = 0.025
            Assert.That(bias, Is.EqualTo(0.025f).Within(0.001f));
        }

        // ── ScoreEntity integration ───────────────────────────

        [Test]
        public void ScoreEntity_WritesToTaskScoresAtOrdinal()
        {
            SetupValidSpawnState();
            _entity.CurrentGameTime = _entity.SpawnTime + 1f;
            var task = new SpawnEntryTask();
            task.ScoreEntity(12, _entity);
            Assert.That(_entity.TaskScores[12], Is.EqualTo(SpawnEntryTask.MaxBaseScore));
        }

        [Test]
        public void ScoreEntity_NoModifiers_PureScore()
        {
            SetupValidSpawnState();
            _entity.CurrentGameTime = _entity.SpawnTime + 1f;
            var task = new SpawnEntryTask();
            task.ScoreEntity(12, _entity);
            // SpawnEntry is a gating task — no personality/raid modifiers
            Assert.That(_entity.TaskScores[12], Is.EqualTo(SpawnEntryTask.MaxBaseScore));
        }

        // ── Constants ─────────────────────────────────────────

        [Test]
        public void BotActionTypeId_IsSpawnEntry()
        {
            var task = new SpawnEntryTask();
            Assert.That(task.BotActionTypeId, Is.EqualTo(BotActionTypeId.SpawnEntry));
        }

        [Test]
        public void ActionReason_IsSpawnEntry()
        {
            var task = new SpawnEntryTask();
            Assert.That(task.ActionReason, Is.EqualTo("SpawnEntry"));
        }

        [Test]
        public void MaxBaseScore_IsCorrect()
        {
            Assert.That(SpawnEntryTask.MaxBaseScore, Is.EqualTo(0.80f));
        }

        // ── QuestTaskFactory integration ────────────────────────

        [Test]
        public void QuestTaskFactory_TaskCount_IsThirteen()
        {
            Assert.That(QuestTaskFactory.TaskCount, Is.EqualTo(13));
        }

        [Test]
        public void QuestTaskFactory_Create_IncludesSpawnEntryTask()
        {
            var manager = QuestTaskFactory.Create();
            var entity = new BotEntity(1);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.SpawnTime = 100f;
            entity.CurrentGameTime = 101f;
            entity.SpawnEntryDuration = 4f;
            entity.IsSpawnEntryComplete = false;

            manager.ScoreAndPick(entity);
            // SpawnEntryTask is at index 12 (0-indexed, last)
            Assert.That(entity.TaskScores[12], Is.EqualTo(SpawnEntryTask.MaxBaseScore));
        }

        // ── Helpers ────────────────────────────────────────────

        private void SetupValidSpawnState()
        {
            _entity.SpawnTime = 100f;
            _entity.SpawnEntryDuration = 4f;
            _entity.CurrentGameTime = 102f; // 2 seconds in
            _entity.IsSpawnEntryComplete = false;
        }

        private void SetupDirectionBiasState()
        {
            _entity.SpawnFacingBias = 1f;
            _entity.SpawnFacingX = 0f;
            _entity.SpawnFacingZ = 1f;
            _entity.DistanceToObjective = 100f;
            _entity.CurrentPositionX = 0f;
            _entity.CurrentPositionZ = 0f;

            // Create a squad with an objective
            var squad = new SquadEntity(0, 1, 6);
            squad.Objective.SetObjective(0f, 0f, 100f);
            _entity.Squad = squad;
        }
    }
}
