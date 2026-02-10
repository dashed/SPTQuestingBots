using System.Runtime.CompilerServices;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
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
                _count++;
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
                    continue;
                if (currentTime - evt.Time > maxAge)
                    continue;

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
                    continue;

                float age = currentTime - evt.Time;
                if (age > timeWindow)
                    continue;

                float dx = posX - evt.X;
                float dz = posZ - evt.Z;
                float distSqr = dx * dx + dz * dz;

                if (distSqr < radiusSqr)
                {
                    count++;
                    // Explosions count as extra intensity
                    if (evt.Type == CombatEventType.Explosion)
                        count += 2;
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
                    continue;
                if (!evt.IsBoss)
                    continue;
                if (currentTime - evt.Time > decayTime)
                    continue;

                float dx = posX - evt.X;
                float dz = posZ - evt.Z;
                float distSqr = dx * dx + dz * dz;

                if (distSqr < radiusSqr)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Mark expired events as inactive.
        /// Call once per tick to keep the buffer clean.
        /// </summary>
        public static void CleanupExpired(float currentTime, float maxAge)
        {
            for (int i = 0; i < _count; i++)
            {
                int idx = (_head - 1 - i + _capacity * 2) % _capacity;
                ref CombatEvent evt = ref _events[idx];
                if (evt.IsActive && currentTime - evt.Time > maxAge)
                {
                    evt.IsActive = false;
                }
            }
        }

        /// <summary>
        /// Get the number of events currently in the buffer (including inactive).
        /// </summary>
        public static int Count => _count;

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
                        active++;
                }
                return active;
            }
        }

        /// <summary>
        /// Clear all events. Call at raid end.
        /// </summary>
        public static void Clear()
        {
            for (int i = 0; i < _capacity; i++)
            {
                _events[i] = default;
            }
            _head = 0;
            _count = 0;
        }
    }
}
