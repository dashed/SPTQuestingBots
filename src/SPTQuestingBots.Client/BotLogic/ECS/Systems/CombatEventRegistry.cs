using System.Runtime.CompilerServices;
using SPTQuestingBots.Controllers;
using SPTQuestingBots.ZoneMovement.Core;

namespace SPTQuestingBots.BotLogic.ECS.Systems;

/// <summary>
/// Global registry of combat events (gunshots, explosions, airdrops).
/// Uses a fixed-size ring buffer for zero-allocation event storage.
/// All methods are static with AggressiveInlining. Pure C# — no Unity deps.
/// Ported from Vulture's CombatSoundListener, adapted for QuestingBots ECS.
/// </summary>
public static class CombatEventRegistry
{
    /// <summary>Default ring buffer capacity.</summary>
    public const int DefaultCapacity = 128;

    private static CombatEvent[] _events;
    private static int _capacity;
    private static int _head; // Next write index
    private static int _count; // Number of active events (up to capacity)

    static CombatEventRegistry()
    {
        _capacity = DefaultCapacity;
        _events = new CombatEvent[_capacity];
        _head = 0;
        _count = 0;
    }

    /// <summary>
    /// Re-initialize with a specific capacity. Clears all events.
    /// </summary>
    public static void Initialize(int capacity)
    {
        _capacity = capacity > 0 ? capacity : DefaultCapacity;
        _events = new CombatEvent[_capacity];
        _head = 0;
        _count = 0;
        LoggingController.LogInfo("[CombatEventRegistry] Initialized with capacity=" + _capacity);
    }

    /// <summary>
    /// Record a new combat event. Overwrites oldest if buffer is full.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void RecordEvent(CombatEvent evt)
    {
        evt.IsActive = true;
        _events[_head] = evt;
        _head = (_head + 1) % _capacity;
        if (_count < _capacity)
        {
            _count++;
        }

        LoggingController.LogInfo(
            "[CombatEventRegistry] Recorded type="
                + evt.Type
                + " event at ("
                + evt.X.ToString("F0")
                + ","
                + evt.Y.ToString("F0")
                + ","
                + evt.Z.ToString("F0")
                + "), power="
                + evt.Power.ToString("F0")
                + " isBoss="
                + evt.IsBoss
                + " count="
                + _count
        );
    }

    /// <summary>
    /// Record a combat event with explicit parameters (convenience overload).
    /// </summary>
    public static void RecordEvent(float x, float y, float z, float time, float power, byte type, bool isBoss)
    {
        RecordEvent(
            new CombatEvent
            {
                X = x,
                Y = y,
                Z = z,
                Time = time,
                Power = power,
                Type = type,
                IsBoss = isBoss,
                IsActive = true,
            }
        );
    }

    /// <summary>
    /// Find the nearest active, non-expired event within maxRange of (botX, botZ).
    /// Returns the event via out parameter and true if found, false otherwise.
    /// Uses squared distance on XZ plane for efficiency.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool GetNearestEvent(float botX, float botZ, float maxRange, float currentTime, float maxAge, out CombatEvent nearest)
    {
        float maxRangeSqr = maxRange * maxRange;
        float bestDistSqr = float.MaxValue;
        nearest = default;
        bool found = false;

        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + _capacity * 2) % _capacity;
            ref CombatEvent evt = ref _events[idx];
            if (!evt.IsActive)
            {
                continue;
            }

            if (currentTime - evt.Time > maxAge)
            {
                continue;
            }

            float dx = botX - evt.X;
            float dz = botZ - evt.Z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr < maxRangeSqr && distSqr < bestDistSqr)
            {
                bestDistSqr = distSqr;
                nearest = evt;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// Count active combat events within radius of (posX, posZ) within timeWindow seconds.
    /// Explosions count as 3 events worth of intensity.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetIntensity(float posX, float posZ, float radius, float timeWindow, float currentTime)
    {
        float radiusSqr = radius * radius;
        int count = 0;

        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + _capacity * 2) % _capacity;
            ref CombatEvent evt = ref _events[idx];
            if (!evt.IsActive)
            {
                continue;
            }

            float age = currentTime - evt.Time;
            if (age > timeWindow)
            {
                continue;
            }

            float dx = posX - evt.X;
            float dz = posZ - evt.Z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr < radiusSqr)
            {
                count++;
                // Explosions count as extra intensity
                if (evt.Type == CombatEventType.Explosion)
                {
                    count += 2;
                }
            }
        }

        return count;
    }

    /// <summary>
    /// Check if a position is within a recent boss activity zone.
    /// Returns true if any boss-tagged event is within radius and not older than decayTime.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsInBossZone(float posX, float posZ, float radius, float decayTime, float currentTime)
    {
        float radiusSqr = radius * radius;

        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + _capacity * 2) % _capacity;
            ref CombatEvent evt = ref _events[idx];
            if (!evt.IsActive)
            {
                continue;
            }

            if (!evt.IsBoss)
            {
                continue;
            }

            if (currentTime - evt.Time > decayTime)
            {
                continue;
            }

            float dx = posX - evt.X;
            float dz = posZ - evt.Z;
            float distSqr = dx * dx + dz * dz;

            if (distSqr < radiusSqr)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Mark expired events as inactive.
    /// Call once per tick to keep the buffer clean.
    /// </summary>
    public static void CleanupExpired(float currentTime, float maxAge)
    {
        int expiredCount = 0;
        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + _capacity * 2) % _capacity;
            ref CombatEvent evt = ref _events[idx];
            if (evt.IsActive && currentTime - evt.Time > maxAge)
            {
                evt.IsActive = false;
                expiredCount++;
            }
        }
        if (expiredCount > 0)
        {
            LoggingController.LogDebug(
                "[CombatEventRegistry] Cleaned up " + expiredCount + " expired events, maxAge=" + maxAge.ToString("F0") + "s"
            );
        }
    }

    /// <summary>
    /// Get the number of events currently in the buffer (including inactive).
    /// </summary>
    public static int Count
    {
        get { return _count; }
    }

    /// <summary>
    /// Get the number of active (non-expired) events in the buffer.
    /// </summary>
    public static int ActiveCount
    {
        get
        {
            int active = 0;
            for (int i = 0; i < _count; i++)
            {
                int idx = (_head - 1 - i + _capacity * 2) % _capacity;
                if (_events[idx].IsActive)
                {
                    active++;
                }
            }
            return active;
        }
    }

    /// <summary>
    /// Gathers all active, non-expired events as <see cref="CombatPullPoint"/> values
    /// with linearly decayed strength. Used by the convergence field to pull bots
    /// toward recent combat activity.
    /// </summary>
    /// <param name="buffer">Pre-allocated output buffer. Must be at least <see cref="DefaultCapacity"/> elements.</param>
    /// <param name="currentTime">Current game time.</param>
    /// <param name="maxAge">Maximum event age in seconds. Events older than this are skipped.</param>
    /// <param name="forceMultiplier">Global force multiplier applied to all pull strengths.</param>
    /// <returns>Number of valid entries written to <paramref name="buffer"/>.</returns>
    public static int GatherCombatPull(CombatPullPoint[] buffer, float currentTime, float maxAge, float forceMultiplier)
    {
        int written = 0;
        if (buffer == null || maxAge <= 0f)
        {
            return 0;
        }

        int maxWrite = buffer.Length;

        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + _capacity * 2) % _capacity;
            ref CombatEvent evt = ref _events[idx];
            if (!evt.IsActive)
            {
                continue;
            }

            float age = currentTime - evt.Time;
            if (age > maxAge || age < 0f)
            {
                continue;
            }

            // Linear decay: full strength at age=0, zero at age=maxAge
            float decay = 1f - (age / maxAge);
            // Scale by event power (normalized by gunshot baseline of 100)
            float strength = decay * (evt.Power / 100f) * forceMultiplier;

            if (strength < 0.01f)
            {
                continue;
            }

            if (written >= maxWrite)
            {
                break;
            }

            buffer[written] = new CombatPullPoint
            {
                X = evt.X,
                Z = evt.Z,
                Strength = strength,
            };
            written++;
        }

        return written;
    }

    /// <summary>
    /// Gathers all active, non-expired events into the output buffer.
    /// Used by <see cref="Components.DynamicObjectiveScanner"/> to pass
    /// raw event data to <see cref="DynamicObjectiveGenerator"/>.
    /// </summary>
    /// <param name="buffer">Pre-allocated output buffer for events.</param>
    /// <param name="currentTime">Current game time.</param>
    /// <param name="maxAge">Maximum event age in seconds.</param>
    /// <returns>Number of valid events written to <paramref name="buffer"/>.</returns>
    public static int GatherActiveEvents(CombatEvent[] buffer, float currentTime, float maxAge)
    {
        int written = 0;
        if (buffer == null || maxAge <= 0f)
        {
            return 0;
        }

        int maxWrite = buffer.Length;

        for (int i = 0; i < _count; i++)
        {
            int idx = (_head - 1 - i + _capacity * 2) % _capacity;
            ref CombatEvent evt = ref _events[idx];
            if (!evt.IsActive)
            {
                continue;
            }

            float age = currentTime - evt.Time;
            if (age > maxAge || age < 0f)
            {
                continue;
            }

            if (written >= maxWrite)
            {
                break;
            }

            buffer[written] = evt;
            written++;
        }

        return written;
    }

    /// <summary>
    /// Clear all events. Call at raid end.
    /// </summary>
    public static void Clear()
    {
        LoggingController.LogInfo("[CombatEventRegistry] Clearing all events, count=" + _count);
        for (int i = 0; i < _capacity; i++)
        {
            _events[i] = default;
        }
        _head = 0;
        _count = 0;
    }
}
