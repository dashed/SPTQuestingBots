using UnityEngine;

namespace SPTQuestingBots.Models.Pathing
{
    public static class PathfindingThrottle
    {
        private static int _callsThisFrame = 0;
        private static int _lastFrame = -1;
        private const int MaxCallsPerFrame = 5;

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
