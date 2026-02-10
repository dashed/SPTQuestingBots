using System.Runtime.CompilerServices;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Caches human player positions for efficient per-bot distance queries.
    /// Refreshed once per HiveMind tick. Zero allocation after initialization.
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public static class HumanPlayerCache
    {
        /// <summary>Maximum number of human players cached.</summary>
        public const int MaxPlayers = 6;

        private static readonly float[] _posX = new float[MaxPlayers];
        private static readonly float[] _posY = new float[MaxPlayers];
        private static readonly float[] _posZ = new float[MaxPlayers];
        private static int _count;

        /// <summary>Number of cached human players.</summary>
        public static int PlayerCount => _count;

        /// <summary>Whether any human players are cached.</summary>
        public static bool HasPlayers => _count > 0;

        /// <summary>
        /// Set cached player positions. Called once per tick by HiveMind update.
        /// Positions beyond MaxPlayers are ignored.
        /// </summary>
        public static void SetPositions(float[] x, float[] y, float[] z, int count)
        {
            int clamp = count > MaxPlayers ? MaxPlayers : count;
            if (clamp < 0)
                clamp = 0;
            for (int i = 0; i < clamp; i++)
            {
                _posX[i] = x[i];
                _posY[i] = y[i];
                _posZ[i] = z[i];
            }
            _count = clamp;
        }

        /// <summary>
        /// Compute the minimum squared distance from the given position to any cached player.
        /// Returns float.MaxValue if no players are cached.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeMinSqrDistance(float x, float y, float z)
        {
            float min = float.MaxValue;
            for (int i = 0; i < _count; i++)
            {
                float dx = x - _posX[i];
                float dy = y - _posY[i];
                float dz = z - _posZ[i];
                float sqr = dx * dx + dy * dy + dz * dz;
                if (sqr < min)
                    min = sqr;
            }
            return min;
        }

        /// <summary>Reset cached state.</summary>
        public static void Clear()
        {
            _count = 0;
        }
    }
}
