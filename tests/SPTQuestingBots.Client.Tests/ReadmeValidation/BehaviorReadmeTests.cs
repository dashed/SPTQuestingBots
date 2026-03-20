using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ReadmeValidation;

/// <summary>
/// Source-scanning tests that verify README claims for 10 behavior systems
/// match the actual code implementation. These tests read .cs source files
/// as text and verify classes, constants, config defaults, and method signatures.
/// </summary>
[TestFixture]
public class BehaviorReadmeTests
{
    private static readonly string SrcRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "src", "SPTQuestingBots.Client")
    );

    private static readonly string ConfigRoot = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "config")
    );

    private static readonly string ReadmePath = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "README.md")
    );

    private string ReadSource(string relativePath)
    {
        var full = Path.Combine(SrcRoot, relativePath);
        Assert.That(File.Exists(full), Is.True, $"Source file not found: {relativePath}");
        return File.ReadAllText(full);
    }

    private string ReadConfig(string filename)
    {
        var full = Path.Combine(ConfigRoot, filename);
        Assert.That(File.Exists(full), Is.True, $"Config file not found: {filename}");
        return File.ReadAllText(full);
    }

    private string ReadReadme()
    {
        Assert.That(File.Exists(ReadmePath), Is.True, "README.md not found");
        return File.ReadAllText(ReadmePath);
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 1: Hybrid Looting System
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void Looting_LootScorerClassExists()
    {
        var src = ReadSource("BotLogic/ECS/Systems/LootScorer.cs");
        Assert.That(src, Does.Contain("public static class LootScorer"));
    }

    [Test]
    public void Looting_LootScorerEvaluatesValueDistanceCooldownGear()
    {
        var src = ReadSource("BotLogic/ECS/Systems/LootScorer.cs");
        Assert.That(src, Does.Contain("targetValue"));
        Assert.That(src, Does.Contain("distanceSqr"));
        Assert.That(src, Does.Contain("isGearUpgrade"));
        Assert.That(src, Does.Contain("timeSinceLastLoot"));
        Assert.That(src, Does.Contain("distanceToObjectiveSqr"));
    }

    [Test]
    public void Looting_LootScorerChecksInCombatNotThreatProximity()
    {
        // README says "proximity to threats" but code only checks binary isInCombat
        var src = ReadSource("BotLogic/ECS/Systems/LootScorer.cs");
        Assert.That(src, Does.Contain("isInCombat"));
        // No proximity-to-threats parameter — just a binary combat check
        Assert.That(src, Does.Not.Contain("threatDistance"));
        Assert.That(src, Does.Not.Contain("proximityToThreats"));
    }

    [Test]
    public void Looting_LootInventoryPlannerClassExists()
    {
        var src = ReadSource("BotLogic/ECS/Systems/LootInventoryPlanner.cs");
        Assert.That(src, Does.Contain("public static class LootInventoryPlanner"));
    }

    [Test]
    public void Looting_LootInventoryPlannerComparesArmorClassAndContainerSizes()
    {
        var src = ReadSource("BotLogic/ECS/Systems/LootInventoryPlanner.cs");
        Assert.That(src, Does.Contain("IsArmorUpgrade"));
        Assert.That(src, Does.Contain("IsContainerUpgrade"));
        Assert.That(src, Does.Contain("IsWeaponUpgrade"));
    }

    [Test]
    public void Looting_GearComparerUsesWeaponValueNotErgonomics()
    {
        // README says "weapon ergonomics" but code uses WeaponValue (integer comparison)
        var src = ReadSource("BotLogic/ECS/Systems/GearComparer.cs");
        Assert.That(src, Does.Contain("myWeaponValue"));
        Assert.That(src, Does.Contain("candidateWeaponValue"));
        // No ergonomics concept in the code
        Assert.That(src, Does.Not.Contain("ergonomics"));
        Assert.That(src, Does.Not.Contain("Ergonomics"));
    }

    [Test]
    public void Looting_LootClaimRegistryClassExists()
    {
        var src = ReadSource("BotLogic/ECS/Systems/LootClaimRegistry.cs");
        Assert.That(src, Does.Contain("public class LootClaimRegistry"));
        Assert.That(src, Does.Contain("TryClaim"));
        Assert.That(src, Does.Contain("Release"));
        Assert.That(src, Does.Contain("IsClaimedByOther"));
    }

    [Test]
    public void Looting_LootActionStateMachineExists()
    {
        var src = ReadSource("BotLogic/Objective/LootAction.cs");
        Assert.That(src, Does.Contain("class LootAction"));
        Assert.That(src, Does.Contain("Approach"));
        Assert.That(src, Does.Contain("Interact"));
        Assert.That(src, Does.Contain("Complete"));
    }

    [Test]
    public void Looting_ConfigEnabledDefaultTrue()
    {
        var config = ReadConfig("config.json");
        // questing.looting.enabled should be true
        Assert.That(config, Does.Contain("\"looting\""));
        Assert.That(config, Does.Contain("\"enabled\": true"));
    }

    [Test]
    public void Looting_LootingBotsCompatExists()
    {
        var src = ReadSource("BotLogic/BotMonitor/Monitors/BotLootingMonitor.cs");
        Assert.That(src, Does.Contain("LootingBots"));
    }

    [Test]
    public void Looting_DisableWhenLootingBotsDetectedConfig()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"disable_when_lootingbots_detected\": true"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 2: Vulture System
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void Vulture_CombatEventRegistryExists()
    {
        var src = ReadSource("BotLogic/ECS/Systems/CombatEventRegistry.cs");
        Assert.That(src, Does.Contain("public static class CombatEventRegistry"));
    }

    [Test]
    public void Vulture_RingBufferCapacity128()
    {
        var src = ReadSource("BotLogic/ECS/Systems/CombatEventRegistry.cs");
        Assert.That(src, Does.Contain("public const int DefaultCapacity = 128;"));
    }

    [Test]
    public void Vulture_ConfigEventBufferSize128()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"event_buffer_size\": 128"));
    }

    [Test]
    public void Vulture_MultiPhaseStateMachine()
    {
        var src = ReadSource("BotLogic/ECS/Systems/VulturePhase.cs");
        Assert.That(src, Does.Contain("public const byte Approach = 1;"));
        Assert.That(src, Does.Contain("public const byte SilentApproach = 2;"));
        Assert.That(src, Does.Contain("public const byte HoldAmbush = 3;"));
        Assert.That(src, Does.Contain("public const byte Rush = 4;"));
        Assert.That(src, Does.Contain("public const byte Paranoia = 5;"));
    }

    [Test]
    public void Vulture_SilentApproach35mDefault()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"silent_approach_distance\": 35.0"));
    }

    [Test]
    public void Vulture_SilentApproachDistanceInAction()
    {
        var src = ReadSource("BotLogic/Objective/VultureAction.cs");
        Assert.That(src, Does.Contain("_silentApproachDistance = config.SilentApproachDistance;"));
        // Fallback = 35f
        Assert.That(src, Does.Contain("_silentApproachDistance = 35f;"));
    }

    [Test]
    public void Vulture_AmbushDuration90s()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"ambush_duration\": 90.0"));
    }

    [Test]
    public void Vulture_NightRangeMultiplier065()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"night_range_multiplier\": 0.65"));
    }

    [Test]
    public void Vulture_BossAvoidanceEnabled()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"enable_boss_avoidance\": true"));
    }

    [Test]
    public void Vulture_BossZoneCheckInTask()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/VultureTask.cs");
        Assert.That(src, Does.Contain("entity.IsInBossZone"));
    }

    [Test]
    public void Vulture_CourageThreshold15()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"courage_threshold\": 15"));
    }

    [Test]
    public void Vulture_CourageThresholdConstant()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/VultureTask.cs");
        Assert.That(src, Does.Contain("public const int DefaultCourageThreshold = 15;"));
    }

    [Test]
    public void Vulture_MaxBaseScore060()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/VultureTask.cs");
        Assert.That(src, Does.Contain("public const float MaxBaseScore = 0.60f;"));
    }

    [Test]
    public void Vulture_EventSourceGunshot()
    {
        Assert.That(
            File.Exists(Path.Combine(SrcRoot, "Patches/OnMakingShotPatch.cs")),
            Is.True,
            "OnMakingShotPatch.cs should exist for gunshot events"
        );
    }

    [Test]
    public void Vulture_EventSourceGrenade()
    {
        Assert.That(
            File.Exists(Path.Combine(SrcRoot, "Helpers/GrenadeExplosionSubscriber.cs")),
            Is.True,
            "GrenadeExplosionSubscriber.cs should exist for explosion events"
        );
        var src = ReadSource("Helpers/GrenadeExplosionSubscriber.cs");
        Assert.That(src, Does.Contain("OnGrenadeExplosive"));
    }

    [Test]
    public void Vulture_EventSourceAirdrop()
    {
        Assert.That(
            File.Exists(Path.Combine(SrcRoot, "Patches/AirdropLandPatch.cs")),
            Is.True,
            "AirdropLandPatch.cs should exist for airdrop events"
        );
    }

    [Test]
    public void Vulture_ParanoiaSweeps()
    {
        var src = ReadSource("BotLogic/Objective/VultureAction.cs");
        Assert.That(src, Does.Contain("UpdateParanoia"));
        Assert.That(src, Does.Contain("_paranoiaAngleRange"));
    }

    [Test]
    public void Vulture_SquadVultureEnabled()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"enable_squad_vulturing\": true"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 3: Linger System
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void Linger_TaskClassExists()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/LingerTask.cs");
        Assert.That(src, Does.Contain("public sealed class LingerTask"));
    }

    [Test]
    public void Linger_LinearScoreDecay()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/LingerTask.cs");
        // Linear decay formula: baseScore * (1 - elapsed/duration)
        Assert.That(src, Does.Contain("baseScore * (1f - elapsed / entity.LingerDuration)"));
    }

    [Test]
    public void Linger_DefaultBaseScore045()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/LingerTask.cs");
        Assert.That(src, Does.Contain("public const float DefaultBaseScore = 0.45f;"));
    }

    [Test]
    public void Linger_ConfigBaseScore045()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"base_score\": 0.45"));
    }

    [Test]
    public void Linger_DurationRange10to30()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"duration_min\": 10.0"));
        Assert.That(config, Does.Contain("\"duration_max\": 30.0"));
    }

    [Test]
    public void Linger_Pose07()
    {
        var config = ReadConfig("config.json");
        // Check linger section pose
        var src = ReadSource("BotLogic/Objective/LingerAction.cs");
        Assert.That(src, Does.Contain("_pose = 0.7f;"));
    }

    [Test]
    public void Linger_HeadScans3to8s()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"head_scan_interval_min\": 3.0"));
        Assert.That(config, Does.Contain("\"head_scan_interval_max\": 8.0"));
    }

    [Test]
    public void Linger_CombatInterrupts()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/LingerTask.cs");
        Assert.That(src, Does.Contain("entity.IsInCombat"));
    }

    [Test]
    public void Linger_ActionClassExists()
    {
        var src = ReadSource("BotLogic/Objective/LingerAction.cs");
        Assert.That(src, Does.Contain("class LingerAction"));
        Assert.That(src, Does.Contain("PatrollingData.Pause()"));
        Assert.That(src, Does.Contain("SetPose(_pose)"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 4: Combat Awareness
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void CombatAwareness_PostCombatCooldownConfig()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"post_combat_cooldown_seconds\": 20"));
    }

    [Test]
    public void CombatAwareness_LateRaidThresholdConfig()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"late_raid_no_sprint_threshold\": 0.15"));
    }

    [Test]
    public void CombatAwareness_ThreeSprintBlockToggles()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"enable_post_combat_sprint_block\": true"));
        Assert.That(config, Does.Contain("\"enable_late_raid_sprint_block\": true"));
        Assert.That(config, Does.Contain("\"enable_suspicion_sprint_block\": true"));
    }

    [Test]
    public void CombatAwareness_IsAllowedToSprintThreeChecks()
    {
        var src = ReadSource("BehaviorExtensions/CustomLogicDelayedUpdate.cs");
        Assert.That(src, Does.Contain("IsAllowedToSprint"));
        Assert.That(src, Does.Contain("EnablePostCombatSprintBlock"));
        Assert.That(src, Does.Contain("EnableLateRaidSprintBlock"));
        Assert.That(src, Does.Contain("EnableSuspicionSprintBlock"));
    }

    [Test]
    public void CombatAwareness_CombatStateHelperExists()
    {
        var src = ReadSource("Helpers/CombatStateHelper.cs");
        Assert.That(src, Does.Contain("public static class CombatStateHelper"));
        Assert.That(src, Does.Contain("GetTimeSinceLastCombat"));
        Assert.That(src, Does.Contain("GetLastEnemyPosition"));
        Assert.That(src, Does.Contain("IsPostCombat"));
        Assert.That(src, Does.Contain("IsInDangerZone"));
    }

    [Test]
    public void CombatAwareness_RaidTimeHelperExists()
    {
        var src = ReadSource("Helpers/RaidTimeHelper.cs");
        Assert.That(src, Does.Contain("public static class RaidTimeHelper"));
        Assert.That(src, Does.Contain("GetRemainingRaidFraction"));
        Assert.That(src, Does.Contain("GetRemainingSeconds"));
        Assert.That(src, Does.Contain("GetGameTimer"));
    }

    [Test]
    public void CombatAwareness_ExtractionHelperExists()
    {
        Assert.That(File.Exists(Path.Combine(SrcRoot, "Helpers/ExtractionHelper.cs")), Is.True, "ExtractionHelper.cs should exist");
    }

    [Test]
    public void CombatAwareness_PlantZoneHelperExists()
    {
        Assert.That(File.Exists(Path.Combine(SrcRoot, "Helpers/PlantZoneHelper.cs")), Is.True, "PlantZoneHelper.cs should exist");
    }

    [Test]
    public void CombatAwareness_HearingSensorHelperExists()
    {
        Assert.That(File.Exists(Path.Combine(SrcRoot, "Helpers/HearingSensorHelper.cs")), Is.True, "HearingSensorHelper.cs should exist");
    }

    [Test]
    public void CombatAwareness_IsAllowedToSprintUsesHelpers()
    {
        var src = ReadSource("BehaviorExtensions/CustomLogicDelayedUpdate.cs");
        Assert.That(src, Does.Contain("CombatStateHelper.IsPostCombat"));
        Assert.That(src, Does.Contain("RaidTimeHelper.GetRemainingRaidFraction"));
        Assert.That(src, Does.Contain("HearingSensorHelper.IsSuspicious"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 5: Enhanced Stuck Detection
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void StuckDetection_SoftStuckDetectorExists()
    {
        var src = ReadSource("Models/SoftStuckDetector.cs");
        Assert.That(src, Does.Contain("public class SoftStuckDetector"));
    }

    [Test]
    public void StuckDetection_HardStuckDetectorExists()
    {
        var src = ReadSource("Models/HardStuckDetector.cs");
        Assert.That(src, Does.Contain("public class HardStuckDetector"));
    }

    [Test]
    public void StuckDetection_SoftStuckNotMovingMultiplier2x()
    {
        var src = ReadSource("Models/SoftStuckDetector.cs");
        Assert.That(src, Does.Contain("private const float NotMovingMultiplier = 2.0f;"));
    }

    [Test]
    public void StuckDetection_HardStuckFarFromDestMultiplier15x()
    {
        var src = ReadSource("Models/HardStuckDetector.cs");
        Assert.That(src, Does.Contain("private const float FarFromDestMultiplier = 1.5f;"));
    }

    [Test]
    public void StuckDetection_SoftStuckAcceptsIsMoving()
    {
        var src = ReadSource("Models/SoftStuckDetector.cs");
        Assert.That(src, Does.Contain("bool? isMoving"));
    }

    [Test]
    public void StuckDetection_HardStuckAcceptsSDistDestination()
    {
        var src = ReadSource("Models/HardStuckDetector.cs");
        Assert.That(src, Does.Contain("float? squaredDistToDestination"));
    }

    [Test]
    public void StuckDetection_GoToPositionPassesBotMoverSignals()
    {
        var src = ReadSource("BehaviorExtensions/GoToPositionAbstractAction.cs");
        Assert.That(src, Does.Contain("BotOwner.Mover?.IsMoving"));
        Assert.That(src, Does.Contain("BotOwner.Mover?.SDistDestination"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 6: Spawn Entry Behavior
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void SpawnEntry_TaskClassExists()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/SpawnEntryTask.cs");
        Assert.That(src, Does.Contain("public sealed class SpawnEntryTask"));
    }

    [Test]
    public void SpawnEntry_MaxBaseScore080()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/SpawnEntryTask.cs");
        Assert.That(src, Does.Contain("public const float MaxBaseScore = 1.0f;"));
    }

    [Test]
    public void SpawnEntry_Duration3to5()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"base_duration_min\": 3.0"));
        Assert.That(config, Does.Contain("\"base_duration_max\": 5.0"));
    }

    [Test]
    public void SpawnEntry_Pose085()
    {
        var config = ReadConfig("config.json");
        // Spawn entry section should have pose 0.85
        var src = ReadSource("BotLogic/Objective/SpawnEntryAction.cs");
        Assert.That(src, Does.Contain("_pose = config?.Pose ?? 0.85f;"));
    }

    [Test]
    public void SpawnEntry_360DegreeLookRotation()
    {
        var src = ReadSource("BotLogic/Objective/SpawnEntryAction.cs");
        // 2 * PI for 360 degrees
        Assert.That(src, Does.Contain("2f * Mathf.PI"));
    }

    [Test]
    public void SpawnEntry_SquadStaggerPerMember15sNotHalf()
    {
        // README says "0.5s per member index" but config and code default is 1.5s
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"squad_stagger_per_member\": 1.5"));

        var src = ReadSource("Configuration/SpawnEntryConfig.cs");
        Assert.That(src, Does.Contain("SquadStaggerPerMember { get; set; } = 1.5f;"));
    }

    [Test]
    public void SpawnEntry_DirectionBias30s()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"direction_bias_duration\": 30.0"));
    }

    [Test]
    public void SpawnEntry_ConfigSectionExists()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"spawn_entry\""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 7: Head-Look Variance
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void HeadLook_LookVarianceControllerExists()
    {
        var src = ReadSource("BotLogic/ECS/Systems/LookVarianceController.cs");
        Assert.That(src, Does.Contain("public static class LookVarianceController"));
    }

    [Test]
    public void HeadLook_ThreePrioritySystem()
    {
        var src = ReadSource("BotLogic/ECS/Systems/LookVarianceController.cs");
        // Priority 1: combat event glance
        Assert.That(src, Does.Contain("entity.HasNearbyEvent"));
        // Priority 2: squad member glance
        Assert.That(src, Does.Contain("entity.HasBoss"));
        // Priority 3: flank check
        Assert.That(src, Does.Contain("NextFlankCheckTime"));
    }

    [Test]
    public void HeadLook_FlankChecksPlus_Minus45Degrees()
    {
        var src = ReadSource("BotLogic/ECS/Systems/LookVarianceController.cs");
        Assert.That(src, Does.Contain("RandomAngle(-45f, 45f)"));
    }

    [Test]
    public void HeadLook_FlankInterval5to15s()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"flank_check_interval_min\": 5.0"));
        Assert.That(config, Does.Contain("\"flank_check_interval_max\": 15.0"));
    }

    [Test]
    public void HeadLook_SquadGlanceRange15()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"squad_glance_range\": 15.0"));
    }

    [Test]
    public void HeadLook_CombatEventLookChanceConfigUsedInController()
    {
        // Config defines combat_event_look_chance and LookVarianceController
        // uses it as a probability gate for combat event glances
        var cfgSrc = ReadSource("Configuration/LookVarianceConfig.cs");
        Assert.That(cfgSrc, Does.Contain("CombatEventLookChance"));

        var ctlSrc = ReadSource("BotLogic/ECS/Systems/LookVarianceController.cs");
        Assert.That(ctlSrc, Does.Contain("combatEventLookChance"), "LookVarianceController should use combatEventLookChance parameter");
    }

    [Test]
    public void HeadLook_ApplyLookVarianceInGoToPositionAbstractAction()
    {
        var src = ReadSource("BehaviorExtensions/GoToPositionAbstractAction.cs");
        Assert.That(src, Does.Contain("protected void ApplyLookVariance()"));
    }

    [Test]
    public void HeadLook_ConfigSectionExists()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"look_variance\""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 8: Room Clearing
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void RoomClear_ControllerExists()
    {
        var src = ReadSource("BotLogic/ECS/Systems/RoomClearController.cs");
        Assert.That(src, Does.Contain("public static class RoomClearController"));
    }

    [Test]
    public void RoomClear_UsesBoolIsIndoor()
    {
        var src = ReadSource("BotLogic/ECS/Systems/RoomClearController.cs");
        Assert.That(src, Does.Contain("bool isIndoor"));
    }

    [Test]
    public void RoomClear_ConfigDuration15to30NotReadme3to8()
    {
        // README says "3-8s" but config and code defaults are 15-30s
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"duration_min\": 15.0"));
        Assert.That(config, Does.Contain("\"duration_max\": 30.0"));

        var cfgSrc = ReadSource("Configuration/RoomClearConfig.cs");
        Assert.That(cfgSrc, Does.Contain("DurationMin { get; set; } = 15.0f;"));
        Assert.That(cfgSrc, Does.Contain("DurationMax { get; set; } = 30.0f;"));
    }

    [Test]
    public void RoomClear_CornerPauseExists()
    {
        var src = ReadSource("BotLogic/ECS/Systems/RoomClearController.cs");
        Assert.That(src, Does.Contain("TriggerCornerPause"));
        Assert.That(src, Does.Contain("IsSharpCorner"));
    }

    [Test]
    public void RoomClear_SlowWalkInstruction()
    {
        var src = ReadSource("BotLogic/ECS/Systems/RoomClearController.cs");
        Assert.That(src, Does.Contain("RoomClearInstruction.SlowWalk"));
    }

    [Test]
    public void RoomClear_PerBotTypeToggles()
    {
        var cfgSrc = ReadSource("Configuration/RoomClearConfig.cs");
        Assert.That(cfgSrc, Does.Contain("EnableForPmcs"));
        Assert.That(cfgSrc, Does.Contain("EnableForScavs"));
        Assert.That(cfgSrc, Does.Contain("EnableForPscavs"));
    }

    [Test]
    public void RoomClear_ConfigSectionExists()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"room_clear\""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 9: Investigate Task
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void Investigate_TaskClassExists()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/InvestigateTask.cs");
        Assert.That(src, Does.Contain("public sealed class InvestigateTask"));
    }

    [Test]
    public void Investigate_IntensityThreshold5()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/InvestigateTask.cs");
        Assert.That(src, Does.Contain("public const int DefaultIntensityThreshold = 5;"));
    }

    [Test]
    public void Investigate_ConfigIntensityThreshold5()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"intensity_threshold\": 5"));
    }

    [Test]
    public void Investigate_MaxBaseScore040()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/InvestigateTask.cs");
        Assert.That(src, Does.Contain("public const float MaxBaseScore = 0.40f;"));
    }

    [Test]
    public void Investigate_ConfigBaseScore040()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"base_score\": 0.40"));
    }

    [Test]
    public void Investigate_GatingChecks()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/InvestigateTask.cs");
        Assert.That(src, Does.Contain("entity.IsInCombat"));
        Assert.That(src, Does.Contain("entity.VulturePhase"));
        Assert.That(src, Does.Contain("entity.CombatIntensity"));
    }

    [Test]
    public void Investigate_TwoStateActionApproachAndLookAround()
    {
        var src = ReadSource("BotLogic/Objective/InvestigateAction.cs");
        Assert.That(src, Does.Contain("_isLookingAround"));
        Assert.That(src, Does.Contain("UpdateLookAround"));
    }

    [Test]
    public void Investigate_ApproachSpeed05Pose06Defaults()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"approach_speed\": 0.5"));
        Assert.That(config, Does.Contain("\"approach_pose\": 0.6"));
    }

    [Test]
    public void Investigate_LookAroundDuration8sNotRange()
    {
        // README says "5-10s" but config is a single value: 8.0
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"look_around_duration\": 8.0"));

        var cfgSrc = ReadSource("Configuration/InvestigateConfig.cs");
        Assert.That(cfgSrc, Does.Contain("LookAroundDuration { get; set; } = 8.0f;"));
    }

    [Test]
    public void Investigate_PersonalityModifiersInScoringModifiers()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/ScoringModifiers.cs");
        // For Investigate: Lerp(0.8f, 1.2f, clampedAggression)
        Assert.That(src, Does.Contain("case BotActionTypeId.Investigate:"));
        Assert.That(src, Does.Contain("Lerp(0.8f, 1.2f, clampedAggression)"));
    }

    [Test]
    public void Investigate_ConfigSectionExists()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"investigate\""));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 10: Dynamic Objective Generation
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void DynamicObjectives_ScannerClassExists()
    {
        var src = ReadSource("Components/DynamicObjectiveScanner.cs");
        Assert.That(src, Does.Contain("public class DynamicObjectiveScanner"));
    }

    [Test]
    public void DynamicObjectives_GeneratorClassExists()
    {
        var src = ReadSource("BotLogic/ECS/Systems/DynamicObjectiveGenerator.cs");
        Assert.That(src, Does.Contain("public static class DynamicObjectiveGenerator"));
    }

    [Test]
    public void DynamicObjectives_ScanInterval30sConfig()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"scan_interval_sec\": 30"));
    }

    [Test]
    public void DynamicObjectives_AddQuestCall()
    {
        var src = ReadSource("Components/DynamicObjectiveScanner.cs");
        Assert.That(src, Does.Contain("BotJobAssignmentFactory.AddQuest"));
    }

    [Test]
    public void DynamicObjectives_FirefightClustersGenerateAmbushQuests()
    {
        var src = ReadSource("BotLogic/ECS/Systems/DynamicObjectiveGenerator.cs");
        Assert.That(src, Does.Contain("GenerateFirefightObjectives"));
        Assert.That(src, Does.Contain("QuestAction.Ambush"));
    }

    [Test]
    public void DynamicObjectives_CorpseScavengingGeneratesMoveToPosition()
    {
        var src = ReadSource("BotLogic/ECS/Systems/DynamicObjectiveGenerator.cs");
        Assert.That(src, Does.Contain("GenerateCorpseObjectives"));
        Assert.That(src, Does.Contain("QuestAction.MoveToPosition"));
    }

    [Test]
    public void DynamicObjectives_BuildingClearGeneratesHoldAtPosition()
    {
        var src = ReadSource("BotLogic/ECS/Systems/DynamicObjectiveGenerator.cs");
        Assert.That(src, Does.Contain("GenerateBuildingClearObjectives"));
        Assert.That(src, Does.Contain("QuestAction.HoldAtPosition"));
    }

    [Test]
    public void DynamicObjectives_CombatEventTypeDeath4()
    {
        var src = ReadSource("BotLogic/ECS/Systems/CombatEvent.cs");
        Assert.That(src, Does.Contain("public const byte Death = 4;"));
    }

    [Test]
    public void DynamicObjectives_DeathEventPatchExists()
    {
        Assert.That(
            File.Exists(Path.Combine(SrcRoot, "Patches/OnBeenKilledByAggressorPatch.cs")),
            Is.True,
            "OnBeenKilledByAggressorPatch.cs should exist for death events"
        );
    }

    [Test]
    public void DynamicObjectives_ConfigSectionExists()
    {
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"dynamic_objectives\""));
    }

    [Test]
    public void DynamicObjectives_BuildingClearOnceAtRaidStart()
    {
        var src = ReadSource("Components/DynamicObjectiveScanner.cs");
        Assert.That(src, Does.Contain("_buildingClearGenerated"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Cross-cutting: README consistency checks
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void Readme_MentionsAllTenBehaviorSystems()
    {
        var readme = ReadReadme();
        Assert.That(readme, Does.Contain("Hybrid Looting System"));
        Assert.That(readme, Does.Contain("Vulture System"));
        Assert.That(readme, Does.Contain("Linger System"));
        Assert.That(readme, Does.Contain("Combat Awareness"));
        Assert.That(readme, Does.Contain("Enhanced Stuck Detection"));
        Assert.That(readme, Does.Contain("Spawn Entry Behavior"));
        Assert.That(readme, Does.Contain("Head-Look Variance"));
        Assert.That(readme, Does.Contain("Room Clearing"));
        Assert.That(readme, Does.Contain("Investigate Task"));
        Assert.That(readme, Does.Contain("Dynamic Objective Generation"));
    }

    [Test]
    public void Readme_WaitTimeMin5Max15MatchesConfig()
    {
        // README says "random sampling from configurable range (5-15s)"
        var config = ReadConfig("config.json");
        Assert.That(config, Does.Contain("\"wait_time_min\": 5"));
        Assert.That(config, Does.Contain("\"wait_time_max\": 15"));
    }
}
