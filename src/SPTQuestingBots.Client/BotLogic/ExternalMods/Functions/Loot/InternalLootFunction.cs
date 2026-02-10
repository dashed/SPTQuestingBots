using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EFT;

namespace SPTQuestingBots.BotLogic.ExternalMods.Functions.Loot
{
    public class InternalLootFunction : AbstractLootFunction
    {
        public override string MonitoredLayerName => "Looting";

        private bool _hasTarget;
        private bool _isLooting;
        private bool _forceScan;
        private float _preventedUntilTime;

        public InternalLootFunction(BotOwner _botOwner)
            : base(_botOwner) { }

        /// <summary>
        /// The game time until which this bot is prevented from looting.
        /// </summary>
        public float PreventedUntilTime => _preventedUntilTime;

        /// <summary>
        /// Sets whether this bot currently has a loot target.
        /// Called by the HiveMind integration layer.
        /// </summary>
        public void SetHasTarget(bool value) => _hasTarget = value;

        /// <summary>
        /// Sets whether this bot is currently in the act of looting.
        /// Called by the loot action when it begins/ends interaction.
        /// </summary>
        public void SetIsLooting(bool value) => _isLooting = value;

        /// <summary>
        /// Reads and clears the force-scan flag.
        /// Returns <c>true</c> exactly once after <see cref="TryForceBotToScanLoot"/>
        /// was called, then resets.
        /// </summary>
        public bool ConsumeForceScan()
        {
            if (!_forceScan)
                return false;
            _forceScan = false;
            return true;
        }

        public override bool IsSearchingForLoot()
        {
            return _hasTarget;
        }

        public override bool IsLooting()
        {
            return _isLooting;
        }

        public override bool TryPreventBotFromLooting(float duration)
        {
            _preventedUntilTime = UnityEngine.Time.time + duration;
            return true;
        }

        public override bool TryForceBotToScanLoot()
        {
            _forceScan = true;
            return true;
        }
    }
}
