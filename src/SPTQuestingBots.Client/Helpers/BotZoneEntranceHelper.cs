using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Reads building/zone entrance points from BSG's <see cref="BotZoneEntranceInfo"/>.
    /// Entrances define outside/inside/center positions for building doorways,
    /// useful for path planning and room clear initiation.
    /// <para>
    /// Pure search logic is in <see cref="EntrancePointScorer"/>.
    /// </para>
    /// </summary>
    public static class BotZoneEntranceHelper
    {
        /// <summary>
        /// Get all entrance points on the current map.
        /// Returns empty array if data is unavailable.
        /// </summary>
        public static EntrancePointData[] GetAllEntrances()
        {
            try
            {
                var botGame = Singleton<IBotGame>.Instance;
                if (botGame == null)
                    return System.Array.Empty<EntrancePointData>();

                var coversData = botGame.BotsController?.CoversData;
                if (coversData == null)
                    return System.Array.Empty<EntrancePointData>();

                var entranceInfo = coversData.EntranceInfo;
                if (entranceInfo == null)
                    return System.Array.Empty<EntrancePointData>();

                var entranceList = entranceInfo.EntranceList;
                if (entranceList == null || entranceList.Count == 0)
                    return System.Array.Empty<EntrancePointData>();

                var results = new EntrancePointData[entranceList.Count];
                int validCount = 0;

                for (int i = 0; i < entranceList.Count; i++)
                {
                    var entrance = entranceList[i];
                    if (entrance == null)
                        continue;

                    results[validCount] = new EntrancePointData
                    {
                        OutsideX = entrance.PointOutSide.x,
                        OutsideY = entrance.PointOutSide.y,
                        OutsideZ = entrance.PointOutSide.z,
                        InsideX = entrance.PointInside.x,
                        InsideY = entrance.PointInside.y,
                        InsideZ = entrance.PointInside.z,
                        CenterX = entrance.CenterPoint.x,
                        CenterY = entrance.CenterPoint.y,
                        CenterZ = entrance.CenterPoint.z,
                        ConnectedAreaId = entrance.ConnectedAreaId,
                        Id = entrance.Id,
                    };
                    validCount++;
                }

                if (validCount < results.Length)
                {
                    var trimmed = new EntrancePointData[validCount];
                    System.Array.Copy(results, trimmed, validCount);
                    results = trimmed;
                }

                LoggingController.LogInfo("[BotZoneEntranceHelper] Loaded " + validCount + " entrance points");
                return results;
            }
            catch (System.Exception ex)
            {
                LoggingController.LogWarning("[BotZoneEntranceHelper] Failed to read entrances: " + ex.Message);
                return System.Array.Empty<EntrancePointData>();
            }
        }
    }
}
