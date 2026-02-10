using EFT;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.Objective
{
    /// <summary>
    /// BigBrain action for linger behavior — bots pause briefly after completing
    /// an objective, adopting a slight crouch and periodically scanning with
    /// random head rotations before moving on.
    /// </summary>
    internal class LingerAction : BehaviorExtensions.CustomLogicDelayedUpdate
    {
        private float _pose;
        private float _headScanIntervalMin;
        private float _headScanIntervalMax;
        private float _nextScanTime;

        public LingerAction(BotOwner _BotOwner)
            : base(_BotOwner, 100) { }

        public override void Start()
        {
            base.Start();

            BotOwner.PatrollingData.Pause();
            BotOwner.Mover.Stop();

            var config = ConfigController.Config?.Questing?.Linger;
            if (config != null)
            {
                _pose = config.Pose;
                _headScanIntervalMin = config.HeadScanIntervalMin;
                _headScanIntervalMax = config.HeadScanIntervalMax;
            }
            else
            {
                _pose = 0.7f;
                _headScanIntervalMin = 3f;
                _headScanIntervalMax = 8f;
            }

            BotOwner.SetPose(_pose);
            _nextScanTime = 0f;

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                entity.IsLingering = true;
                LoggingController.LogInfo(
                    "[LingerAction] Bot "
                        + BotOwner.GetText()
                        + ": started lingering (duration="
                        + entity.LingerDuration.ToString("F1")
                        + "s)"
                );
            }
        }

        public override void Stop()
        {
            base.Stop();

            BotOwner.PatrollingData.Unpause();
            BotOwner.SetPose(1f);

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                entity.IsLingering = false;
                entity.ObjectiveCompletedTime = 0f;
                entity.LingerDuration = 0f;
                LoggingController.LogInfo("[LingerAction] Bot " + BotOwner.GetText() + ": stopped lingering");
            }
        }

        public override void Update(DrakiaXYZ.BigBrain.Brains.CustomLayer.ActionData data)
        {
            // Throttle expensive updates
            if (!canUpdate())
            {
                return;
            }

            // Random head scan
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

            // Maintain pose
            BotOwner.SetPose(_pose);
        }
    }
}
