using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Formation type for en-route movement.
    /// </summary>
    public enum FormationType : byte
    {
        /// <summary>Followers trail behind boss in a line.</summary>
        Column,

        /// <summary>Followers fan out perpendicular to boss heading.</summary>
        Spread,
    }

    /// <summary>
    /// Pure-logic static class for computing en-route formation positions
    /// relative to the boss's current position and heading.
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public static class FormationPositionUpdater
    {
        /// <summary>
        /// Select formation type based on available path width.
        /// Column for narrow paths, Spread for open areas.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static FormationType SelectFormation(float pathWidth, float switchWidth)
        {
            return pathWidth < switchWidth ? FormationType.Column : FormationType.Spread;
        }

        /// <summary>
        /// Compute normalized heading from previous to current position.
        /// Returns false if positions too close (&lt; 0.1m).
        /// </summary>
        public static bool ComputeHeading(float prevX, float prevZ, float currX, float currZ, out float hx, out float hz)
        {
            float dx = currX - prevX;
            float dz = currZ - prevZ;
            float lenSqr = dx * dx + dz * dz;
            if (lenSqr < 0.01f) // 0.1m threshold squared
            {
                hx = 0f;
                hz = 0f;
                return false;
            }
            float invLen = 1f / (float)System.Math.Sqrt(lenSqr);
            hx = dx * invLen;
            hz = dz * invLen;
            return true;
        }

        /// <summary>
        /// Column formation: followers trail behind boss in a line.
        /// Positions are along -heading at increasing multiples of spacing.
        /// Output: x,y,z triples in outPositions.
        /// </summary>
        public static void ComputeColumnPositions(
            float bossX,
            float bossY,
            float bossZ,
            float headingX,
            float headingZ,
            int count,
            float spacing,
            float[] outPositions
        )
        {
            for (int i = 0; i < count; i++)
            {
                float dist = spacing * (i + 1);
                outPositions[i * 3] = bossX - headingX * dist;
                outPositions[i * 3 + 1] = bossY;
                outPositions[i * 3 + 2] = bossZ - headingZ * dist;
            }
        }

        /// <summary>
        /// Spread formation: followers fan out perpendicular to boss heading,
        /// offset behind boss by one spacing unit.
        /// Perpendicular = rotate heading 90 degrees: perpX = -headingZ, perpZ = headingX.
        /// Centered: for odd count, middle follower directly behind.
        /// </summary>
        public static void ComputeSpreadPositions(
            float bossX,
            float bossY,
            float bossZ,
            float headingX,
            float headingZ,
            int count,
            float spacing,
            float[] outPositions
        )
        {
            // Behind offset: one spacing unit behind boss
            float behindX = bossX - headingX * spacing;
            float behindZ = bossZ - headingZ * spacing;

            // Perpendicular direction (90 degree rotation in XZ plane)
            float perpX = -headingZ;
            float perpZ = headingX;

            // Center the spread: offset = (i - (count-1)/2) * spacing
            float halfCount = (count - 1) / 2f;
            for (int i = 0; i < count; i++)
            {
                float lateralOffset = (i - halfCount) * spacing;
                outPositions[i * 3] = behindX + perpX * lateralOffset;
                outPositions[i * 3 + 1] = bossY;
                outPositions[i * 3 + 2] = behindZ + perpZ * lateralOffset;
            }
        }

        /// <summary>
        /// Dispatch to Column or Spread based on formation type.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ComputeFormationPositions(
            FormationType type,
            float bossX,
            float bossY,
            float bossZ,
            float headingX,
            float headingZ,
            int count,
            float spacing,
            float[] outPositions
        )
        {
            if (type == FormationType.Column)
                ComputeColumnPositions(bossX, bossY, bossZ, headingX, headingZ, count, spacing, outPositions);
            else
                ComputeSpreadPositions(bossX, bossY, bossZ, headingX, headingZ, count, spacing, outPositions);
        }
    }
}
