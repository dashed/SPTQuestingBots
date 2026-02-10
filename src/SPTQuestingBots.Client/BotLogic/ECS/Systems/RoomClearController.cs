namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Instructions returned by <see cref="RoomClearController"/> to govern
    /// bot movement during an indoor room-clear phase.
    /// </summary>
    public enum RoomClearInstruction : byte
    {
        /// <summary>No room clearing active.</summary>
        None = 0,

        /// <summary>Indoor: walk speed, lowered pose.</summary>
        SlowWalk = 1,

        /// <summary>Sharp corner detected: brief pause.</summary>
        PauseAtCorner = 2,
    }

    /// <summary>
    /// Pure C# controller for room clearing behavior.
    /// Detects outdoor-to-indoor environment transitions, manages room clear timers,
    /// and computes corner angles. No Unity dependencies — fully testable.
    /// </summary>
    public static class RoomClearController
    {
        private static readonly System.Random _rng = new System.Random();

        /// <summary>
        /// Evaluate room-clear state for a single bot entity.
        /// Called each tick from <see cref="Objective.GoToObjectiveAction.Update"/>.
        /// </summary>
        /// <param name="entity">The bot entity to update.</param>
        /// <param name="environmentId">BSG environment ID (0 = indoor).</param>
        /// <param name="currentTime">Current game time (Time.time).</param>
        /// <param name="durationMin">Minimum room clear duration in seconds.</param>
        /// <param name="durationMax">Maximum room clear duration in seconds.</param>
        /// <param name="cornerPauseDuration">Duration of corner pauses (unused here, see TriggerCornerPause).</param>
        /// <returns>The movement instruction to apply.</returns>
        public static RoomClearInstruction Update(
            BotEntity entity,
            int environmentId,
            float currentTime,
            float durationMin,
            float durationMax,
            float cornerPauseDuration
        )
        {
            // Track environment transition
            bool wasOutdoor = entity.LastEnvironmentId != 0; // 0 = indoor in BSG
            bool isIndoor = environmentId == 0;
            entity.LastEnvironmentId = environmentId;

            // Outdoor->Indoor transition: start room clear
            if (wasOutdoor && isIndoor && !entity.IsInRoomClear)
            {
                float duration = durationMin + (float)(_rng.NextDouble() * (durationMax - durationMin));
                entity.RoomClearUntil = currentTime + duration;
                entity.IsInRoomClear = true;
                return RoomClearInstruction.SlowWalk;
            }

            // Currently in room clear mode
            if (entity.IsInRoomClear)
            {
                // Timer expired
                if (currentTime >= entity.RoomClearUntil)
                {
                    entity.IsInRoomClear = false;
                    return RoomClearInstruction.None;
                }

                // Corner pause active
                if (entity.CornerPauseUntil > currentTime)
                {
                    return RoomClearInstruction.PauseAtCorner;
                }

                return RoomClearInstruction.SlowWalk;
            }

            return RoomClearInstruction.None;
        }

        /// <summary>
        /// Check if three 2D points form a sharp corner (angle above threshold).
        /// Returns true if the angle between the two direction vectors at the corner
        /// point exceeds <paramref name="thresholdDegrees"/>.
        /// Used to detect doorway/hallway turns.
        /// </summary>
        public static bool IsSharpCorner(
            float fromX,
            float fromZ,
            float cornerX,
            float cornerZ,
            float toX,
            float toZ,
            float thresholdDegrees
        )
        {
            // Vector from->corner
            float ax = cornerX - fromX;
            float az = cornerZ - fromZ;
            // Vector corner->to
            float bx = toX - cornerX;
            float bz = toZ - cornerZ;

            float dot = ax * bx + az * bz;
            float magA = (float)System.Math.Sqrt(ax * ax + az * az);
            float magB = (float)System.Math.Sqrt(bx * bx + bz * bz);

            if (magA < 0.001f || magB < 0.001f)
                return false;

            float cosAngle = dot / (magA * magB);
            // Clamp to [-1, 1] to avoid NaN from floating point
            if (cosAngle > 1f)
                cosAngle = 1f;
            if (cosAngle < -1f)
                cosAngle = -1f;

            float angleDegrees = (float)(System.Math.Acos(cosAngle) * 180.0 / System.Math.PI);

            // A "sharp" turn means the angle between the two direction vectors is large
            // (close to 180 = U-turn, close to 0 = straight ahead)
            // We want to detect when the turn angle exceeds the threshold
            return angleDegrees > thresholdDegrees;
        }

        /// <summary>Start a corner pause if not already pausing.</summary>
        public static void TriggerCornerPause(BotEntity entity, float currentTime, float pauseDuration)
        {
            if (entity.CornerPauseUntil <= currentTime)
            {
                entity.CornerPauseUntil = currentTime + pauseDuration;
            }
        }
    }
}
