using EFT;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.BotLogic.Objective
{
    /// <summary>
    /// BigBrain action for spawn entry behavior — bots pause briefly after spawning,
    /// doing a smooth 360-degree look rotation to scan surroundings before moving on.
    /// </summary>
    internal class SpawnEntryAction : BehaviorExtensions.CustomLogicDelayedUpdate
    {
        private float _pose;
        private float _spawnFacingX;
        private float _spawnFacingZ;
        private float _duration;
        private float _startTime;

        public SpawnEntryAction(BotOwner _BotOwner)
            : base(_BotOwner, 100) { }

        public override void Start()
        {
            base.Start();

            BotOwner.PatrollingData.Pause();
            BotOwner.Mover.Stop();

            var config = ConfigController.Config?.Questing?.SpawnEntry;
            _pose = config?.Pose ?? 0.85f;

            BotOwner.SetPose(_pose);

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                _spawnFacingX = entity.SpawnFacingX;
                _spawnFacingZ = entity.SpawnFacingZ;
                _duration = entity.SpawnEntryDuration;
                _startTime = entity.SpawnTime;

                LoggingController.LogInfo(
                    "[SpawnEntryAction] Bot "
                        + BotOwner.GetText()
                        + ": started spawn entry scan (duration="
                        + _duration.ToString("F1")
                        + "s)"
                );
            }
            else
            {
                _duration = 0f;
                _startTime = Time.time;
            }
        }

        public override void Stop()
        {
            base.Stop();

            BotOwner.PatrollingData.Unpause();
            BotOwner.SetPose(1f);

            if (BotEntityBridge.TryGetEntity(BotOwner, out var entity))
            {
                entity.IsSpawnEntryComplete = true;
                LoggingController.LogInfo("[SpawnEntryAction] Bot " + BotOwner.GetText() + ": stopped spawn entry scan");
            }
        }

        public override void Update(DrakiaXYZ.BigBrain.Brains.CustomLayer.ActionData data)
        {
            // Throttle expensive updates
            if (!canUpdate())
            {
                return;
            }

            // Maintain pose
            BotOwner.SetPose(_pose);

            // Compute smooth 360-degree rotation over duration
            float elapsed = Time.time - _startTime;
            if (_duration <= 0f || elapsed < 0f)
                return;

            // angle = (elapsed / duration) * 2 * PI
            float angle = (elapsed / _duration) * 2f * Mathf.PI;

            // Rotate spawn facing direction by angle
            float cosA = Mathf.Cos(angle);
            float sinA = Mathf.Sin(angle);
            float lookX = _spawnFacingX * cosA - _spawnFacingZ * sinA;
            float lookZ = _spawnFacingX * sinA + _spawnFacingZ * cosA;

            Vector3 lookDir = new Vector3(lookX, 0f, lookZ);
            Vector3 lookPoint = BotOwner.Position + lookDir * 20f;
            UpdateBotSteering(lookPoint);
        }
    }
}
