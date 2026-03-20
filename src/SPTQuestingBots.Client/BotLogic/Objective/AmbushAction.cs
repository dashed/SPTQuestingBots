using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFT;
using SPTQuestingBots.BotLogic.BotMonitor.Monitors;

namespace SPTQuestingBots.BotLogic.Objective
{
    public class AmbushAction : BehaviorExtensions.GoToPositionAbstractAction
    {
        private bool allowedToIgnoreHearing = true;
        private bool isIgnoringHearing = false;

        public AmbushAction(BotOwner _BotOwner)
            : base(_BotOwner, 100)
        {
            SetBaseAction(BotActionNodesClass.CreateNode(BotLogicDecision.holdPosition, BotOwner));
        }

        public AmbushAction(BotOwner _BotOwner, bool _allowedToIgnoreHearing)
            : this(_BotOwner)
        {
            allowedToIgnoreHearing = _allowedToIgnoreHearing;
        }

        public override void Start()
        {
            base.Start();

            BotOwner.PatrollingData.Pause();

            StartActionElapsedTime();
        }

        public override void Stop()
        {
            base.Stop();

            BotOwner.PatrollingData.Unpause();

            PauseActionElapsedTime();

            // If the bot was instructed to ignore its hearing, reverse the instruction so it can be effective in combat again
            if (allowedToIgnoreHearing && isIgnoringHearing)
            {
                ObjectiveManager.BotMonitor.GetMonitor<BotHearingMonitor>().TrySetIgnoreHearing((float)ActionElapsedTimeRemaining, false);
                isIgnoringHearing = false;
            }
        }

        public override void Update(DrakiaXYZ.BigBrain.Brains.CustomLayer.ActionData data)
        {
            UpdateBaseAction(data);

            // While the bot is moving to the ambush position, have it look where it's going. Once at the ambush position, have it look to the
            // a specific location if defined by the quest. Otherwise, use the game's ambush cover fire position if available,
            // or look where it just came from.
            if (!ObjectiveManager.IsCloseToObjective())
            {
                UpdateBotSteering();
            }
            else
            {
                if (ObjectiveManager.LookToPosition.HasValue)
                {
                    UpdateBotSteering(ObjectiveManager.LookToPosition.Value);
                }
                else if (TryGetGameAmbushLookPoint(out var lookPoint))
                {
                    UpdateBotSteering(lookPoint);
                }
                else
                {
                    TryLookToLastCorner();
                }
            }

            // Don't allow expensive parts of this behavior to run too often
            if (!canUpdate())
            {
                return;
            }

            if (!ObjectiveManager.Position.HasValue)
            {
                throw new InvalidOperationException("Cannot go to a null position");
            }

            ObjectiveManager.StartJobAssigment();

            // This doesn't really need to be updated every frame
            CanSprint = IsAllowedToSprint();

            if (!ObjectiveManager.IsCloseToObjective())
            {
                RecalculatePath(ObjectiveManager.Position.Value);
                isIgnoringHearing = false;
                RestartActionElapsedTime();

                return;
            }

            CheckMinElapsedActionTime();

            // Needed in case somebody drops the layer priorities of this mod. Without doing this, SAIN will prevent bots from staying in their ambush spots.
            if (allowedToIgnoreHearing && !isIgnoringHearing)
            {
                ObjectiveManager.BotMonitor.GetMonitor<BotHearingMonitor>().TrySetIgnoreHearing((float)ActionElapsedTimeRemaining, true);
                isIgnoringHearing = true;
            }

            restartStuckTimer();
        }

        /// <summary>
        /// Try to get a look direction from the game's BotAmbushData cover point.
        /// When the game has a valid ambush cover, its FirePosition gives a good
        /// direction for the bot to face (toward the expected threat).
        /// </summary>
        private bool TryGetGameAmbushLookPoint(out UnityEngine.Vector3 lookPoint)
        {
            try
            {
                if (BotOwner.Ambush != null && BotOwner.Ambush.TryGetAmbushPoint(out var coverPoint))
                {
                    lookPoint = coverPoint.FirePosition;
                    return true;
                }
            }
            catch
            {
                // BotAmbushData may not be fully initialized
            }

            lookPoint = default;
            return false;
        }
    }
}
