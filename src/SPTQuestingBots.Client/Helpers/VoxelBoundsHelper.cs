using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Reads authoritative map bounds from BSG's pre-computed voxel grid
    /// (<see cref="AIVoxelesData.MinVoxelesValues"/> / <see cref="AIVoxelesData.MaxVoxelesValues"/>).
    /// These bounds are exact, unlike spawn-point-based inference which can miss map edges.
    /// </summary>
    public static class VoxelBoundsHelper
    {
        /// <summary>
        /// Try to read the voxel-grid min/max bounds from the current map's <see cref="AICoversData"/>.
        /// Returns false if the game data is not yet initialized or the voxel grid is absent.
        /// </summary>
        /// <param name="min">Voxel grid minimum corner (world space).</param>
        /// <param name="max">Voxel grid maximum corner (world space).</param>
        /// <returns>True if bounds were read successfully.</returns>
        public static bool TryGetVoxelBounds(out Vector3 min, out Vector3 max)
        {
            min = default;
            max = default;

            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                if (botGame == null)
                    return false;

                var coversData = botGame.BotsController?.CoversData;
                if (coversData == null)
                    return false;

                var voxels = coversData.Voxels;
                if (voxels == null)
                    return false;

                min = voxels.MinVoxelesValues;
                max = voxels.MaxVoxelesValues;

                // Sanity check: max must be greater than min in at least XZ
                if (max.x <= min.x || max.z <= min.z)
                {
                    LoggingController.LogWarning(
                        "[VoxelBoundsHelper] Voxel bounds look degenerate: min=("
                            + min.x.ToString("F0")
                            + ","
                            + min.z.ToString("F0")
                            + ") max=("
                            + max.x.ToString("F0")
                            + ","
                            + max.z.ToString("F0")
                            + ")"
                    );
                    return false;
                }

                LoggingController.LogInfo(
                    "[VoxelBoundsHelper] Voxel bounds: min=("
                        + min.x.ToString("F0")
                        + ","
                        + min.y.ToString("F0")
                        + ","
                        + min.z.ToString("F0")
                        + ") max=("
                        + max.x.ToString("F0")
                        + ","
                        + max.y.ToString("F0")
                        + ","
                        + max.z.ToString("F0")
                        + ")"
                );
                return true;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogWarning("[VoxelBoundsHelper] Failed to read voxel bounds: " + ex.Message);
                return false;
            }
        }
    }
}
