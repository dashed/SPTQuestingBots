using System.Linq;
using EFT;
using EFT.Interactive;
using SPTQuestingBots.Controllers;
using UnityEngine;

namespace SPTQuestingBots.Helpers
{
    /// <summary>
    /// Caches door colliders at map start and disables physics collision
    /// between bots and doors. Ported from Phobos DoorSystem + PhobosLayer.
    ///
    /// Two features:
    /// 1. Per-bot collision bypass: Physics.IgnoreCollision + EFTPhysicsClass.IgnoreCollision
    ///    between each bot's colliders and every door collider on the map.
    /// 2. Door caching: FindObjectsOfType&lt;WorldInteractiveObject&gt; filtered to doors with colliders,
    ///    cached once per raid for O(1) per-bot setup.
    /// </summary>
    public static class DoorCollisionHelper
    {
        private static Collider[] _doorColliders;

        /// <summary>Whether doors have been cached for the current raid.</summary>
        public static bool IsInitialized => _doorColliders != null;

        /// <summary>Number of cached door colliders.</summary>
        public static int DoorCount => _doorColliders?.Length ?? 0;

        /// <summary>
        /// Cache all door colliders on the map. Call once at raid start
        /// (e.g. from GameWorld.OnGameStarted postfix).
        /// </summary>
        public static void Initialize()
        {
            var interactables = Object.FindObjectsOfType<WorldInteractiveObject>();
            _doorColliders = interactables.Where(obj => obj.Collider != null).OfType<Door>().Select(door => door.Collider).ToArray();

            LoggingController.LogInfo($"DoorCollisionHelper: cached {_doorColliders.Length} door colliders");
        }

        /// <summary>
        /// Disable collision between a bot's physics colliders and all cached door colliders.
        /// Call once per bot, typically in the BigBrain layer constructor.
        ///
        /// Matches Phobos PhobosLayer constructor pattern:
        /// - Player.CharacterController.GetCollider() → EFTPhysicsClass.IgnoreCollision
        /// - Player.POM.Collider → Physics.IgnoreCollision
        /// </summary>
        public static void ApplyDoorBypass(Player player)
        {
            if (_doorColliders == null || player == null)
                return;

            var botCollider = player.CharacterController?.GetCollider();
            var pomCollider = player.POM?.Collider;

            if (botCollider == null && pomCollider == null)
            {
                LoggingController.LogWarning($"DoorCollisionHelper: no colliders found for {player.ProfileId}");
                return;
            }

            for (int i = 0; i < _doorColliders.Length; i++)
            {
                var doorCollider = _doorColliders[i];
                if (doorCollider == null)
                    continue;

                if (pomCollider != null)
                    Physics.IgnoreCollision(pomCollider, doorCollider);

                if (botCollider != null)
                    EFTPhysicsClass.IgnoreCollision(botCollider, doorCollider);
            }
        }

        /// <summary>
        /// Clear cached door colliders. Call at raid end.
        /// </summary>
        public static void Clear()
        {
            _doorColliders = null;
        }
    }
}
