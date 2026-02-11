using System;
using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Assigns communication tiers to squad followers and degrades relayed positions with noise.
/// Tier 1 (Direct) followers are within comm range of the leader and receive exact positions.
/// Tier 2 (Relayed) followers are within comm range of a Tier 1 member and receive noisy positions.
/// Tier 0 (None) followers are unreachable and receive no position update.
/// <para>
/// Pure C# — no Unity or EFT dependencies — fully testable.
/// </para>
/// </summary>
public static class ObjectiveSharingCalculator
{
    /// <summary>Follower is unreachable — no position shared.</summary>
    public const byte TierNone = 0;

    /// <summary>Follower is within direct comm range of the leader.</summary>
    public const byte TierDirect = 1;

    /// <summary>Follower received position relayed through a Tier 1 member.</summary>
    public const byte TierRelayed = 2;

    /// <summary>Static buffer for insertion-sorted indices. Safe: Update is single-threaded.</summary>
    private static readonly int[] _sortedIndices = new int[SquadObjective.MaxMembers];

    /// <summary>Static buffer for squared distances. Safe: Update is single-threaded.</summary>
    private static readonly float[] _sqrDistances = new float[SquadObjective.MaxMembers];

    /// <summary>
    /// Assign communication tiers to each follower based on proximity and comm range.
    /// Phase 1: closest followers within leader comm range get TierDirect (up to trustedCount).
    /// Phase 2: remaining followers within comm range of nearest TierDirect member get TierRelayed.
    /// </summary>
    /// <param name="leaderX">Leader position X.</param>
    /// <param name="leaderY">Leader position Y.</param>
    /// <param name="leaderZ">Leader position Z.</param>
    /// <param name="leaderEarPiece">Whether the leader has an earpiece.</param>
    /// <param name="followerX">Follower X positions.</param>
    /// <param name="followerY">Follower Y positions.</param>
    /// <param name="followerZ">Follower Z positions.</param>
    /// <param name="followerEarPiece">Whether each follower has an earpiece.</param>
    /// <param name="followerCount">Number of followers.</param>
    /// <param name="trustedCount">Maximum number of Tier 1 (direct) followers.</param>
    /// <param name="commRangeNoEarpiece">Base comm range without earpieces.</param>
    /// <param name="commRangeEarpiece">Extended comm range when both have earpieces.</param>
    /// <param name="enableCommRange">Whether comm range checking is enabled.</param>
    /// <param name="outTiers">Output tier for each follower.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AssignTiers(
        float leaderX,
        float leaderY,
        float leaderZ,
        bool leaderEarPiece,
        float[] followerX,
        float[] followerY,
        float[] followerZ,
        bool[] followerEarPiece,
        int followerCount,
        int trustedCount,
        float commRangeNoEarpiece,
        float commRangeEarpiece,
        bool enableCommRange,
        byte[] outTiers
    )
    {
        if (followerCount <= 0)
        {
            return;
        }

        int count = followerCount > SquadObjective.MaxMembers ? SquadObjective.MaxMembers : followerCount;

        // Compute squared distances and initialize tiers
        for (int i = 0; i < count; i++)
        {
            float dx = followerX[i] - leaderX;
            float dy = followerY[i] - leaderY;
            float dz = followerZ[i] - leaderZ;
            _sqrDistances[i] = dx * dx + dy * dy + dz * dz;
            outTiers[i] = TierNone;
        }

        // Insertion sort indices by distance (at most 6 elements)
        for (int i = 0; i < count; i++)
        {
            _sortedIndices[i] = i;
        }

        for (int i = 1; i < count; i++)
        {
            int key = _sortedIndices[i];
            float keyDist = _sqrDistances[key];
            int j = i - 1;
            while (j >= 0 && _sqrDistances[_sortedIndices[j]] > keyDist)
            {
                _sortedIndices[j + 1] = _sortedIndices[j];
                j--;
            }

            _sortedIndices[j + 1] = key;
        }

        LoggingController.LogDebug(
            "[ObjectiveSharingCalculator] AssignTiers: "
                + count
                + " followers, trustedCount="
                + trustedCount
                + ", commRange="
                + enableCommRange
        );

        // Phase 1: Assign TierDirect to closest followers within leader comm range
        int directAssigned = 0;
        for (int s = 0; s < count && directAssigned < trustedCount; s++)
        {
            int idx = _sortedIndices[s];
            bool inRange =
                !enableCommRange
                || CommunicationRange.IsInRange(
                    leaderEarPiece,
                    followerEarPiece[idx],
                    _sqrDistances[idx],
                    commRangeNoEarpiece,
                    commRangeEarpiece
                );

            if (inRange)
            {
                outTiers[idx] = TierDirect;
                directAssigned++;
            }
        }

        // Phase 2: Assign TierRelayed to remaining followers within comm range of nearest TierDirect
        for (int s = 0; s < count; s++)
        {
            int idx = _sortedIndices[s];
            if (outTiers[idx] != TierNone)
            {
                continue;
            }

            // Find nearest TierDirect member
            float bestSqrDist = float.MaxValue;
            int bestDirect = -1;
            for (int d = 0; d < count; d++)
            {
                if (outTiers[d] != TierDirect)
                {
                    continue;
                }

                float dx = followerX[idx] - followerX[d];
                float dy = followerY[idx] - followerY[d];
                float dz = followerZ[idx] - followerZ[d];
                float sqrDist = dx * dx + dy * dy + dz * dz;
                if (sqrDist < bestSqrDist)
                {
                    bestSqrDist = sqrDist;
                    bestDirect = d;
                }
            }

            if (bestDirect < 0)
            {
                continue;
            }

            bool relayInRange =
                !enableCommRange
                || CommunicationRange.IsInRange(
                    followerEarPiece[bestDirect],
                    followerEarPiece[idx],
                    bestSqrDist,
                    commRangeNoEarpiece,
                    commRangeEarpiece
                );

            if (relayInRange)
            {
                outTiers[idx] = TierRelayed;
                LoggingController.LogDebug(
                    "[ObjectiveSharingCalculator] Follower " + idx + " assigned TierRelayed via direct member " + bestDirect
                );
            }
        }

        LoggingController.LogDebug("[ObjectiveSharingCalculator] Tier assignment complete: direct=" + directAssigned);
    }

    /// <summary>
    /// Degrade Tier 2 (relayed) positions by adding Gaussian noise to X and Z.
    /// Y coordinates are left unchanged. NaN positions are skipped.
    /// Higher coordination levels produce less noise.
    /// </summary>
    /// <param name="tiers">Communication tier for each follower.</param>
    /// <param name="count">Number of followers.</param>
    /// <param name="positions">Flat float array [x,y,z, x,y,z, ...] — modified in place.</param>
    /// <param name="coordinationLevel">Squad coordination level (1-5). Higher = less noise.</param>
    /// <param name="baseNoise">Base noise magnitude in meters.</param>
    /// <param name="rng">Random number generator for deterministic testing.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void DegradePositions(
        byte[] tiers,
        int count,
        float[] positions,
        float coordinationLevel,
        float baseNoise,
        System.Random rng
    )
    {
        if (count <= 0 || baseNoise <= 0f)
        {
            return;
        }

        float noiseScale = baseNoise * (6f - coordinationLevel) / 5f;

        for (int i = 0; i < count; i++)
        {
            if (tiers[i] != TierRelayed)
            {
                continue;
            }

            int off = i * 3;
            float x = positions[off];
            float z = positions[off + 2];

            // Skip NaN positions (failed validation)
            if (float.IsNaN(x) || float.IsNaN(z))
            {
                continue;
            }

            // Box-Muller transform for Gaussian noise
            double u1 = 1.0 - rng.NextDouble();
            double u2 = rng.NextDouble();
            double mag = System.Math.Sqrt(-2.0 * System.Math.Log(u1));
            double theta = 2.0 * System.Math.PI * u2;

            float noiseX = (float)(mag * System.Math.Cos(theta)) * noiseScale;
            float noiseZ = (float)(mag * System.Math.Sin(theta)) * noiseScale;

            positions[off] = x + noiseX;
            // positions[off + 1] (Y) unchanged
            positions[off + 2] = z + noiseZ;
        }
    }
}
