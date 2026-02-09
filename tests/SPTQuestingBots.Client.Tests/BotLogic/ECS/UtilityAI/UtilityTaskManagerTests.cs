using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI
{
    /// <summary>
    /// Concrete test task that allows controlling scores externally.
    /// </summary>
    internal class TestTask : UtilityTask
    {
        private readonly Dictionary<int, float> _scores = new Dictionary<int, float>();
        public int UpdateScoresCalls;
        public int UpdateCalls;
        public int ActivateCalls;
        public int DeactivateCalls;
        public BotEntity LastActivated;
        public BotEntity LastDeactivated;

        public TestTask(float hysteresis)
            : base(hysteresis) { }

        public void SetScore(int entityId, float score) => _scores[entityId] = score;

        public override void UpdateScores(int ordinal, IReadOnlyList<BotEntity> entities)
        {
            UpdateScoresCalls++;
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (_scores.TryGetValue(entity.Id, out float score))
                    entity.TaskScores[ordinal] = score;
                else
                    entity.TaskScores[ordinal] = 0f;
            }
        }

        public override void Update()
        {
            UpdateCalls++;
        }

        public override void Activate(BotEntity entity)
        {
            base.Activate(entity);
            ActivateCalls++;
            LastActivated = entity;
        }

        public override void Deactivate(BotEntity entity)
        {
            base.Deactivate(entity);
            DeactivateCalls++;
            LastDeactivated = entity;
        }
    }

    [TestFixture]
    public class UtilityTaskManagerTests
    {
        private BotEntity CreateEntity(int id, int taskCount)
        {
            var entity = new BotEntity(id);
            entity.TaskScores = new float[taskCount];
            return entity;
        }

        // ── PickTask: Basic Selection ──────────────────────────

        [Test]
        public void PickTask_WithNoCurrentTask_SelectsHighestScoringTask()
        {
            var taskA = new TestTask(0f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var entity = CreateEntity(0, 2);

            taskA.SetScore(0, 0.3f);
            taskB.SetScore(0, 0.7f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);

            Assert.AreSame(taskB, entity.TaskAssignment.Task);
            Assert.AreEqual(1, entity.TaskAssignment.Ordinal);
        }

        [Test]
        public void PickTask_WithAllZeroScores_DoesNotAssignTask()
        {
            var taskA = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA });
            var entity = CreateEntity(0, 1);

            // All scores default to 0
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);

            Assert.IsNull(entity.TaskAssignment.Task);
        }

        [Test]
        public void PickTask_WithSingleTask_SelectsIt()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var entity = CreateEntity(0, 1);

            task.SetScore(0, 0.5f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);

            Assert.AreSame(task, entity.TaskAssignment.Task);
            Assert.AreEqual(0, entity.TaskAssignment.Ordinal);
        }

        [Test]
        public void PickTask_SelectsFirstTaskOnTie()
        {
            // When scores are equal (both > 0), the first one seen wins
            // because we use strict > (not >=), and the current task is seeded first
            var taskA = new TestTask(0f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var entity = CreateEntity(0, 2);

            taskA.SetScore(0, 0.5f);
            taskB.SetScore(0, 0.5f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);

            // First task wins on tie (first one to set highestScore)
            Assert.AreSame(taskA, entity.TaskAssignment.Task);
        }

        // ── PickTask: Hysteresis ──────────────────────────────

        [Test]
        public void PickTask_WithHysteresis_CurrentTaskGetsBonus()
        {
            var taskA = new TestTask(0.2f); // 0.2 hysteresis
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var entity = CreateEntity(0, 2);

            // First assignment: A wins
            taskA.SetScore(0, 0.5f);
            taskB.SetScore(0, 0.3f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);
            Assert.AreSame(taskA, entity.TaskAssignment.Task);

            // Now B scores higher than A's raw score, but NOT higher than A + hysteresis
            taskA.SetScore(0, 0.4f);
            taskB.SetScore(0, 0.55f); // 0.55 < 0.4 + 0.2 = 0.6
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);

            // A should still be active due to hysteresis
            Assert.AreSame(taskA, entity.TaskAssignment.Task);
        }

        [Test]
        public void PickTask_HysteresisOvercome_SwitchesToNewTask()
        {
            var taskA = new TestTask(0.2f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var entity = CreateEntity(0, 2);

            // First assignment: A wins
            taskA.SetScore(0, 0.5f);
            taskB.SetScore(0, 0.3f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);
            Assert.AreSame(taskA, entity.TaskAssignment.Task);

            // B now exceeds A + hysteresis
            taskA.SetScore(0, 0.4f);
            taskB.SetScore(0, 0.65f); // 0.65 > 0.4 + 0.2 = 0.6
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);

            Assert.AreSame(taskB, entity.TaskAssignment.Task);
            Assert.AreEqual(1, entity.TaskAssignment.Ordinal);
        }

        [Test]
        public void PickTask_BothTasksHaveHysteresis_StickinessApplies()
        {
            var taskA = new TestTask(0.25f);
            var taskB = new TestTask(0.10f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var entity = CreateEntity(0, 2);

            // A wins initially
            taskA.SetScore(0, 0.5f);
            taskB.SetScore(0, 0.3f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);
            Assert.AreSame(taskA, entity.TaskAssignment.Task);

            // B overtakes A (with A's hysteresis)
            taskA.SetScore(0, 0.3f);
            taskB.SetScore(0, 0.65f); // > 0.3 + 0.25 = 0.55
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);
            Assert.AreSame(taskB, entity.TaskAssignment.Task);

            // Now A tries to retake — must beat B + B's hysteresis (0.10)
            taskA.SetScore(0, 0.7f);
            taskB.SetScore(0, 0.65f); // A needs > 0.65 + 0.10 = 0.75
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);
            Assert.AreSame(taskB, entity.TaskAssignment.Task); // B holds — 0.7 < 0.75

            // A pushes past B's hysteresis
            taskA.SetScore(0, 0.8f); // > 0.65 + 0.10 = 0.75
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);
            Assert.AreSame(taskA, entity.TaskAssignment.Task); // A retakes
        }

        // ── PickTask: Lifecycle (Activate/Deactivate) ────────

        [Test]
        public void PickTask_CallsActivateOnNewTask()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var entity = CreateEntity(0, 1);

            task.SetScore(0, 0.5f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);

            Assert.AreEqual(1, task.ActivateCalls);
            Assert.AreSame(entity, task.LastActivated);
        }

        [Test]
        public void PickTask_CallsDeactivateOnOldTask()
        {
            var taskA = new TestTask(0f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var entity = CreateEntity(0, 2);

            // Assign A
            taskA.SetScore(0, 0.5f);
            taskB.SetScore(0, 0.3f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);

            // Switch to B
            taskA.SetScore(0, 0.3f);
            taskB.SetScore(0, 0.7f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);

            Assert.AreEqual(1, taskA.DeactivateCalls);
            Assert.AreSame(entity, taskA.LastDeactivated);
            Assert.AreEqual(1, taskB.ActivateCalls);
        }

        [Test]
        public void PickTask_NoSwitch_DoesNotCallLifecycle()
        {
            var task = new TestTask(0.2f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var entity = CreateEntity(0, 1);

            task.SetScore(0, 0.5f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);
            Assert.AreEqual(1, task.ActivateCalls);

            // Pick again — same task, no lifecycle calls
            manager.PickTask(entity);
            Assert.AreEqual(1, task.ActivateCalls); // Not called again
            Assert.AreEqual(0, task.DeactivateCalls);
        }

        // ── PickTask: Inactive Entities ──────────────────────

        [Test]
        public void PickTask_InactiveEntity_DeactivatesCurrentTask()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var entity = CreateEntity(0, 1);

            task.SetScore(0, 0.5f);
            manager.UpdateScores(new[] { entity });
            manager.PickTask(entity);
            Assert.AreSame(task, entity.TaskAssignment.Task);

            // Deactivate entity
            entity.IsActive = false;
            manager.PickTasks(new[] { entity });

            Assert.IsNull(entity.TaskAssignment.Task);
            Assert.AreEqual(1, task.DeactivateCalls);
        }

        [Test]
        public void PickTask_InactiveEntityWithNoTask_DoesNothing()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var entity = CreateEntity(0, 1);
            entity.IsActive = false;

            // Should not throw or assign
            manager.PickTasks(new[] { entity });
            Assert.IsNull(entity.TaskAssignment.Task);
        }

        // ── Update: Full Cycle ───────────────────────────────

        [Test]
        public void Update_CallsAllThreePhases()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var entity = CreateEntity(0, 1);

            task.SetScore(0, 0.5f);
            manager.Update(new[] { entity });

            Assert.AreEqual(1, task.UpdateScoresCalls);
            Assert.AreEqual(1, task.UpdateCalls);
            Assert.AreSame(task, entity.TaskAssignment.Task);
        }

        [Test]
        public void UpdateScores_CallsAllTasks()
        {
            var taskA = new TestTask(0f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var entity = CreateEntity(0, 2);

            manager.UpdateScores(new[] { entity });

            Assert.AreEqual(1, taskA.UpdateScoresCalls);
            Assert.AreEqual(1, taskB.UpdateScoresCalls);
        }

        [Test]
        public void UpdateTasks_CallsAllTasks()
        {
            var taskA = new TestTask(0f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });

            manager.UpdateTasks();

            Assert.AreEqual(1, taskA.UpdateCalls);
            Assert.AreEqual(1, taskB.UpdateCalls);
        }

        // ── RemoveEntity ─────────────────────────────────────

        [Test]
        public void RemoveEntity_DeactivatesCurrentTask()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var entity = CreateEntity(0, 1);

            task.SetScore(0, 0.5f);
            manager.Update(new[] { entity });
            Assert.AreSame(task, entity.TaskAssignment.Task);

            manager.RemoveEntity(entity);

            Assert.IsNull(entity.TaskAssignment.Task);
            Assert.AreEqual(1, task.DeactivateCalls);
        }

        [Test]
        public void RemoveEntity_WithNoTask_DoesNotThrow()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var entity = CreateEntity(0, 1);

            // No task assigned — should not throw
            Assert.DoesNotThrow(() => manager.RemoveEntity(entity));
        }

        // ── Multi-Entity ─────────────────────────────────────

        [Test]
        public void PickTasks_MultipleEntities_IndependentScoring()
        {
            var taskA = new TestTask(0f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });

            var entity1 = CreateEntity(0, 2);
            var entity2 = CreateEntity(1, 2);

            // Entity 1 prefers A, entity 2 prefers B
            taskA.SetScore(0, 0.7f);
            taskA.SetScore(1, 0.3f);
            taskB.SetScore(0, 0.3f);
            taskB.SetScore(1, 0.7f);

            manager.Update(new[] { entity1, entity2 });

            Assert.AreSame(taskA, entity1.TaskAssignment.Task);
            Assert.AreSame(taskB, entity2.TaskAssignment.Task);
        }

        [Test]
        public void PickTasks_MultipleEntities_CanAllHaveSameTask()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });

            var entity1 = CreateEntity(0, 1);
            var entity2 = CreateEntity(1, 1);

            task.SetScore(0, 0.5f);
            task.SetScore(1, 0.5f);

            manager.Update(new[] { entity1, entity2 });

            Assert.AreSame(task, entity1.TaskAssignment.Task);
            Assert.AreSame(task, entity2.TaskAssignment.Task);
            Assert.AreEqual(2, task.ActiveEntityCount);
        }
    }

    // ── UtilityTask Activate/Deactivate Tests ────────────

    [TestFixture]
    public class UtilityTaskTests
    {
        [Test]
        public void Activate_AddsEntityToActiveList()
        {
            var task = new TestTask(0f);
            var entity = new BotEntity(0);

            task.Activate(entity);

            Assert.AreEqual(1, task.ActiveEntityCount);
            Assert.AreSame(entity, task.ActiveEntities[0]);
        }

        [Test]
        public void Activate_DuplicateEntity_NoDoubleAdd()
        {
            var task = new TestTask(0f);
            var entity = new BotEntity(0);

            task.Activate(entity);
            task.Activate(entity);

            Assert.AreEqual(1, task.ActiveEntityCount);
        }

        [Test]
        public void Deactivate_RemovesEntityFromActiveList()
        {
            var task = new TestTask(0f);
            var entity = new BotEntity(0);

            task.Activate(entity);
            task.Deactivate(entity);

            Assert.AreEqual(0, task.ActiveEntityCount);
        }

        [Test]
        public void Deactivate_NonexistentEntity_NoOp()
        {
            var task = new TestTask(0f);
            var entity = new BotEntity(0);

            // Should not throw
            Assert.DoesNotThrow(() => task.Deactivate(entity));
            Assert.AreEqual(0, task.ActiveEntityCount);
        }

        [Test]
        public void Activate_MultipleEntities_AllTracked()
        {
            var task = new TestTask(0f);
            var e1 = new BotEntity(0);
            var e2 = new BotEntity(1);
            var e3 = new BotEntity(2);

            task.Activate(e1);
            task.Activate(e2);
            task.Activate(e3);

            Assert.AreEqual(3, task.ActiveEntityCount);
        }

        [Test]
        public void Deactivate_MiddleEntity_SwapRemoves()
        {
            var task = new TestTask(0f);
            var e1 = new BotEntity(0);
            var e2 = new BotEntity(1);
            var e3 = new BotEntity(2);

            task.Activate(e1);
            task.Activate(e2);
            task.Activate(e3);

            // Remove middle entity — e3 should swap into its position
            task.Deactivate(e2);

            Assert.AreEqual(2, task.ActiveEntityCount);
            // After swap-remove: [e1, e3]
            Assert.AreSame(e1, task.ActiveEntities[0]);
            Assert.AreSame(e3, task.ActiveEntities[1]);
        }

        [Test]
        public void Deactivate_LastEntity_SimplePop()
        {
            var task = new TestTask(0f);
            var e1 = new BotEntity(0);
            var e2 = new BotEntity(1);

            task.Activate(e1);
            task.Activate(e2);

            task.Deactivate(e2);

            Assert.AreEqual(1, task.ActiveEntityCount);
            Assert.AreSame(e1, task.ActiveEntities[0]);
        }
    }

    // ── BotEntity TaskScores Tests ───────────────────────

    [TestFixture]
    public class BotEntityTaskScoresTests
    {
        [Test]
        public void TaskScores_DefaultIsNull()
        {
            var entity = new BotEntity(0);
            Assert.IsNull(entity.TaskScores);
        }

        [Test]
        public void TaskScores_WhenAllocated_InitializedToZero()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[3];

            Assert.AreEqual(0f, entity.TaskScores[0]);
            Assert.AreEqual(0f, entity.TaskScores[1]);
            Assert.AreEqual(0f, entity.TaskScores[2]);
        }

        [Test]
        public void TaskAssignment_DefaultIsEmpty()
        {
            var entity = new BotEntity(0);
            Assert.IsNull(entity.TaskAssignment.Task);
            Assert.AreEqual(0, entity.TaskAssignment.Ordinal);
        }

        [Test]
        public void TaskScores_CanReadAndWrite()
        {
            var entity = new BotEntity(0);
            entity.TaskScores = new float[2];

            entity.TaskScores[0] = 0.7f;
            entity.TaskScores[1] = 0.3f;

            Assert.AreEqual(0.7f, entity.TaskScores[0], 0.001f);
            Assert.AreEqual(0.3f, entity.TaskScores[1], 0.001f);
        }
    }

    // ── Hysteresis Scenario Tests ────────────────────────

    [TestFixture]
    public class HysteresisScenarioTests
    {
        private BotEntity CreateEntity(int id, int taskCount)
        {
            var entity = new BotEntity(id);
            entity.TaskScores = new float[taskCount];
            return entity;
        }

        [Test]
        public void Scenario_BotApproachesObjective_TransitionsFromMoveToGuard()
        {
            // Simulates Phobos GotoObjective→Guard transition
            var moveTask = new TestTask(0.25f); // GotoObjective hysteresis
            var guardTask = new TestTask(0.10f); // Guard hysteresis
            var manager = new UtilityTaskManager(new UtilityTask[] { moveTask, guardTask });
            var bot = CreateEntity(0, 2);

            // Step 1: Far from objective — Move wins
            moveTask.SetScore(0, 0.5f);
            guardTask.SetScore(0, 0f);
            manager.Update(new[] { bot });
            Assert.AreSame(moveTask, bot.TaskAssignment.Task, "Bot should be moving toward objective");

            // Step 2: Approaching — Move still wins (Guard increasing but < Move + hysteresis)
            moveTask.SetScore(0, 0.55f);
            guardTask.SetScore(0, 0.5f); // 0.5 < 0.55 + 0.25 = 0.80
            manager.Update(new[] { bot });
            Assert.AreSame(moveTask, bot.TaskAssignment.Task, "Move should hold with hysteresis");

            // Step 3: At objective boundary — Guard overtakes Move + hysteresis
            moveTask.SetScore(0, 0.15f); // Utility decaying inside radius
            guardTask.SetScore(0, 0.65f); // 0.65 > 0.15 + 0.25 = 0.40
            manager.Update(new[] { bot });
            Assert.AreSame(guardTask, bot.TaskAssignment.Task, "Guard should take over at objective");

            // Step 4: Slight movement — Guard holds with its own hysteresis
            moveTask.SetScore(0, 0.5f);
            guardTask.SetScore(0, 0.55f); // Move needs > 0.55 + 0.10 = 0.65 to retake
            manager.Update(new[] { bot });
            Assert.AreSame(guardTask, bot.TaskAssignment.Task, "Guard should hold with hysteresis");

            // Step 5: Bot pushed out — Move retakes
            moveTask.SetScore(0, 0.7f); // > 0.55 + 0.10 = 0.65
            guardTask.SetScore(0, 0.2f);
            manager.Update(new[] { bot });
            Assert.AreSame(moveTask, bot.TaskAssignment.Task, "Move should retake when bot is pushed out");
        }

        [Test]
        public void Scenario_MultipleBotsAtDifferentDistances()
        {
            var moveTask = new TestTask(0.25f);
            var guardTask = new TestTask(0.10f);
            var manager = new UtilityTaskManager(new UtilityTask[] { moveTask, guardTask });

            var farBot = CreateEntity(0, 2);
            var nearBot = CreateEntity(1, 2);
            var atObjective = CreateEntity(2, 2);

            // Far bot: Move
            moveTask.SetScore(0, 0.5f);
            guardTask.SetScore(0, 0f);

            // Near bot: approaching, still Move
            moveTask.SetScore(1, 0.55f);
            guardTask.SetScore(1, 0.4f);

            // At objective: Guard
            moveTask.SetScore(2, 0.1f);
            guardTask.SetScore(2, 0.65f);

            manager.Update(new[] { farBot, nearBot, atObjective });

            Assert.AreSame(moveTask, farBot.TaskAssignment.Task);
            Assert.AreSame(moveTask, nearBot.TaskAssignment.Task);
            Assert.AreSame(guardTask, atObjective.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_ZeroHysteresis_ImmediateSwitching()
        {
            var taskA = new TestTask(0f);
            var taskB = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
            var bot = CreateEntity(0, 2);

            // A wins
            taskA.SetScore(0, 0.5f);
            taskB.SetScore(0, 0.3f);
            manager.Update(new[] { bot });
            Assert.AreSame(taskA, bot.TaskAssignment.Task);

            // B barely overtakes — switches immediately (no hysteresis)
            taskA.SetScore(0, 0.49f);
            taskB.SetScore(0, 0.50f);
            manager.Update(new[] { bot });
            Assert.AreSame(taskB, bot.TaskAssignment.Task);

            // A barely overtakes back
            taskA.SetScore(0, 0.51f);
            taskB.SetScore(0, 0.50f);
            manager.Update(new[] { bot });
            Assert.AreSame(taskA, bot.TaskAssignment.Task);
        }

        [Test]
        public void Scenario_EntityBecomesInactive_TaskDeactivated()
        {
            var task = new TestTask(0f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var bot = CreateEntity(0, 1);

            task.SetScore(0, 0.5f);
            manager.Update(new[] { bot });
            Assert.AreSame(task, bot.TaskAssignment.Task);
            Assert.AreEqual(1, task.ActiveEntityCount);

            // Bot dies / deactivates
            bot.IsActive = false;
            manager.Update(new[] { bot });

            Assert.IsNull(bot.TaskAssignment.Task);
            Assert.AreEqual(0, task.ActiveEntityCount);
        }

        [Test]
        public void Scenario_TaskScoreDropsToZero_NoOtherTask_StaysCurrent()
        {
            // If the current task scores 0 but no other task scores higher than hysteresis,
            // the entity stays on the current task
            var task = new TestTask(0.25f);
            var manager = new UtilityTaskManager(new UtilityTask[] { task });
            var bot = CreateEntity(0, 1);

            task.SetScore(0, 0.5f);
            manager.Update(new[] { bot });
            Assert.AreSame(task, bot.TaskAssignment.Task);

            // Score drops but 0 + 0.25 = 0.25 — no challenger
            task.SetScore(0, 0f);
            manager.Update(new[] { bot });
            Assert.AreSame(task, bot.TaskAssignment.Task);
        }
    }

    // ── UtilityTaskAssignment Tests ──────────────────────

    [TestFixture]
    public class UtilityTaskAssignmentTests
    {
        [Test]
        public void Default_HasNullTask()
        {
            var assignment = default(UtilityTaskAssignment);
            Assert.IsNull(assignment.Task);
            Assert.AreEqual(0, assignment.Ordinal);
        }

        [Test]
        public void Constructor_StoresTaskAndOrdinal()
        {
            var task = new TestTask(0.1f);
            var assignment = new UtilityTaskAssignment(task, 3);

            Assert.AreSame(task, assignment.Task);
            Assert.AreEqual(3, assignment.Ordinal);
        }
    }
}
