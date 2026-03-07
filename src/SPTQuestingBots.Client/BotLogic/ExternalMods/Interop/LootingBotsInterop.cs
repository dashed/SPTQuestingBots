using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Bootstrap;
using EFT;

namespace LootingBots
{
    internal static class LootingBotsInterop
    {
        internal const string ExternalTypeName = "LootingBots.External, skwizzy.LootingBots";

        internal static readonly string[] RequiredMethodNames = ["ForceBotToScanLoot", "PreventBotFromLooting"];

        private static bool _LootingBotsLoadedChecked = false;
        private static bool _LootingBotsInteropInited = false;
        private static bool _LootingBotsInteropAvailable = false;

        private static bool _IsLootingBotsLoaded;
        private static Type _LootingBotsExternalType;
        private static string _lastInitError = string.Empty;
        private static MethodInfo _ForceBotToScanLootMethod;
        private static MethodInfo _PreventBotFromLootingMethod;

        internal static string LastInitError => _lastInitError;

        /**
         * Return true if Looting Bots is loaded in the client
         */
        public static bool IsLootingBotsLoaded()
        {
            // Only check for LootingBots once
            if (!_LootingBotsLoadedChecked)
            {
                _LootingBotsLoadedChecked = true;
                _IsLootingBotsLoaded = Chainloader.PluginInfos.ContainsKey("me.skwizzy.lootingbots");
            }

            return _IsLootingBotsLoaded;
        }

        /**
         * Initialize the Looting Bots interop class data, return true on success
         */
        public static bool Init()
        {
            if (!IsLootingBotsLoaded())
            {
                _lastInitError = "LootingBots is not loaded.";
                return false;
            }

            if (!_LootingBotsInteropInited)
            {
                _LootingBotsInteropInited = true;
                _LootingBotsInteropAvailable = tryInitializeInterop();
            }

            return _LootingBotsInteropAvailable;
        }

        private static bool tryInitializeInterop()
        {
            _LootingBotsExternalType = Type.GetType(ExternalTypeName);
            if (_LootingBotsExternalType == null)
            {
                _lastInitError = "Could not resolve type " + ExternalTypeName + ".";
                return false;
            }

            var missingMethods = new List<string>();
            _ForceBotToScanLootMethod = getRequiredMethod(
                _LootingBotsExternalType,
                "ForceBotToScanLoot",
                typeof(bool),
                missingMethods,
                typeof(BotOwner)
            );
            _PreventBotFromLootingMethod = getRequiredMethod(
                _LootingBotsExternalType,
                "PreventBotFromLooting",
                typeof(bool),
                missingMethods,
                typeof(BotOwner),
                typeof(float)
            );

            if (missingMethods.Count > 0)
            {
                _lastInitError = "Missing required LootingBots interop methods: " + string.Join(", ", missingMethods);
                return false;
            }

            _lastInitError = string.Empty;
            return true;
        }

        private static MethodInfo getRequiredMethod(
            Type externalType,
            string methodName,
            Type returnType,
            List<string> missingMethods,
            params Type[] parameterTypes
        )
        {
            MethodInfo method = externalType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            if (method == null || method.ReturnType != returnType)
            {
                missingMethods.Add(methodName + "(" + string.Join(", ", parameterTypes.Select(t => t.Name)) + ") -> " + returnType.Name);
                return null;
            }

            return method;
        }

        /**
         * Force a bot to search for loot immediately if Looting Bots is loaded. Return true if successful.
         */
        public static bool TryForceBotToScanLoot(BotOwner botOwner)
        {
            if (!Init())
                return false;
            if (_ForceBotToScanLootMethod == null)
                return false;

            return (bool)_ForceBotToScanLootMethod.Invoke(null, new object[] { botOwner });
        }

        /**
         * Prevent a bot from searching for loot (until the scan timer expires) if Looting Bots is loaded. Return true if successful.
         */
        public static bool TryPreventBotFromLooting(BotOwner botOwner, float duration)
        {
            if (!Init())
                return false;
            if (_PreventBotFromLootingMethod == null)
                return false;

            return (bool)_PreventBotFromLootingMethod.Invoke(null, new object[] { botOwner, duration });
        }
    }
}
