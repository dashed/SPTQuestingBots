using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using HarmonyLib;
using SPTQuestingBots.BotLogic;
using SPTQuestingBots.Components;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;
using SPTQuestingBots.Models;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.BehaviorExtensions
{
    public abstract class GoToPositionAbstractAction : CustomLogicDelayedUpdate
    {
        private const int BRAIN_LAYER_ERROR_MESSAGE_INTERVAL = 30;

        protected bool CanSprint { get; set; } = true;

        private static FieldInfo botZoneField = null;

        private SoftStuckDetector _softStuck;
        private HardStuckDetector _hardStuck;
        private Stopwatch timeSinceLastBrainLayerMessageTimer = Stopwatch.StartNew();
        private bool loggedBrainLayerError = false;

        /// <summary>
        /// Custom mover controller for Phobos-style Player.Move() movement.
        /// Null when custom movement is disabled. Lazy-initialized on first Start().
        /// </summary>
        private CustomMoverController _customMover;

        /// <summary>Whether the custom mover is currently active for this bot.</summary>
        protected bool UseCustomMover => _customMover != null && _customMover.IsActive;

        /// <summary>The custom mover controller (null when not using custom movement).</summary>
        protected CustomMoverController CustomMover => _customMover;

        protected double TimeSinceLastBrainLayerMessage => timeSinceLastBrainLayerMessageTimer.ElapsedMilliseconds / 1000.0;

        public GoToPositionAbstractAction(BotOwner _BotOwner, int delayInterval)
            : base(_BotOwner, delayInterval)
        {
            if (botZoneField == null)
            {
                botZoneField = AccessTools.Field(typeof(BotsGroup), "<BotZone>k__BackingField");
            }

            var remedies = ConfigController.Config.Questing.StuckBotDetection.StuckBotRemedies;
            _softStuck = new SoftStuckDetector(remedies.MinTimeBeforeVaulting, remedies.MinTimeBeforeJumping, remedies.SoftStuckFailDelay);
            _hardStuck = new HardStuckDetector(
                pathRetryDelay: remedies.HardStuckPathRetryDelay,
                teleportDelay: remedies.HardStuckTeleportDelay,
                failDelay: remedies.HardStuckFailDelay
            );
        }

        public GoToPositionAbstractAction(BotOwner _BotOwner)
            : this(_BotOwner, updateInterval) { }

        public override void Start()
        {
            base.Start();

            restartStuckTimer();

            BotOwner.PatrollingData.Pause();

            // Activate custom mover if enabled in config
            if (ConfigController.Config.Questing.BotPathing.UseCustomMover)
            {
                if (_customMover == null)
                {
                    _customMover = new CustomMoverController(BotOwner, CustomMoverConfig.CreateDefault());
                }

                _customMover.Activate();
            }
        }

        public override void Stop()
        {
            base.Stop();

            // Deactivate custom mover before unpausing patrol
            _customMover?.Deactivate();

            BotOwner.PatrollingData.Unpause();

            updateBotZoneForGroup();
        }

        public NavMeshPathStatus? RecalculatePath(Vector3 position)
        {
            return RecalculatePath(position, 0.2f, 0.5f);
        }

        public NavMeshPathStatus? RecalculatePath(Vector3 position, float targetVariationAllowed, float reachDist, bool force = false)
        {
            // If a bot is jumping or vaulting, recalculate its path after it finishes
            if (
                BotOwner.GetPlayer.MovementContext.PlayerAnimatorIsJumpSetted()
                || BotOwner.GetPlayer.MovementContext.PlayerAnimatorGetIsVaulting()
            )
            {
                ObjectiveManager.BotPath.ForcePathRecalculation();
                return ObjectiveManager.BotPath.Status;
            }

            if (!isAQuestingBotsBrainLayerActive())
            {
                return ObjectiveManager.BotPath.Status;
            }

            Models.Pathing.BotPathUpdateNeededReason updateReason = ObjectiveManager.BotPath.CheckIfUpdateIsNeeded(
                position,
                targetVariationAllowed,
                reachDist,
                force
            );

            if (ObjectiveManager.BotPath.Status != NavMeshPathStatus.PathInvalid)
            {
                if (updateReason != Models.Pathing.BotPathUpdateNeededReason.None)
                {
                    if (UseCustomMover)
                    {
                        // Feed NavMesh corners to our custom path follower instead of BSG's mover
                        _customMover.SetPath(ObjectiveManager.BotPath.Corners, position);
                    }
                    else
                    {
                        BotOwner.FollowPath(ObjectiveManager.BotPath, true, false);
                    }
                }
            }
            else
            {
                if (UseCustomMover)
                {
                    _customMover.Follower.FailPath();
                }
                else
                {
                    BotOwner.Mover?.Stop();
                }
            }

            return ObjectiveManager.BotPath.Status;
        }

        private bool isAQuestingBotsBrainLayerActive()
        {
            string activeLayerName = BotOwner.Brain.ActiveLayerName();
            if (LogicLayerMonitor.QuestingBotsBrainLayerNames.Contains(activeLayerName))
            {
                loggedBrainLayerError = false;
                return true;
            }

            if (!loggedBrainLayerError || (TimeSinceLastBrainLayerMessage >= BRAIN_LAYER_ERROR_MESSAGE_INTERVAL))
            {
                LoggingController.LogError(
                    "Cannot recalculate path for "
                        + BotOwner.GetText()
                        + " because the active brain layer is not a Questing Bots layer. This is normally caused by an exception in the update logic of another layer. Active layer name: "
                        + activeLayerName
                );

                loggedBrainLayerError = true;
                timeSinceLastBrainLayerMessageTimer.Restart();
            }

            return false;
        }

        protected void tryJump(bool useEFTMethod = true, bool force = false)
        {
            MovementContext movementContext = BotOwner.GetPlayer.MovementContext;

            if (useEFTMethod)
            {
                movementContext.TryJump();
                return;
            }

            if (movementContext.CanJump || force)
            {
                movementContext.method_2(1f);
                movementContext.PlayerAnimatorEnableJump(true);
            }
        }

        /// <summary>
        /// Execute one frame of custom mover movement. Call every frame from Update()
        /// when the custom mover is active (before the throttled path recalculation).
        /// </summary>
        /// <returns>The current path follower status, or Idle if custom mover is not active.</returns>
        protected PathFollowerStatus TickCustomMover(bool isSprinting)
        {
            if (!UseCustomMover)
                return PathFollowerStatus.Idle;

            return _customMover.Tick(isSprinting);
        }

        protected void restartStuckTimer()
        {
            _softStuck.Reset();
            _hardStuck.Reset();
        }

        protected bool checkIfBotIsStuck()
        {
            return checkIfBotIsStuck(true);
        }

        protected bool checkIfBotIsStuck(bool drawPath)
        {
            if (!ConfigController.Config.Questing.StuckBotDetection.StuckBotRemedies.Enabled)
            {
                return false;
            }

            float currentTime = Time.time;
            Vector3 currentPos = BotOwner.Position;
            float moveSpeed = BotOwner.GetPlayer.MovementContext.CharacterMovementSpeed;

            // Update soft stuck detector
            if (_softStuck.Update(currentPos, moveSpeed, currentTime))
            {
                handleSoftStuckTransition();
            }

            // Update hard stuck detector
            if (_hardStuck.Update(currentPos, moveSpeed, currentTime))
            {
                handleHardStuckTransition();
            }

            // Bot is hopelessly stuck when hard detector reaches Failed
            if (_hardStuck.Status == HardStuckStatus.Failed)
            {
                if (drawPath && ConfigController.Config.Debug.ShowFailedPaths)
                {
                    drawBotPath(Color.red);
                }

                return true;
            }

            return false;
        }

        protected void drawBotPath(Color color)
        {
            Vector3[] botPath = BotOwner.Mover?.GetCurrentPath();
            if (botPath == null)
            {
                LoggingController.LogWarning("Cannot draw null path for " + BotOwner.GetText());
                return;
            }

            // The visual representation of the bot's path needs to be offset vertically so it's raised above the ground
            List<Vector3> adjustedPathCorners = new List<Vector3>();
            foreach (Vector3 corner in botPath)
            {
                adjustedPathCorners.Add(new Vector3(corner.x, corner.y + 0.75f, corner.z));
            }

            string pathName = "BotPath_" + BotOwner.Id + "_" + DateTime.Now.ToFileTime();

            Models.Pathing.PathVisualizationData botPathRendering = new Models.Pathing.PathVisualizationData(
                pathName,
                adjustedPathCorners.ToArray(),
                color
            );
            Singleton<GameWorld>.Instance.GetComponent<PathRenderer>().AddOrUpdatePath(botPathRendering);
        }

        protected void outlineTargetPosition(Color color)
        {
            if (!ObjectiveManager.Position.HasValue)
            {
                LoggingController.LogError("Cannot outline null position for bot " + BotOwner.GetText());
                return;
            }

            DebugHelpers.outlinePosition(ObjectiveManager.Position.Value, color);
        }

        protected void updateBotZoneForGroup(bool allowForFollowers = false)
        {
            if (!ConfigController.Config.Questing.UpdateBotZoneAfterStopping)
            {
                return;
            }

            BotSpawner botSpawnerClass = Singleton<IBotGame>.Instance.BotsController.BotSpawner;
            BotZone closestBotZone = botSpawnerClass.GetClosestZone(BotOwner.Position, out float dist);

            if (BotOwner.BotsGroup.BotZone == closestBotZone)
            {
                return;
            }

            // Do not allow followers to set the BotZone
            if (!allowForFollowers && !BotOwner.Boss.IamBoss && (BotOwner.BotsGroup.MembersCount > 1))
            {
                return;
            }

            botZoneField.SetValue(BotOwner.BotsGroup, closestBotZone);
            BotOwner.PatrollingData.PointChooser.ShallChangeWay(true);
        }

        private void handleSoftStuckTransition()
        {
            if (!BotOwner.GetPlayer.MovementContext.IsGrounded)
            {
                return;
            }

            switch (_softStuck.Status)
            {
                case SoftStuckStatus.Vaulting:
                    LoggingController.LogWarning(BotOwner.GetText() + " is stuck. Trying to vault...");
                    BotOwner.Mover.Stop();
                    BotOwner.Mover.SetPose(1f);
                    BotOwner.GetPlayer.MovementContext.TryVaulting();
                    break;
                case SoftStuckStatus.Jumping:
                    LoggingController.LogWarning(BotOwner.GetText() + " is stuck. Trying to jump...");
                    BotOwner.Mover.Stop();
                    BotOwner.Mover.SetPose(1f);
                    tryJump(false);
                    break;
            }
        }

        private void handleHardStuckTransition()
        {
            switch (_hardStuck.Status)
            {
                case HardStuckStatus.Retrying:
                    LoggingController.LogWarning(BotOwner.GetText() + " is hard stuck. Retrying path...");
                    ObjectiveManager.BotPath.ForcePathRecalculation();
                    break;
                case HardStuckStatus.Teleport:
                    LoggingController.LogWarning(BotOwner.GetText() + " is hard stuck. Attempting teleport...");
                    attemptSafeTeleport();
                    break;
            }
        }

        private void attemptSafeTeleport()
        {
            var remedies = ConfigController.Config.Questing.StuckBotDetection.StuckBotRemedies;
            if (!remedies.TeleportEnabled)
            {
                return;
            }

            // Need a path corner to teleport to
            Vector3[] corners = ObjectiveManager.BotPath.Corners;
            if (corners == null || corners.Length == 0)
            {
                return;
            }

            Vector3 teleportPos = corners[0];
            teleportPos.y += 0.25f;

            float maxDistSqr = remedies.TeleportMaxPlayerDistance * remedies.TeleportMaxPlayerDistance;

            // Safety checks against all human players
            var allPlayers = Singleton<GameWorld>.Instance?.AllAlivePlayersList;
            if (allPlayers != null)
            {
                for (int i = 0; i < allPlayers.Count; i++)
                {
                    var player = allPlayers[i];
                    if (player == null || player.IsAI)
                    {
                        continue;
                    }

                    if (player.HealthController?.IsAlive != true)
                    {
                        continue;
                    }

                    // Proximity check
                    if ((player.Position - BotOwner.Position).sqrMagnitude <= maxDistSqr)
                    {
                        LoggingController.LogWarning(BotOwner.GetText() + " teleport blocked: human player too close.");
                        return;
                    }

                    // Line-of-sight check from human head to bot position
                    Vector3 humanHeadPos = player.PlayerBones.Head.Original.position;
                    if (!Physics.Linecast(humanHeadPos, BotOwner.Position, LayerMaskClass.HighPolyWithTerrainMask))
                    {
                        LoggingController.LogWarning(BotOwner.GetText() + " teleport blocked: visible to human player.");
                        return;
                    }
                }
            }

            LoggingController.LogWarning("Teleporting " + BotOwner.GetText() + " to " + teleportPos);
            BotOwner.GetPlayer.Teleport(teleportPos);
        }
    }
}
