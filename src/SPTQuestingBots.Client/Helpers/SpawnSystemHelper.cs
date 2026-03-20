using System;
using System.Reflection;
using Comfort.Common;
using EFT;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Patches.Spawning.ScavLimits;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Safe wrappers around game spawning APIs (BotZone capacity, SpawnDelaysService,
    /// ISpawnSystem validation, BotSpawnLimiter, profile pre-warming, NonWaves phase).
    /// Every method has null safety and try/catch with graceful fallback.
    /// </summary>
    public static class SpawnSystemHelper
    {
        private static FieldInfo _nonWavesSpawnPhaseField;
        private static bool _nonWavesFieldResolved;

        /// <summary>
        /// Item 7: Check whether a BotZone has capacity for the requested bot count.
        /// Falls back to true (allow spawn) if zone is null or check fails.
        /// </summary>
        public static bool HasZoneFreeSpace(BotZone zone, int count)
        {
            if (zone == null || count <= 0)
            {
                return true;
            }

            try
            {
                return zone.HaveFreeSpace(count);
            }
            catch (Exception ex)
            {
                LoggingController.LogWarning("SpawnSystemHelper: zone capacity check failed, allowing spawn. " + ex.Message);
                return true;
            }
        }

        /// <summary>
        /// Item 8: Get the number of bots currently in the spawn delay queue plus those
        /// actively loading. Returns 0 if BotSpawner or SpawnDelaysService is unavailable.
        /// </summary>
        public static int GetDelayedAndLoadingBotCount()
        {
            try
            {
                BotSpawner botSpawner = Singleton<IBotGame>.Instance?.BotsController?.BotSpawner;
                if (botSpawner == null)
                {
                    return 0;
                }

                int delayed = 0;

                if (botSpawner.SpawnDelaysService != null)
                {
                    delayed += botSpawner.SpawnDelaysService.WaitCount;
                }

                delayed += botSpawner.InSpawnProcess;

                return delayed;
            }
            catch (Exception ex)
            {
                LoggingController.LogWarning("SpawnSystemHelper: failed to read delayed/loading bot count. " + ex.Message);
                return 0;
            }
        }

        /// <summary>
        /// Item 9: Validate spawn positions using the game's ISpawnSystem.
        /// Returns true if all positions pass validation, or if ISpawnSystem is unavailable (fallback).
        /// </summary>
        public static bool ValidateSpawnPositions(Vector3[] positions, BotZone zone, BotCreationDataClass data)
        {
            if (positions == null || positions.Length == 0 || zone == null || data == null)
            {
                return true;
            }

            try
            {
                BotSpawner botSpawner = Singleton<IBotGame>.Instance?.BotsController?.BotSpawner;
                ISpawnSystem spawnSystem = botSpawner?.SpawnSystem;
                if (spawnSystem == null)
                {
                    return true;
                }

                // We need an IPlayer for validation — use first human player if available
                IPlayer person = GetNearestHumanPlayer(positions[0]);
                if (person == null)
                {
                    // Cannot validate without a player reference; allow spawn
                    return true;
                }

                for (int i = 0; i < positions.Length; i++)
                {
                    Vector3 pos = positions[i];
                    if (!spawnSystem.ValidateSpawnPosition(ref pos, zone, person, data))
                    {
                        LoggingController.LogWarning("SpawnSystemHelper: ISpawnSystem rejected position " + positions[i]);
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                LoggingController.LogWarning("SpawnSystemHelper: spawn position validation failed, allowing spawn. " + ex.Message);
                return true;
            }
        }

        /// <summary>
        /// Item 12: Notify the game's BotSpawnLimiter that we spawned a bot,
        /// so NonWaves system is aware of our spawns. No-op if limiter unavailable.
        /// </summary>
        public static void NotifyBotSpawnLimiter(BotOwner bot, BotCreationDataClass data)
        {
            if (bot == null || data == null)
            {
                return;
            }

            try
            {
                BotsController botsController = Singleton<IBotGame>.Instance?.BotsController;
                BotSpawnLimiter limiter = botsController?.BotSpawnLimiter;
                if (limiter == null)
                {
                    return;
                }

                // Find the nearest human player — BotSpawnLimiter tracks per-player spawn counts
                Player nearestPlayer = GetNearestHumanPlayerAsPlayer(bot.Position);
                if (nearestPlayer == null)
                {
                    return;
                }

                limiter.IncreaseUsedPlayerSpawns(nearestPlayer, data);
            }
            catch (Exception ex)
            {
                LoggingController.LogWarning(
                    "SpawnSystemHelper: failed to notify BotSpawnLimiter for " + bot.GetText() + ". " + ex.Message
                );
            }
        }

        /// <summary>
        /// Item 13: Pre-warm bot profiles so the game's profile cache is ready
        /// when we start generating bots. No-op if BotSpawner unavailable.
        /// </summary>
        public static void PreWarmBotProfiles(WildSpawnType role, BotDifficulty difficulty, int count)
        {
            if (count <= 0)
            {
                return;
            }

            try
            {
                BotSpawner botSpawner = Singleton<IBotGame>.Instance?.BotsController?.BotSpawner;
                if (botSpawner == null)
                {
                    LoggingController.LogWarning("SpawnSystemHelper: BotSpawner unavailable, skipping profile pre-warm for " + role);
                    return;
                }

                botSpawner.AddToTargetBackup(difficulty, role, count);
                LoggingController.LogInfo("SpawnSystemHelper: pre-warmed " + count + " profiles for " + role + " (" + difficulty + ")");
            }
            catch (Exception ex)
            {
                LoggingController.LogWarning("SpawnSystemHelper: profile pre-warm failed for " + role + ". " + ex.Message);
            }
        }

        /// <summary>
        /// Item 14: Check whether the NonWaves spawn scenario is currently in its
        /// burst (on) phase. Returns false if scenario is unavailable or check fails.
        /// When NonWaves is burst-spawning, our generators should defer to avoid overcrowding.
        /// </summary>
        public static bool IsNonWavesInBurstPhase()
        {
            try
            {
                NonWavesSpawnScenario scenario = NonWavesSpawnScenarioCreatePatch.MostRecentNonWavesSpawnScenario;
                if (scenario == null)
                {
                    return false;
                }

                // The Enabled property tells us if NonWaves is configured and active
                if (!scenario.Enabled)
                {
                    return false;
                }

                // bool_2 is the private on/off phase toggle:
                //   true = NonWaves is in its "spawn on" phase (actively spawning scavs)
                //   false = NonWaves is in its "spawn off" phase (paused between bursts)
                if (!_nonWavesFieldResolved)
                {
                    _nonWavesSpawnPhaseField = ReflectionHelper.RequireField(
                        typeof(NonWavesSpawnScenario),
                        "bool_2",
                        "SpawnSystemHelper — spawn on/off phase toggle"
                    );
                    _nonWavesFieldResolved = true;
                }

                if (_nonWavesSpawnPhaseField == null)
                {
                    return false;
                }

                return (bool)_nonWavesSpawnPhaseField.GetValue(scenario);
            }
            catch (Exception ex)
            {
                LoggingController.LogWarning("SpawnSystemHelper: NonWaves phase check failed. " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Reset cached reflection state. Call between raids.
        /// </summary>
        public static void Clear()
        {
            _nonWavesFieldResolved = false;
            _nonWavesSpawnPhaseField = null;
        }

        private static IPlayer GetNearestHumanPlayer(Vector3 position)
        {
            try
            {
                GameWorld gameWorld = Singleton<GameWorld>.Instance;
                if (gameWorld == null)
                {
                    return null;
                }

                IPlayer nearest = null;
                float nearestDist = float.MaxValue;

                for (int i = 0; i < gameWorld.AllAlivePlayersList.Count; i++)
                {
                    Player player = gameWorld.AllAlivePlayersList[i];
                    if (player == null || player.IsAI)
                    {
                        continue;
                    }

                    float dist = (player.Position - position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = player;
                    }
                }

                return nearest;
            }
            catch
            {
                return null;
            }
        }

        private static Player GetNearestHumanPlayerAsPlayer(Vector3 position)
        {
            try
            {
                GameWorld gameWorld = Singleton<GameWorld>.Instance;
                if (gameWorld == null)
                {
                    return null;
                }

                Player nearest = null;
                float nearestDist = float.MaxValue;

                for (int i = 0; i < gameWorld.AllAlivePlayersList.Count; i++)
                {
                    Player player = gameWorld.AllAlivePlayersList[i];
                    if (player == null || player.IsAI)
                    {
                        continue;
                    }

                    float dist = (player.Position - position).sqrMagnitude;
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearest = player;
                    }
                }

                return nearest;
            }
            catch
            {
                return null;
            }
        }
    }
}
