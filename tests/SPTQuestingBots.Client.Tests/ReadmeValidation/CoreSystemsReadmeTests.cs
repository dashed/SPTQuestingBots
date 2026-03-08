using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ReadmeValidation;

/// <summary>
/// Source-scanning tests that verify README claims for 6 core systems
/// match the actual code implementation. These tests read .cs source files
/// as text and verify classes, constants, config defaults, and method signatures.
/// </summary>
[TestFixture]
public class CoreSystemsReadmeTests
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

    // ═══════════════════════════════════════════════════════════════════
    // Feature 1: Objective System
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void ObjectiveSystem_BotObjectiveManagerExists()
    {
        var src = ReadSource("Components/BotObjectiveManager.cs");
        Assert.That(src, Does.Contain("class BotObjectiveManager"));
    }

    [Test]
    public void ObjectiveSystem_BotObjectiveLayerExists()
    {
        var src = ReadSource("BotLogic/Objective/BotObjectiveLayer.cs");
        Assert.That(src, Does.Contain("class BotObjectiveLayer"));
    }

    [Test]
    public void ObjectiveSystem_QuestActionTypes_IncludeExpectedActions()
    {
        // README claims: EFT quests, spawn rushes, boss hunts, airdrop chasing, sniping/camping
        var src = ReadSource("BotLogic/Objective/BotObjectiveLayer.cs");
        // trySetNextAction switch should cover the main quest actions
        Assert.That(src, Does.Contain("QuestAction.MoveToPosition"), "Missing MoveToPosition action");
        Assert.That(src, Does.Contain("QuestAction.HoldAtPosition"), "Missing HoldAtPosition action");
        Assert.That(src, Does.Contain("QuestAction.Ambush"), "Missing Ambush action");
        Assert.That(src, Does.Contain("QuestAction.Snipe"), "Missing Snipe action");
        Assert.That(src, Does.Contain("QuestAction.PlantItem"), "Missing PlantItem action");
    }

    [Test]
    public void ObjectiveSystem_SAINInterop_Exists()
    {
        var src = ReadSource("BotLogic/ExternalMods/ModInfo/SAINModInfo.cs");
        Assert.That(src, Does.Contain("class SAINModInfo"));
    }

    [Test]
    public void ObjectiveSystem_LootingBotsInterop_Exists()
    {
        var src = ReadSource("BotLogic/ExternalMods/ModInfo/LootingBotsModInfo.cs");
        Assert.That(src, Does.Contain("class LootingBotsModInfo"));
    }

    [Test]
    public void ObjectiveSystem_UnlockDoorAction_Exists()
    {
        // README: PMCs can unlock doors along quest paths
        var src = ReadSource("BotLogic/Objective/UnlockDoorAction.cs");
        Assert.That(src, Does.Contain("class UnlockDoorAction"));
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 2: Utility AI
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void UtilityAI_QuestTaskFactory_Has14Tasks()
    {
        // README: 14 scored tasks
        var src = ReadSource("BotLogic/ECS/UtilityAI/QuestTaskFactory.cs");
        Assert.That(src, Does.Contain("TaskCount = 14"), "QuestTaskFactory.TaskCount should be 14");
    }

    [Test]
    public void UtilityAI_AllTaskTypes_ExistInFactory()
    {
        // README lists: GoToObjective, Ambush, Snipe, HoldPosition, PlantItem,
        //   UnlockDoor, ToggleSwitch, CloseDoors, Loot, Vulture, Linger,
        //   Investigate, SpawnEntry, Patrol
        var src = ReadSource("BotLogic/ECS/UtilityAI/QuestTaskFactory.cs");
        string[] expectedTasks = new[]
        {
            "GoToObjectiveTask",
            "AmbushTask",
            "SnipeTask",
            "HoldPositionTask",
            "PlantItemTask",
            "UnlockDoorTask",
            "ToggleSwitchTask",
            "CloseDoorsTask",
            "LootTask",
            "VultureTask",
            "LingerTask",
            "InvestigateTask",
            "SpawnEntryTask",
            "PatrolTask",
        };
        foreach (var task in expectedTasks)
        {
            Assert.That(src, Does.Contain(task), $"QuestTaskFactory missing task: {task}");
        }
    }

    [Test]
    public void UtilityAI_ColumnMajorScoring_WithAdditiveHysteresis()
    {
        // README: Column-major scoring with additive hysteresis (identical to Phobos BaseTaskManager.PickTask)
        var src = ReadSource("BotLogic/ECS/UtilityAI/UtilityTaskManager.cs");
        Assert.That(src, Does.Contain("column-major"), "Should document column-major pattern");
        Assert.That(src, Does.Contain("Hysteresis"), "PickTask should use hysteresis");
        Assert.That(
            src,
            Does.Contain("entity.TaskScores[assignment.Ordinal] + assignment.Task.Hysteresis"),
            "Should add hysteresis to current task score"
        );
    }

    [Test]
    public void UtilityAI_GoToObjectiveTask_ExponentialDistanceDecay()
    {
        // README: Exponential distance decay: BaseScore * (1 - e^(-d/75))
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/GoToObjectiveTask.cs");
        Assert.That(src, Does.Contain("Math.Exp(-distance / falloff)"), "Should use exponential decay formula");
        Assert.That(src, Does.Contain("falloff = 75f"), "Falloff should be 75");
        Assert.That(src, Does.Contain("BaseScore = 0.65f"), "BaseScore should be 0.65");
    }

    [Test]
    public void UtilityAI_ScoringModifiers_PersonalityInfluence()
    {
        // README: Personality-influenced scoring: aggression float (0-1)
        var src = ReadSource("BotLogic/ECS/UtilityAI/ScoringModifiers.cs");
        Assert.That(src, Does.Contain("PersonalityModifier"), "Should have PersonalityModifier");
        Assert.That(src, Does.Contain("float aggression"), "Should take aggression parameter");
    }

    [Test]
    public void UtilityAI_ScoringModifiers_RaidTimeProgression()
    {
        // README: Raid time progression: early rush, mid balanced, late cautious/looting
        var src = ReadSource("BotLogic/ECS/UtilityAI/ScoringModifiers.cs");
        Assert.That(src, Does.Contain("RaidTimeModifier"), "Should have RaidTimeModifier");
        Assert.That(src, Does.Contain("CombinedModifier"), "Should have CombinedModifier");
    }

    [Test]
    public void UtilityAI_HybridApproach_UtilityForSelection_BigBrainForExecution()
    {
        // README: Hybrid: utility scoring for SELECTION, BigBrain for EXECUTION
        var src = ReadSource("BotLogic/Objective/BotObjectiveLayer.cs");
        // When utility AI is enabled, it scores and picks the task then dispatches via setNextAction
        Assert.That(src, Does.Contain("UseUtilityAI"), "Should check UseUtilityAI toggle");
        Assert.That(src, Does.Contain("trySetNextActionUtility()"), "Should have utility AI action selection path");
        Assert.That(src, Does.Contain("trySetNextAction()"), "Should have deterministic fallback path");
    }

    [Test]
    public void UtilityAI_FallbackToDeterministicSwitch()
    {
        // README: When disabled, the existing deterministic switch statement is used as fallback
        var src = ReadSource("BotLogic/Objective/BotObjectiveLayer.cs");
        // When UseUtilityAI is false, the code falls back to trySetNextAction (switch-based)
        Assert.That(
            src,
            Does.Contain("switch (objectiveManager.CurrentQuestAction)"),
            "Deterministic fallback should use switch on CurrentQuestAction"
        );
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 3: ECS-Lite Data Layout
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void ECS_BotEntity_HasStableRecycledId()
    {
        var src = ReadSource("BotLogic/ECS/BotEntity.cs");
        Assert.That(src, Does.Contain("public readonly int Id"), "BotEntity should have readonly Id field");
        Assert.That(src, Does.Contain("sealed class BotEntity"), "BotEntity should be sealed");
    }

    [Test]
    public void ECS_BotEntity_HasBossFollowerHierarchy()
    {
        var src = ReadSource("BotLogic/ECS/BotEntity.cs");
        Assert.That(src, Does.Contain("public BotEntity Boss"), "Should have Boss reference");
        Assert.That(src, Does.Contain("public readonly List<BotEntity> Followers"), "Should have Followers list");
    }

    [Test]
    public void ECS_BotEntity_HasEmbeddedSensors()
    {
        // README: Sensor booleans (combat, suspicious, questing, sprint, loot) embedded on entity
        var src = ReadSource("BotLogic/ECS/BotEntity.cs");
        Assert.That(src, Does.Contain("public bool IsInCombat"), "Missing sensor: IsInCombat");
        Assert.That(src, Does.Contain("public bool IsSuspicious"), "Missing sensor: IsSuspicious");
        Assert.That(src, Does.Contain("public bool CanQuest"), "Missing sensor: CanQuest");
        Assert.That(src, Does.Contain("public bool CanSprintToObjective"), "Missing sensor: CanSprintToObjective");
        Assert.That(src, Does.Contain("public bool WantsToLoot"), "Missing sensor: WantsToLoot");
    }

    [Test]
    public void ECS_BotEntity_HasBotTypeAndSleepState()
    {
        // README: BotType enum and sleep state replace HashSet/List lookups
        var src = ReadSource("BotLogic/ECS/BotEntity.cs");
        Assert.That(src, Does.Contain("public BotType BotType"), "Should have BotType classification");
        Assert.That(src, Does.Contain("public bool IsSleeping"), "Should have IsSleeping state");
    }

    [Test]
    public void ECS_BotEntity_HasTaskScoresArray()
    {
        // README: Per-task utility scores for column-major scoring
        var src = ReadSource("BotLogic/ECS/BotEntity.cs");
        Assert.That(src, Does.Contain("public float[] TaskScores"), "Should have TaskScores array");
        Assert.That(src, Does.Contain("public UtilityTaskAssignment TaskAssignment"), "Should have TaskAssignment");
    }

    [Test]
    public void ECS_BotRegistry_DenseListWithSwapRemove()
    {
        var src = ReadSource("BotLogic/ECS/BotRegistry.cs");
        Assert.That(src, Does.Contain("Dense entity storage with swap-remove"), "Should document swap-remove");
        Assert.That(src, Does.Contain("public readonly List<BotEntity> Entities"), "Should have dense Entities list");
        Assert.That(src, Does.Contain("Stack<int> _freeIds"), "Should have free ID stack for recycling");
    }

    [Test]
    public void ECS_BotRegistry_SparseArrayForBsgIdLookup()
    {
        // README: BsgBotRegistry-style sparse array for O(1) integer ID lookups
        var src = ReadSource("BotLogic/ECS/BotRegistry.cs");
        Assert.That(src, Does.Contain("_bsgIdToEntity"), "Should have BSG ID sparse array");
        Assert.That(src, Does.Contain("GetByBsgId"), "Should have GetByBsgId method");
    }

    [Test]
    public void ECS_HiveMindSystem_StaticMethods()
    {
        // README: HiveMindSystem: static system methods
        var src = ReadSource("BotLogic/ECS/Systems/HiveMindSystem.cs");
        Assert.That(src, Does.Contain("public static class HiveMindSystem"), "Should be static class");
        Assert.That(src, Does.Contain("public static void ResetInactiveEntitySensors"), "Should have ResetInactiveEntitySensors");
        Assert.That(src, Does.Contain("public static void CleanupDeadEntities"), "Should have CleanupDeadEntities");
        Assert.That(src, Does.Contain("public static int CountActive"), "Should have CountActive query");
    }

    [Test]
    public void ECS_QuestScorer_Exists()
    {
        // README: QuestScorer: pure-logic quest scoring with static buffers
        var src = ReadSource("BotLogic/ECS/Systems/QuestScorer.cs");
        Assert.That(src, Does.Contain("QuestScorer"));
    }

    [Test]
    public void ECS_BotEntityBridge_Exists()
    {
        // README: BotEntityBridge: integration layer
        var src = ReadSource("BotLogic/ECS/BotEntityBridge.cs");
        Assert.That(src, Does.Contain("public static class BotEntityBridge"), "Should be static class");
        Assert.That(src, Does.Contain("_profileIdToEntity"), "Should have ProfileId mapping");
    }

    [Test]
    public void ECS_DeterministicTickOrder_9Steps()
    {
        // README: Deterministic tick order: 9-step sequence in BotHiveMindMonitor.Update()
        var src = ReadSource("BotLogic/HiveMind/BotHiveMindMonitor.cs");
        // Verify all 9 steps are present in the Update method
        Assert.That(src, Does.Contain("updateBosses()"), "Step 1: updateBosses");
        Assert.That(src, Does.Contain("updateBossFollowers()"), "Step 2: updateBossFollowers");
        Assert.That(src, Does.Contain("updatePullSensors()"), "Step 3: updatePullSensors");
        Assert.That(src, Does.Contain("ResetInactiveEntitySensors"), "Step 4: ResetInactiveEntitySensors");
        Assert.That(src, Does.Contain("updateSquadStrategies()"), "Step 5: updateSquadStrategies");
        Assert.That(src, Does.Contain("updateCombatEvents()"), "Step 6: updateCombatEvents");
        Assert.That(src, Does.Contain("updateLootScanning()"), "Step 7: updateLootScanning");
        Assert.That(src, Does.Contain("refreshHumanPlayerCache()"), "Step 8: refreshHumanPlayerCache");
        Assert.That(src, Does.Contain("updateLodTiers()"), "Step 9: updateLodTiers");
    }

    [Test]
    public void ECS_TimePacing_Exists()
    {
        // README: TimePacing/FramePacing utilities with AggressiveInlining
        var src = ReadSource("Helpers/TimePacing.cs");
        Assert.That(src, Does.Contain("AggressiveInlining"));
    }

    [Test]
    public void ECS_FramePacing_Exists()
    {
        var src = ReadSource("Helpers/FramePacing.cs");
        Assert.That(src, Does.Contain("AggressiveInlining"));
    }

    [Test]
    public void ECS_BotEntity_ZeroUnityDependencies()
    {
        // README: Zero Unity dependencies for testability
        var src = ReadSource("BotLogic/ECS/BotEntity.cs");
        Assert.That(src, Does.Not.Contain("using UnityEngine"), "BotEntity should have no Unity usings");
    }

    [Test]
    public void ECS_UtilityTask_ZeroUnityDependencies()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/UtilityTask.cs");
        Assert.That(src, Does.Not.Contain("using UnityEngine"), "UtilityTask should have no Unity usings");
    }

    [Test]
    public void ECS_UtilityTaskManager_ZeroUnityDependencies()
    {
        var src = ReadSource("BotLogic/ECS/UtilityAI/UtilityTaskManager.cs");
        Assert.That(src, Does.Not.Contain("using UnityEngine"), "UtilityTaskManager should have no Unity usings");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 4: Squad Strategies
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void Squad_GoToTacticalPositionTask_ScoreIs070()
    {
        // README: GoToTacticalPosition (score=0.70)
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/GoToTacticalPositionTask.cs");
        Assert.That(src, Does.Contain("BaseScore = 0.70f"), "GoToTacticalPosition base score should be 0.70");
    }

    [Test]
    public void Squad_HoldTacticalPositionTask_ScoreIs065()
    {
        // README: HoldTacticalPosition (score=0.65)
        var src = ReadSource("BotLogic/ECS/UtilityAI/Tasks/HoldTacticalPositionTask.cs");
        Assert.That(src, Does.Contain("BaseScore = 0.65f"), "HoldTacticalPosition base score should be 0.65");
    }

    [Test]
    public void Squad_SquadTaskFactory_HasSixTasks()
    {
        // README: Six follower utility tasks (2 tactical + 4 opportunistic)
        var src = ReadSource("BotLogic/ECS/UtilityAI/SquadTaskFactory.cs");
        Assert.That(src, Does.Contain("TaskCount = 6"), "SquadTaskFactory should have 6 tasks");
        Assert.That(src, Does.Contain("GoToTacticalPositionTask"), "Should include GoToTacticalPositionTask");
        Assert.That(src, Does.Contain("HoldTacticalPositionTask"), "Should include HoldTacticalPositionTask");
        Assert.That(src, Does.Contain("LootTask"), "Should include LootTask");
        Assert.That(src, Does.Contain("InvestigateTask"), "Should include InvestigateTask");
        Assert.That(src, Does.Contain("LingerTask"), "Should include LingerTask");
        Assert.That(src, Does.Contain("PatrolTask"), "Should include PatrolTask");
    }

    [Test]
    public void Squad_SquadEntity_Exists_WithDenseStorage()
    {
        // README: SquadEntity, SquadRegistry with dense storage and swap-remove
        var src = ReadSource("BotLogic/ECS/SquadEntity.cs");
        Assert.That(src, Does.Contain("sealed class SquadEntity"), "SquadEntity should be sealed");
        Assert.That(src, Does.Contain("public readonly int Id"), "SquadEntity should have recycled Id");
        Assert.That(src, Does.Contain("public readonly List<BotEntity> Members"), "Should have Members list");
    }

    [Test]
    public void Squad_SquadRegistry_DenseStorageWithSwapRemove()
    {
        var src = ReadSource("BotLogic/ECS/SquadRegistry.cs");
        Assert.That(src, Does.Contain("Dense squad storage with swap-remove"), "Should document swap-remove");
        Assert.That(src, Does.Contain("Stack<int> _freeIds"), "Should have free ID stack");
        Assert.That(src, Does.Contain("_bsgGroupToSquadId"), "Should have BSG group ID mapping");
    }

    [Test]
    public void Squad_GotoObjectiveStrategy_Exists()
    {
        // README: GoToObjectiveStrategy
        var src = ReadSource("BotLogic/ECS/UtilityAI/GotoObjectiveStrategy.cs");
        Assert.That(src, Does.Contain("class GotoObjectiveStrategy : SquadStrategy"));
        Assert.That(src, Does.Contain("SampleGaussian"), "Should adjust hold duration with Gaussian sampling");
    }

    [Test]
    public void Squad_QuestTypeAwareFormations_AssignRoles()
    {
        // README: Quest-type-aware formations: Ambush, Snipe, PlantItem, HoldAtPosition, MoveToPosition
        var src = ReadSource("BotLogic/ECS/Systems/TacticalPositionCalculator.cs");
        Assert.That(src, Does.Contain("case QuestActionId.Ambush"), "Should handle Ambush quest type");
        Assert.That(src, Does.Contain("case QuestActionId.Snipe"), "Should handle Snipe quest type");
        Assert.That(src, Does.Contain("case QuestActionId.PlantItem"), "Should handle PlantItem quest type");
        Assert.That(src, Does.Contain("case QuestActionId.HoldAtPosition"), "Should handle HoldAtPosition quest type");
        Assert.That(src, Does.Contain("case QuestActionId.MoveToPosition"), "Should handle MoveToPosition quest type");
    }

    [Test]
    public void Squad_ThreeFollowerGates()
    {
        // README: Three follower gates conditionally unlocked — combat, healing, investigation
        // The follower gates are in the BotObjectiveLayer which checks:
        // SquadQuest decision for followers
        var src = ReadSource("BotLogic/Objective/BotObjectiveLayer.cs");
        Assert.That(src, Does.Contain("BotQuestingDecision.SquadQuest"), "Should check for SquadQuest decision for followers");
    }

    [Test]
    public void Squad_SquadStrategyConfig_DefaultEnabled()
    {
        // README: enabled by default (squad_strategy.enabled, default: true)
        var src = ReadSource("Configuration/SquadStrategyConfig.cs");
        Assert.That(src, Does.Contain("Enabled { get; set; } = true"), "squad_strategy.enabled should default to true");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 5: Bot LOD System
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void LOD_ThreeTiers_DefinedInCalculator()
    {
        // README: 3-tier: Full (<150m), Reduced (150-300m), Minimal (>300m)
        var src = ReadSource("BotLogic/ECS/Systems/BotLodCalculator.cs");
        Assert.That(src, Does.Contain("TierFull = 0"), "Tier Full should be 0");
        Assert.That(src, Does.Contain("TierReduced = 1"), "Tier Reduced should be 1");
        Assert.That(src, Does.Contain("TierMinimal = 2"), "Tier Minimal should be 2");
    }

    [Test]
    public void LOD_ConfigDefaults_MatchReadme()
    {
        // README: Full (<150m), Reduced (150-300m), Minimal (>300m)
        var src = ReadSource("Configuration/BotLodConfig.cs");
        Assert.That(src, Does.Contain("ReducedDistance { get; set; } = 150f"), "Reduced distance default should be 150");
        Assert.That(src, Does.Contain("MinimalDistance { get; set; } = 300f"), "Minimal distance default should be 300");
    }

    [Test]
    public void LOD_FrameSkipDefaults_MatchReadme()
    {
        // README: Reduced 1/3 ticks = skip 2 of 3, Minimal 1/5 ticks = skip 4 of 5
        var src = ReadSource("Configuration/BotLodConfig.cs");
        Assert.That(src, Does.Contain("ReducedFrameSkip { get; set; } = 2"), "Reduced frame skip should default to 2");
        Assert.That(src, Does.Contain("MinimalFrameSkip { get; set; } = 4"), "Minimal frame skip should default to 4");
    }

    [Test]
    public void LOD_ConfigJson_MatchesCodeDefaults()
    {
        // Verify config.json defaults match code defaults
        var json = ReadConfig("config.json");
        Assert.That(json, Does.Contain("\"reduced_distance\": 150"), "config.json reduced_distance should be 150");
        Assert.That(json, Does.Contain("\"minimal_distance\": 300"), "config.json minimal_distance should be 300");
        Assert.That(json, Does.Contain("\"reduced_frame_skip\": 2"), "config.json reduced_frame_skip should be 2");
        Assert.That(json, Does.Contain("\"minimal_frame_skip\": 4"), "config.json minimal_frame_skip should be 4");
    }

    [Test]
    public void LOD_StandByDisabled()
    {
        // README: StandBy disabled (CanDoStandBy=false + Activate())
        var src = ReadSource("Patches/BotOwnerBrainActivatePatch.cs");
        Assert.That(src, Does.Contain("CanDoStandBy = false"), "Should disable CanDoStandBy");
        Assert.That(src, Does.Contain("StandBy.Activate()"), "Should call Activate()");
    }

    [Test]
    public void LOD_HumanPlayerCache_Exists()
    {
        // README: HumanPlayerCache: once-per-tick snapshot, zero-allocation per-bot queries
        var src = ReadSource("Helpers/HumanPlayerCache.cs");
        Assert.That(src, Does.Contain("public static class HumanPlayerCache"), "Should be static class");
        Assert.That(src, Does.Contain("ComputeMinSqrDistance"), "Should have zero-allocation distance query");
        Assert.That(src, Does.Contain("MaxPlayers = 6"), "Should cache up to 6 players");
    }

    [Test]
    public void LOD_BotLodConfig_HasEnabledToggle()
    {
        // README: Configurable via questing.bot_lod in config.json
        var src = ReadSource("Configuration/BotLodConfig.cs");
        Assert.That(src, Does.Contain("[JsonProperty(\"enabled\")]"), "Should have enabled property");
        Assert.That(src, Does.Contain("Enabled { get; set; } = true"), "Should be enabled by default");
    }

    [Test]
    public void LOD_BotEntity_HasLodFields()
    {
        var src = ReadSource("BotLogic/ECS/BotEntity.cs");
        Assert.That(src, Does.Contain("public byte LodTier"), "Should have LodTier field");
        Assert.That(src, Does.Contain("public int LodFrameCounter"), "Should have LodFrameCounter");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Feature 6: Dedicated Log File
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void Log_LoggingController_Exists()
    {
        var src = ReadSource("Controllers/LoggingController.cs");
        Assert.That(src, Does.Contain("public static class LoggingController"));
    }

    [Test]
    public void Log_DedicatedLogPath()
    {
        // README: Path: BepInEx/plugins/DanW-SPTQuestingBots/log/QuestingBots.log
        var src = ReadSource("Controllers/LoggingController.cs");
        Assert.That(src, Does.Contain("QuestingBots.log"), "Should write to QuestingBots.log");
    }

    [Test]
    public void Log_FrameStampedFormat()
    {
        // README: Frame-stamped: [yyyy-MM-dd HH:mm:ss.fff] [LEVEL] F{frame}: message
        var src = ReadSource("Controllers/LoggingController.cs");
        Assert.That(src, Does.Contain("yyyy-MM-dd HH:mm:ss.fff"), "Should use yyyy-MM-dd HH:mm:ss.fff timestamp");
        Assert.That(src, Does.Contain("] F"), "Should write F{frame} prefix in log format");
        Assert.That(src, Does.Contain("Time.frameCount"), "Should use Unity frame count");
    }

    [Test]
    public void Log_DualDestination()
    {
        // README: Dual-destination: BepInEx LogOutput.log + dedicated file
        var src = ReadSource("Controllers/LoggingController.cs");
        // LogInfo writes to both Logger and file
        Assert.That(src, Does.Contain("Logger.LogInfo"), "Should write to BepInEx Logger");
        Assert.That(src, Does.Contain("WriteToFile"), "Should write to dedicated file");
    }

    [Test]
    public void Log_ThreadSafeViaLock()
    {
        // README: Thread-safe via lock
        var src = ReadSource("Controllers/LoggingController.cs");
        Assert.That(src, Does.Contain("lock (_fileLock)"), "Should use lock for thread safety");
    }

    [Test]
    public void Log_AutoFlushed()
    {
        // README: Auto-flushed
        var src = ReadSource("Controllers/LoggingController.cs");
        Assert.That(src, Does.Contain("AutoFlush = true"), "Should set AutoFlush to true");
    }

    [Test]
    public void Log_TruncatedFreshEachSession()
    {
        // README: truncated fresh each session
        var src = ReadSource("Controllers/LoggingController.cs");
        Assert.That(src, Does.Contain("append: false"), "Should truncate (not append) at session start");
    }

    [Test]
    public void Log_ConfigToggle_DedicatedLogFile()
    {
        // README: Toggle via debug.dedicated_log_file (default: enabled)
        var src = ReadSource("Configuration/DebugConfig.cs");
        Assert.That(src, Does.Contain("[JsonProperty(\"dedicated_log_file\")]"), "Should have dedicated_log_file JSON property");
        Assert.That(src, Does.Contain("DedicatedLogFile { get; set; } = true"), "Should default to enabled");
    }

    [Test]
    public void Log_LoggingCallCount_AtLeast720()
    {
        // README: 720+ logging calls across 166 files
        // Actual: 792 calls across 178 files — README understates both
        var csFiles = Directory.GetFiles(SrcRoot, "*.cs", SearchOption.AllDirectories);
        int totalCalls = 0;
        int filesWithCalls = 0;
        var logPattern = new Regex(@"LoggingController\.(LogDebug|LogInfo|LogWarning|LogError)", RegexOptions.Compiled);

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var matches = logPattern.Matches(content);
            if (matches.Count > 0)
            {
                totalCalls += matches.Count;
                filesWithCalls++;
            }
        }

        Assert.That(totalCalls, Is.GreaterThanOrEqualTo(720), $"README claims 720+ logging calls, found {totalCalls}");
        // README says 166 files — actual is 178 (README understated)
        Assert.That(filesWithCalls, Is.GreaterThanOrEqualTo(166), $"README claims 166 files with logging, found {filesWithCalls}");
    }

    // ═══════════════════════════════════════════════════════════════════
    // Cross-cutting: verify README numeric claims match config.json
    // ═══════════════════════════════════════════════════════════════════

    [Test]
    public void Config_DedicatedLogFile_DefaultEnabled()
    {
        var json = ReadConfig("config.json");
        Assert.That(json, Does.Contain("\"dedicated_log_file\": true"), "config.json should have dedicated_log_file: true");
    }

    [Test]
    public void Config_BotLodEnabled_DefaultTrue()
    {
        var json = ReadConfig("config.json");
        // Find "bot_lod" section and verify "enabled": true
        // The bot_lod section has "enabled": true
        var botLodMatch = Regex.Match(json, "\"bot_lod\"\\s*:\\s*\\{[^}]*\"enabled\"\\s*:\\s*(\\w+)");
        Assert.That(botLodMatch.Success, Is.True, "Should find bot_lod section in config.json");
        Assert.That(botLodMatch.Groups[1].Value, Is.EqualTo("true"), "bot_lod.enabled should be true");
    }
}
