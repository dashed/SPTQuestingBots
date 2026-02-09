using System;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.BotLogic.ECS.Systems
{
    /// <summary>
    /// Pure-logic static class for computing tactical positions for squad members.
    /// Assigns roles based on quest action type and computes positions relative to
    /// the objective and approach direction.
    /// <para>
    /// Pure C# — no Unity or EFT dependencies — fully testable.
    /// </para>
    /// </summary>
    public static class TacticalPositionCalculator
    {
        /// <summary>
        /// Assign roles to followers based on quest action type.
        /// </summary>
        /// <param name="questActionId">Current quest action (see <see cref="QuestActionId"/>).</param>
        /// <param name="followerCount">Number of followers to assign roles to.</param>
        /// <param name="outRoles">Output array for roles (length must be >= followerCount).</param>
        public static void AssignRoles(int questActionId, int followerCount, SquadRole[] outRoles)
        {
            switch (questActionId)
            {
                case QuestActionId.MoveToPosition:
                    for (int i = 0; i < followerCount; i++)
                        outRoles[i] = SquadRole.Escort;
                    break;
                case QuestActionId.Ambush:
                    AssignPattern(followerCount, outRoles, SquadRole.Flanker, SquadRole.Overwatch, SquadRole.Guard);
                    break;
                case QuestActionId.Snipe:
                    AssignPattern(followerCount, outRoles, SquadRole.Overwatch, SquadRole.Guard, SquadRole.Guard);
                    break;
                case QuestActionId.PlantItem:
                    AssignPattern(followerCount, outRoles, SquadRole.Guard, SquadRole.Escort, SquadRole.Flanker);
                    break;
                case QuestActionId.HoldAtPosition:
                    AssignPattern(followerCount, outRoles, SquadRole.Flanker, SquadRole.Overwatch, SquadRole.Guard);
                    break;
                case QuestActionId.ToggleSwitch:
                case QuestActionId.CloseNearbyDoors:
                    for (int i = 0; i < followerCount; i++)
                        outRoles[i] = SquadRole.Guard;
                    break;
                default:
                    AssignPattern(followerCount, outRoles, SquadRole.Guard, SquadRole.Flanker, SquadRole.Overwatch);
                    break;
            }
        }

        /// <summary>
        /// Assign first=role1, second=role2, remaining=role3.
        /// </summary>
        private static void AssignPattern(int count, SquadRole[] outRoles, SquadRole first, SquadRole second, SquadRole remaining)
        {
            for (int i = 0; i < count; i++)
            {
                if (i == 0)
                    outRoles[i] = first;
                else if (i == 1)
                    outRoles[i] = second;
                else
                    outRoles[i] = remaining;
            }
        }

        /// <summary>
        /// Compute tactical positions for each role relative to the objective and approach direction.
        /// </summary>
        /// <param name="objX">Objective X.</param>
        /// <param name="objY">Objective Y.</param>
        /// <param name="objZ">Objective Z.</param>
        /// <param name="approachX">Approach origin X (e.g., leader position).</param>
        /// <param name="approachZ">Approach origin Z.</param>
        /// <param name="roles">Role for each member.</param>
        /// <param name="count">Number of members.</param>
        /// <param name="outPositions">Output x,y,z triples (length >= count * 3).</param>
        /// <param name="config">Distance configuration.</param>
        public static void ComputePositions(
            float objX,
            float objY,
            float objZ,
            float approachX,
            float approachZ,
            SquadRole[] roles,
            int count,
            float[] outPositions,
            SquadStrategyConfig config
        )
        {
            for (int i = 0; i < count; i++)
            {
                float x,
                    y,
                    z;
                switch (roles[i])
                {
                    case SquadRole.Guard:
                        ComputeGuardPosition(objX, objY, objZ, i * (360f / Math.Max(count, 1)), config.GuardDistance, out x, out y, out z);
                        break;
                    case SquadRole.Flanker:
                        float side = (i % 2 == 0) ? 1f : -1f;
                        ComputeFlankPosition(objX, objY, objZ, approachX, approachZ, side, config.FlankDistance, out x, out y, out z);
                        break;
                    case SquadRole.Overwatch:
                        ComputeOverwatchPosition(objX, objY, objZ, approachX, approachZ, config.OverwatchDistance, out x, out y, out z);
                        break;
                    case SquadRole.Escort:
                        float lateralOffset = (i % 2 == 0) ? 2f : -2f;
                        ComputeEscortPosition(
                            approachX,
                            objY,
                            approachZ,
                            objX,
                            objZ,
                            config.EscortDistance,
                            lateralOffset,
                            out x,
                            out y,
                            out z
                        );
                        break;
                    default:
                        x = objX;
                        y = objY;
                        z = objZ;
                        break;
                }

                outPositions[i * 3] = x;
                outPositions[i * 3 + 1] = y;
                outPositions[i * 3 + 2] = z;
            }
        }

        /// <summary>
        /// Guard: circle around objective at given angle and radius.
        /// </summary>
        public static void ComputeGuardPosition(
            float objX,
            float objY,
            float objZ,
            float angleDeg,
            float radius,
            out float x,
            out float y,
            out float z
        )
        {
            float rad = angleDeg * (float)(Math.PI / 180.0);
            x = objX + radius * (float)Math.Cos(rad);
            y = objY;
            z = objZ + radius * (float)Math.Sin(rad);
        }

        /// <summary>
        /// Flanker: perpendicular to approach direction, offset to one side.
        /// </summary>
        public static void ComputeFlankPosition(
            float objX,
            float objY,
            float objZ,
            float approachX,
            float approachZ,
            float side,
            float distance,
            out float x,
            out float y,
            out float z
        )
        {
            float dx = objX - approachX;
            float dz = objZ - approachZ;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.001f)
            {
                x = objX + distance * side;
                y = objY;
                z = objZ;
                return;
            }

            // Perpendicular: rotate 90 degrees
            float perpX = -dz / len;
            float perpZ = dx / len;
            x = objX + perpX * distance * side;
            y = objY;
            z = objZ + perpZ * distance * side;
        }

        /// <summary>
        /// Overwatch: behind approach direction, further away from objective.
        /// </summary>
        public static void ComputeOverwatchPosition(
            float objX,
            float objY,
            float objZ,
            float approachX,
            float approachZ,
            float distance,
            out float x,
            out float y,
            out float z
        )
        {
            float dx = approachX - objX;
            float dz = approachZ - objZ;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.001f)
            {
                x = objX;
                y = objY;
                z = objZ - distance;
                return;
            }

            x = objX + (dx / len) * distance;
            y = objY;
            z = objZ + (dz / len) * distance;
        }

        /// <summary>
        /// Escort: trail behind the boss toward the objective, with a lateral offset.
        /// </summary>
        public static void ComputeEscortPosition(
            float bossX,
            float bossY,
            float bossZ,
            float objX,
            float objZ,
            float trailDist,
            float lateralOffset,
            out float x,
            out float y,
            out float z
        )
        {
            float dx = objX - bossX;
            float dz = objZ - bossZ;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len < 0.001f)
            {
                x = bossX + lateralOffset;
                y = bossY;
                z = bossZ;
                return;
            }

            float nx = dx / len;
            float nz = dz / len;
            x = bossX + nx * trailDist + (-nz) * lateralOffset;
            y = bossY;
            z = bossZ + nz * trailDist + nx * lateralOffset;
        }
    }
}
