using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BepInEx.Bootstrap;
using EFT;
using UnityEngine;
using UnityEngine.AI;

namespace SAIN.Plugin
{
    internal static class SAINInterop
    {
        internal const string ExternalTypeName = "SAIN.Interop.SAINExternal, SAIN";

        internal static readonly string[] RuntimeRequiredMethodNames = ["ExtractBot", "TrySetExfilForBot", "IgnoreHearing"];

        internal static readonly string[] OptionalMethodNames =
        [
            "IsPathTowardEnemy",
            "TimeSinceSenseEnemy",
            "CanBotQuest",
            "GetExtractedBots",
            "GetPersonality",
        ];

        private static bool _SAINLoadedChecked = false;
        private static bool _SAINInteropInited = false;
        private static bool _SAINInteropAvailable = false;

        private static bool _IsSAINLoaded;
        private static Type _SAINExternalType;
        private static string _lastInitError = string.Empty;

        private static MethodInfo _ExtractBotMethod;
        private static MethodInfo _SetExfilForBotMethod;
        private static MethodInfo _IsPathTowardEnemyMethod;
        private static MethodInfo _TimeSinceSenseEnemyMethod;
        private static MethodInfo _CanBotQuestMethod;
        private static MethodInfo _GetExtractedBotsMethod;
        private static MethodInfo _IgnoreHearingMethod;
        private static MethodInfo _GetPersonalityMethod;

        internal static string LastInitError => _lastInitError;

        /**
         * Return true if SAIN is loaded in the client
         */
        public static bool IsSAINLoaded()
        {
            // Only check for SAIN once
            if (!_SAINLoadedChecked)
            {
                _SAINLoadedChecked = true;
                _IsSAINLoaded = Chainloader.PluginInfos.ContainsKey("me.sol.sain");
            }

            return _IsSAINLoaded;
        }

        /**
         * Initialize the SAIN interop class data, return true on success
         */
        public static bool Init()
        {
            if (!IsSAINLoaded())
            {
                _lastInitError = "SAIN is not loaded.";
                return false;
            }

            if (!_SAINInteropInited)
            {
                _SAINInteropInited = true;
                _SAINInteropAvailable = tryInitializeInterop();
            }

            return _SAINInteropAvailable;
        }

        private static bool tryInitializeInterop()
        {
            _SAINExternalType = Type.GetType(ExternalTypeName);
            if (_SAINExternalType == null)
            {
                _lastInitError = "Could not resolve type " + ExternalTypeName + ".";
                return false;
            }

            var missingMembers = new List<string>();

            _ExtractBotMethod = getRequiredMethod(_SAINExternalType, "ExtractBot", typeof(bool), missingMembers, typeof(BotOwner));
            _SetExfilForBotMethod = getRequiredMethod(
                _SAINExternalType,
                "TrySetExfilForBot",
                typeof(bool),
                missingMembers,
                typeof(BotOwner)
            );
            _IgnoreHearingMethod = getRequiredMethod(
                _SAINExternalType,
                "IgnoreHearing",
                typeof(bool),
                missingMembers,
                typeof(BotOwner),
                typeof(bool),
                typeof(bool),
                typeof(float)
            );

            _IsPathTowardEnemyMethod = getOptionalMethod(
                _SAINExternalType,
                "IsPathTowardEnemy",
                typeof(bool),
                typeof(NavMeshPath),
                typeof(BotOwner),
                typeof(float),
                typeof(float)
            );
            _TimeSinceSenseEnemyMethod = getOptionalMethod(_SAINExternalType, "TimeSinceSenseEnemy", typeof(float), typeof(BotOwner));
            _CanBotQuestMethod = getOptionalMethod(
                _SAINExternalType,
                "CanBotQuest",
                typeof(bool),
                typeof(BotOwner),
                typeof(Vector3),
                typeof(float)
            );
            _GetExtractedBotsMethod = getOptionalMethod(_SAINExternalType, "GetExtractedBots", typeof(void), typeof(List<string>));
            _GetPersonalityMethod = getOptionalMethod(_SAINExternalType, "GetPersonality", typeof(string), typeof(BotOwner));

            if (missingMembers.Count > 0)
            {
                _lastInitError = "Missing required SAIN interop members: " + string.Join(", ", missingMembers);
                return false;
            }

            _lastInitError = string.Empty;
            return true;
        }

        private static MethodInfo getRequiredMethod(
            Type externalType,
            string methodName,
            Type returnType,
            List<string> missingMembers,
            params Type[] parameterTypes
        )
        {
            MethodInfo method = getMethod(externalType, methodName, returnType, parameterTypes);
            if (method == null)
            {
                missingMembers.Add(formatMethodSignature(methodName, returnType, parameterTypes));
            }

            return method;
        }

        private static MethodInfo getOptionalMethod(Type externalType, string methodName, Type returnType, params Type[] parameterTypes) =>
            getMethod(externalType, methodName, returnType, parameterTypes);

        private static MethodInfo getMethod(Type externalType, string methodName, Type returnType, params Type[] parameterTypes)
        {
            MethodInfo method = externalType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, parameterTypes, null);
            if ((method == null) || (method.ReturnType != returnType))
            {
                return null;
            }

            return method;
        }

        private static string formatMethodSignature(string methodName, Type returnType, params Type[] parameterTypes) =>
            methodName + "(" + string.Join(", ", parameterTypes.Select(getFriendlyTypeName)) + ") -> " + getFriendlyTypeName(returnType);

        private static string getFriendlyTypeName(Type type)
        {
            if (type == typeof(void))
            {
                return "void";
            }

            if (!type.IsGenericType)
            {
                return type.Name;
            }

            string genericTypeName = type.Name;
            int genericSuffixIndex = genericTypeName.IndexOf('`');
            if (genericSuffixIndex >= 0)
            {
                genericTypeName = genericTypeName.Substring(0, genericSuffixIndex);
            }

            return genericTypeName + "<" + string.Join(", ", type.GetGenericArguments().Select(getFriendlyTypeName)) + ">";
        }

        /// <summary>
        /// Sets this bot to ignore hearing for a specified duration, or until a bot sees an enemy;
        /// </summary>
        /// <param name="value">Set Ignore to On or Off</param>
        /// <param name="ignoreUnderFire">Set bot to ignore being under fire (shots being ~2m or closer to them by default)</param>
        /// <param name="duration">if greater than 0, stop ignoring hearing after that time has passed. 0 means they will ignore hearing forever until they see an enemy.</param>
        /// <returns>True if the bot was successfully set to ignore hearing</returns>
        public static bool IgnoreHearing(BotOwner botOwner, bool value, bool ignoreUnderFire, float duration = 0)
        {
            if (botOwner == null)
                return false;
            if (!Init())
                return false;
            if (_IgnoreHearingMethod == null)
                return false;

            return (bool)_IgnoreHearingMethod.Invoke(null, new object[] { botOwner, value, ignoreUnderFire, duration });
        }

        /// <summary>
        /// Get the Current Personality of a bot;
        /// </summary>
        /// <param name="botOwner">The bot to check</param>
        /// <returns>A string of a bot's personality, returns string.Empty if it could not be found.</returns>
        public static string GetPersonality(BotOwner botOwner)
        {
            string result = string.Empty;
            if (botOwner == null)
                return result;
            if (!Init())
                return result;
            if (_GetPersonalityMethod == null)
                return result;

            result = (string)_GetPersonalityMethod.Invoke(null, new object[] { botOwner });
            return result;
        }

        /// <summary>
        /// Get a list of all "Player.ProfileID"s of bots that have extracted. The list must not be null. The list is cleared before adding all extracted ProfileIDs;
        /// </summary>
        /// <param name="list">An already existing list to add to</param>
        /// <returns>True if the list was successfully updated</returns>
        public static bool GetExtractedBots(List<string> list)
        {
            if (list == null)
                return false;
            if (!Init())
                return false;
            if (_GetExtractedBotsMethod == null)
                return false;

            _GetExtractedBotsMethod.Invoke(null, new object[] { list });
            return true;
        }

        /**
         * Force a bot into the Extract layer if SAIN is loaded. Return true if the bot was set to extract.
         */
        public static bool TryExtractBot(BotOwner botOwner)
        {
            if (!Init())
                return false;
            if (_ExtractBotMethod == null)
                return false;

            return (bool)_ExtractBotMethod.Invoke(null, new object[] { botOwner });
        }

        /**
         * Try to select an exfil point for the bot if SAIN is loaded. Return true if an exfil was assigned to the bot.
         */
        public static bool TrySetExfilForBot(BotOwner botOwner)
        {
            if (!Init())
                return false;
            if (_SetExfilForBotMethod == null)
                return false;

            return (bool)_SetExfilForBotMethod.Invoke(null, new object[] { botOwner });
        }

        /// <summary>
        /// Compare a NavMeshPath to the pre-calculated NavMeshPath that leads directly to a bot's Active Enemy.
        /// </summary>
        /// <param name="path">The Path To Test</param>
        /// <param name="botOwner">The Bot in Question</param>
        /// <param name="ratioSameOverAll">How many nodes along a path are allowed to be the same divided by the total nodes in the Path To Test. Example: 3 nodes are the same, with 10 total nodes = 0.3 ratio, so if the input value is 0.25, this will return false.</param>
        /// <param name="sqrDistCheck">How Close a node can be to be considered the same.</param>
        /// <returns>True if the path leads in the same direction as their active enemy.</returns>
        public static bool IsPathTowardEnemy(
            NavMeshPath path,
            BotOwner botOwner,
            float ratioSameOverAll = 0.25f,
            float sqrDistCheck = 0.05f
        )
        {
            if (!Init())
                return false;
            if (_IsPathTowardEnemyMethod == null)
                return false;

            return (bool)_IsPathTowardEnemyMethod.Invoke(null, new object[] { path, botOwner, ratioSameOverAll, sqrDistCheck });
        }

        /// <summary>
        /// Compare a NavMeshPath to the pre-calculated NavMeshPath that leads directly to a bot's Active Enemy.
        /// </summary>
        /// <param name="path">The Path To Test</param>
        /// <param name="botOwner">The Bot in Question</param>
        /// <param name="ratioSameOverAll">How many nodes along a path are allowed to be the same divided by the total nodes in the Path To Test. Example: 3 nodes are the same, with 10 total nodes = 0.3 ratio, so if the input value is 0.25, this will return false.</param>
        /// <param name="sqrDistCheck">How Close a node can be to be considered the same.</param>
        /// <returns>True if the path leads in the same direction as their active enemy.</returns>
        public static bool CanBotQuest(BotOwner botOwner, Vector3 questPosition, float dotThreshold = 0.33f)
        {
            if (!Init())
                return false;
            if (_CanBotQuestMethod == null)
                return false;

            return (bool)_CanBotQuestMethod.Invoke(null, new object[] { botOwner, questPosition, dotThreshold });
        }

        public static float TimeSinceSenseEnemy(BotOwner botOwner)
        {
            if (!Init())
                return float.MaxValue;
            if (_TimeSinceSenseEnemyMethod == null)
                return float.MaxValue;

            return (float)_TimeSinceSenseEnemyMethod.Invoke(null, new object[] { botOwner });
        }
    }
}
