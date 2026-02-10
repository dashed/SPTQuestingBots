using UnityEngine;

namespace SPTQuestingBots.Models.Pathing
{
    /// <summary>
    /// Rate-limits NavMesh.CalculatePath calls to a maximum number per frame.
    /// Prevents frame spikes when many bots request pathfinding simultaneously.
    /// </summary>
    public static class PathfindingThrottle
    {
        private static int _callsThisFrame = 0;
        private static int _lastFrame = -1;
        private const int MaxCallsPerFrame = 5;

        /// <summary>
        /// Returns true if a pathfinding call is allowed this frame, incrementing the counter.
        /// Returns false if the per-frame budget has been exhausted.
        /// </summary>
        public static bool CanCalculatePath()
        {
            int currentFrame = Time.frameCount;
            if (currentFrame != _lastFrame)
            {
                _lastFrame = currentFrame;
                _callsThisFrame = 0;
            }

            if (_callsThisFrame >= MaxCallsPerFrame)
                return false;

            _callsThisFrame++;
            return true;
        }
    }
}
