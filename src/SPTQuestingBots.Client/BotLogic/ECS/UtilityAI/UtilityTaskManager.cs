using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI;

/// <summary>
/// Evaluates utility scores across all registered tasks and selects the
/// highest-scoring task per entity with additive hysteresis.
/// <para>
/// Mirrors Phobos <c>BaseTaskManager&lt;T&gt;</c>
/// (<c>Phobos/Orchestration/BaseTaskManager.cs:38-76</c>).
/// </para>
/// <para>
/// Update flow (called each tick):
/// <list type="number">
/// <item><c>UpdateScores</c> — each task computes scores for all entities (column-major)</item>
/// <item><c>PickTasks</c> — for each entity, select highest-scoring task with hysteresis</item>
/// <item><c>UpdateTasks</c> — each task executes behavior for its active entities</item>
/// </list>
/// </para>
/// <para>
/// Pure C# — no Unity or EFT dependencies — fully testable.
/// </para>
/// </summary>
public class UtilityTaskManager
{
    /// <summary>Registered tasks in evaluation order.</summary>
    public readonly UtilityTask[] Tasks;

    public UtilityTaskManager(UtilityTask[] tasks)
    {
        Tasks = tasks;
    }

    /// <summary>
    /// Full update cycle: score → pick → execute.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Update(IReadOnlyList<BotEntity> entities)
    {
        UpdateScores(entities);
        PickTasks(entities);
        UpdateTasks();
    }

    /// <summary>
    /// Phase 1: Each task computes scores for all entities (column-major).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateScores(IReadOnlyList<BotEntity> entities)
    {
        for (int i = 0; i < Tasks.Length; i++)
        {
            Tasks[i].UpdateScores(i, entities);
        }
    }

    /// <summary>
    /// Phase 2: For each entity, select the highest-scoring task.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PickTasks(IReadOnlyList<BotEntity> entities)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];

            if (!entity.IsActive)
            {
                // Deactivate any current task for inactive entities
                if (entity.TaskAssignment.Task != null)
                {
                    entity.TaskAssignment.Task.Deactivate(entity);
                    entity.TaskAssignment = default;
                }

                continue;
            }

            PickTask(entity);
        }
    }

    /// <summary>
    /// Phase 3: Each task executes behavior for its active entities.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void UpdateTasks()
    {
        for (int i = 0; i < Tasks.Length; i++)
        {
            Tasks[i].Update();
        }
    }

    /// <summary>
    /// Core task selection with hysteresis for a single entity.
    /// Mirrors <c>Phobos/Orchestration/BaseTaskManager.cs:38-76</c>.
    /// <para>
    /// The current task receives an additive hysteresis bonus. A competing task
    /// must exceed <c>currentScore + hysteresis</c> to trigger a switch.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void PickTask(BotEntity entity)
    {
        var assignment = entity.TaskAssignment;

        float highestScore = 0f;
        int nextTaskOrdinal = 0;

        // Seed from current task — including hysteresis
        if (assignment.Task != null)
        {
            nextTaskOrdinal = assignment.Ordinal;
            highestScore = entity.TaskScores[assignment.Ordinal] + assignment.Task.Hysteresis;
        }

        UtilityTask nextTask = null;

        for (int j = 0; j < Tasks.Length; j++)
        {
            float score = entity.TaskScores[j];
            if (score <= highestScore)
            {
                continue;
            }

            highestScore = score;
            nextTaskOrdinal = j;
            nextTask = Tasks[j];
        }

        // If no task beats the current (with hysteresis), keep current
        if (nextTask == null)
        {
            return;
        }

        // Switch tasks
        var previousTaskName = assignment.Task != null ? assignment.Task.GetType().Name : "None";
        assignment.Task?.Deactivate(entity);
        nextTask.Activate(entity);

        entity.TaskAssignment = new UtilityTaskAssignment(nextTask, nextTaskOrdinal);
        LoggingController.LogDebug(
            "[UtilityTaskManager] Entity "
                + entity.Id
                + " switched from "
                + previousTaskName
                + " to "
                + nextTask.GetType().Name
                + " (score="
                + highestScore.ToString("F3")
                + ")"
        );
    }

    /// <summary>
    /// Score all tasks for a single entity and pick the best one.
    /// Convenience method for per-entity evaluation (e.g., BigBrain layer tick).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ScoreAndPick(BotEntity entity)
    {
        if (!entity.IsActive)
        {
            if (entity.TaskAssignment.Task != null)
            {
                entity.TaskAssignment.Task.Deactivate(entity);
                entity.TaskAssignment = default;
            }

            return;
        }

        for (int i = 0; i < Tasks.Length; i++)
        {
            Tasks[i].ScoreEntity(i, entity);
        }

        PickTask(entity);
    }

    /// <summary>
    /// Remove an entity from all task tracking.
    /// </summary>
    public void RemoveEntity(BotEntity entity)
    {
        if (entity.TaskAssignment.Task != null)
        {
            LoggingController.LogDebug(
                "[UtilityTaskManager] Removing entity " + entity.Id + " from task " + entity.TaskAssignment.Task.GetType().Name
            );
        }

        entity.TaskAssignment.Task?.Deactivate(entity);
        entity.TaskAssignment = default;
    }
}
