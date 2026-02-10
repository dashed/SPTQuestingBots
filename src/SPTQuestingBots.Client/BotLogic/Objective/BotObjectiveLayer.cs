using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFT;
using SPTQuestingBots.BehaviorExtensions;
using SPTQuestingBots.BotLogic.BotMonitor;
using SPTQuestingBots.BotLogic.BotMonitor.Monitors;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Models.Questing;

namespace SPTQuestingBots.BotLogic.Objective
{
    internal class BotObjectiveLayer : CustomLayerForQuesting
    {
        private static UtilityTaskManager _taskManager;
        private static UtilityTaskManager _followerTaskManager;

        public BotObjectiveLayer(BotOwner _botOwner, int _priority)
            : base(_botOwner, _priority, 25) { }

        public override string GetName()
        {
            return "BotObjectiveLayer";
        }

        public override Action GetNextAction()
        {
            return base.GetNextAction();
        }

        public override bool IsCurrentActionEnding()
        {
            return base.IsCurrentActionEnding();
        }

        public override bool IsActive()
        {
            if (!canUpdate())
            {
                return previousState;
            }

            // LOD skip: reduce update frequency for distant bots
            var lodConfig = Controllers.ConfigController.Config?.Questing?.BotLod;
            if (lodConfig != null && lodConfig.Enabled)
            {
                if (
                    BotLogic.ECS.BotEntityBridge.TryGetEntity(BotOwner, out var lodEntity)
                    && BotLodCalculator.ShouldSkipUpdate(
                        lodEntity.LodTier,
                        lodEntity.LodFrameCounter,
                        lodConfig.ReducedFrameSkip,
                        lodConfig.MinimalFrameSkip
                    )
                )
                {
                    return previousState;
                }
            }

            BotQuestingDecisionMonitor decisionMonitor = objectiveManager.BotMonitor.GetMonitor<BotQuestingDecisionMonitor>();

            if (!decisionMonitor.IsAllowedToQuest())
            {
                return updatePreviousState(false);
            }

            if (decisionMonitor.HasAQuestingBoss)
            {
                // Gate 1: allow followers with squad tactical positions
                if (
                    QuestingBotsPluginConfig.SquadStrategyEnabled.Value
                    && decisionMonitor.CurrentDecision == BotQuestingDecision.SquadQuest
                )
                {
                    return updatePreviousState(trySetNextActionForFollower());
                }

                return updatePreviousState(false);
            }

            float pauseRequestTime = getPauseRequestTime();
            if (pauseRequestTime > 0)
            {
                //LoggingController.LogInfo("Pausing layer for " + pauseRequestTime + "s...");
                return pauseLayer(pauseRequestTime);
            }

            // Check if the bot has wandered too far from its followers
            if (decisionMonitor.CurrentDecision == BotQuestingDecision.Regroup)
            {
                setNextAction(BotActionType.BossRegroup, "BossRegroup");
                return updatePreviousState(true);
            }

            if (decisionMonitor.CurrentDecision != BotQuestingDecision.Quest)
            {
                return updatePreviousState(false);
            }

            // Determine what type of action is needed for the bot to complete its assignment
            if (QuestingBotsPluginConfig.UseUtilityAI.Value)
            {
                bool utilityResult = trySetNextActionUtility();
                if (!utilityResult)
                {
                    utilityResult = tryZoneMovementFallback();
                }
                return updatePreviousState(utilityResult);
            }
            else
                return updatePreviousState(trySetNextAction());
        }

        private bool trySetNextAction()
        {
            switch (objectiveManager.CurrentQuestAction)
            {
                case QuestAction.MoveToPosition:
                    if (objectiveManager.MustUnlockDoor)
                    {
                        string interactiveObjectShortID = objectiveManager.GetCurrentQuestInteractiveObject().Id.Abbreviate();
                        setNextAction(BotActionType.UnlockDoor, "UnlockDoor (" + interactiveObjectShortID + ")");
                    }
                    else
                    {
                        setNextAction(BotActionType.GoToObjective, "GoToObjective");
                    }
                    return updatePreviousState(true);

                case QuestAction.HoldAtPosition:
                    setNextAction(BotActionType.HoldPosition, "HoldPosition (" + objectiveManager.MinElapsedActionTime + "s)");
                    return updatePreviousState(true);

                case QuestAction.Ambush:
                    if (!objectiveManager.IsCloseToObjective())
                    {
                        setNextAction(BotActionType.GoToObjective, "GoToAmbushPosition");
                    }
                    else
                    {
                        setNextAction(BotActionType.Ambush, "Ambush (" + objectiveManager.MinElapsedActionTime + "s)");
                    }
                    return updatePreviousState(true);

                case QuestAction.Snipe:
                    if (!objectiveManager.IsCloseToObjective())
                    {
                        setNextAction(BotActionType.GoToObjective, "GoToSnipePosition");
                    }
                    else
                    {
                        setNextAction(BotActionType.Snipe, "Snipe (" + objectiveManager.MinElapsedActionTime + "s)");
                    }
                    return updatePreviousState(true);

                case QuestAction.PlantItem:
                    if (!objectiveManager.IsCloseToObjective())
                    {
                        setNextAction(BotActionType.GoToObjective, "GoToPlantPosition");
                    }
                    else
                    {
                        setNextAction(BotActionType.PlantItem, "PlantItem (" + objectiveManager.MinElapsedActionTime + "s)");
                    }
                    return updatePreviousState(true);

                case QuestAction.ToggleSwitch:
                    setNextAction(BotActionType.ToggleSwitch, "ToggleSwitch");
                    return updatePreviousState(true);

                case QuestAction.CloseNearbyDoors:
                    setNextAction(BotActionType.CloseNearbyDoors, "CloseNearbyDoors");
                    return updatePreviousState(true);

                case QuestAction.RequestExtract:
                    if (objectiveManager.BotMonitor.GetMonitor<BotExtractMonitor>().TryInstructBotToExtract())
                    {
                        objectiveManager.StopQuesting();
                    }
                    objectiveManager.CompleteObjective();
                    return updatePreviousState(true);
            }

            // Failsafe
            return updatePreviousState(false);
        }

        private bool trySetNextActionForFollower()
        {
            if (_followerTaskManager == null)
                _followerTaskManager = SquadTaskFactory.Create();

            if (!BotEntityBridge.TryGetEntity(BotOwner, out var entity))
                return false;

            // Ensure TaskScores array is allocated for follower tasks
            if (entity.TaskScores == null || entity.TaskScores.Length < SquadTaskFactory.TaskCount)
                entity.TaskScores = new float[SquadTaskFactory.TaskCount];

            // Sync position for distance calculations
            BotEntityBridge.SyncPosition(BotOwner);

            // Score and pick
            _followerTaskManager.ScoreAndPick(entity);

            var task = entity.TaskAssignment.Task as QuestUtilityTask;
            if (task == null)
                return false;

            setNextAction((BotActionType)task.BotActionTypeId, task.ActionReason);
            return true;
        }

        private bool tryZoneMovementFallback()
        {
            var zoneConfig = Controllers.ConfigController.Config?.Questing?.ZoneMovement;
            if (zoneConfig == null || !zoneConfig.Enabled)
                return false;

            setNextAction(BotActionType.GoToObjective, "ZoneWander");
            LoggingController.LogDebug(BotOwner.GetText() + " falling back to zone wander");
            return true;
        }

        private bool trySetNextActionUtility()
        {
            if (_taskManager == null)
                _taskManager = QuestTaskFactory.Create();

            if (!BotEntityBridge.TryGetEntity(BotOwner, out var entity))
                return false;

            // Ensure TaskScores array is allocated and correctly sized
            if (entity.TaskScores == null || entity.TaskScores.Length < QuestTaskFactory.TaskCount)
                entity.TaskScores = new float[QuestTaskFactory.TaskCount];

            // Sync quest state from BotObjectiveManager into entity fields for scoring
            BotEntityBridge.SyncQuestState(BotOwner);

            // Score all tasks and pick the best one
            _taskManager.ScoreAndPick(entity);

            // Read the selected task
            var task = entity.TaskAssignment.Task as QuestUtilityTask;
            if (task == null)
                return false;

            // Map BotActionTypeId back to BotActionType and dispatch
            setNextAction((BotActionType)task.BotActionTypeId, task.ActionReason);
            return true;
        }
    }
}
