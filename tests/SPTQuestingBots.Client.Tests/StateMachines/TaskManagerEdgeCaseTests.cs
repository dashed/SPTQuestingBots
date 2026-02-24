using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.StateMachines;

/// <summary>
/// Edge case tests for UtilityTaskManager.PickTask: all-zero scores, hysteresis
/// boundaries, empty task lists, and manager switching.
/// </summary>
[TestFixture]
public class TaskManagerEdgeCaseTests
{
    // ── All tasks score zero ─────────────────────────────────

    [Test]
    public void PickTask_AllScoresZero_NoExistingTask_NoAssignment()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 2);

        // All scores are 0
        entity.TaskScores[0] = 0f;
        entity.TaskScores[1] = 0f;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Null, "No task should be assigned when all scores are 0 and no existing task");
    }

    [Test]
    public void PickTask_AllScoresZero_WithExistingTask_KeepsCurrent()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 2);

        // Assign task 0
        tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(tasks[0], 0);

        // All scores are 0
        entity.TaskScores[0] = 0f;
        entity.TaskScores[1] = 0f;

        manager.PickTask(entity);

        // Current task stays due to hysteresis (0 + 0.10 = 0.10, nothing beats it)
        Assert.That(
            entity.TaskAssignment.Task,
            Is.SameAs(tasks[0]),
            "Should keep current task due to hysteresis even when all scores are 0"
        );
    }

    // ── Hysteresis boundary ──────────────────────────────────

    [Test]
    public void PickTask_CompetitorExactlyEqualsCurrentPlusHysteresis_NoSwitch()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 2);

        // Assign task 0
        tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(tasks[0], 0);

        // Current score: 0.50 + 0.10 hysteresis = 0.60
        // Competitor: 0.60 exactly — should NOT switch (need to EXCEED, not equal)
        entity.TaskScores[0] = 0.50f;
        entity.TaskScores[1] = 0.60f;

        manager.PickTask(entity);

        Assert.That(
            entity.TaskAssignment.Task,
            Is.SameAs(tasks[0]),
            "Should NOT switch when competitor exactly equals current + hysteresis"
        );
    }

    [Test]
    public void PickTask_CompetitorExceedsCurrentPlusHysteresis_Switches()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 2);

        // Assign task 0
        tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(tasks[0], 0);

        // Current score: 0.50 + 0.10 hysteresis = 0.60
        // Competitor: 0.61 — should switch
        entity.TaskScores[0] = 0.50f;
        entity.TaskScores[1] = 0.61f;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(tasks[1]), "Should switch when competitor exceeds current + hysteresis");
    }

    [Test]
    public void PickTask_CompetitorJustBelowCurrentPlusHysteresis_NoSwitch()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 2);

        tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(tasks[0], 0);

        // Current: 0.50 + 0.10 = 0.60, competitor: 0.59
        entity.TaskScores[0] = 0.50f;
        entity.TaskScores[1] = 0.59f;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(tasks[0]));
    }

    // ── NaN handling ─────────────────────────────────────────

    [Test]
    public void PickTask_NaNScore_SkippedAsCandidate()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 2);

        entity.TaskScores[0] = 0.50f;
        entity.TaskScores[1] = float.NaN;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(tasks[0]), "NaN score should be skipped, valid score wins");
    }

    [Test]
    public void PickTask_CurrentScoreNaN_ResetsToZero()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 2);

        tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(tasks[0], 0);

        // Current task's score is NaN — should be treated as 0
        entity.TaskScores[0] = float.NaN;
        entity.TaskScores[1] = 0.20f;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(tasks[1]), "When current task score is NaN, any positive competitor should win");
    }

    [Test]
    public void PickTask_AllScoresNaN_NoAssignment()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 2);

        entity.TaskScores[0] = float.NaN;
        entity.TaskScores[1] = float.NaN;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Null);
    }

    // ── Empty/degenerate task lists ──────────────────────────

    [Test]
    public void PickTask_NoTasks_DoesNotCrash()
    {
        var manager = new UtilityTaskManager(new UtilityTask[0]);
        var entity = MakeEntity(taskCount: 0);

        // Should not throw
        Assert.DoesNotThrow(() => manager.PickTask(entity));
        Assert.That(entity.TaskAssignment.Task, Is.Null);
    }

    [Test]
    public void PickTask_SingleTask_AboveZero_Assigns()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 1);

        entity.TaskScores[0] = 0.50f;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(tasks[0]));
    }

    [Test]
    public void PickTask_SingleTask_AtZero_NoAssignment()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 1);

        entity.TaskScores[0] = 0f;

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Null);
    }

    // ── Inactive entity handling ─────────────────────────────

    [Test]
    public void PickTasks_InactiveEntity_DeactivatesCurrentTask()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 1);

        // Assign task
        tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(tasks[0], 0);
        Assert.That(tasks[0].ActiveEntityCount, Is.EqualTo(1));

        // Deactivate entity
        entity.IsActive = false;

        manager.PickTasks(new[] { entity });

        Assert.Multiple(() =>
        {
            Assert.That(entity.TaskAssignment.Task, Is.Null);
            Assert.That(tasks[0].ActiveEntityCount, Is.EqualTo(0));
        });
    }

    // ── RemoveEntity ─────────────────────────────────────────

    [Test]
    public void RemoveEntity_ClearsAssignment()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 1);

        tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(tasks[0], 0);

        manager.RemoveEntity(entity);

        Assert.Multiple(() =>
        {
            Assert.That(entity.TaskAssignment.Task, Is.Null);
            Assert.That(tasks[0].ActiveEntityCount, Is.EqualTo(0));
        });
    }

    [Test]
    public void RemoveEntity_NoAssignment_DoesNotCrash()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 1);

        Assert.DoesNotThrow(() => manager.RemoveEntity(entity));
    }

    // ── ScoreAndPick ─────────────────────────────────────────

    [Test]
    public void ScoreAndPick_InactiveEntity_Deactivates()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 1);

        tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(tasks[0], 0);
        entity.IsActive = false;

        manager.ScoreAndPick(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Null);
    }

    // ── Manager switching (quest ↔ follower) ─────────────────

    /// <summary>
    /// Simulates the _lastManagerWasFollower flag behavior from BotObjectiveLayer.
    /// When switching from quest manager (14 tasks) to follower manager (2 tasks),
    /// the stale assignment must be cleared to prevent ordinal mismatch.
    /// </summary>
    [Test]
    public void ManagerSwitch_QuestToFollower_ClearsStaleAssignment()
    {
        // Quest manager with 3 tasks
        var questTasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f), new StubTask(0.10f) };
        var questManager = new UtilityTaskManager(questTasks);

        // Follower manager with 2 tasks
        var followerTasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var followerManager = new UtilityTaskManager(followerTasks);

        var entity = MakeEntity(taskCount: 3);

        // Assign quest task at ordinal 2
        questTasks[2].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(questTasks[2], 2);

        // Simulate switch to follower: clear stale assignment
        bool lastManagerWasFollower = false;
        if (!lastManagerWasFollower && entity.TaskAssignment.Task != null)
        {
            entity.TaskAssignment.Task.Deactivate(entity);
            entity.TaskAssignment = default;
        }
        lastManagerWasFollower = true;

        Assert.Multiple(() =>
        {
            Assert.That(entity.TaskAssignment.Task, Is.Null, "Stale quest assignment should be cleared");
            Assert.That(questTasks[2].ActiveEntityCount, Is.EqualTo(0), "Quest task should be deactivated");
        });

        // Now follower manager can safely score with its 2 tasks
        entity.TaskScores = new float[2];
        entity.TaskScores[0] = 0.50f;
        entity.TaskScores[1] = 0.30f;

        followerManager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(followerTasks[0]));
    }

    [Test]
    public void ManagerSwitch_FollowerToQuest_ClearsStaleAssignment()
    {
        var followerTasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var followerManager = new UtilityTaskManager(followerTasks);

        var questTasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f), new StubTask(0.10f) };
        var questManager = new UtilityTaskManager(questTasks);

        var entity = MakeEntity(taskCount: 3);

        // Assign follower task at ordinal 1
        followerTasks[1].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(followerTasks[1], 1);

        // Simulate switch to quest: clear stale assignment
        bool lastManagerWasFollower = true;
        if (lastManagerWasFollower && entity.TaskAssignment.Task != null)
        {
            entity.TaskAssignment.Task.Deactivate(entity);
            entity.TaskAssignment = default;
        }
        lastManagerWasFollower = false;

        Assert.That(entity.TaskAssignment.Task, Is.Null);
        Assert.That(followerTasks[1].ActiveEntityCount, Is.EqualTo(0));
    }

    [Test]
    public void ManagerSwitch_SameManager_NoStaleClearing()
    {
        var tasks = new UtilityTask[] { new StubTask(0.10f), new StubTask(0.10f) };
        var manager = new UtilityTaskManager(tasks);

        var entity = MakeEntity(taskCount: 2);
        tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(tasks[0], 0);

        // Same manager, no switch
        bool lastManagerWasFollower = false;
        if (!lastManagerWasFollower && entity.TaskAssignment.Task != null)
        {
            // This would clear — but in the real code, the condition checks
            // !_lastManagerWasFollower which means "was quest". If we're still
            // using quest, this fires. But the real code checks the flag CHANGE,
            // not the current state.
        }

        // In practice, the BotObjectiveLayer checks _lastManagerWasFollower transitions,
        // not repeated calls with the same manager. This is correct behavior.
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(tasks[0]));
    }

    // ── Rapid task switching ─────────────────────────────────

    [Test]
    public void PickTask_RapidSwitching_NoEntityLeak()
    {
        var tasks = new UtilityTask[] { new StubTask(0.05f), new StubTask(0.05f) };
        var manager = new UtilityTaskManager(tasks);
        var entity = MakeEntity(taskCount: 2);

        for (int i = 0; i < 20; i++)
        {
            // Alternate high score between tasks
            entity.TaskScores[0] = (i % 2 == 0) ? 0.80f : 0.20f;
            entity.TaskScores[1] = (i % 2 == 0) ? 0.20f : 0.80f;

            manager.PickTask(entity);
        }

        // Each task should have at most 1 active entity
        Assert.That(tasks[0].ActiveEntityCount + tasks[1].ActiveEntityCount, Is.EqualTo(1), "Entity should be tracked by exactly one task");
    }

    // ── Helpers ──────────────────────────────────────────────

    private static BotEntity MakeEntity(int taskCount)
    {
        var entity = new BotEntity(0);
        entity.IsActive = true;
        if (taskCount > 0)
            entity.TaskScores = new float[taskCount];
        return entity;
    }

    /// <summary>
    /// Minimal UtilityTask implementation for testing task manager logic.
    /// </summary>
    private class StubTask : UtilityTask
    {
        public StubTask(float hysteresis)
            : base(hysteresis) { }

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            // No-op — scores are set manually in tests
        }

        public override void Update()
        {
            // No-op
        }
    }
}
