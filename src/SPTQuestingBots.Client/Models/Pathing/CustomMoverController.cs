using System.Runtime.CompilerServices;
using EFT;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.Helpers;
using UnityEngine;
using UnityEngine.AI;

namespace SPTQuestingBots.Models.Pathing
{
    /// <summary>
    /// Thin Unity integration layer that bridges the pure-logic <see cref="CustomPathFollower"/>
    /// to BSG's Player.Move() API. Handles world-to-local direction rotation, path smoothing,
    /// sprint gating, and ECS movement state synchronization.
    ///
    /// Pattern matches Phobos's MovementSystem: compute world-space direction, rotate into
    /// player-local space, call Player.Move(localDir) + CharacterController.SetSteerDirection(worldDir).
    ///
    /// This class has Unity dependencies and cannot be unit tested in isolation.
    /// All logic is delegated to <see cref="CustomPathFollower"/> which is fully testable.
    /// </summary>
    public class CustomMoverController
    {
        private readonly BotOwner _bot;
        private readonly CustomPathFollower _follower;
        private readonly CustomMoverConfig _config;
        private BotEntity _entity;

        /// <summary>Whether the custom mover is currently active for this bot.</summary>
        public bool IsActive => _entity != null && _entity.Movement.IsCustomMoverActive;

        /// <summary>The underlying pure-logic path follower.</summary>
        public CustomPathFollower Follower => _follower;

        /// <summary>Configuration in use.</summary>
        public CustomMoverConfig Config => _config;

        public CustomMoverController(BotOwner bot, CustomMoverConfig config)
        {
            _bot = bot;
            _config = config;
            _follower = new CustomPathFollower(config);
        }

        /// <summary>
        /// Activate the custom mover, taking over movement from BSG's BotMover.
        /// Stops BSG's mover and sets the ECS flag for the ManualFixedUpdate patch.
        /// </summary>
        public void Activate()
        {
            if (_entity == null)
            {
                string profileId = _bot.Profile?.Id;
                if (profileId != null)
                    _entity = BotEntityBridge.GetEntityByProfileId(profileId);
            }

            CustomMoverHandoff.Activate(_bot);
        }

        /// <summary>
        /// Deactivate the custom mover, returning control to BSG's BotMover.
        /// Syncs BSG state fields and resets ECS movement state.
        /// </summary>
        public void Deactivate()
        {
            _follower.ResetPath();
            CustomMoverHandoff.Deactivate(_bot);
        }

        /// <summary>
        /// Load a new path from NavMesh corners. Applies Chaikin smoothing
        /// before feeding to the path follower.
        /// </summary>
        /// <param name="corners">Raw NavMesh path corners.</param>
        /// <param name="target">The ultimate destination position.</param>
        public void SetPath(Vector3[] corners, Vector3 target)
        {
            if (corners == null || corners.Length == 0)
            {
                _follower.SetPath(null, target);
                SyncMovementState();
                return;
            }

            // Apply Chaikin smoothing to reduce corner jitter
            Vector3[] smoothed = PathSmoother.Smooth(corners, _config);
            _follower.SetPath(smoothed, target);
            SyncMovementState();
        }

        /// <summary>
        /// Process one movement frame. Updates path following, computes direction,
        /// and calls Player.Move() to execute movement.
        ///
        /// Call this every frame from the action's Update() method.
        /// </summary>
        /// <param name="isSprinting">Whether the bot is currently sprinting.</param>
        /// <returns>The current path follower status.</returns>
        public PathFollowerStatus Tick(bool isSprinting)
        {
            if (!IsActive)
                return PathFollowerStatus.Idle;

            Vector3 position = _bot.Position;

            // Advance path following logic
            PathFollowerStatus status = _follower.Tick(position, isSprinting);

            if (status == PathFollowerStatus.Following)
            {
                TryNavMeshCornerCut(position);
                ExecuteMovement(position);
            }

            SyncMovementState();
            return status;
        }

        /// <summary>
        /// Check if the bot can sprint given the current path smoothness.
        /// Delegates to <see cref="CustomPathFollower.CanSprint"/>.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool CanSprint(SprintUrgency urgency)
        {
            return _follower.CanSprint(urgency);
        }

        /// <summary>
        /// Attempt corner-cutting via NavMesh.Raycast. When the bot is close to
        /// the current corner (within 1m) and has clear NavMesh line-of-sight to
        /// the next corner, skip the current corner for smoother trajectories.
        /// Ported from Phobos MovementSystem.cs:244.
        /// </summary>
        private void TryNavMeshCornerCut(Vector3 position)
        {
            if (_follower.IsOnLastCorner)
                return;

            if (!_follower.IsCloseEnoughForCornerCut(position))
                return;

            int nextIndex = _follower.CurrentCorner + 1;
            Vector3 nextCorner = _follower.Corners[nextIndex];

            bool blocked = NavMesh.Raycast(position, nextCorner, out _, NavMesh.AllAreas);
            if (!blocked)
            {
                _follower.TryCornerCut(position, true);
            }
        }

        /// <summary>
        /// Execute the Player.Move() call for this frame.
        /// Computes world-space direction, rotates to player-local space,
        /// and calls the BSG player movement API.
        /// </summary>
        private void ExecuteMovement(Vector3 position)
        {
            Player player = _bot.GetPlayer;
            if (player == null)
                return;

            // Compute world-space move direction (blended with path-deviation spring)
            Vector3 moveVector = _follower.ComputeMoveDirection(position);
            if (moveVector.sqrMagnitude < 0.001f)
                return;

            moveVector.Normalize();

            // Rotate world-space direction into player-local space for Player.Move()
            // Pattern matches Phobos: Quaternion.Euler(0, 0, rotation.x) * Vector2(dir.x, dir.z)
            Vector2 moveDir = CalcMoveDirection(moveVector, player.Rotation);

            // SetSteerDirection uses world-space direction (for character controller facing)
            player.CharacterController.SetSteerDirection(moveVector);

            // Player.Move uses player-local direction
            player.Move(moveDir);

            // Update aiming to match movement speed (prevents aim desync during movement)
            _bot.AimingManager?.CurrentAiming?.Move(player.Speed);
        }

        /// <summary>
        /// Transform a world-space direction vector into player-local movement input.
        /// Matches Phobos's CalcMoveDirection exactly:
        /// projects direction onto XZ plane, then rotates by player's yaw angle.
        /// </summary>
        /// <param name="direction">World-space movement direction (normalized).</param>
        /// <param name="rotation">Player rotation: x = yaw, y = pitch.</param>
        /// <returns>Player-local movement input for Player.Move().</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector2 CalcMoveDirection(Vector3 direction, Vector2 rotation)
        {
            // Project 3D direction onto XZ horizontal plane as Vector2
            Vector2 dir2d = new Vector2(direction.x, direction.z);

            // Rotate by player's yaw to get local-space input
            Vector3 rotated = Quaternion.Euler(0f, 0f, rotation.x) * dir2d;
            return new Vector2(rotated.x, rotated.y);
        }

        /// <summary>
        /// Sync the path follower's state into the ECS BotEntity.Movement struct
        /// for dense iteration queries and debug display.
        /// </summary>
        private void SyncMovementState()
        {
            if (_entity == null)
                return;

            _entity.Movement.Status = MapStatus(_follower.Status);
            _entity.Movement.CurrentCornerIndex = _follower.CurrentCorner;
            _entity.Movement.TotalCorners = _follower.TotalCorners;
            _entity.Movement.RetryCount = _follower.RetryCount;
            _entity.Movement.SprintAngleJitter = _follower.ComputeSprintAngleJitter();
            _entity.Movement.LastPathUpdateTime = Time.time;
        }

        /// <summary>
        /// Map CustomPathFollower status to ECS PathFollowStatus.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static BotLogic.ECS.PathFollowStatus MapStatus(PathFollowerStatus status)
        {
            switch (status)
            {
                case PathFollowerStatus.Following:
                    return BotLogic.ECS.PathFollowStatus.Following;
                case PathFollowerStatus.Reached:
                    return BotLogic.ECS.PathFollowStatus.Reached;
                case PathFollowerStatus.Failed:
                    return BotLogic.ECS.PathFollowStatus.Failed;
                default:
                    return BotLogic.ECS.PathFollowStatus.None;
            }
        }
    }
}
