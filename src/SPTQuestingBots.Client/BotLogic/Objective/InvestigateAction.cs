using System.Diagnostics;
using EFT;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.Objective
{
    /// <summary>
    /// BigBrain action for investigate behavior — bots hear gunfire and cautiously
    /// approach the event location. Two simple states: Approach (cautious walk) then
    /// LookAround (stop and scan). Lighter-weight than VultureAction.
    /// </summary>
    internal class InvestigateAction : BehaviorExtensions.GoToPositionAbstractAction
    {
        private static readonly System.Random SharedRandom = new System.Random();
        private Vector3 _targetPosition;
        private bool _hasTarget;
        private Stopwatch _timer = Stopwatch.StartNew();
        private bool _isLookingAround;
        private float _movementTimeout;
        private float _approachPose;
        private float _arrivalDistanceSqr;
        private float _lookAroundDuration;
        private float _headScanIntervalMin;
        private float _headScanIntervalMax;
        private float _nextScanTime;

        public InvestigateAction(BotOwner _BotOwner)
            : base(_BotOwner, 100) { }

        public override void Start()
        {
            base.Start();

            _timer.Restart();
            _isLookingAround = false;

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity) && entity.HasNearbyEvent)
            {
                _targetPosition = new Vector3(entity.NearbyEventX, entity.NearbyEventY, entity.NearbyEventZ);

                // Prefer game's search point when it's based on actual enemy position —
                // it's more accurate than our combat event centroid
                if (TryGetGameSearchTarget(entity, out var searchTarget))
                {
                    _targetPosition = searchTarget;
                    LoggingController.LogInfo(
                        "[InvestigateAction] Bot "
                            + BotOwner.GetText()
                            + ": using game SearchData target at ("
                            + _targetPosition.x.ToString("F0")
                            + ","
                            + _targetPosition.y.ToString("F0")
                            + ","
                            + _targetPosition.z.ToString("F0")
                            + ")"
                    );
                }

                _hasTarget = true;
                entity.IsInvestigating = true;
                entity.InvestigateTimeoutAt = entity.CurrentGameTime + 60f;
                LoggingController.LogInfo(
                    "[InvestigateAction] Bot "
                        + BotOwner.GetText()
                        + ": started investigating at ("
                        + _targetPosition.x.ToString("F0")
                        + ","
                        + _targetPosition.y.ToString("F0")
                        + ","
                        + _targetPosition.z.ToString("F0")
                        + ")"
                );

                var config = ConfigController.Config?.Questing?.Investigate;
                if (config != null)
                {
                    _movementTimeout = config.MovementTimeout;
                    _approachPose = config.ApproachPose;
                    _arrivalDistanceSqr = config.ArrivalDistance * config.ArrivalDistance;
                    _lookAroundDuration = config.LookAroundDuration;
                    _headScanIntervalMin = config.HeadScanIntervalMin;
                    _headScanIntervalMax = config.HeadScanIntervalMax;
                }
                else
                {
                    _movementTimeout = 45f;
                    _approachPose = 0.6f;
                    _arrivalDistanceSqr = 15f * 15f;
                    _lookAroundDuration = 8f;
                    _headScanIntervalMin = 2f;
                    _headScanIntervalMax = 5f;
                }

                _nextScanTime = 0f;
            }
            else
            {
                _hasTarget = false;
                LoggingController.LogWarning("[InvestigateAction] Bot " + BotOwner.GetText() + ": no target event found on Start()");
            }
        }

        public override void Stop()
        {
            base.Stop();

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                LoggingController.LogInfo("[InvestigateAction] Bot " + BotOwner.GetText() + ": stopped investigating");
                entity.IsInvestigating = false;
                entity.HasNearbyEvent = false;
            }
        }

        public override void Update(DrakiaXYZ.BigBrain.Brains.CustomLayer.ActionData data)
        {
            if (!_hasTarget)
                return;

            if (_isLookingAround)
            {
                UpdateLookAround();
                return;
            }

            // Per-frame movement during approach
            if (UseCustomMover)
            {
                TickCustomMover(false);
                BotOwner.SetPose(_approachPose);
                BotOwner.BotLay.GetUp(true);
                BotOwner.DoorOpener.ManualUpdate();
            }
            else
            {
                CanSprint = false;
                UpdateBotMovement(false);
                BotOwner.SetPose(_approachPose);
            }

            // Throttle expensive updates
            if (!canUpdate())
            {
                UpdateBotSteering(_targetPosition);
                return;
            }

            // Movement timeout
            float elapsedSeconds = (float)_timer.ElapsedMilliseconds / 1000f;
            if (elapsedSeconds > _movementTimeout)
            {
                LoggingController.LogInfo(
                    "[InvestigateAction] Bot " + BotOwner.GetText() + ": movement timeout after " + elapsedSeconds.ToString("F1") + "s"
                );
                CompleteInvestigation();
                return;
            }

            // Check arrival
            float distSqr = DistanceSqrToTarget();
            if (distSqr < _arrivalDistanceSqr)
            {
                float dist = (float)System.Math.Sqrt(distSqr);
                LoggingController.LogInfo(
                    "[InvestigateAction] Bot "
                        + BotOwner.GetText()
                        + ": arrived at investigation target at "
                        + dist.ToString("F0")
                        + "m, looking around"
                );
                _isLookingAround = true;
                _timer.Restart();
                _nextScanTime = 0f;
                BotOwner.Mover.Stop();
                return;
            }

            // Navigate
            RecalculatePath(_targetPosition);
            UpdateBotSteering(_targetPosition);

            if (checkIfBotIsStuck())
            {
                LoggingController.LogInfo("[InvestigateAction] Bot " + BotOwner.GetText() + ": stuck during approach, completing");
                CompleteInvestigation();
            }
        }

        private void UpdateLookAround()
        {
            float elapsed = (float)_timer.ElapsedMilliseconds / 1000f;

            // Duration expired
            if (elapsed > _lookAroundDuration)
            {
                LoggingController.LogInfo(
                    "[InvestigateAction] Bot " + BotOwner.GetText() + ": look-around complete after " + elapsed.ToString("F1") + "s"
                );
                CompleteInvestigation();
                return;
            }

            // Maintain cautious pose
            BotOwner.SetPose(_approachPose);

            // Random head scan
            if (Time.time >= _nextScanTime)
            {
                float angle = (float)(SharedRandom.NextDouble() * 2 - 1) * 120f;
                Vector3 lookDir = Quaternion.Euler(0, angle, 0) * BotOwner.LookDirection;
                Vector3 lookPoint = BotOwner.Position + lookDir * 20f;
                BotOwner.Steering.LookToPoint(lookPoint);

                _nextScanTime =
                    Time.time + _headScanIntervalMin + (float)(SharedRandom.NextDouble() * (_headScanIntervalMax - _headScanIntervalMin));
            }
        }

        private float DistanceSqrToTarget()
        {
            Vector3 pos = BotOwner.Position;
            float dx = pos.x - _targetPosition.x;
            float dz = pos.z - _targetPosition.z;
            return dx * dx + dz * dz;
        }

        private void CompleteInvestigation()
        {
            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                LoggingController.LogInfo("[InvestigateAction] Bot " + BotOwner.GetText() + ": investigation completed");
                entity.IsInvestigating = false;
                entity.HasNearbyEvent = false;
            }
        }

        /// <summary>
        /// Try to get a better investigation target from the game's BotSearchData.
        /// The game's SearchPoint with TypeSearch == playerPosition is based on actual
        /// enemy last-known position, which is more accurate than our combat event centroid.
        /// </summary>
        private bool TryGetGameSearchTarget(BotEntity entity, out Vector3 target)
        {
            // Only prefer game's target when it's a player-position type search
            // (based on actual enemy sighting, not generic map point)
            if (entity.HasGameSearchTarget && entity.GameSearchTargetType == 0) // 0 = playerPosition
            {
                target = new Vector3(entity.GameSearchTargetX, entity.GameSearchTargetY, entity.GameSearchTargetZ);
                return true;
            }

            target = default;
            return false;
        }
    }
}
