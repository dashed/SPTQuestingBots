using System;
using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Generates evenly-distributed candidate positions using a golden-angle sunflower spiral.
    /// Ported from Phobos LocationGatherer.CollectSyntheticCoverData.
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public static class SunflowerSpiral
    {
        /// <summary>
        /// Golden angle in radians: PI * (3 - sqrt(5)) ≈ 2.3999 rad.
        /// </summary>
        public static readonly float GoldenAngle = (float)(System.Math.PI * (3.0 - System.Math.Sqrt(5.0)));

        /// <summary>
        /// Generate evenly-distributed XZ positions in a sunflower spiral pattern.
        /// Uses Vogel's formula: r = innerRadius * sqrt(i / count).
        /// </summary>
        /// <param name="centerX">Center X coordinate.</param>
        /// <param name="centerZ">Center Z coordinate.</param>
        /// <param name="innerRadius">Radius of the spiral (typically 0.75 * location radius).</param>
        /// <param name="count">Number of positions to generate.</param>
        /// <param name="outXZ">Output array for XZ pairs. Must have length >= count * 2.</param>
        /// <returns>Number of positions generated.</returns>
        public static int Generate(float centerX, float centerZ, float innerRadius, int count, float[] outXZ)
        {
            if (count <= 0 || outXZ == null || innerRadius <= 0f)
                return 0;

            int maxCount = System.Math.Min(count, outXZ.Length / 2);
            LoggingController.LogDebug(
                "[SunflowerSpiral] Generating "
                    + maxCount
                    + " candidates (center=("
                    + centerX
                    + ", "
                    + centerZ
                    + "), radius="
                    + innerRadius
                    + ")"
            );

            for (int i = 0; i < maxCount; i++)
            {
                float theta = i * GoldenAngle;
                float r = innerRadius * (float)System.Math.Sqrt((float)i / maxCount);
                outXZ[i * 2] = centerX + r * (float)System.Math.Cos(theta);
                outXZ[i * 2 + 1] = centerZ + r * (float)System.Math.Sin(theta);
            }

            return maxCount;
        }

        /// <summary>
        /// Compute the optimal NavMesh sample epsilon for a given radius and point count.
        /// From Phobos: 0.886 * innerRadius / sqrt(count), derived from 0.5 * r * sqrt(PI/N).
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ComputeSampleEpsilon(float innerRadius, int count)
        {
            if (count <= 0 || innerRadius <= 0f)
                return 0f;
            return 0.886f * innerRadius / (float)System.Math.Sqrt(count);
        }
    }
}
