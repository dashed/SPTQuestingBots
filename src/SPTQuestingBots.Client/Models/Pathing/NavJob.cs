using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.Models.Pathing
{
    public class NavJob
    {
        public readonly Vector3 Origin;
        public readonly Vector3 Target;
        public NavMeshPathStatus Status = NavMeshPathStatus.PathInvalid;
        public Vector3[] Path;
        public bool IsReady => Path != null;

        public NavJob(Vector3 origin, Vector3 target)
        {
            Origin = origin;
            Target = target;
        }
    }
}
