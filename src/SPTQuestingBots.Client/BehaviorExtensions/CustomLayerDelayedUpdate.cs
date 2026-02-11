using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DrakiaXYZ.BigBrain.Brains;
using EFT;

namespace SPTQuestingBots.BehaviorExtensions
{
    public enum BotActionType
    {
        Undefined,
        GoToObjective,
        FollowBoss,
        HoldPosition,
        Ambush,
        Snipe,
        PlantItem,
        BossRegroup,
        FollowerRegroup,
        Sleep,
        ToggleSwitch,
        UnlockDoor,
        CloseNearbyDoors,
        Loot,
        Vulture,
        Linger,
        Investigate,
        SpawnEntry,
        Patrol,
    }

    internal abstract class CustomLayerDelayedUpdate : CustomLayer
    {
        protected bool previousState { get; private set; } = false;

        private readonly UpdateThrottle _throttle;
        private BotActionType nextAction = BotActionType.Undefined;
        private BotActionType previousAction = BotActionType.Undefined;
        private string actionReason = "???";

        public CustomLayerDelayedUpdate(BotOwner _botOwner, int _priority)
            : this(_botOwner, _priority, UpdateThrottle.DefaultIntervalMs) { }

        public CustomLayerDelayedUpdate(BotOwner _botOwner, int _priority, int delayInterval)
            : base(_botOwner, _priority)
        {
            _throttle = new UpdateThrottle(delayInterval);
        }

        public override bool IsCurrentActionEnding()
        {
            return nextAction != previousAction;
        }

        public override Action GetNextAction()
        {
            //LoggingController.LogInfo(BotOwner.GetText() + " is swtiching from " + previousAction.ToString() + " to " + nextAction.ToString());

            previousAction = nextAction;

            switch (nextAction)
            {
                case BotActionType.GoToObjective:
                    return new Action(typeof(BotLogic.Objective.GoToObjectiveAction), actionReason);
                case BotActionType.FollowBoss:
                    return new Action(typeof(BotLogic.Follow.FollowBossAction), actionReason);
                case BotActionType.HoldPosition:
                    return new Action(typeof(BotLogic.Objective.HoldAtObjectiveAction), actionReason);
                case BotActionType.Ambush:
                    return new Action(typeof(BotLogic.Objective.AmbushAction), actionReason);
                case BotActionType.Snipe:
                    return new Action(typeof(BotLogic.Objective.SnipeAction), actionReason);
                case BotActionType.PlantItem:
                    return new Action(typeof(BotLogic.Objective.PlantItemAction), actionReason);
                case BotActionType.BossRegroup:
                    return new Action(typeof(BotLogic.Follow.BossRegroupAction), actionReason);
                case BotActionType.FollowerRegroup:
                    return new Action(typeof(BotLogic.Follow.FollowerRegroupAction), actionReason);
                case BotActionType.Sleep:
                    return new Action(typeof(BotLogic.Sleep.SleepingAction), actionReason);
                case BotActionType.ToggleSwitch:
                    return new Action(typeof(BotLogic.Objective.ToggleSwitchAction), actionReason);
                case BotActionType.UnlockDoor:
                    return new Action(typeof(BotLogic.Objective.UnlockDoorAction), actionReason);
                case BotActionType.CloseNearbyDoors:
                    return new Action(typeof(BotLogic.Objective.CloseNearbyDoorsAction), actionReason);
                case BotActionType.Loot:
                    return new Action(typeof(BotLogic.Objective.LootAction), actionReason);
                case BotActionType.Vulture:
                    return new Action(typeof(BotLogic.Objective.VultureAction), actionReason);
                case BotActionType.Linger:
                    return new Action(typeof(BotLogic.Objective.LingerAction), actionReason);
                case BotActionType.Investigate:
                    return new Action(typeof(BotLogic.Objective.InvestigateAction), actionReason);
                case BotActionType.SpawnEntry:
                    return new Action(typeof(BotLogic.Objective.SpawnEntryAction), actionReason);
                case BotActionType.Patrol:
                    return new Action(typeof(BotLogic.Objective.PatrolAction), actionReason);
            }

            throw new InvalidOperationException("Invalid action selected for layer");
        }

        protected void setNextAction(BotActionType actionType, string reason)
        {
            nextAction = actionType;
            actionReason = reason;
        }

        protected bool canUpdate()
        {
            return _throttle.CanUpdate();
        }

        protected bool updatePreviousState(bool newState)
        {
            previousState = newState;
            return previousState;
        }

        protected bool pauseLayer()
        {
            return pauseLayer(0);
        }

        protected bool pauseLayer(float minTime)
        {
            previousState = false;
            _throttle.Pause(minTime);

            return false;
        }
    }
}
