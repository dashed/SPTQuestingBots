using System.Collections.Generic;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.UtilityAI;

/// <summary>
/// Abstract base class for scored utility tasks. Each task computes a utility
/// score for every entity (column-major), and the task manager selects the
/// highest-scoring task per entity with additive hysteresis.
/// <para>
/// Mirrors Phobos <c>Task&lt;T&gt;</c> pattern:
/// <c>Phobos/Tasks/Task.cs</c> + <c>Phobos/Orchestration/BaseTaskManager.cs</c>.
/// </para>
/// <para>
/// Pure C# — no Unity or EFT dependencies — fully testable.
/// </para>
/// </summary>
public abstract class UtilityTask
{
    /// <summary>
    /// Additive bonus added to this task's score when it is the current task.
    /// A competing task must exceed <c>currentScore + Hysteresis</c> to take over.
    /// Typical values: 0.10–0.25.
    /// </summary>
    public readonly float Hysteresis;

    private readonly List<BotEntity> _activeEntities = new List<BotEntity>(16);
    private readonly HashSet<int> _activeEntityIds = new HashSet<int>();

    protected UtilityTask(float hysteresis)
    {
        Hysteresis = hysteresis;
    }

    /// <summary>
    /// Read-only view of entities currently assigned to this task.
    /// </summary>
    public IReadOnlyList<BotEntity> ActiveEntities
    {
        get { return _activeEntities; }
    }

    /// <summary>
    /// Compute the utility score for a single entity and write the result
    /// to <c>entity.TaskScores[ordinal]</c>.
    /// </summary>
    /// <param name="ordinal">This task's index in the task manager's array.</param>
    /// <param name="entity">The entity to score.</param>
    public abstract void ScoreEntity(int ordinal, BotEntity entity);

    /// <summary>
    /// Column-major score update: compute the utility score for ALL entities
    /// and write the result to <c>entity.TaskScores[ordinal]</c>.
    /// Default implementation calls <see cref="ScoreEntity"/> in a loop.
    /// </summary>
    /// <param name="ordinal">This task's index in the task manager's array.</param>
    /// <param name="entities">All registered entities.</param>
    public virtual void UpdateScores(int ordinal, IReadOnlyList<BotEntity> entities)
    {
        for (int i = 0; i < entities.Count; i++)
        {
            ScoreEntity(ordinal, entities[i]);
        }
    }

    /// <summary>
    /// Execute behavior for all entities currently assigned to this task.
    /// Called every tick after task selection.
    /// </summary>
    public abstract void Update();

    /// <summary>
    /// Called when an entity switches TO this task.
    /// Override to perform activation logic (e.g., start movement).
    /// </summary>
    public virtual void Activate(BotEntity entity)
    {
        if (!_activeEntityIds.Add(entity.Id))
        {
            return;
        }

        _activeEntities.Add(entity);
        LoggingController.LogDebug(
            "[UtilityTask] Activated " + GetType().Name + " for entity " + entity.Id + " (activeCount=" + _activeEntities.Count + ")"
        );
    }

    /// <summary>
    /// Called when an entity switches AWAY from this task.
    /// Override to perform cleanup.
    /// </summary>
    public virtual void Deactivate(BotEntity entity)
    {
        if (!_activeEntityIds.Remove(entity.Id))
        {
            return;
        }

        LoggingController.LogDebug("[UtilityTask] Deactivated " + GetType().Name + " for entity " + entity.Id);

        for (int i = 0; i < _activeEntities.Count; i++)
        {
            if (_activeEntities[i].Id != entity.Id)
            {
                continue;
            }

            // Swap-remove for O(1) removal
            int lastIndex = _activeEntities.Count - 1;
            if (i != lastIndex)
            {
                _activeEntities[i] = _activeEntities[lastIndex];
            }

            _activeEntities.RemoveAt(lastIndex);
            return;
        }
    }

    /// <summary>
    /// Returns the number of entities currently assigned to this task.
    /// </summary>
    public int ActiveEntityCount
    {
        get { return _activeEntities.Count; }
    }
}
