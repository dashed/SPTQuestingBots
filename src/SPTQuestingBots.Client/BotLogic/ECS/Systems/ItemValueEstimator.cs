using System;
using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Delegate for looking up item prices by template ID.
    /// Injected at runtime to keep scoring logic pure C#.
    /// </summary>
    public delegate int PriceLookup(int templateId);

    /// <summary>
    /// Pure-logic item value estimation. Static class with AggressiveInlining.
    /// No Unity or EFT dependencies.
    /// </summary>
    public static class ItemValueEstimator
    {
        /// <summary>
        /// Get the estimated value of a single item by template ID.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetValue(int templateId, PriceLookup lookup)
        {
            if (lookup == null)
                return 0;
            return lookup(templateId);
        }

        /// <summary>
        /// Get the total value of a weapon by summing its mod template values.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetWeaponValue(int[] modTemplateIds, int modCount, PriceLookup lookup)
        {
            if (modTemplateIds == null || lookup == null || modCount <= 0)
                return 0;

            int total = 0;
            int len = Math.Min(modCount, modTemplateIds.Length);
            for (int i = 0; i < len; i++)
            {
                total += lookup(modTemplateIds[i]);
            }
            return total;
        }

        /// <summary>
        /// Normalize a raw value to the 0-1 range by dividing by cap, clamped.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float NormalizeValue(float rawValue, float cap)
        {
            if (cap <= 0f || rawValue <= 0f)
                return 0f;
            float result = rawValue / cap;
            return result > 1f ? 1f : result;
        }
    }
}
