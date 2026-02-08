using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.Models.Pathing
{
    public class NavJobExecutor
    {
        private readonly Queue<NavJob> _jobQueue = new Queue<NavJob>(20);
        private readonly int _batchSize;

        public NavJobExecutor(int batchSize = 5)
        {
            _batchSize = batchSize;
        }

        public NavJob Submit(Vector3 origin, Vector3 target)
        {
            var job = new NavJob(origin, target);
            Submit(job);
            return job;
        }

        public void Submit(NavJob job)
        {
            _jobQueue.Enqueue(job);
        }

        public void Update()
        {
            var counter = 0;
            var rampedBatchSize = Mathf.Min(Mathf.CeilToInt(_jobQueue.Count / 2f), _batchSize);

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
