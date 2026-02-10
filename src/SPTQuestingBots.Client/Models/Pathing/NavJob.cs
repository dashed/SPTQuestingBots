using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.Models.Pathing
{
    /// <summary>
    /// Represents a single asynchronous NavMesh pathfinding request.
    /// Submitted to <see cref="NavJobExecutor"/> and completed during its batched update.
    /// </summary>
    public class NavJob
    {
        /// <summary>Starting position for the path calculation.</summary>
        public readonly Vector3 Origin;

        /// <summary>Destination position for the path calculation.</summary>
        public readonly Vector3 Target;

        /// <summary>NavMesh path status after calculation. Defaults to PathInvalid until processed.</summary>
        public NavMeshPathStatus Status = NavMeshPathStatus.PathInvalid;

        /// <summary>Calculated path corners. Null until the job is processed.</summary>
        public Vector3[] Path;

        /// <summary>Whether the job has been processed and results are available.</summary>
        public bool IsReady => Path != null;

        /// <summary>
        /// Creates a new pathfinding job from origin to target.
        /// </summary>
        public NavJob(Vector3 origin, Vector3 target)
        {
            Origin = origin;
            Target = target;
        }
    }
}
