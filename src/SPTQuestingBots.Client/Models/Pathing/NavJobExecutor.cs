using System.Collections.Generic;
using SPTQuestingBots.Controllers;
using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.Models.Pathing
{
    /// <summary>
    /// Batched NavMesh pathfinding executor that processes queued <see cref="NavJob"/> requests
    /// in rate-limited batches to avoid frame spikes.
    /// </summary>
    /// <remarks>
    /// Uses a ramped batch size (half of queue depth, capped by max batch size) to spread
    /// pathfinding load across frames when many requests arrive simultaneously.
    /// </remarks>
    public class NavJobExecutor
    {
        private readonly Queue<NavJob> _jobQueue = new Queue<NavJob>(20);
        private readonly int _batchSize;

        /// <summary>
        /// Creates a new executor with the specified maximum batch size per frame.
        /// </summary>
        public NavJobExecutor(int batchSize = 5)
        {
            _batchSize = batchSize;
        }

        /// <summary>
        /// Creates and enqueues a new pathfinding job, returning it for result polling.
        /// </summary>
        public NavJob Submit(Vector3 origin, Vector3 target)
        {
            var job = new NavJob(origin, target);
            Submit(job);
            return job;
        }

        /// <summary>
        /// Enqueues an existing pathfinding job for processing.
        /// </summary>
        public void Submit(NavJob job)
        {
            _jobQueue.Enqueue(job);
        }

        /// <summary>
        /// Processes up to the ramped batch size of queued jobs this frame.
        /// Call once per frame from a MonoBehaviour.Update.
        /// </summary>
        public void Update()
        {
            var counter = 0;
            var rampedBatchSize = Mathf.Min(Mathf.CeilToInt(_jobQueue.Count / 2f), _batchSize);

            if (rampedBatchSize > 0)
            {
                LoggingController.LogDebug(
                    "[NavJobExecutor] Processing batch: " + rampedBatchSize + " jobs (queue=" + _jobQueue.Count + ")"
                );
            }

            while (_jobQueue.Count > 0 && counter < rampedBatchSize)
            {
                var job = _jobQueue.Dequeue();
                var path = new NavMeshPath();
                NavMesh.CalculatePath(job.Origin, job.Target, NavMesh.AllAreas, path);
                job.Path = path.corners;
                job.Status = path.status;
                counter++;
            }
        }
    }
}
