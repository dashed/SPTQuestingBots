using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI
{
    // ── Helper ─────────────────────────────────────────────

    internal static class QuestEntityHelper
    {
        /// <summary>
        /// Creates a BotEntity with quest state pre-set for scoring.
        /// TaskScores allocated with QuestTaskFactory.TaskCount slots.
        /// </summary>
        public static BotEntity Create(
            int id,
            int questAction = QuestActionId.Undefined,
            bool hasActiveObjective = true,
            bool isCloseToObjective = false,
            bool mustUnlockDoor = false,
            float distanceToObjective = 100f
        )
        {
            var entity = new BotEntity(id);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.CurrentQuestAction = questAction;
            entity.HasActiveObjective = hasActiveObjective;
            entity.IsCloseToObjective = isCloseToObjective;
            entity.MustUnlockDoor = mustUnlockDoor;
            entity.DistanceToObjective = distanceToObjective;
            return entity;
        }
    }

    // ── QuestActionId Tests ────────────────────────────────

    [TestFixture]
    public class QuestActionIdTests
    {
        [Test]
        public void Constants_MatchExpectedValues()
        {
            Assert.AreEqual(0, QuestActionId.Undefined);
            Assert.AreEqual(1, QuestActionId.MoveToPosition);
            Assert.AreEqual(2, QuestActionId.HoldAtPosition);
            Assert.AreEqual(3, QuestActionId.Ambush);
            Assert.AreEqual(4, QuestActionId.Snipe);
            Assert.AreEqual(5, QuestActionId.PlantItem);
            Assert.AreEqual(6, QuestActionId.ToggleSwitch);
            Assert.AreEqual(7, QuestActionId.RequestExtract);
            Assert.AreEqual(8, QuestActionId.CloseNearbyDoors);
        }
    }

    // ── BotActionTypeId Tests ──────────────────────────────

    [TestFixture]
    public class BotActionTypeIdTests
    {
        [Test]
        public void Constants_MatchExpectedValues()
        {
            Assert.AreEqual(0, BotActionTypeId.Undefined);
            Assert.AreEqual(1, BotActionTypeId.GoToObjective);
            Assert.AreEqual(3, BotActionTypeId.HoldPosition);
            Assert.AreEqual(4, BotActionTypeId.Ambush);
            Assert.AreEqual(5, BotActionTypeId.Snipe);
            Assert.AreEqual(6, BotActionTypeId.PlantItem);
            Assert.AreEqual(10, BotActionTypeId.ToggleSwitch);
            Assert.AreEqual(11, BotActionTypeId.UnlockDoor);
            Assert.AreEqual(12, BotActionTypeId.CloseNearbyDoors);
        }
    }

    // ── BotEntity Quest State Tests ────────────────────────

    [TestFixture]
    public class BotEntityQuestStateTests
    {
        [Test]
        public void DefaultValues_AreCorrect()
        {
            var entity = new BotEntity(0);

            Assert.AreEqual(0, entity.CurrentQuestAction);
            Assert.AreEqual(float.MaxValue, entity.DistanceToObjective);
            Assert.IsFalse(entity.IsCloseToObjective);
            Assert.IsFalse(entity.MustUnlockDoor);
            Assert.IsFalse(entity.HasActiveObjective);
        }

        [Test]
        public void QuestState_CanBeSetAndRead()
        {
            var entity = new BotEntity(0);

            entity.CurrentQuestAction = QuestActionId.Ambush;
            entity.DistanceToObjective = 15.5f;
            entity.IsCloseToObjective = true;
            entity.MustUnlockDoor = true;
            entity.HasActiveObjective = true;

            Assert.AreEqual(QuestActionId.Ambush, entity.CurrentQuestAction);
            Assert.AreEqual(15.5f, entity.DistanceToObjective, 0.01f);
            Assert.IsTrue(entity.IsCloseToObjective);
            Assert.IsTrue(entity.MustUnlockDoor);
            Assert.IsTrue(entity.HasActiveObjective);
        }
    }

    // ── GoToObjectiveTask Tests ────────────────────────────

    [TestFixture]
    public class GoToObjectiveTaskTests
    {
        // ── Gate checks (still return 0) ────────────────────

        [Test]
        public void Score_NoActiveObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, hasActiveObjective: false);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        [Test]
        public void Score_MustUnlockDoor_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, mustUnlockDoor: true);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        [Test]
        public void Score_HoldAtPosition_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.HoldAtPosition);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        [Test]
        public void Score_ToggleSwitch_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.ToggleSwitch);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        [Test]
        public void Score_CloseNearbyDoors_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.CloseNearbyDoors);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        [Test]
        public void Score_RequestExtract_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.RequestExtract);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        [Test]
        public void Score_Undefined_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Undefined);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        [Test]
        public void Score_AmbushCloseToObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Ambush, isCloseToObjective: true);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        [Test]
        public void Score_SnipeCloseToObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Snipe, isCloseToObjective: true);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        [Test]
        public void Score_PlantItemCloseToObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.PlantItem, isCloseToObjective: true);
            Assert.AreEqual(0f, GoToObjectiveTask.Score(entity));
        }

        // ── Continuous scoring (distance-based) ─────────────

        [Test]
        public void Score_MoveToPosition_ReturnsPositive()
        {
            // Default helper distance is 100m
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition);
            float score = GoToObjectiveTask.Score(entity);
            Assert.That(score, Is.GreaterThan(0f));
            Assert.That(score, Is.LessThanOrEqualTo(GoToObjectiveTask.BaseScore));
        }

        [Test]
        public void Score_AmbushFarFromObjective_ReturnsPositive()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Ambush, isCloseToObjective: false);
            float score = GoToObjectiveTask.Score(entity);
            Assert.That(score, Is.GreaterThan(0f));
            Assert.That(score, Is.LessThanOrEqualTo(GoToObjectiveTask.BaseScore));
        }

        [Test]
        public void Score_SnipeFarFromObjective_ReturnsPositive()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Snipe, isCloseToObjective: false);
            float score = GoToObjectiveTask.Score(entity);
            Assert.That(score, Is.GreaterThan(0f));
            Assert.That(score, Is.LessThanOrEqualTo(GoToObjectiveTask.BaseScore));
        }

        [Test]
        public void Score_PlantItemFarFromObjective_ReturnsPositive()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.PlantItem, isCloseToObjective: false);
            float score = GoToObjectiveTask.Score(entity);
            Assert.That(score, Is.GreaterThan(0f));
            Assert.That(score, Is.LessThanOrEqualTo(GoToObjectiveTask.BaseScore));
        }

        // ── Distance gradient tests ─────────────────────────

        [Test]
        public void Score_AtZeroDistance_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, distanceToObjective: 0f);
            float score = GoToObjectiveTask.Score(entity);
            Assert.AreEqual(0f, score, 0.001f);
        }

        [Test]
        public void Score_At50m_GreaterThan03()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, distanceToObjective: 50f);
            float score = GoToObjectiveTask.Score(entity);
            Assert.That(score, Is.GreaterThan(0.3f));
        }

        [Test]
        public void Score_At200m_ApproachesBaseScore()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, distanceToObjective: 200f);
            float score = GoToObjectiveTask.Score(entity);
            Assert.That(score, Is.GreaterThan(0.6f));
            Assert.That(score, Is.LessThanOrEqualTo(GoToObjectiveTask.BaseScore));
        }

        [Test]
        public void Score_AtVeryLargeDistance_ApproachesBaseScore()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, distanceToObjective: 1000f);
            float score = GoToObjectiveTask.Score(entity);
            Assert.AreEqual(GoToObjectiveTask.BaseScore, score, 0.01f);
        }

        [Test]
        public void Score_MonotonicallyIncreasingWithDistance()
        {
            float[] distances = { 0f, 10f, 25f, 50f, 75f, 100f, 150f, 200f, 500f };
            float prev = -1f;

            for (int i = 0; i < distances.Length; i++)
            {
                var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, distanceToObjective: distances[i]);
                float score = GoToObjectiveTask.Score(entity);
                Assert.That(
                    score,
                    Is.GreaterThanOrEqualTo(prev),
                    $"Score at {distances[i]}m ({score}) should be >= score at previous distance ({prev})"
                );
                prev = score;
            }
        }

        [Test]
        public void Score_NeverExceedsBaseScore()
        {
            float[] distances = { 0f, 50f, 100f, 500f, 10000f };

            for (int i = 0; i < distances.Length; i++)
            {
                var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, distanceToObjective: distances[i]);
                float score = GoToObjectiveTask.Score(entity);
                Assert.That(
                    score,
                    Is.LessThanOrEqualTo(GoToObjectiveTask.BaseScore),
                    $"Score at {distances[i]}m ({score}) should not exceed BaseScore"
                );
            }
        }

        [Test]
        public void Score_MatchesExpectedExponentialFormula()
        {
            float distance = 75f;
            float falloff = 75f;
            float expected = GoToObjectiveTask.BaseScore * (1f - (float)Math.Exp(-distance / falloff));

            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, distanceToObjective: distance);
            float score = GoToObjectiveTask.Score(entity);
            Assert.AreEqual(expected, score, 0.001f);
        }

        // ── Properties and ScoreEntity ──────────────────────

        [Test]
        public void Properties_CorrectValues()
        {
            var task = new GoToObjectiveTask();
            Assert.AreEqual(BotActionTypeId.GoToObjective, task.BotActionTypeId);
            Assert.AreEqual("GoToObjective", task.ActionReason);
            Assert.AreEqual(0.25f, task.Hysteresis, 0.001f);
        }

        [Test]
        public void ScoreEntity_WritesToTaskScores()
        {
            var task = new GoToObjectiveTask();
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, distanceToObjective: 100f);

            task.ScoreEntity(0, entity);

            Assert.That(entity.TaskScores[0], Is.GreaterThan(0f));
            Assert.That(entity.TaskScores[0], Is.LessThanOrEqualTo(GoToObjectiveTask.BaseScore));
        }
    }

    // ── AmbushTask Tests ───────────────────────────────────

    [TestFixture]
    public class AmbushTaskTests
    {
        [Test]
        public void Score_NoActiveObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Ambush, hasActiveObjective: false, isCloseToObjective: true);
            Assert.AreEqual(0f, AmbushTask.Score(entity));
        }

        [Test]
        public void Score_WrongAction_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Snipe, isCloseToObjective: true);
            Assert.AreEqual(0f, AmbushTask.Score(entity));
        }

        [Test]
        public void Score_NotCloseToObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Ambush, isCloseToObjective: false);
            Assert.AreEqual(0f, AmbushTask.Score(entity));
        }

        [Test]
        public void Score_AmbushAndClose_ReturnsBaseScore()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Ambush, isCloseToObjective: true);
            Assert.AreEqual(AmbushTask.BaseScore, AmbushTask.Score(entity));
        }

        [Test]
        public void Properties_CorrectValues()
        {
            var task = new AmbushTask();
            Assert.AreEqual(BotActionTypeId.Ambush, task.BotActionTypeId);
            Assert.AreEqual("Ambush", task.ActionReason);
            Assert.AreEqual(0.15f, task.Hysteresis, 0.001f);
        }
    }

    // ── SnipeTask Tests ────────────────────────────────────

    [TestFixture]
    public class SnipeTaskTests
    {
        [Test]
        public void Score_NoActiveObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Snipe, hasActiveObjective: false, isCloseToObjective: true);
            Assert.AreEqual(0f, SnipeTask.Score(entity));
        }

        [Test]
        public void Score_WrongAction_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Ambush, isCloseToObjective: true);
            Assert.AreEqual(0f, SnipeTask.Score(entity));
        }

        [Test]
        public void Score_NotCloseToObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Snipe, isCloseToObjective: false);
            Assert.AreEqual(0f, SnipeTask.Score(entity));
        }

        [Test]
        public void Score_SnipeAndClose_ReturnsBaseScore()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Snipe, isCloseToObjective: true);
            Assert.AreEqual(SnipeTask.BaseScore, SnipeTask.Score(entity));
        }

        [Test]
        public void Properties_CorrectValues()
        {
            var task = new SnipeTask();
            Assert.AreEqual(BotActionTypeId.Snipe, task.BotActionTypeId);
            Assert.AreEqual("Snipe", task.ActionReason);
            Assert.AreEqual(0.15f, task.Hysteresis, 0.001f);
        }
    }

    // ── HoldPositionTask Tests ─────────────────────────────

    [TestFixture]
    public class HoldPositionTaskTests
    {
        [Test]
        public void Score_NoActiveObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.HoldAtPosition, hasActiveObjective: false);
            Assert.AreEqual(0f, HoldPositionTask.Score(entity));
        }

        [Test]
        public void Score_WrongAction_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition);
            Assert.AreEqual(0f, HoldPositionTask.Score(entity));
        }

        [Test]
        public void Score_HoldAtPosition_ReturnsBaseScore()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.HoldAtPosition);
            Assert.AreEqual(HoldPositionTask.BaseScore, HoldPositionTask.Score(entity));
        }

        [Test]
        public void Score_IgnoresProximity()
        {
            // HoldPosition scores regardless of distance
            var far = QuestEntityHelper.Create(0, QuestActionId.HoldAtPosition, isCloseToObjective: false);
            var close = QuestEntityHelper.Create(1, QuestActionId.HoldAtPosition, isCloseToObjective: true);
            Assert.AreEqual(HoldPositionTask.Score(far), HoldPositionTask.Score(close));
        }

        [Test]
        public void Properties_CorrectValues()
        {
            var task = new HoldPositionTask();
            Assert.AreEqual(BotActionTypeId.HoldPosition, task.BotActionTypeId);
            Assert.AreEqual("HoldPosition", task.ActionReason);
            Assert.AreEqual(0.10f, task.Hysteresis, 0.001f);
        }
    }

    // ── PlantItemTask Tests ────────────────────────────────

    [TestFixture]
    public class PlantItemTaskTests
    {
        [Test]
        public void Score_NoActiveObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.PlantItem, hasActiveObjective: false, isCloseToObjective: true);
            Assert.AreEqual(0f, PlantItemTask.Score(entity));
        }

        [Test]
        public void Score_WrongAction_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.Ambush, isCloseToObjective: true);
            Assert.AreEqual(0f, PlantItemTask.Score(entity));
        }

        [Test]
        public void Score_NotCloseToObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.PlantItem, isCloseToObjective: false);
            Assert.AreEqual(0f, PlantItemTask.Score(entity));
        }

        [Test]
        public void Score_PlantItemAndClose_ReturnsBaseScore()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.PlantItem, isCloseToObjective: true);
            Assert.AreEqual(PlantItemTask.BaseScore, PlantItemTask.Score(entity));
        }

        [Test]
        public void Properties_CorrectValues()
        {
            var task = new PlantItemTask();
            Assert.AreEqual(BotActionTypeId.PlantItem, task.BotActionTypeId);
            Assert.AreEqual("PlantItem", task.ActionReason);
            Assert.AreEqual(0.15f, task.Hysteresis, 0.001f);
        }
    }

    // ── UnlockDoorTask Tests ───────────────────────────────

    [TestFixture]
    public class UnlockDoorTaskTests
    {
        [Test]
        public void Score_NoActiveObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, hasActiveObjective: false, mustUnlockDoor: true);
            Assert.AreEqual(0f, UnlockDoorTask.Score(entity));
        }

        [Test]
        public void Score_NoDoorToUnlock_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, mustUnlockDoor: false);
            Assert.AreEqual(0f, UnlockDoorTask.Score(entity));
        }

        [Test]
        public void Score_MustUnlockDoor_ReturnsBaseScore()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, mustUnlockDoor: true);
            Assert.AreEqual(UnlockDoorTask.BaseScore, UnlockDoorTask.Score(entity));
        }

        [Test]
        public void Score_IgnoresQuestAction()
        {
            // UnlockDoor scores regardless of quest action type
            var entity1 = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, mustUnlockDoor: true);
            var entity2 = QuestEntityHelper.Create(1, QuestActionId.Ambush, mustUnlockDoor: true);
            Assert.AreEqual(UnlockDoorTask.Score(entity1), UnlockDoorTask.Score(entity2));
        }

        [Test]
        public void Properties_CorrectValues()
        {
            var task = new UnlockDoorTask();
            Assert.AreEqual(BotActionTypeId.UnlockDoor, task.BotActionTypeId);
            Assert.AreEqual("UnlockDoor", task.ActionReason);
            Assert.AreEqual(0.20f, task.Hysteresis, 0.001f);
        }
    }

    // ── ToggleSwitchTask Tests ─────────────────────────────

    [TestFixture]
    public class ToggleSwitchTaskTests
    {
        [Test]
        public void Score_NoActiveObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.ToggleSwitch, hasActiveObjective: false);
            Assert.AreEqual(0f, ToggleSwitchTask.Score(entity));
        }

        [Test]
        public void Score_WrongAction_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition);
            Assert.AreEqual(0f, ToggleSwitchTask.Score(entity));
        }

        [Test]
        public void Score_ToggleSwitch_ReturnsBaseScore()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.ToggleSwitch);
            Assert.AreEqual(ToggleSwitchTask.BaseScore, ToggleSwitchTask.Score(entity));
        }

        [Test]
        public void Properties_CorrectValues()
        {
            var task = new ToggleSwitchTask();
            Assert.AreEqual(BotActionTypeId.ToggleSwitch, task.BotActionTypeId);
            Assert.AreEqual("ToggleSwitch", task.ActionReason);
            Assert.AreEqual(0.10f, task.Hysteresis, 0.001f);
        }
    }

    // ── CloseDoorsTask Tests ───────────────────────────────

    [TestFixture]
    public class CloseDoorsTaskTests
    {
        [Test]
        public void Score_NoActiveObjective_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.CloseNearbyDoors, hasActiveObjective: false);
            Assert.AreEqual(0f, CloseDoorsTask.Score(entity));
        }

        [Test]
        public void Score_WrongAction_ReturnsZero()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition);
            Assert.AreEqual(0f, CloseDoorsTask.Score(entity));
        }

        [Test]
        public void Score_CloseNearbyDoors_ReturnsBaseScore()
        {
            var entity = QuestEntityHelper.Create(0, QuestActionId.CloseNearbyDoors);
            Assert.AreEqual(CloseDoorsTask.BaseScore, CloseDoorsTask.Score(entity));
        }

        [Test]
        public void Properties_CorrectValues()
        {
            var task = new CloseDoorsTask();
            Assert.AreEqual(BotActionTypeId.CloseNearbyDoors, task.BotActionTypeId);
            Assert.AreEqual("CloseNearbyDoors", task.ActionReason);
            Assert.AreEqual(0.10f, task.Hysteresis, 0.001f);
        }
    }

    // ── QuestUtilityTask Base Tests ────────────────────────

    [TestFixture]
    public class QuestUtilityTaskBaseTests
    {
        [Test]
        public void Update_IsNoOp()
        {
            // QuestUtilityTask.Update() does nothing — BigBrain handles execution
            var task = new GoToObjectiveTask();
            Assert.DoesNotThrow(() => task.Update());
        }

        [Test]
        public void UpdateScores_DefaultUsesScoreEntity()
        {
            // The default UpdateScores (from UtilityTask base) calls ScoreEntity per entity
            var task = new GoToObjectiveTask();
            var entity = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition);

            task.UpdateScores(0, new[] { entity });

            Assert.That(entity.TaskScores[0], Is.GreaterThan(0f));
            Assert.That(entity.TaskScores[0], Is.LessThanOrEqualTo(GoToObjectiveTask.BaseScore));
        }
    }

    // ── ScoreAndPick Tests ─────────────────────────────────

    [TestFixture]
    public class ScoreAndPickTests
    {
        [Test]
        public void ScoreAndPick_ScoresAndSelectsBestTask()
        {
            var taskA = new TestTask(0f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var entity = new BotEntity(0);
            entity.TaskScores = new float[2];

            taskA.SetScore(0, 0.3f);
            taskB.SetScore(0, 0.7f);

            manager.ScoreAndPick(entity);

            Assert.AreSame(taskB, entity.TaskAssignment.Task);
            Assert.AreEqual(1, entity.TaskAssignment.Ordinal);
        }

        [Test]
        public void ScoreAndPick_InactiveEntity_DeactivatesTask()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var entity = new BotEntity(0);
            entity.TaskScores = new float[1];

            task.SetScore(0, 0.5f);
            manager.ScoreAndPick(entity);
            Assert.AreSame(task, entity.TaskAssignment.Task);

            entity.IsActive = false;
            manager.ScoreAndPick(entity);

            Assert.IsNull(entity.TaskAssignment.Task);
        }

        [Test]
        public void ScoreAndPick_WithHysteresis_RespectsCurrentTask()
        {
            var taskA = new TestTask(0.25f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var entity = new BotEntity(0);
            entity.TaskScores = new float[2];

            // A wins initially
            taskA.SetScore(0, 0.6f);
            taskB.SetScore(0, 0.3f);
            manager.ScoreAndPick(entity);
            Assert.AreSame(taskA, entity.TaskAssignment.Task);

            // B scores higher than A's raw score but not enough to beat hysteresis
            taskA.SetScore(0, 0.4f);
            taskB.SetScore(0, 0.55f); // 0.55 < 0.4 + 0.25 = 0.65
            manager.ScoreAndPick(entity);
            Assert.AreSame(taskA, entity.TaskAssignment.Task);
        }
    }

    // ── Integration Scenario: Quest Action Transitions ─────

    [TestFixture]
    public class QuestActionTransitionTests
    {
        private UtilityTaskManager CreateQuestManager()
        {
            return new UtilityTaskManager(
                new UtilityTask[]
                {
                    new GoToObjectiveTask(),
                    new AmbushTask(),
                    new SnipeTask(),
                    new HoldPositionTask(),
                    new PlantItemTask(),
                    new UnlockDoorTask(),
                    new ToggleSwitchTask(),
                    new CloseDoorsTask(),
                }
            );
        }

        [Test]
        public void Scenario_MoveToPosition_SelectsGoToObjective()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition);

            manager.ScoreAndPick(bot);

            Assert.IsNotNull(bot.TaskAssignment.Task);
            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_MoveToPosition_WithDoor_SelectsUnlockDoor()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, mustUnlockDoor: true);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<UnlockDoorTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_AmbushFarAway_SelectsGoToObjective()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.Ambush, isCloseToObjective: false);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_AmbushClose_SelectsAmbush()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.Ambush, isCloseToObjective: true);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<AmbushTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_SnipeFarAway_SelectsGoToObjective()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.Snipe, isCloseToObjective: false);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_SnipeClose_SelectsSnipe()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.Snipe, isCloseToObjective: true);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<SnipeTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_PlantItemFarAway_SelectsGoToObjective()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.PlantItem, isCloseToObjective: false);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_PlantItemClose_SelectsPlantItem()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.PlantItem, isCloseToObjective: true);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<PlantItemTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_HoldAtPosition_SelectsHoldPosition()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.HoldAtPosition);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<HoldPositionTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_ToggleSwitch_SelectsToggleSwitch()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.ToggleSwitch);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<ToggleSwitchTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_CloseNearbyDoors_SelectsCloseDoors()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.CloseNearbyDoors);

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<CloseDoorsTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_NoActiveObjective_NoTaskSelected()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, hasActiveObjective: false);

            manager.ScoreAndPick(bot);

            Assert.IsNull(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_Undefined_NoTaskSelected()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.Undefined);

            manager.ScoreAndPick(bot);

            Assert.IsNull(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_RequestExtract_NoTaskSelected()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.RequestExtract);

            manager.ScoreAndPick(bot);

            Assert.IsNull(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_AmbushApproachTransition_GoToThenAmbush()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.Ambush, isCloseToObjective: false);

            // Phase 1: Far away — GoToObjective
            manager.ScoreAndPick(bot);
            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);

            // Phase 2: Arrive at objective
            bot.IsCloseToObjective = true;
            manager.ScoreAndPick(bot);

            // GoToObjective has 0.25 hysteresis, but its score drops to 0
            // while AmbushTask scores 0.65. 0.65 > 0 + 0.25 = 0.25 → switches
            Assert.IsInstanceOf<AmbushTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_DoorEncounteredDuringTravel()
        {
            var manager = CreateQuestManager();
            var bot = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition, mustUnlockDoor: false);

            // Traveling normally
            manager.ScoreAndPick(bot);
            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);

            // Door encountered
            bot.MustUnlockDoor = true;
            manager.ScoreAndPick(bot);

            // GoToObjective scores 0 (MustUnlockDoor), UnlockDoor scores 0.70
            // 0.70 > 0 + 0.25 = 0.25 → switches
            Assert.IsInstanceOf<UnlockDoorTask>(bot.TaskAssignment.Task);

            // Door unlocked
            bot.MustUnlockDoor = false;
            manager.ScoreAndPick(bot);

            // UnlockDoor scores 0, GoToObjective scores ~0.48 (at 100m)
            // ~0.48 > 0 + 0.20 = 0.20 → switches back
            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_MultipleBotsDifferentActions()
        {
            var manager = CreateQuestManager();

            var traveler = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition);
            var ambusher = QuestEntityHelper.Create(1, QuestActionId.Ambush, isCloseToObjective: true);
            var holder = QuestEntityHelper.Create(2, QuestActionId.HoldAtPosition);

            var bots = new[] { traveler, ambusher, holder };
            manager.Update(bots);

            Assert.IsInstanceOf<GoToObjectiveTask>(traveler.TaskAssignment.Task);
            Assert.IsInstanceOf<AmbushTask>(ambusher.TaskAssignment.Task);
            Assert.IsInstanceOf<HoldPositionTask>(holder.TaskAssignment.Task);
        }
    }

    // ── QuestTaskFactory Tests ─────────────────────────────

    [TestFixture]
    public class QuestTaskFactoryTests
    {
        [Test]
        public void TaskCount_Is14()
        {
            Assert.AreEqual(14, QuestTaskFactory.TaskCount);
        }

        [Test]
        public void Create_Returns14Tasks()
        {
            var manager = QuestTaskFactory.Create();
            Assert.AreEqual(14, manager.Tasks.Length);
        }

        [Test]
        public void Create_TasksAreCorrectTypes()
        {
            var manager = QuestTaskFactory.Create();

            Assert.IsInstanceOf<GoToObjectiveTask>(manager.Tasks[0]);
            Assert.IsInstanceOf<AmbushTask>(manager.Tasks[1]);
            Assert.IsInstanceOf<SnipeTask>(manager.Tasks[2]);
            Assert.IsInstanceOf<HoldPositionTask>(manager.Tasks[3]);
            Assert.IsInstanceOf<PlantItemTask>(manager.Tasks[4]);
            Assert.IsInstanceOf<UnlockDoorTask>(manager.Tasks[5]);
            Assert.IsInstanceOf<ToggleSwitchTask>(manager.Tasks[6]);
            Assert.IsInstanceOf<CloseDoorsTask>(manager.Tasks[7]);
            Assert.IsInstanceOf<LootTask>(manager.Tasks[8]);
        }

        [Test]
        public void Create_ManagerWorksEndToEnd()
        {
            var manager = QuestTaskFactory.Create();
            var bot = QuestEntityHelper.Create(0, QuestActionId.MoveToPosition);
            bot.TaskScores = new float[QuestTaskFactory.TaskCount];

            manager.ScoreAndPick(bot);

            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);
        }
    }
}
