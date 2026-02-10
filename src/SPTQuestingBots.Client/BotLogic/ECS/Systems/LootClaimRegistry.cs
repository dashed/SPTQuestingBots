using System.Collections.Generic;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Multi-bot loot deconfliction. One instance per raid.
    /// Tracks which bot has claimed which loot item to prevent contention.
    /// Pure C# — no Unity or EFT dependencies.
    /// </summary>
    public class LootClaimRegistry
    {
        private readonly Dictionary<int, int> _lootToBotId = new Dictionary<int, int>();
        private readonly Dictionary<int, List<int>> _botToLootIds = new Dictionary<int, List<int>>();

        /// <summary>
        /// Try to claim a loot item for a bot.
        /// Returns false if already claimed by a different bot.
        /// Returns true if unclaimed or already claimed by the same bot.
        /// </summary>
        public bool TryClaim(int botId, int lootId)
        {
            if (_lootToBotId.TryGetValue(lootId, out int existingBotId))
            {
                return existingBotId == botId;
            }

            _lootToBotId[lootId] = botId;

            if (!_botToLootIds.TryGetValue(botId, out var lootIds))
            {
                lootIds = new List<int>();
                _botToLootIds[botId] = lootIds;
            }
            lootIds.Add(lootId);
            return true;
        }

        /// <summary>
        /// Release a specific claim.
        /// </summary>
        public void Release(int botId, int lootId)
        {
            if (_lootToBotId.TryGetValue(lootId, out int existingBotId) && existingBotId == botId)
            {
                _lootToBotId.Remove(lootId);
            }

            if (_botToLootIds.TryGetValue(botId, out var lootIds))
            {
                lootIds.Remove(lootId);
            }
        }

        /// <summary>
        /// Release all claims for a bot (e.g., on bot death or despawn).
        /// </summary>
        public void ReleaseAll(int botId)
        {
            if (_botToLootIds.TryGetValue(botId, out var lootIds))
            {
                for (int i = 0; i < lootIds.Count; i++)
                {
                    _lootToBotId.Remove(lootIds[i]);
                }
                lootIds.Clear();
                _botToLootIds.Remove(botId);
            }
        }

        /// <summary>
        /// Check if loot is claimed by a bot other than the given one.
        /// </summary>
        public bool IsClaimedByOther(int botId, int lootId)
        {
            if (_lootToBotId.TryGetValue(lootId, out int existingBotId))
            {
                return existingBotId != botId;
            }
            return false;
        }

        /// <summary>
        /// Get total number of active claims.
        /// </summary>
        public int GetClaimCount()
        {
            return _lootToBotId.Count;
        }

        /// <summary>
        /// Reset all claims (e.g., at raid end).
        /// </summary>
        public void Clear()
        {
            _lootToBotId.Clear();
            _botToLootIds.Clear();
        }
    }
}
