using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using BrainLayerPrioritiesConfig = SPTQuestingBots.Configuration.BrainLayerPrioritiesConfig;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

// ── 1. Task Registration Completeness ───────────────────

[TestFixture]
public class TaskRegistrationTests
{
    [Test]
    public void QuestTaskFactory_TaskCountMatchesActualArray()
    {
        var manager = QuestTaskFactory.Create();
        Assert.AreEqual(
            QuestTaskFactory.TaskCount,
            manager.Tasks.Length,
            "QuestTaskFactory.TaskCount constant must match the actual number of tasks created"
        );
    }

    [Test]
    public void SquadTaskFactory_TaskCountMatchesActualArray()
    {
        var manager = SquadTaskFactory.Create();
        Assert.AreEqual(
            SquadTaskFactory.TaskCount,
            manager.Tasks.Length,
            "SquadTaskFactory.TaskCount constant must match the actual number of tasks created"
        );
    }

    [Test]
    public void QuestTaskFactory_AllTasksAreQuestUtilityTasks()
    {
        var manager = QuestTaskFactory.Create();
        for (int i = 0; i < manager.Tasks.Length; i++)
        {
            Assert.IsInstanceOf<QuestUtilityTask>(
                manager.Tasks[i],
                $"Task at index {i} ({manager.Tasks[i].GetType().Name}) must be a QuestUtilityTask"
            );
        }
    }

    [Test]
    public void SquadTaskFactory_AllTasksAreQuestUtilityTasks()
    {
        var manager = SquadTaskFactory.Create();
        for (int i = 0; i < manager.Tasks.Length; i++)
        {
            Assert.IsInstanceOf<QuestUtilityTask>(
                manager.Tasks[i],
                $"Task at index {i} ({manager.Tasks[i].GetType().Name}) must be a QuestUtilityTask"
            );
        }
    }

    [Test]
    public void QuestTaskFactory_All14TaskTypesPresent()
    {
        var manager = QuestTaskFactory.Create();
        var taskTypes = manager.Tasks.Select(t => t.GetType()).ToHashSet();

        Assert.Contains(typeof(GoToObjectiveTask), taskTypes.ToList());
        Assert.Contains(typeof(AmbushTask), taskTypes.ToList());
        Assert.Contains(typeof(SnipeTask), taskTypes.ToList());
        Assert.Contains(typeof(HoldPositionTask), taskTypes.ToList());
        Assert.Contains(typeof(PlantItemTask), taskTypes.ToList());
        Assert.Contains(typeof(UnlockDoorTask), taskTypes.ToList());
        Assert.Contains(typeof(ToggleSwitchTask), taskTypes.ToList());
        Assert.Contains(typeof(CloseDoorsTask), taskTypes.ToList());
        Assert.Contains(typeof(LootTask), taskTypes.ToList());
        Assert.Contains(typeof(VultureTask), taskTypes.ToList());
        Assert.Contains(typeof(LingerTask), taskTypes.ToList());
        Assert.Contains(typeof(InvestigateTask), taskTypes.ToList());
        Assert.Contains(typeof(SpawnEntryTask), taskTypes.ToList());
        Assert.Contains(typeof(PatrolTask), taskTypes.ToList());
    }

    [Test]
    public void QuestTaskFactory_NoDuplicateTaskTypes()
    {
        var manager = QuestTaskFactory.Create();
        var taskTypes = manager.Tasks.Select(t => t.GetType()).ToList();
        var uniqueTypes = taskTypes.Distinct().ToList();

        Assert.AreEqual(taskTypes.Count, uniqueTypes.Count, "QuestTaskFactory should not register duplicate task types");
    }

    [Test]
    public void SquadTaskFactory_ContainsExpectedTasks()
    {
        var manager = SquadTaskFactory.Create();
        Assert.IsInstanceOf<GoToTacticalPositionTask>(manager.Tasks[0]);
        Assert.IsInstanceOf<HoldTacticalPositionTask>(manager.Tasks[1]);
    }
}

// ── 2. BotActionTypeId ↔ GetNextAction Completeness ──────

[TestFixture]
public class ActionTypeIdMappingTests
{
    [Test]
    public void BotActionTypeId_AllQuestTasks_HaveValidActionTypeId()
    {
        // Every task in QuestTaskFactory must map to a valid, non-zero BotActionTypeId
        var manager = QuestTaskFactory.Create();
        for (int i = 0; i < manager.Tasks.Length; i++)
        {
            var task = (QuestUtilityTask)manager.Tasks[i];
            Assert.That(
                task.BotActionTypeId,
                Is.GreaterThan(0),
                $"Task {task.GetType().Name} has invalid BotActionTypeId={task.BotActionTypeId}"
            );
        }
    }

    [Test]
    public void BotActionTypeId_AllQuestTasks_MapToDistinctOrIntentionallySharedIds()
    {
        // Some tasks intentionally share BotActionTypeId (e.g. GoToTacticalPositionTask uses GoToObjective).
        // For quest tasks, verify that the mapping is intentional.
        var manager = QuestTaskFactory.Create();
        var idToTasks = new Dictionary<int, List<string>>();

        for (int i = 0; i < manager.Tasks.Length; i++)
        {
            var task = (QuestUtilityTask)manager.Tasks[i];
            if (!idToTasks.ContainsKey(task.BotActionTypeId))
                idToTasks[task.BotActionTypeId] = new List<string>();
            idToTasks[task.BotActionTypeId].Add(task.GetType().Name);
        }

        // Each BotActionTypeId should map to exactly one quest task
        foreach (var kv in idToTasks)
        {
            Assert.AreEqual(
                1,
                kv.Value.Count,
                $"BotActionTypeId {kv.Key} is shared by multiple quest tasks: {string.Join(", ", kv.Value)}. "
                    + "If intentional, update this test."
            );
        }
    }

    [Test]
    public void BotActionTypeId_Constants_AreContiguous()
    {
        // Verify that BotActionTypeId constants form a contiguous range from 0..18
        var fields = typeof(BotActionTypeId)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(f => f.FieldType == typeof(int))
            .Select(f => (int)f.GetValue(null))
            .OrderBy(v => v)
            .ToList();

        Assert.AreEqual(0, fields.First(), "First BotActionTypeId should be 0 (Undefined)");
        Assert.AreEqual(18, fields.Last(), "Last BotActionTypeId should be 18 (Patrol)");

        // Check contiguity
        for (int i = 0; i < fields.Count - 1; i++)
        {
            Assert.AreEqual(fields[i] + 1, fields[i + 1], $"Gap detected between BotActionTypeId values {fields[i]} and {fields[i + 1]}");
        }
    }

    [Test]
    public void BotActionTypeId_ConstantCount_Matches19Values()
    {
        // 0=Undefined through 18=Patrol = 19 constants
        var count = typeof(BotActionTypeId)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Count(f => f.FieldType == typeof(int));

        Assert.AreEqual(19, count, "BotActionTypeId should have exactly 19 constants (0..18)");
    }

    [Test]
    public void BotActionTypeId_AllExtendedTaskIds_ArePresent()
    {
        // Verify the extended task IDs (Loot through Patrol) are correctly defined
        Assert.AreEqual(13, BotActionTypeId.Loot);
        Assert.AreEqual(14, BotActionTypeId.Vulture);
        Assert.AreEqual(15, BotActionTypeId.Linger);
        Assert.AreEqual(16, BotActionTypeId.Investigate);
        Assert.AreEqual(17, BotActionTypeId.SpawnEntry);
        Assert.AreEqual(18, BotActionTypeId.Patrol);
    }
}

// ── 3. Task Scoring with Personality & Raid Time Modifiers ──

[TestFixture]
public class TaskScoringModifierIntegrationTests
{
    private BotEntity CreateEntity(int id, int questAction, float aggression = 0.5f, float raidTimeRatio = 0.5f)
    {
        var entity = new BotEntity(id);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.CurrentQuestAction = questAction;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.Aggression = aggression;
        entity.RaidTimeNormalized = raidTimeRatio;
        return entity;
    }

    [Test]
    public void GoToObjectiveTask_Score_DoesNotApplyModifiers()
    {
        // GoToObjectiveTask uses raw distance-based scoring, no personality/raid modifiers
        var entity1 = CreateEntity(0, QuestActionId.MoveToPosition, aggression: 0.0f);
        var entity2 = CreateEntity(1, QuestActionId.MoveToPosition, aggression: 1.0f);

        float score1 = GoToObjectiveTask.Score(entity1);
        float score2 = GoToObjectiveTask.Score(entity2);

        Assert.AreEqual(score1, score2, 0.001f, "GoToObjectiveTask should not vary with aggression");
    }

    [Test]
    public void AllQuestTasks_ScoreNonNegative_ForAnyValidEntity()
    {
        // Verify that no task produces a negative score under normal conditions
        var manager = QuestTaskFactory.Create();

        // Test with all quest action types
        int[] actions =
        {
            QuestActionId.MoveToPosition,
            QuestActionId.HoldAtPosition,
            QuestActionId.Ambush,
            QuestActionId.Snipe,
            QuestActionId.PlantItem,
            QuestActionId.ToggleSwitch,
            QuestActionId.CloseNearbyDoors,
        };

        foreach (var action in actions)
        {
            var entity = CreateEntity(0, action);
            entity.IsCloseToObjective = true;
            entity.MustUnlockDoor = false;
            entity.DistanceToObjective = 50f;

            manager.ScoreAndPick(entity);

            for (int i = 0; i < entity.TaskScores.Length; i++)
            {
                Assert.That(
                    entity.TaskScores[i],
                    Is.GreaterThanOrEqualTo(0f),
                    $"Task ordinal {i} produced negative score {entity.TaskScores[i]} for action {action}"
                );
            }
        }
    }

    [Test]
    public void AllQuestTasks_ScoreFinite_ForAnyValidEntity()
    {
        // Verify that no task produces NaN or Infinity under normal conditions
        var manager = QuestTaskFactory.Create();
        var entity = CreateEntity(0, QuestActionId.MoveToPosition);
        entity.DistanceToObjective = 50f;

        manager.ScoreAndPick(entity);

        for (int i = 0; i < entity.TaskScores.Length; i++)
        {
            Assert.IsFalse(float.IsNaN(entity.TaskScores[i]), $"Task ordinal {i} produced NaN score");
            Assert.IsFalse(float.IsInfinity(entity.TaskScores[i]), $"Task ordinal {i} produced Infinity score");
        }
    }
}

// ── 4. Cross-TaskManager Stale Assignment Bug ────────────

[TestFixture]
public class CrossTaskManagerAssignmentTests
{
    private BotEntity CreateEntity(int id, int scoreSlots)
    {
        var entity = new BotEntity(id);
        entity.TaskScores = new float[scoreSlots];
        return entity;
    }

    [Test]
    public void StaleAssignment_FromLargerTaskManager_FixedByRemoveEntity()
    {
        // Fixed scenario: entity switches from a 14-task manager to a 2-task manager.
        // The caller (BotObjectiveLayer) now clears the stale assignment before switching.
        var questTaskA = new TestTask(0.0f);
        var questTaskB = new TestTask(0.0f);
        var questTaskC = new TestTask(0.0f); // ordinal=2

        var questManager = new UtilityTaskManager(new UtilityTask[] { questTaskA, questTaskB, questTaskC });
        var entity = CreateEntity(0, 3);

        // Quest task C wins with high score at ordinal=2
        questTaskA.SetScore(0, 0.1f);
        questTaskB.SetScore(0, 0.2f);
        questTaskC.SetScore(0, 0.9f);
        questManager.ScoreAndPick(entity);
        Assert.AreSame(questTaskC, entity.TaskAssignment.Task);
        Assert.AreEqual(2, entity.TaskAssignment.Ordinal);

        // FIX: clear stale assignment before switching managers
        // (BotObjectiveLayer does this when _lastManagerWasFollower changes)
        questManager.RemoveEntity(entity);
        Assert.IsNull(entity.TaskAssignment.Task);

        // Now switch to a 2-task "follower" manager
        var followerTask1 = new TestTask(0.0f);
        var followerTask2 = new TestTask(0.0f);
        var followerManager = new UtilityTaskManager(new UtilityTask[] { followerTask1, followerTask2 });

        // Follower tasks score modest values at ordinals 0 and 1
        followerTask1.SetScore(0, 0.4f);
        followerTask2.SetScore(0, 0.5f);

        // Score and pick — no stale assignment to interfere
        followerManager.ScoreAndPick(entity);

        // followerTask2 wins with highest score 0.5
        Assert.AreSame(followerTask2, entity.TaskAssignment.Task, "After clearing stale assignment, follower task manager picks correctly");
    }

    [Test]
    public void StaleAssignment_CanBeFixedByClearingAssignment()
    {
        // Demonstrate the fix: clear TaskAssignment before switching managers
        var questTask = new TestTask(0.0f);
        var questManager = new UtilityTaskManager(new UtilityTask[] { questTask });
        var entity = CreateEntity(0, 2);

        questTask.SetScore(0, 0.9f);
        questManager.ScoreAndPick(entity);
        Assert.AreSame(questTask, entity.TaskAssignment.Task);

        // FIX: Remove entity from old manager before switching
        questManager.RemoveEntity(entity);
        Assert.IsNull(entity.TaskAssignment.Task);

        // Now follower manager works correctly
        var followerTask = new TestTask(0.0f);
        var followerManager = new UtilityTaskManager(new UtilityTask[] { followerTask });

        followerTask.SetScore(0, 0.5f);
        followerManager.ScoreAndPick(entity);
        Assert.AreSame(followerTask, entity.TaskAssignment.Task);
    }

    [Test]
    public void ScoreArrayResize_PreservesStaleScores()
    {
        // If TaskScores is resized from 2 to 14, old scores at [0] and [1] are lost
        // because the code allocates a NEW array. Verify this behavior.
        var entity = new BotEntity(0);
        entity.TaskScores = new float[2];
        entity.TaskScores[0] = 0.8f;
        entity.TaskScores[1] = 0.6f;

        // Simulate the resize check from BotObjectiveLayer.trySetNextActionUtility
        if (entity.TaskScores == null || entity.TaskScores.Length < QuestTaskFactory.TaskCount)
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        // Old scores are gone — new array is all zeros
        Assert.AreEqual(0f, entity.TaskScores[0]);
        Assert.AreEqual(0f, entity.TaskScores[1]);
        Assert.AreEqual(QuestTaskFactory.TaskCount, entity.TaskScores.Length);
    }
}

// ── 5. UtilityTaskManager Edge Cases ─────────────────────

[TestFixture]
public class UtilityTaskManagerEdgeCaseTests
{
    [Test]
    public void PickTask_WithNegativeScore_IgnoresNegativeTask()
    {
        var taskA = new TestTask(0f);
        var taskB = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        var entity = new BotEntity(0);
        entity.TaskScores = new float[2];

        taskA.SetScore(0, -0.5f);
        taskB.SetScore(0, 0.3f);
        manager.ScoreAndPick(entity);

        Assert.AreSame(taskB, entity.TaskAssignment.Task);
    }

    [Test]
    public void PickTask_AllNegativeScores_NoTaskAssigned()
    {
        var taskA = new TestTask(0f);
        var taskB = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        var entity = new BotEntity(0);
        entity.TaskScores = new float[2];

        taskA.SetScore(0, -0.5f);
        taskB.SetScore(0, -0.3f);
        manager.ScoreAndPick(entity);

        Assert.IsNull(entity.TaskAssignment.Task);
    }

    [Test]
    public void PickTask_NaNScore_DoesNotWinSelection()
    {
        // NaN scores are explicitly skipped by the IsNaN guard
        var taskA = new TestTask(0f);
        var taskB = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        var entity = new BotEntity(0);
        entity.TaskScores = new float[2];

        taskA.SetScore(0, float.NaN);
        taskB.SetScore(0, 0.3f);

        manager.ScoreAndPick(entity);

        // TaskB should win because NaN is explicitly skipped
        Assert.AreSame(taskB, entity.TaskAssignment.Task);
    }

    [Test]
    public void PickTask_AllNaNScores_NoTaskAssigned()
    {
        // When all scores are NaN, no task should be assigned
        var taskA = new TestTask(0f);
        var taskB = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        var entity = new BotEntity(0);
        entity.TaskScores = new float[2];

        taskA.SetScore(0, float.NaN);
        taskB.SetScore(0, float.NaN);

        manager.ScoreAndPick(entity);

        Assert.IsNull(entity.TaskAssignment.Task, "All-NaN scores should leave no task assigned");
    }

    [Test]
    public void PickTask_CurrentTaskScoreNaN_WithHysteresis_HandledGracefully()
    {
        // Fixed: NaN in current task score is guarded — highestScore resets to 0,
        // and NaN scores in the loop are skipped. The highest finite-scored task wins.
        var taskA = new TestTask(0.2f);
        var taskB = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        var entity = new BotEntity(0);
        entity.TaskScores = new float[2];

        // First: assign taskA normally
        taskA.SetScore(0, 0.5f);
        taskB.SetScore(0, 0.3f);
        manager.ScoreAndPick(entity);
        Assert.AreSame(taskA, entity.TaskAssignment.Task);

        // Now taskA's score becomes NaN
        entity.TaskScores[0] = float.NaN;

        // PickTask trace (fixed):
        //   highestScore = NaN + 0.2 = NaN → reset to 0
        //   j=0: NaN → skipped (IsNaN guard)
        //   j=1: 0.3 > 0 → nextTask=taskB, highestScore=0.3
        // Result: taskB correctly wins as the highest finite-scored task
        manager.PickTask(entity);

        Assert.AreSame(taskB, entity.TaskAssignment.Task, "NaN-scored task should be skipped; highest finite task wins");
    }

    [Test]
    public void PickTask_InfinityScore_WinsSelection()
    {
        var taskA = new TestTask(0f);
        var taskB = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        var entity = new BotEntity(0);
        entity.TaskScores = new float[2];

        taskA.SetScore(0, float.PositiveInfinity);
        taskB.SetScore(0, 0.99f);
        manager.ScoreAndPick(entity);

        Assert.AreSame(taskA, entity.TaskAssignment.Task);
    }

    [Test]
    public void PickTask_EmptyEntitiesList_DoesNotThrow()
    {
        var task = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { task });

        Assert.DoesNotThrow(() => manager.Update(Array.Empty<BotEntity>()));
    }

    [Test]
    public void PickTask_ZeroTasks_DoesNotThrow()
    {
        var manager = new UtilityTaskManager(Array.Empty<UtilityTask>());
        var entity = new BotEntity(0);
        entity.TaskScores = Array.Empty<float>();

        Assert.DoesNotThrow(() => manager.ScoreAndPick(entity));
        Assert.IsNull(entity.TaskAssignment.Task);
    }

    [Test]
    public void ScoreAndPick_EntityReactivated_CanGetNewTask()
    {
        var task = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { task });
        var entity = new BotEntity(0);
        entity.TaskScores = new float[1];

        task.SetScore(0, 0.5f);
        manager.ScoreAndPick(entity);
        Assert.AreSame(task, entity.TaskAssignment.Task);

        // Deactivate
        entity.IsActive = false;
        manager.ScoreAndPick(entity);
        Assert.IsNull(entity.TaskAssignment.Task);

        // Reactivate
        entity.IsActive = true;
        manager.ScoreAndPick(entity);
        Assert.AreSame(task, entity.TaskAssignment.Task);
    }
}

// ── 6. BotRegistry Edge Cases ────────────────────────────

[TestFixture]
public class BotRegistryEdgeCaseTests
{
    [Test]
    public void AddWithBsgId_SetsUpSparseArrayLookup()
    {
        var registry = new BotRegistry();
        var entity = registry.Add(42);

        Assert.AreSame(entity, registry.GetByBsgId(42));
    }

    [Test]
    public void GetByBsgId_NegativeId_ReturnsNull()
    {
        var registry = new BotRegistry();
        Assert.IsNull(registry.GetByBsgId(-1));
    }

    [Test]
    public void GetByBsgId_OutOfRange_ReturnsNull()
    {
        var registry = new BotRegistry();
        registry.Add(5);

        Assert.IsNull(registry.GetByBsgId(100));
    }

    [Test]
    public void ClearBsgId_RemovesSparseMapping()
    {
        var registry = new BotRegistry();
        var entity = registry.Add(10);
        Assert.AreSame(entity, registry.GetByBsgId(10));

        registry.ClearBsgId(10);
        Assert.IsNull(registry.GetByBsgId(10));
    }

    [Test]
    public void ClearBsgId_NegativeId_DoesNotThrow()
    {
        var registry = new BotRegistry();
        Assert.DoesNotThrow(() => registry.ClearBsgId(-1));
    }

    [Test]
    public void ClearBsgId_OutOfRange_DoesNotThrow()
    {
        var registry = new BotRegistry();
        Assert.DoesNotThrow(() => registry.ClearBsgId(100));
    }

    [Test]
    public void AddWithBsgId_LargeId_GrowsSparseArray()
    {
        var registry = new BotRegistry();
        var entity = registry.Add(1000);

        Assert.AreSame(entity, registry.GetByBsgId(1000));
        Assert.AreEqual(1, registry.Count);
    }

    [Test]
    public void AddWithBsgId_ZeroId_Works()
    {
        var registry = new BotRegistry();
        var entity = registry.Add(0);

        Assert.AreSame(entity, registry.GetByBsgId(0));
        Assert.AreEqual(0, entity.BsgId);
    }

    [Test]
    public void Add_WithoutBsgId_HasNegativeBsgId()
    {
        var registry = new BotRegistry();
        var entity = registry.Add();

        Assert.AreEqual(-1, entity.BsgId, "Entities added without BSG ID should have BsgId=-1");
    }

    [Test]
    public void Remove_WithoutBsgId_DoesNotThrow()
    {
        var registry = new BotRegistry();
        var entity = registry.Add();
        Assert.AreEqual(-1, entity.BsgId);

        Assert.DoesNotThrow(() => registry.Remove(entity));
        Assert.AreEqual(0, registry.Count);
    }

    [Test]
    public void Remove_AutoClearsBsgId()
    {
        // Fixed: BotRegistry.Remove() now auto-clears the BSG ID mapping
        // using the BsgId field stored on the entity.
        var registry = new BotRegistry();
        var entity = registry.Add(42);
        Assert.AreEqual(42, entity.BsgId);

        registry.Remove(entity);

        // The BSG ID mapping is cleared automatically
        Assert.IsNull(registry.GetByBsgId(42), "Remove() should auto-clear BSG ID mapping");
    }

    [Test]
    public void Clear_ClearsBsgIdMappings()
    {
        var registry = new BotRegistry();
        registry.Add(10);
        registry.Add(20);

        registry.Clear();

        Assert.IsNull(registry.GetByBsgId(10));
        Assert.IsNull(registry.GetByBsgId(20));
    }

    [Test]
    public void Remove_ThenReAdd_WithSameBsgId_Works()
    {
        var registry = new BotRegistry();
        var e1 = registry.Add(42);
        registry.Remove(e1);
        // No need to call ClearBsgId manually — Remove() does it automatically

        var e2 = registry.Add(42);
        Assert.AreSame(e2, registry.GetByBsgId(42));
        Assert.AreNotSame(e1, e2);
    }

    [Test]
    public void Remove_AllEntities_ThenAdd_ResetsIds()
    {
        var registry = new BotRegistry();
        var e0 = registry.Add();
        var e1 = registry.Add();
        var e2 = registry.Add();

        // Remove all — the "remove last entity" path resets everything
        registry.Remove(e2);
        registry.Remove(e1);
        registry.Remove(e0);

        Assert.AreEqual(0, registry.Count);

        // IDs should start fresh
        var fresh = registry.Add();
        Assert.AreEqual(0, fresh.Id);
    }

    [Test]
    public void Indexer_FreedId_ThrowsKeyNotFound()
    {
        var registry = new BotRegistry();
        var e0 = registry.Add();
        var e1 = registry.Add();
        registry.Remove(e0);

        Assert.Throws<KeyNotFoundException>(() =>
        {
            var _ = registry[0];
        });

        // e1 is still accessible
        Assert.AreSame(e1, registry[1]);
    }
}

// ── 7. BotLodCalculator Edge Cases ───────────────────────

[TestFixture]
public class BotLodCalculatorEdgeCaseTests
{
    [Test]
    public void ShouldSkipUpdate_NegativeSkip_DivisionByZeroGuard()
    {
        // Skip=-1 means cycle length = 0, which causes DivideByZeroException
        // Documenting that negative skip values are NOT safe
        Assert.Throws<DivideByZeroException>(() => BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, 0, -1, 4));
    }

    [Test]
    public void ShouldSkipUpdate_NegativeFrameCounter_StillWorks()
    {
        // C# modulo with negative dividend can return negative values
        // -1 % 3 = -1 (in C#), which != 0, so it would skip
        bool skip = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, -1, 2, 4);
        Assert.IsTrue(skip, "Negative frame counter should be treated as non-zero modulo");
    }

    [Test]
    public void ComputeTier_NaN_ReturnsFull()
    {
        // NaN >= threshold is false for all comparisons, so it falls through to TierFull
        byte tier = BotLodCalculator.ComputeTier(float.NaN, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierFull, tier);
    }

    [Test]
    public void ComputeTier_NegativeDistance_ReturnsFull()
    {
        byte tier = BotLodCalculator.ComputeTier(-100f, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierFull, tier);
    }

    [Test]
    public void ComputeTier_Infinity_ReturnsMinimal()
    {
        byte tier = BotLodCalculator.ComputeTier(float.PositiveInfinity, 22500f, 90000f);
        Assert.AreEqual(BotLodCalculator.TierMinimal, tier);
    }

    [Test]
    public void ComputeTier_ZeroThresholds_AllMinimal()
    {
        // If both thresholds are 0, any positive distance is >= both
        byte tier = BotLodCalculator.ComputeTier(1f, 0f, 0f);
        Assert.AreEqual(BotLodCalculator.TierMinimal, tier);
    }

    [Test]
    public void ComputeTier_EqualThresholds_NoReducedTier()
    {
        // If reduced == minimal, the reduced band has zero width.
        // Distance < threshold → Full; Distance >= threshold → Minimal (checked first)
        byte tierBelow = BotLodCalculator.ComputeTier(49999f, 50000f, 50000f);
        byte tierAt = BotLodCalculator.ComputeTier(50000f, 50000f, 50000f);

        Assert.AreEqual(BotLodCalculator.TierFull, tierBelow);
        Assert.AreEqual(BotLodCalculator.TierMinimal, tierAt);
    }

    [Test]
    public void ShouldSkipUpdate_UnknownTier_SkipsLikeMinimal()
    {
        // Tier values > 2 fall into the else branch (same as minimal)
        bool skip = BotLodCalculator.ShouldSkipUpdate(255, 1, 2, 4);
        bool minimalSkip = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, 1, 2, 4);
        Assert.AreEqual(minimalSkip, skip);
    }

    [Test]
    public void ShouldSkipUpdate_FrameCounterOverflow_HandledByModulo()
    {
        // int.MaxValue % (skip+1) is well-defined
        bool result = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, int.MaxValue, 2, 4);
        // int.MaxValue % 3 = int.MaxValue - (int.MaxValue/3)*3
        // 2147483647 % 3 = 2 → != 0 → skip
        Assert.IsTrue(result);
    }
}

// ── 8. BrainLayerPrioritiesConfig Defaults ───────────────

[TestFixture]
public class BrainLayerPrioritiesConfigTests
{
    [Test]
    public void Defaults_MatchExpectedPriorities()
    {
        var config = new BrainLayerPrioritiesConfig();

        Assert.AreEqual(99, config.Sleeping, "Sleeping should have highest priority (99)");
        Assert.AreEqual(26, config.Regrouping, "Regrouping should be second highest (26)");
        Assert.AreEqual(19, config.Following, "Following should be third (19)");
        Assert.AreEqual(18, config.Questing, "Questing should have lowest priority (18)");
    }

    [Test]
    public void Priorities_SleepingGreatest()
    {
        var config = new BrainLayerPrioritiesConfig();
        Assert.That(config.Sleeping, Is.GreaterThan(config.Regrouping));
        Assert.That(config.Regrouping, Is.GreaterThan(config.Following));
        Assert.That(config.Following, Is.GreaterThan(config.Questing));
    }

    [Test]
    public void Priorities_AllDistinct()
    {
        var config = new BrainLayerPrioritiesConfig();
        var priorities = new[] { config.Sleeping, config.Regrouping, config.Following, config.Questing };
        Assert.AreEqual(4, priorities.Distinct().Count(), "All layer priorities must be distinct");
    }
}

// ── 9. Full Pipeline Integration: QuestTaskFactory → Score → Pick → Lifecycle ─

[TestFixture]
public class FullPipelineIntegrationTests
{
    [Test]
    public void Pipeline_AllTasksCanScore_WithDefaultEntity()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 50f;

        // Should not throw
        Assert.DoesNotThrow(() => manager.ScoreAndPick(entity));
    }

    [Test]
    public void Pipeline_SquadTasks_CanScore_WithDefaultEntity()
    {
        var manager = SquadTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 100f;
        entity.TacticalPositionZ = 200f;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;

        Assert.DoesNotThrow(() => manager.ScoreAndPick(entity));
    }

    [Test]
    public void Pipeline_EntityDeactivated_DuringScoring_HandledGracefully()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 50f;

        // Score and pick — entity gets a task
        manager.ScoreAndPick(entity);
        Assert.IsNotNull(entity.TaskAssignment.Task);

        // Deactivate between scoring calls
        entity.IsActive = false;
        manager.ScoreAndPick(entity);
        Assert.IsNull(entity.TaskAssignment.Task);
    }

    [Test]
    public void Pipeline_RapidTaskSwitching_LifecycleCallsCorrect()
    {
        // Simulate rapid quest action changes across 5 ticks
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = true;

        var observedTasks = new List<Type>();

        // Tick 1: MoveToPosition (far)
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;
        manager.ScoreAndPick(entity);
        if (entity.TaskAssignment.Task != null)
            observedTasks.Add(entity.TaskAssignment.Task.GetType());

        // Tick 2: HoldAtPosition
        entity.CurrentQuestAction = QuestActionId.HoldAtPosition;
        manager.ScoreAndPick(entity);
        if (entity.TaskAssignment.Task != null)
            observedTasks.Add(entity.TaskAssignment.Task.GetType());

        // Tick 3: Ambush (close)
        entity.CurrentQuestAction = QuestActionId.Ambush;
        entity.IsCloseToObjective = true;
        manager.ScoreAndPick(entity);
        if (entity.TaskAssignment.Task != null)
            observedTasks.Add(entity.TaskAssignment.Task.GetType());

        // Tick 4: ToggleSwitch
        entity.CurrentQuestAction = QuestActionId.ToggleSwitch;
        manager.ScoreAndPick(entity);
        if (entity.TaskAssignment.Task != null)
            observedTasks.Add(entity.TaskAssignment.Task.GetType());

        // Tick 5: MoveToPosition again
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.IsCloseToObjective = false;
        manager.ScoreAndPick(entity);
        if (entity.TaskAssignment.Task != null)
            observedTasks.Add(entity.TaskAssignment.Task.GetType());

        // All 5 ticks should produce a task
        Assert.AreEqual(5, observedTasks.Count, "Every tick should produce a task assignment");

        // Verify expected task types
        Assert.AreEqual(typeof(GoToObjectiveTask), observedTasks[0]);
        Assert.AreEqual(typeof(HoldPositionTask), observedTasks[1]);
        Assert.AreEqual(typeof(AmbushTask), observedTasks[2]);
        Assert.AreEqual(typeof(ToggleSwitchTask), observedTasks[3]);
        Assert.AreEqual(typeof(GoToObjectiveTask), observedTasks[4]);
    }

    [Test]
    public void Pipeline_MultiEntity_IndependentLifecycle()
    {
        var manager = QuestTaskFactory.Create();

        var bot1 = new BotEntity(0);
        bot1.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot1.HasActiveObjective = true;
        bot1.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot1.DistanceToObjective = 100f;

        var bot2 = new BotEntity(1);
        bot2.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot2.HasActiveObjective = true;
        bot2.CurrentQuestAction = QuestActionId.Ambush;
        bot2.IsCloseToObjective = true;

        manager.Update(new[] { bot1, bot2 });

        Assert.IsInstanceOf<GoToObjectiveTask>(bot1.TaskAssignment.Task);
        Assert.IsInstanceOf<AmbushTask>(bot2.TaskAssignment.Task);

        // Kill bot1, bot2 still works
        bot1.IsActive = false;
        manager.Update(new[] { bot1, bot2 });

        Assert.IsNull(bot1.TaskAssignment.Task);
        Assert.IsInstanceOf<AmbushTask>(bot2.TaskAssignment.Task);
    }
}
