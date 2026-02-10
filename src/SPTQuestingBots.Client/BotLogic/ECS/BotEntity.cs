using System;
using System.Collections.Generic;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;

namespace SPTQuestingBots.BotLogic.ECS
{
    /// <summary>
    /// Per-bot data container with a stable recycled ID.
    /// Pure C# — no Unity or EFT dependencies — fully testable in net9.0.
    /// </summary>
    public sealed class BotEntity : IEquatable<BotEntity>
    {
        // ── Phase 1: Identity + Hierarchy ────────────────────────

        /// <summary>Stable slot ID assigned by BotRegistry. Recycled on removal.</summary>
        public readonly int Id;

        /// <summary>Whether this entity is currently active in the world.</summary>
        public bool IsActive;

        /// <summary>
        /// Boss entity for followers, null for bosses and solo bots.
        /// Replaces BotHiveMindMonitor.botBosses dictionary lookup.
        /// </summary>
        public BotEntity Boss;

        /// <summary>
        /// Direct followers of this entity (empty for non-bosses).
        /// Replaces BotHiveMindMonitor.botFollowers dictionary lookup.
        /// </summary>
        public readonly List<BotEntity> Followers;

        // ── Phase 2: Sensor State ───────────────────────────────

        /// <summary>Whether this bot is currently in combat. Default: false.</summary>
        public bool IsInCombat;

        /// <summary>Whether this bot has detected something suspicious. Default: false.</summary>
        public bool IsSuspicious;

        /// <summary>Whether this bot is allowed to quest. Default: false.</summary>
        public bool CanQuest;

        /// <summary>Whether this bot can sprint to its current objective. Default: true.</summary>
        public bool CanSprintToObjective;

        /// <summary>Whether this bot wants to loot. Default: false.</summary>
        public bool WantsToLoot;

        /// <summary>
        /// Last time this bot started looting. Replaces botLastLootingTime dictionary.
        /// Default: <see cref="DateTime.MinValue"/>.
        /// </summary>
        public DateTime LastLootingTime;

        // ── Phase 2: Classification + Sleep ─────────────────────

        /// <summary>
        /// Bot classification. Replaces registeredPMCs/registeredBosses HashSet lookups.
        /// Default: <see cref="BotType.Unknown"/>.
        /// </summary>
        public BotType BotType;

        /// <summary>
        /// Whether this bot is currently sleeping (AI disabled by limiter).
        /// Replaces sleepingBotIds list lookup.
        /// Default: false.
        /// </summary>
        public bool IsSleeping;

        // ── Phase 6: Zone Movement Field State ─────────────────

        /// <summary>
        /// Per-bot noise seed for zone movement field composition.
        /// Set once from bot profile ID hash. Default: 0 (uninitialized).
        /// </summary>
        public int FieldNoiseSeed;

        /// <summary>
        /// Whether this bot has an active field state for zone movement.
        /// </summary>
        public bool HasFieldState;

        // ── Movement State ─────────────────────────────────────

        /// <summary>
        /// Per-bot movement tracking for the custom path-following system.
        /// Inline value type for dense iteration and zero-allocation reads.
        /// </summary>
        public MovementState Movement;

        // ── Utility AI: Task Scores ──────────────────────────

        /// <summary>
        /// Per-task utility scores. Sized to the number of registered utility tasks.
        /// Written by each task's UpdateScores (column-major), read by UtilityTaskManager.PickTask.
        /// Null when utility AI is not active for this entity.
        /// </summary>
        public float[] TaskScores;

        /// <summary>
        /// The currently assigned utility task and its ordinal index.
        /// Default: no task assigned.
        /// </summary>
        public UtilityTaskAssignment TaskAssignment;

        // ── Quest State for Utility AI ─────────────────────────

        /// <summary>
        /// Current quest action as an int (mirrors <c>QuestAction</c> enum ordinal).
        /// Synced from <c>BotObjectiveManager.CurrentQuestAction</c> before scoring.
        /// See <see cref="UtilityAI.QuestActionId"/> for constant values.
        /// Default: 0 (Undefined).
        /// </summary>
        public int CurrentQuestAction;

        /// <summary>
        /// Distance from bot to its current objective position.
        /// Synced from <c>BotObjectiveManager.DistanceToObjective</c> before scoring.
        /// Default: <see cref="float.MaxValue"/> (no objective).
        /// </summary>
        public float DistanceToObjective;

        /// <summary>
        /// Whether the bot is within the "close to objective" threshold.
        /// Synced from <c>BotObjectiveManager.IsCloseToObjective()</c> before scoring.
        /// Default: false.
        /// </summary>
        public bool IsCloseToObjective;

        /// <summary>
        /// Whether the current path requires unlocking a door first.
        /// Synced from <c>BotObjectiveManager.MustUnlockDoor</c> before scoring.
        /// Default: false.
        /// </summary>
        public bool MustUnlockDoor;

        /// <summary>
        /// Whether the bot has an active quest assignment.
        /// Synced from <c>BotObjectiveManager.IsJobAssignmentActive</c> before scoring.
        /// Default: false.
        /// </summary>
        public bool HasActiveObjective;

        // ── Phase 8: Job Assignment State ─────────────────────

        /// <summary>
        /// Number of consecutive failed quest assignments for this bot.
        /// Replaces BotJobAssignmentFactory dictionary lookup.
        /// Default: 0.
        /// </summary>
        public int ConsecutiveFailedAssignments;

        // ── Squad State ─────────────────────────────────────────

        /// <summary>
        /// Reference to the squad this bot belongs to. Null if not in a squad.
        /// </summary>
        public SquadEntity Squad;

        /// <summary>
        /// This bot's role within its squad. Default: None.
        /// </summary>
        public SquadRole SquadRole;

        /// <summary>
        /// Tactical position X coordinate assigned by the squad strategy.
        /// </summary>
        public float TacticalPositionX;

        /// <summary>
        /// Tactical position Y coordinate assigned by the squad strategy.
        /// </summary>
        public float TacticalPositionY;

        /// <summary>
        /// Tactical position Z coordinate assigned by the squad strategy.
        /// </summary>
        public float TacticalPositionZ;

        /// <summary>
        /// Whether this bot has been assigned a tactical position by the squad strategy.
        /// </summary>
        public bool HasTacticalPosition;

        /// <summary>
        /// Whether this bot has an earpiece equipped. Used for communication range checks.
        /// Synced from game equipment on registration.
        /// </summary>
        public bool HasEarPiece;

        /// <summary>
        /// Last objective version this bot saw. Used to detect when the squad objective changes.
        /// </summary>
        public int LastSeenObjectiveVersion;

        /// <summary>
        /// Sharing tier for multi-level objective sharing.
        /// 0 = none (no position received), 1 = direct (from leader), 2 = relayed (through Tier 1 member).
        /// Set during tactical position distribution in GotoObjectiveStrategy.
        /// </summary>
        public byte SharingTier;

        /// <summary>
        /// Current world position X, synced from BotOwner.Position for pure-logic access.
        /// </summary>
        public float CurrentPositionX;

        /// <summary>
        /// Current world position Y, synced from BotOwner.Position for pure-logic access.
        /// </summary>
        public float CurrentPositionY;

        /// <summary>
        /// Current world position Z, synced from BotOwner.Position for pure-logic access.
        /// </summary>
        public float CurrentPositionZ;

        // ── Formation Movement State ─────────────────────────────

        /// <summary>
        /// Current formation speed decision for this follower bot.
        /// </summary>
        public FormationSpeedDecision FormationSpeed;

        /// <summary>
        /// Whether this bot's boss is currently sprinting. Synced from boss state.
        /// </summary>
        public bool BossIsSprinting;

        /// <summary>
        /// Squared distance from this bot to its boss. Used for formation speed decisions.
        /// </summary>
        public float DistanceToBossSqr;

        /// <summary>
        /// Whether this bot is currently en route to its tactical formation position.
        /// </summary>
        public bool IsEnRouteFormation;

        // ── Voice Command State ─────────────────────────────────

        /// <summary>Time of last voice callout for cooldown tracking.</summary>
        public float LastCalloutTime;

        /// <summary>Previous IsInCombat value for edge detection.</summary>
        public bool PreviousIsInCombat;

        /// <summary>Pending callout to play after a delay (0 = none).</summary>
        public int PendingCalloutId;

        /// <summary>Game time at which to play the pending callout.</summary>
        public float PendingCalloutTime;

        // ── LOD State ─────────────────────────────────────────────

        /// <summary>LOD tier: 0=Full, 1=Reduced, 2=Minimal.</summary>
        public byte LodTier;

        /// <summary>Frame counter for LOD skip logic, incremented each HiveMind tick.</summary>
        public int LodFrameCounter;

        // ── Loot State ──────────────────────────────────────────────

        /// <summary>Whether this bot has an active loot target to pursue.</summary>
        public bool HasLootTarget;

        /// <summary>Unique ID of the current loot target (item ID hash or instance ID).</summary>
        public int LootTargetId;

        /// <summary>Loot target world position X.</summary>
        public float LootTargetX;

        /// <summary>Loot target world position Y.</summary>
        public float LootTargetY;

        /// <summary>Loot target world position Z.</summary>
        public float LootTargetZ;

        /// <summary>Loot target type (see <see cref="Systems.LootTargetType"/>).</summary>
        public byte LootTargetType;

        /// <summary>Estimated value of the current loot target.</summary>
        public float LootTargetValue;

        /// <summary>Cached available inventory grid slots. Synced periodically from InventoryController.</summary>
        public float InventorySpaceFree;

        /// <summary>Whether this bot is currently in a loot interaction (opening container, picking up item).</summary>
        public bool IsLooting;

        /// <summary>Whether this bot is currently moving toward a loot target.</summary>
        public bool IsApproachingLoot;

        // ── Vulture State ──────────────────────────────────────────────

        /// <summary>Whether a combat event was found nearby during the last HiveMind tick.</summary>
        public bool HasNearbyEvent;

        /// <summary>Nearest combat event X position.</summary>
        public float NearbyEventX;

        /// <summary>Nearest combat event Y position.</summary>
        public float NearbyEventY;

        /// <summary>Nearest combat event Z position.</summary>
        public float NearbyEventZ;

        /// <summary>Time of the nearest combat event.</summary>
        public float NearbyEventTime;

        /// <summary>Combat intensity at this bot's position (events within radius in time window).</summary>
        public int CombatIntensity;

        /// <summary>Whether the bot is within a recent boss activity zone.</summary>
        public bool IsInBossZone;

        /// <summary>Cooldown time — bot won't consider vulturing until game time exceeds this value.</summary>
        public float VultureCooldownUntil;

        /// <summary>Current vulture behavior phase. 0=None, see <see cref="Systems.VulturePhase"/>.</summary>
        public byte VulturePhase;

        // ── Personality + Raid Time ──────────────────────────────────

        /// <summary>
        /// Bot personality classification (see <see cref="BotPersonality"/> constants).
        /// Set once on registration from bot difficulty.
        /// </summary>
        public byte Personality;

        /// <summary>
        /// Aggression factor (0.0-1.0) derived from personality.
        /// Timid=0.1, Cautious=0.3, Normal=0.5, Aggressive=0.7, Reckless=0.9.
        /// </summary>
        public float Aggression;

        /// <summary>
        /// Normalized raid time (0.0 = raid start, 1.0 = raid end).
        /// Synced from game timer each HiveMind tick via BotEntityBridge.SyncQuestState().
        /// </summary>
        public float RaidTimeNormalized;

        // ── Investigate State ──────────────────────────────────────────────

        /// <summary>Whether this bot is currently investigating a gunfire event.</summary>
        public bool IsInvestigating;

        /// <summary>Game time at which the current investigation expires.</summary>
        public float InvestigateTimeoutAt;

        // ── Spawn Entry State ──────────────────────────────────────────────

        /// <summary>Game time when this bot spawned. Set once during RegisterBot.</summary>
        public float SpawnTime;

        /// <summary>Whether this bot has completed its spawn entry scan.</summary>
        public bool IsSpawnEntryComplete;

        /// <summary>Duration in seconds for the spawn entry pause (sampled from config + squad stagger).</summary>
        public float SpawnEntryDuration;

        /// <summary>Spawn facing direction X component (normalized).</summary>
        public float SpawnFacingX;

        /// <summary>Spawn facing direction Z component (normalized).</summary>
        public float SpawnFacingZ;

        /// <summary>Spawn facing direction bias factor. Decays linearly from 1.0 to 0 over config duration.</summary>
        public float SpawnFacingBias;

        // ── Room Clear State ──────────────────────────────────────────────

        /// <summary>Previous environment ID for transition detection. -1 = uninitialized.</summary>
        public int LastEnvironmentId = -1;

        /// <summary>Game time at which room clear mode expires.</summary>
        public float RoomClearUntil;

        /// <summary>Whether the bot is currently in room clear mode (indoor transition active).</summary>
        public bool IsInRoomClear;

        /// <summary>Game time at which the current corner pause expires.</summary>
        public float CornerPauseUntil;

        // ── Patrol State ──────────────────────────────────────────────

        /// <summary>Index into PatrolTask.CurrentMapRoutes, -1 = no route assigned.</summary>
        public int PatrolRouteIndex = -1;

        /// <summary>Current waypoint index within the assigned route.</summary>
        public int PatrolWaypointIndex = 0;

        /// <summary>Whether this bot is currently following a patrol route.</summary>
        public bool IsPatrolling = false;

        /// <summary>Game time when patrol cooldown expires (Time.time).</summary>
        public float PatrolCooldownUntil = 0f;

        /// <summary>Game time when the current waypoint pause ends (Time.time).</summary>
        public float PatrolPauseUntil = 0f;

        // ── Linger State ──────────────────────────────────────────────

        /// <summary>Game time when the bot's last objective was completed. 0 = no recent completion.</summary>
        public float ObjectiveCompletedTime;

        /// <summary>How long this bot should linger after completing an objective (seconds).</summary>
        public float LingerDuration;

        /// <summary>Whether this bot is currently in a lingering idle state.</summary>
        public bool IsLingering;

        /// <summary>Current game time, synced from Time.time for pure-logic scoring.</summary>
        public float CurrentGameTime;

        // ── Look Variance State ──────────────────────────────────────────────

        /// <summary>Game time when next flank check should occur.</summary>
        public float NextFlankCheckTime;

        /// <summary>Game time when next POI/event glance should occur.</summary>
        public float NextPoiGlanceTime;

        /// <summary>Bot's current facing direction X (normalized). Synced from movement.</summary>
        public float CurrentFacingX;

        /// <summary>Bot's current facing direction Z (normalized). Synced from movement.</summary>
        public float CurrentFacingZ;

        // ── Constructor ─────────────────────────────────────────

        public BotEntity(int id)
        {
            Id = id;
            IsActive = true;
            Followers = new List<BotEntity>(4);

            // Sensor defaults (match original sensor constructors)
            CanSprintToObjective = true; // BotHiveMindCanSprintToObjectiveSensor default
            LastLootingTime = DateTime.MinValue;

            // Quest state defaults
            DistanceToObjective = float.MaxValue;
        }

        // ── Hierarchy Queries ───────────────────────────────────

        /// <summary>Whether this entity has a boss assigned.</summary>
        public bool HasBoss => Boss != null;

        /// <summary>Whether this entity has any followers.</summary>
        public bool HasFollowers => Followers.Count > 0;

        // ── Sensor Access by Enum ───────────────────────────────

        /// <summary>
        /// Read a sensor boolean by <see cref="BotSensor"/> enum.
        /// Avoids per-sensor method duplication for group queries.
        /// </summary>
        public bool GetSensor(BotSensor sensor)
        {
            switch (sensor)
            {
                case BotSensor.InCombat:
                    return IsInCombat;
                case BotSensor.IsSuspicious:
                    return IsSuspicious;
                case BotSensor.CanQuest:
                    return CanQuest;
                case BotSensor.CanSprintToObjective:
                    return CanSprintToObjective;
                case BotSensor.WantsToLoot:
                    return WantsToLoot;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Write a sensor boolean by <see cref="BotSensor"/> enum.
        /// </summary>
        public void SetSensor(BotSensor sensor, bool value)
        {
            switch (sensor)
            {
                case BotSensor.InCombat:
                    IsInCombat = value;
                    break;
                case BotSensor.IsSuspicious:
                    IsSuspicious = value;
                    break;
                case BotSensor.CanQuest:
                    CanQuest = value;
                    break;
                case BotSensor.CanSprintToObjective:
                    CanSprintToObjective = value;
                    break;
                case BotSensor.WantsToLoot:
                    WantsToLoot = value;
                    break;
            }
        }

        // ── Group Sensor Queries (zero allocation) ──────────────

        /// <summary>
        /// Check if this bot's boss has a given sensor value set.
        /// Returns false if no boss is assigned.
        /// Replaces BotHiveMindAbstractSensor.CheckForBossOfBot.
        /// </summary>
        public bool CheckSensorForBoss(BotSensor sensor)
        {
            return Boss != null && Boss.GetSensor(sensor);
        }

        /// <summary>
        /// Check if any follower of this entity has a given sensor value set.
        /// Replaces BotHiveMindAbstractSensor.checkStateForAnyFollowers.
        /// </summary>
        public bool CheckSensorForAnyFollower(BotSensor sensor)
        {
            for (int i = 0; i < Followers.Count; i++)
            {
                if (Followers[i].GetSensor(sensor))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Check if any group member (boss or any of boss's followers) has a given sensor value set.
        /// The "group boss" is this entity's Boss, or this entity itself if it has no boss.
        /// Replaces BotHiveMindAbstractSensor.checkStateForAnyGroupMembers.
        /// </summary>
        public bool CheckSensorForGroup(BotSensor sensor)
        {
            var groupBoss = Boss ?? this;

            if (groupBoss.GetSensor(sensor))
                return true;

            for (int i = 0; i < groupBoss.Followers.Count; i++)
            {
                if (groupBoss.Followers[i].GetSensor(sensor))
                    return true;
            }

            return false;
        }

        // ── Equality ────────────────────────────────────────────

        public bool Equals(BotEntity other)
        {
            if (ReferenceEquals(other, null))
                return false;

            return Id == other.Id;
        }

        public override bool Equals(object obj) => Equals(obj as BotEntity);

        public override int GetHashCode() => Id;

        public override string ToString() =>
            $"BotEntity(Id={Id}, Type={BotType}, Active={IsActive}, Sleeping={IsSleeping}, "
            + $"Boss={Boss?.Id.ToString() ?? "none"}, Followers={Followers.Count})";
    }
}
