using System.Diagnostics;
using EFT;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.Models.Pathing;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.Objective
{
    /// <summary>
    /// BigBrain action for patrol behavior — bots follow a named route of waypoints,
    /// pausing briefly at each waypoint to scan before moving to the next.
    /// Two states: NavigateToWaypoint → PauseAtWaypoint → (next waypoint or complete).
    /// </summary>
    internal class PatrolAction : BehaviorExtensions.GoToPositionAbstractAction
    {
        private PatrolRoute _route;
        private int _waypointIndex;
        private bool _isPausing;
        private bool _hasRoute;
        private float _pose;
        private float _arrivalDistanceSqr;
        private float _movementTimeout;
        private float _headScanIntervalMin;
        private float _headScanIntervalMax;
        private float _nextScanTime;
        private float _pauseUntil;
        private Stopwatch _timer = Stopwatch.StartNew();

        public PatrolAction(BotOwner _BotOwner)
            : base(_BotOwner, 100) { }

        public override void Start()
        {
            base.Start();

            _timer.Restart();
            _isPausing = false;
            _hasRoute = false;

            var config = ConfigController.Config?.Questing?.Patrol;
            _pose = config?.Pose ?? 0.85f;
            float arrivalRadius = config?.WaypointArrivalRadius ?? 3f;
            _arrivalDistanceSqr = arrivalRadius * arrivalRadius;
            _movementTimeout = 90f;
            _headScanIntervalMin = 3f;
            _headScanIntervalMax = 8f;
            _nextScanTime = 0f;

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                var routes = PatrolTask.CurrentMapRoutes;
                if (entity.PatrolRouteIndex >= 0 && routes != null && entity.PatrolRouteIndex < routes.Length)
                {
                    _route = routes[entity.PatrolRouteIndex];
                    _waypointIndex = entity.PatrolWaypointIndex;

                    if (_waypointIndex < 0 || _waypointIndex >= _route.Waypoints.Length)
                        _waypointIndex = 0;

                    _hasRoute = true;
                    entity.IsPatrolling = true;

                    LoggingController.LogInfo(
                        "[PatrolAction] Bot "
                            + BotOwner.GetText()
                            + ": started patrol route '"
                            + _route.Name
                            + "' at waypoint "
                            + _waypointIndex
                    );
                }
                else
                {
                    LoggingController.LogWarning("[PatrolAction] Bot " + BotOwner.GetText() + ": no valid patrol route on Start()");
                }
            }
        }

        public override void Stop()
        {
            base.Stop();

            BotOwner.SetPose(1f);

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                entity.IsPatrolling = false;
                entity.PatrolWaypointIndex = _waypointIndex;
                LoggingController.LogInfo("[PatrolAction] Bot " + BotOwner.GetText() + ": stopped patrol at waypoint " + _waypointIndex);
            }
        }

        public override void Update(DrakiaXYZ.BigBrain.Brains.CustomLayer.ActionData data)
        {
            if (!_hasRoute || _route == null || _route.Waypoints == null || _route.Waypoints.Length == 0)
                return;

            if (_isPausing)
            {
                UpdatePause();
                return;
            }

            // Per-frame movement during navigation
            if (UseCustomMover)
            {
                TickCustomMover(false);
                BotOwner.SetPose(_pose);
                BotOwner.BotLay.GetUp(true);
                BotOwner.DoorOpener.ManualUpdate();
            }
            else
            {
                CanSprint = false;
                UpdateBotMovement(false);
                BotOwner.SetPose(_pose);
            }

            // Throttle expensive updates
            if (!canUpdate())
            {
                var wp = _route.Waypoints[_waypointIndex];
                Vector3 wpPos = new Vector3(wp.X, wp.Y, wp.Z);
                UpdateBotSteering(wpPos);
                return;
            }

            // Movement timeout
            float elapsedSeconds = (float)_timer.ElapsedMilliseconds / 1000f;
            if (elapsedSeconds > _movementTimeout)
            {
                LoggingController.LogInfo(
                    "[PatrolAction] Bot " + BotOwner.GetText() + ": movement timeout after " + elapsedSeconds.ToString("F1") + "s"
                );
                CompletePatrol();
                return;
            }

            // Check arrival at current waypoint
            var currentWp = _route.Waypoints[_waypointIndex];
            float distSqr = DistanceSqrToWaypoint(currentWp);
            if (distSqr < _arrivalDistanceSqr)
            {
                LoggingController.LogDebug(
                    "[PatrolAction] Bot " + BotOwner.GetText() + ": arrived at waypoint " + _waypointIndex + ", pausing"
                );
                StartPause(currentWp);
                return;
            }

            // Navigate to current waypoint
            Vector3 targetPos = new Vector3(currentWp.X, currentWp.Y, currentWp.Z);
            RecalculatePath(targetPos);
            UpdateBotSteering(targetPos);
            ApplyLookVariance();

            if (checkIfBotIsStuck())
            {
                LoggingController.LogInfo("[PatrolAction] Bot " + BotOwner.GetText() + ": stuck during patrol, completing");
                CompletePatrol();
            }
        }

        private void StartPause(PatrolWaypoint wp)
        {
            _isPausing = true;
            BotOwner.Mover.Stop();

            float pauseMin = wp.PauseDurationMin > 0f ? wp.PauseDurationMin : 2f;
            float pauseMax = wp.PauseDurationMax > pauseMin ? wp.PauseDurationMax : pauseMin + 3f;
            float pauseDuration = pauseMin + (float)(new System.Random().NextDouble() * (pauseMax - pauseMin));

            _pauseUntil = Time.time + pauseDuration;
            _nextScanTime = 0f;
            _timer.Restart();
        }

        private void UpdatePause()
        {
            // Maintain patrol pose
            BotOwner.SetPose(_pose);

            // Pause expired — advance to next waypoint
            if (Time.time >= _pauseUntil)
            {
                AdvanceWaypoint();
                return;
            }

            // Random head scan during pause
            if (Time.time >= _nextScanTime)
            {
                float angle = (float)(new System.Random().NextDouble() * 2 - 1) * 90f;
                Vector3 lookDir = Quaternion.Euler(0, angle, 0) * BotOwner.LookDirection;
                Vector3 lookPoint = BotOwner.Position + lookDir * 15f;
                UpdateBotSteering(lookPoint);

                _nextScanTime =
                    Time.time
                    + _headScanIntervalMin
                    + (float)(new System.Random().NextDouble() * (_headScanIntervalMax - _headScanIntervalMin));
            }
        }

        private void AdvanceWaypoint()
        {
            _waypointIndex++;

            // Check if route is complete
            if (_waypointIndex >= _route.Waypoints.Length)
            {
                if (_route.IsLoop)
                {
                    _waypointIndex = 0;
                    LoggingController.LogDebug("[PatrolAction] Bot " + BotOwner.GetText() + ": looping back to waypoint 0");
                }
                else
                {
                    LoggingController.LogInfo(
                        "[PatrolAction] Bot " + BotOwner.GetText() + ": completed non-loop route '" + _route.Name + "'"
                    );
                    CompletePatrol();
                    return;
                }
            }

            _isPausing = false;
            _timer.Restart();

            // Update entity state
            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                entity.PatrolWaypointIndex = _waypointIndex;
            }
        }

        private void CompletePatrol()
        {
            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                var config = ConfigController.Config?.Questing?.Patrol;
                float cooldownSec = config?.CooldownSec ?? 120f;

                entity.IsPatrolling = false;
                entity.PatrolRouteIndex = -1;
                entity.PatrolWaypointIndex = 0;
                entity.PatrolCooldownUntil = entity.CurrentGameTime + cooldownSec;

                LoggingController.LogInfo(
                    "[PatrolAction] Bot " + BotOwner.GetText() + ": patrol complete, cooldown " + cooldownSec.ToString("F0") + "s"
                );
            }
        }

        private float DistanceSqrToWaypoint(PatrolWaypoint wp)
        {
            Vector3 pos = BotOwner.Position;
            float dx = pos.x - wp.X;
            float dz = pos.z - wp.Z;
            return dx * dx + dz * dz;
        }
    }
}
