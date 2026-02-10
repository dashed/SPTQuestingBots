using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Comfort.Common;
using EFT;
using EFT.Interactive;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Helpers;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.BotLogic.Objective
{
    internal class GoToObjectiveAction : BehaviorExtensions.GoToPositionAbstractAction
    {
        private bool wasStuck = false;
        private Stopwatch unlockDebounceTimer = Stopwatch.StartNew();

        private double unlockDebounceTime => unlockDebounceTimer.ElapsedMilliseconds / 1000.0;

        public GoToObjectiveAction(BotOwner _BotOwner)
            : base(_BotOwner, 100) { }

        public override void Start()
        {
            base.Start();
        }

        public override void Stop()
        {
            base.Stop();
        }

        public override void Update(DrakiaXYZ.BigBrain.Brains.CustomLayer.ActionData data)
        {
            // Execute custom mover every frame (before throttled updates)
            if (UseCustomMover)
            {
                PathFollowerStatus status = TickCustomMover(CanSprint);

                // Context-aware pose for custom mover
                // Personality baseline: aggressive bots stand taller (1.0), cautious crouch more (0.8)
                float pose = 1.0f;

                if (ECS.BotEntityBridge.TryGetEntity(BotOwner, out var poseEntity))
                {
                    // Personality affects base pose: lerp(0.8, 1.0, aggression)
                    pose = 0.8f + 0.2f * poseEntity.Aggression;

                    // Indoor detection: EnvironmentId == 0 means indoor
                    if (BotOwner.AIData?.EnvironmentId == 0)
                    {
                        pose = Math.Min(pose, 0.8f);
                        CanSprint = false;
                    }

                    // Recent combat: cautious posture
                    if (poseEntity.IsInCombat || poseEntity.IsSuspicious)
                    {
                        pose = Math.Min(pose, 0.6f);
                        CanSprint = false;
                    }

                    // Approach behavior: slow down near objective
                    if (poseEntity.DistanceToObjective < 30f && poseEntity.HasActiveObjective)
                    {
                        pose = Math.Min(pose, 0.75f);
                        if (poseEntity.DistanceToObjective < 15f)
                        {
                            CanSprint = false;
                        }
                    }
                }

                // Room clear: detect indoor transition and apply movement instructions
                var roomClearConfig = Controllers.ConfigController.Config.Questing?.RoomClear;
                if (roomClearConfig?.Enabled == true && ECS.BotEntityBridge.TryGetEntity(BotOwner, out var rcEntity))
                {
                    int envId = BotOwner.AIData?.EnvironmentId ?? 1; // default outdoor
                    var instruction = RoomClearController.Update(
                        rcEntity,
                        envId,
                        UnityEngine.Time.time,
                        roomClearConfig.DurationMin,
                        roomClearConfig.DurationMax,
                        roomClearConfig.CornerPauseDuration
                    );

                    if (instruction == RoomClearInstruction.SlowWalk)
                    {
                        pose = Math.Min(pose, roomClearConfig.Pose);
                        CanSprint = false;
                    }
                    else if (instruction == RoomClearInstruction.PauseAtCorner)
                    {
                        pose = Math.Min(pose, roomClearConfig.Pose - 0.1f); // slightly lower at corners
                        CanSprint = false;
                    }
                }

                BotOwner.SetPose(pose);
                BotOwner.BotLay.GetUp(true);
                BotOwner.BewarePlantedMine.Update();
                BotOwner.DoorOpener.ManualUpdate();
            }
            else
            {
                UpdateBotMovement(CanSprint);
            }

            UpdateBotSteering();
            ApplyLookVariance();
            UpdateBotMiscActions();

            // Don't allow expensive parts of this behavior (calculating a path to an objective) to run too often
            if (!canUpdate())
            {
                return;
            }

            // This doesn't really need to be updated every frame
            CanSprint = IsAllowedToSprint();

            // Formation speed override: followers in en-route formation use formation speed logic
            if (ECS.BotEntityBridge.TryGetEntity(BotOwner, out var fmtEntity) && fmtEntity.IsEnRouteFormation)
            {
                CanSprint = FormationSpeedController.ShouldSprint(fmtEntity.FormationSpeed, fmtEntity.BossIsSprinting);
            }

            if (ObjectiveManager.MustUnlockDoor)
            {
                return;
            }

            if (!ObjectiveManager.IsQuestingAllowed || !ObjectiveManager.Position.HasValue)
            {
                return;
            }

            if (!ObjectiveManager.IsJobAssignmentActive)
            {
                return;
            }

            // Check if the bot just completed its objective
            if (ObjectiveManager.IsCloseToObjective())
            {
                // If a door must be opened for the objective, force the bot to open it
                if (ObjectiveManager.DoorIDToUnlockForObjective != "")
                {
                    ensureRequiredDoorIsOpen();
                }

                if (ObjectiveManager.CurrentQuestAction == Models.Questing.QuestAction.MoveToPosition)
                {
                    ObjectiveManager.CompleteObjective();
                }

                //LoggingController.LogInfo(BotOwner.GetText() + " reached its objective (" + ObjectiveManager + ").");

                return;
            }

            ObjectiveManager.StartJobAssigment();

            // Recalculate a path to the bot's objective. This should be done cyclically in case locked doors are opened, etc.
            tryMoveToObjective();

            if (checkIfBotIsStuck())
            {
                if (!wasStuck)
                {
                    ObjectiveManager.StuckCount++;
                    LoggingController.LogWarning("Bot " + BotOwner.GetText() + " is stuck and will get a new objective.");
                }
                wasStuck = true;

                if (ObjectiveManager.TryChangeObjective())
                {
                    restartStuckTimer();
                }
            }
            else
            {
                wasStuck = false;
            }
        }

        private bool tryMoveToObjective()
        {
            // Squad followers navigate to their tactical position instead of the objective
            Vector3 targetPosition = ObjectiveManager.Position.Value;
            if (BotLogic.ECS.BotEntityBridge.TryGetEntity(BotOwner, out var entity) && entity.HasTacticalPosition && entity.HasBoss)
            {
                targetPosition = new Vector3(entity.TacticalPositionX, entity.TacticalPositionY, entity.TacticalPositionZ);
            }

            NavMeshPathStatus? pathStatus = RecalculatePath(targetPosition);

            // Don't complete or fail the objective step except for the action type "MoveToPosition"
            if (ObjectiveManager.CurrentQuestAction != Models.Questing.QuestAction.MoveToPosition)
            {
                return true;
            }

            // Don't complete or fail the objective step if another brain layer is active
            string layerName = BotOwner.Brain.ActiveLayerName() ?? "null";
            if (layerName != nameof(BotObjectiveLayer))
            {
                return true;
            }

            // If the path is invalid, there's nowhere for the bot to move
            //if (!pathStatus.HasValue || (pathStatus.Value == NavMeshPathStatus.PathInvalid))
            if (!pathStatus.HasValue)
            {
                LoggingController.LogWarning("Bot " + BotOwner.GetText() + " cannot find a path to " + ObjectiveManager);
                ObjectiveManager.FailObjective();
                return false;
            }

            // Check if a door must be unlocked to complete proceed with the objective
            if (
                (ObjectiveManager.DoorIDToUnlockForObjective != "")
                && (ObjectiveManager.BotPath.DistanceToTarget < ConfigController.Config.Questing.UnlockingDoors.SearchRadius)
            )
            {
                WorldInteractiveObject doorToUnlockForObjective = Singleton<GameWorld>
                    .Instance.GetComponent<Components.LocationData>()
                    .FindWorldInteractiveObjectsByID(ObjectiveManager.DoorIDToUnlockForObjective);

                if ((doorToUnlockForObjective != null) && (doorToUnlockForObjective.DoorState == EDoorState.Locked))
                {
                    ObjectiveManager.UnlockDoor(doorToUnlockForObjective);
                }
            }

            if (pathStatus.Value == NavMeshPathStatus.PathComplete)
            {
                return true;
            }

            float distanceToEndOfPath = ObjectiveManager.BotPath.GetDistanceToFinalPoint();
            float distanceToObjective = ObjectiveManager.BotPath.DistanceToTarget;
            float missingDistance = ObjectiveManager.BotPath.GetMissingDistanceToTarget();

            // If the bot is far from its objective position but its path is incomplete, have it try going there anyway. Sometimes I get lost too,
            // so who am I to judge?
            if (distanceToEndOfPath > ConfigController.Config.Questing.BotSearchDistances.MaxNavMeshPathError)
            {
                // Check if this is the first time an incomplete path was generated. If so, write a warning message.
                if (ObjectiveManager.HasCompletePath)
                {
                    LoggingController.LogInfo(
                        "Bot "
                            + BotOwner.GetText()
                            + " cannot find a complete path to its objective ("
                            + ObjectiveManager
                            + "). Trying anyway. Distance from end of path to objective: "
                            + missingDistance
                    );
                    ObjectiveManager.ReportIncompletePath();
                }

                return true;
            }

            // Check if it's possible that a locked door is blocking the bot's path
            if (missingDistance <= ConfigController.Config.Questing.UnlockingDoors.SearchRadius)
            {
                // Check if the bot is allowed to unlock doors
                if (ObjectiveManager.MustUnlockDoor || isAllowedToUnlockDoors())
                {
                    // Find a door for the bot to unlock
                    bool foundDoor =
                        ObjectiveManager.MustUnlockDoor
                        || tryFindLockedDoorToOpen(ConfigController.Config.Questing.UnlockingDoors.SearchRadius);
                    Door door = ObjectiveManager.GetCurrentQuestInteractiveObject() as Door;

                    // If there is a door for the bot to unlock, have it try doing that
                    if (foundDoor && (door != null))
                    {
                        LoggingController.LogInfo("Bot " + BotOwner.GetText() + " must unlock door " + door.Id + "...");

                        unlockDebounceTimer.Restart();
                        return true;
                    }
                }
            }

            // Check if the bot got "close enough" to its objective
            if (distanceToObjective < ConfigController.Config.Questing.BotSearchDistances.ObjectiveReachedNavMeshPathError)
            {
                LoggingController.LogInfo(
                    "Bot "
                        + BotOwner.GetText()
                        + " cannot find a complete path to its objective ("
                        + ObjectiveManager
                        + "). Got close enough. Remaining distance to objective: "
                        + distanceToObjective
                );
                ObjectiveManager.CompleteObjective();

                return true;
            }

            //LoggingController.LogInfo("Distance to objective: " + distanceToObjective + ", Distance to end of path: " + distanceToEndOfPath + ", Missing distance: " + missingDistance);

            // If all previous checks fail, the bot is unable to reach its objective position
            LoggingController.LogWarning(
                "Bot "
                    + BotOwner.GetText()
                    + " cannot find a complete path to its objective ("
                    + ObjectiveManager
                    + "). Giving up. Remaining distance to objective: "
                    + distanceToObjective
            );
            ObjectiveManager.FailObjective();
            ObjectiveManager.StuckCount++;

            return false;
        }

        private bool isAllowedToUnlockDoors()
        {
            // Don't search for doors every cycle or too many may be selected in a short time
            if (unlockDebounceTime < ConfigController.Config.Questing.UnlockingDoors.DebounceTime)
            {
                return false;
            }

            BotType botType = BotLogic.ECS.BotEntityBridge.GetBotType(BotOwner);

            if ((botType == BotType.PMC) && ConfigController.Config.Questing.UnlockingDoors.Enabled.PMC)
            {
                return true;
            }
            if ((botType == BotType.Scav) && ConfigController.Config.Questing.UnlockingDoors.Enabled.Scav)
            {
                return true;
            }
            if ((botType == BotType.PScav) && ConfigController.Config.Questing.UnlockingDoors.Enabled.PScav)
            {
                return true;
            }
            if ((botType == BotType.Boss) && ConfigController.Config.Questing.UnlockingDoors.Enabled.Boss)
            {
                return true;
            }

            return false;
        }

        private bool tryFindLockedDoorToOpen(float searchDistance)
        {
            IEnumerable<WorldInteractiveObject> lockedDoors = Singleton<GameWorld>
                .Instance.GetComponent<Components.LocationData>()
                .FindLockedDoorsNearPosition(ObjectiveManager.Position.Value, searchDistance);
            if (!lockedDoors.Any())
            {
                return false;
            }

            WorldInteractiveObject nearestAccessibleDoor = Singleton<GameWorld>
                .Instance.GetComponent<Components.LocationData>()
                .FindFirstAccessibleDoor(lockedDoors, BotOwner.Position);
            if (nearestAccessibleDoor == null)
            {
                return false;
            }

            ObjectiveManager.UnlockDoor(nearestAccessibleDoor);
            return true;
        }

        private void ensureRequiredDoorIsOpen()
        {
            WorldInteractiveObject worldInteractiveObject = Singleton<GameWorld>
                .Instance.GetComponent<Components.LocationData>()
                .FindWorldInteractiveObjectsByID(ObjectiveManager.DoorIDToUnlockForObjective);
            if (worldInteractiveObject == null)
            {
                LoggingController.LogError(
                    "Bot " + BotOwner.GetText() + " cannot find door " + ObjectiveManager.DoorIDToUnlockForObjective
                );
                return;
            }

            if (worldInteractiveObject.DoorState == EDoorState.Open)
            {
                return;
            }

            InteractionResult interactionResult = worldInteractiveObject.GetInteractionResult(EInteractionType.Open, BotOwner);
            BotOwner.InteractWithDoor(worldInteractiveObject, interactionResult);
        }
    }
}
