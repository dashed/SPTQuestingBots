using System.Diagnostics;
using EFT;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.Objective
{
    /// <summary>
    /// BigBrain action for vulture behavior — bots hear gunfire and move to ambush
    /// weakened survivors. Multi-phase state machine: Approach → SilentApproach →
    /// HoldAmbush → Rush → Complete. Ported from Vulture mod's VultureLogic.
    /// </summary>
    internal class VultureAction : BehaviorExtensions.GoToPositionAbstractAction
    {
        private Vector3 _targetPosition;
        private bool _hasTarget;
        private Stopwatch _phaseTimer = Stopwatch.StartNew();
        private float _ambushDuration;
        private float _silentApproachDistance;
        private float _silenceTriggerDuration;
        private float _movementTimeout;
        private bool _enableSilentApproach;
        private bool _enableFlashlightDiscipline;
        private bool _enableParanoia;
        private float _paranoiaIntervalMin;
        private float _paranoiaIntervalMax;
        private float _paranoiaAngleRange;
        private float _nextParanoiaTime;
        private float _ambushDistanceMinSqr;
        private float _ambushDistanceMaxSqr;

        public VultureAction(BotOwner _BotOwner)
            : base(_BotOwner, 100) { }

        public override void Start()
        {
            base.Start();

            _phaseTimer.Restart();

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity) && entity.HasNearbyEvent)
            {
                _targetPosition = new Vector3(entity.NearbyEventX, entity.NearbyEventY, entity.NearbyEventZ);
                _hasTarget = true;
                entity.VulturePhase = VulturePhase.Approach;
                LoggingController.LogInfo(
                    "[VultureAction] Bot "
                        + BotOwner.GetText()
                        + ": started vulture approach to ("
                        + _targetPosition.x.ToString("F0")
                        + ","
                        + _targetPosition.y.ToString("F0")
                        + ","
                        + _targetPosition.z.ToString("F0")
                        + ")"
                );

                var config = ConfigController.Config?.Questing?.Vulture;
                if (config != null)
                {
                    _ambushDuration = config.AmbushDuration;
                    _silentApproachDistance = config.SilentApproachDistance;
                    _silenceTriggerDuration = config.SilenceTriggerDuration;
                    _movementTimeout = config.MovementTimeout;
                    _enableSilentApproach = config.EnableSilentApproach;
                    _enableFlashlightDiscipline = config.EnableFlashlightDiscipline;
                    _enableParanoia = config.EnableParanoia;
                    _paranoiaIntervalMin = config.ParanoiaIntervalMin;
                    _paranoiaIntervalMax = config.ParanoiaIntervalMax;
                    _paranoiaAngleRange = config.ParanoiaAngleRange;
                    _ambushDistanceMinSqr = config.AmbushDistanceMin * config.AmbushDistanceMin;
                    _ambushDistanceMaxSqr = config.AmbushDistanceMax * config.AmbushDistanceMax;
                }
                else
                {
                    _ambushDuration = 90f;
                    _silentApproachDistance = 35f;
                    _silenceTriggerDuration = 45f;
                    _movementTimeout = 90f;
                    _enableSilentApproach = true;
                    _enableFlashlightDiscipline = true;
                    _enableParanoia = true;
                    _paranoiaIntervalMin = 3f;
                    _paranoiaIntervalMax = 6f;
                    _paranoiaAngleRange = 45f;
                    _ambushDistanceMinSqr = 25f * 25f;
                    _ambushDistanceMaxSqr = 30f * 30f;
                }

                _nextParanoiaTime = 0f;
            }
            else
            {
                _hasTarget = false;
                LoggingController.LogWarning("[VultureAction] Bot " + BotOwner.GetText() + ": no target event found on Start()");
            }
        }

        public override void Stop()
        {
            base.Stop();

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                LoggingController.LogInfo(
                    "[VultureAction] Bot " + BotOwner.GetText() + ": stopped, phase " + entity.VulturePhase + " -> Complete"
                );
                entity.VulturePhase = VulturePhase.Complete;
            }
        }

        public override void Update(DrakiaXYZ.BigBrain.Brains.CustomLayer.ActionData data)
        {
            if (!_hasTarget)
                return;

            // Per-frame movement
            if (UseCustomMover)
            {
                byte phase = VulturePhase.None;
                if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
                    phase = entity.VulturePhase;

                bool canSprint = phase == VulturePhase.Approach || phase == VulturePhase.Rush;
                TickCustomMover(canSprint);
                BotOwner.SetPose(phase == VulturePhase.SilentApproach ? 0.6f : 1f);
                BotOwner.BotLay.GetUp(true);
                BotOwner.DoorOpener.ManualUpdate();
            }
            else
            {
                byte phase = VulturePhase.None;
                if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
                    phase = entity.VulturePhase;

                CanSprint = phase == VulturePhase.Approach || phase == VulturePhase.Rush;
                UpdateBotMovement(CanSprint);
            }

            // Throttle expensive updates
            if (!canUpdate())
            {
                UpdateBotSteering(_targetPosition);
                return;
            }

            // Movement timeout
            float elapsedSeconds = (float)_phaseTimer.ElapsedMilliseconds / 1000f;
            if (elapsedSeconds > _movementTimeout)
            {
                LoggingController.LogInfo(
                    "[VultureAction] Bot " + BotOwner.GetText() + ": movement timeout after " + elapsedSeconds.ToString("F1") + "s"
                );
                CompleteVulture();
                return;
            }

            if (!BotEntityBridge.TryGetEntity(BotOwner, out var ent))
                return;

            switch (ent.VulturePhase)
            {
                case VulturePhase.Approach:
                    UpdateApproach(ent);
                    break;
                case VulturePhase.SilentApproach:
                    UpdateSilentApproach(ent);
                    break;
                case VulturePhase.HoldAmbush:
                    UpdateHoldAmbush(ent);
                    break;
                case VulturePhase.Rush:
                    UpdateRush(ent);
                    break;
                case VulturePhase.Paranoia:
                    UpdateParanoia(ent);
                    break;
            }
        }

        private void UpdateApproach(BotEntity entity)
        {
            float distSqr = DistanceSqrToTarget();

            // Transition to silent approach when close enough
            if (_enableSilentApproach && distSqr < _silentApproachDistance * _silentApproachDistance)
            {
                float dist = (float)System.Math.Sqrt(distSqr);
                LoggingController.LogInfo(
                    "[VultureAction] Bot "
                        + BotOwner.GetText()
                        + ": phase Approach -> SilentApproach at distance "
                        + dist.ToString("F0")
                        + "m"
                );
                entity.VulturePhase = VulturePhase.SilentApproach;
                CanSprint = false;

                if (_enableFlashlightDiscipline)
                {
                    LoggingController.LogDebug("[VultureAction] Bot " + BotOwner.GetText() + ": flashlight off for silent approach");
                    BotOwner.BotLight.TurnOff(false, true);
                }

                return;
            }

            // Navigate to target
            RecalculatePath(_targetPosition);
            UpdateBotSteering();

            if (checkIfBotIsStuck())
            {
                LoggingController.LogInfo("[VultureAction] Bot " + BotOwner.GetText() + ": stuck during Approach, completing");
                CompleteVulture();
            }
        }

        private void UpdateSilentApproach(BotEntity entity)
        {
            CanSprint = false;
            float distSqr = DistanceSqrToTarget();

            // Transition to hold ambush when at ambush distance
            if (distSqr < _ambushDistanceMaxSqr)
            {
                float dist = (float)System.Math.Sqrt(distSqr);
                LoggingController.LogInfo(
                    "[VultureAction] Bot "
                        + BotOwner.GetText()
                        + ": phase SilentApproach -> HoldAmbush at distance "
                        + dist.ToString("F0")
                        + "m"
                );
                entity.VulturePhase = VulturePhase.HoldAmbush;
                _phaseTimer.Restart();
                return;
            }

            // Navigate at walking pace
            RecalculatePath(_targetPosition);
            UpdateBotSteering(_targetPosition);

            if (checkIfBotIsStuck())
            {
                LoggingController.LogInfo("[VultureAction] Bot " + BotOwner.GetText() + ": stuck during SilentApproach, completing");
                CompleteVulture();
            }
        }

        private void UpdateHoldAmbush(BotEntity entity)
        {
            float elapsed = (float)_phaseTimer.ElapsedMilliseconds / 1000f;

            // Paranoia: look around periodically
            if (_enableParanoia && Time.time > _nextParanoiaTime)
            {
                LoggingController.LogDebug(
                    "[VultureAction] Bot "
                        + BotOwner.GetText()
                        + ": paranoia lookback triggered at "
                        + elapsed.ToString("F1")
                        + "s into ambush"
                );
                entity.VulturePhase = VulturePhase.Paranoia;
                _nextParanoiaTime =
                    Time.time
                    + _paranoiaIntervalMin
                    + (float)(new System.Random().NextDouble() * (_paranoiaIntervalMax - _paranoiaIntervalMin));
                return;
            }

            // Look toward target
            UpdateBotSteering(_targetPosition);

            // Ambush duration expired → rush in
            if (elapsed > _ambushDuration)
            {
                LoggingController.LogInfo(
                    "[VultureAction] Bot "
                        + BotOwner.GetText()
                        + ": phase HoldAmbush -> Rush (ambush duration "
                        + _ambushDuration.ToString("F0")
                        + "s expired)"
                );
                entity.VulturePhase = VulturePhase.Rush;
                _phaseTimer.Restart();
            }

            // If silence detected (no recent events), rush in early
            if (elapsed > _silenceTriggerDuration && entity.CombatIntensity == 0)
            {
                LoggingController.LogInfo(
                    "[VultureAction] Bot "
                        + BotOwner.GetText()
                        + ": phase HoldAmbush -> Rush (silence detected after "
                        + elapsed.ToString("F1")
                        + "s)"
                );
                entity.VulturePhase = VulturePhase.Rush;
                _phaseTimer.Restart();
            }
        }

        private void UpdateParanoia(BotEntity entity)
        {
            // Random look direction — scan around for threats
            float angle = (float)(new System.Random().NextDouble() * 2 - 1) * _paranoiaAngleRange;
            Vector3 lookDir = Quaternion.Euler(0, angle, 0) * (BotOwner.LookDirection);
            Vector3 lookPoint = BotOwner.Position + lookDir * 20f;
            BotOwner.Steering.LookToPoint(lookPoint);

            // Return to ambush after one tick
            entity.VulturePhase = VulturePhase.HoldAmbush;
        }

        private void UpdateRush(BotEntity entity)
        {
            CanSprint = true;
            float distSqr = DistanceSqrToTarget();

            // Arrived at target
            if (distSqr < _ambushDistanceMinSqr)
            {
                float dist = (float)System.Math.Sqrt(distSqr);
                LoggingController.LogInfo(
                    "[VultureAction] Bot "
                        + BotOwner.GetText()
                        + ": arrived at target during Rush at "
                        + dist.ToString("F0")
                        + "m, completing"
                );
                CompleteVulture();
                return;
            }

            RecalculatePath(_targetPosition);
            UpdateBotSteering(_targetPosition);

            if (checkIfBotIsStuck())
            {
                LoggingController.LogInfo("[VultureAction] Bot " + BotOwner.GetText() + ": stuck during Rush, completing");
                CompleteVulture();
            }
        }

        private float DistanceSqrToTarget()
        {
            Vector3 pos = BotOwner.Position;
            float dx = pos.x - _targetPosition.x;
            float dz = pos.z - _targetPosition.z;
            return dx * dx + dz * dz;
        }

        private void CompleteVulture()
        {
            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                LoggingController.LogInfo(
                    "[VultureAction] Bot " + BotOwner.GetText() + ": vulture completed from phase " + entity.VulturePhase
                );
                entity.VulturePhase = VulturePhase.Complete;
                entity.HasNearbyEvent = false;
            }
        }
    }
}
