using System.Diagnostics;
using EFT;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.Objective
{
    /// <summary>
    /// BigBrain action for approaching and looting a target.
    /// Uses CustomMoverController for approach, then delegates to loot interaction helpers.
    /// State machine: Approach → Interact → Complete/Fail.
    /// </summary>
    internal class LootAction : BehaviorExtensions.GoToPositionAbstractAction
    {
        private enum LootPhase : byte
        {
            Approach,
            Interact,
            Complete,
            Failed,
        }

        private LootPhase _phase = LootPhase.Approach;
        private Stopwatch _interactTimer = Stopwatch.StartNew();
        private float _maxLootTimeSeconds = 30f;
        private Vector3 _targetPosition;
        private bool _hasTarget;

        public LootAction(BotOwner _BotOwner)
            : base(_BotOwner, 100) { }

        public override void Start()
        {
            base.Start();

            _phase = LootPhase.Approach;
            _interactTimer.Restart();

            // Read loot target from entity
            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity) && entity.HasLootTarget)
            {
                _targetPosition = new Vector3(entity.LootTargetX, entity.LootTargetY, entity.LootTargetZ);
                _hasTarget = true;

                entity.IsApproachingLoot = true;

                // Read config timeout
                var config = ConfigController.Config?.Questing?.Looting;
                if (config != null)
                    _maxLootTimeSeconds = config.MaxLootingTimeSeconds;
            }
            else
            {
                _hasTarget = false;
                _phase = LootPhase.Failed;
            }
        }

        public override void Stop()
        {
            base.Stop();

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                entity.IsApproachingLoot = false;
                entity.IsLooting = false;
            }
        }

        public override void Update(DrakiaXYZ.BigBrain.Brains.CustomLayer.ActionData data)
        {
            if (!_hasTarget || _phase == LootPhase.Complete || _phase == LootPhase.Failed)
                return;

            // Execute custom mover every frame
            if (UseCustomMover)
            {
                TickCustomMover(CanSprint);
                BotOwner.SetPose(1f);
                BotOwner.BotLay.GetUp(true);
                BotOwner.DoorOpener.ManualUpdate();
            }
            else
            {
                UpdateBotMovement(CanSprint);
            }

            UpdateBotSteering();

            // Timeout check
            float elapsed = (float)_interactTimer.ElapsedMilliseconds / 1000f;
            if (elapsed > _maxLootTimeSeconds)
            {
                LoggingController.LogWarning(BotOwner.GetText() + " loot action timed out after " + _maxLootTimeSeconds + "s");
                FailAndClear();
                return;
            }

            if (!canUpdate())
                return;

            switch (_phase)
            {
                case LootPhase.Approach:
                    UpdateApproach();
                    break;
                case LootPhase.Interact:
                    UpdateInteract();
                    break;
            }
        }

        private void UpdateApproach()
        {
            // Disable sprint when close
            float distSqr = (BotOwner.Position - _targetPosition).sqrMagnitude;
            CanSprint = distSqr > 36f; // Walk within 6m

            // Check if arrived
            var config = ConfigController.Config?.Questing?.Looting;
            float approachDist = config?.ApproachDistance ?? 0.85f;
            float approachYTol = config?.ApproachYTolerance ?? 0.5f;

            Vector3 delta = BotOwner.Position - _targetPosition;
            float xzDistSqr = delta.x * delta.x + delta.z * delta.z;
            float yDiff = Mathf.Abs(delta.y);

            if (xzDistSqr < approachDist * approachDist && yDiff < approachYTol)
            {
                // Arrived — start interaction
                _phase = LootPhase.Interact;
                _interactTimer.Restart();

                if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
                {
                    entity.IsApproachingLoot = false;
                    entity.IsLooting = true;
                }

                return;
            }

            // Navigate to target
            RecalculatePath(_targetPosition);

            // Check stuck
            if (checkIfBotIsStuck())
            {
                LoggingController.LogWarning(BotOwner.GetText() + " stuck while approaching loot, failing");
                FailAndClear();
            }
        }

        private void UpdateInteract()
        {
            // Interaction is handled by the loot helpers called from HiveMind tick.
            // This action just holds the bot in place during interaction.
            // The HiveMind tick or bridge will clear the loot target when done.

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                if (!entity.HasLootTarget)
                {
                    // Loot target was cleared (interaction complete or canceled)
                    _phase = LootPhase.Complete;
                    entity.IsLooting = false;
                    return;
                }
            }

            // Keep bot still during interaction
            BotOwner.SetPose(0.5f); // Slight crouch for looting animation
        }

        private void FailAndClear()
        {
            _phase = LootPhase.Failed;

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                entity.HasLootTarget = false;
                entity.IsApproachingLoot = false;
                entity.IsLooting = false;
            }
        }
    }
}
