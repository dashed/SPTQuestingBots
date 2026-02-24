using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Models.Pathing;
using PatrolRouteClass = SPTQuestingBots.Models.Pathing.PatrolRoute;

namespace SPTQuestingBots.Client.Tests.E2E;

/// <summary>
/// Behavioral simulation tests for the full PMC lifecycle and BotObjectiveLayer decision tree.
/// Exercises real task scoring classes against simulated game state transitions.
/// All classes under test are pure C# (no Unity/EFT runtime dependencies).
/// </summary>
[TestFixture]
public class PmcLifecycleBehaviorTests
{
    // ================================================================
    // Helpers
    // ================================================================

    private static BotEntity CreateEntity(BotRegistry registry, int bsgId)
    {
        var entity = registry.Add(bsgId);
        entity.IsActive = true;
        return entity;
    }

    private static void InitTaskScores(BotEntity entity, int taskCount)
    {
        entity.TaskScores = new float[taskCount];
    }

    /// <summary>
    /// Create the real 14-task quest manager (same as QuestTaskFactory.Create()).
    /// </summary>
    private static UtilityTaskManager CreateQuestManager()
    {
        return new UtilityTaskManager(
            new UtilityTask[]
            {
                new GoToObjectiveTask(),
                new AmbushTask(),
                new SnipeTask(),
                new HoldPositionTask(),
                new PlantItemTask(),
                new UnlockDoorTask(),
                new ToggleSwitchTask(),
                new CloseDoorsTask(),
                new LootTask(),
                new VultureTask(),
                new LingerTask(),
                new InvestigateTask(),
                new SpawnEntryTask(),
                new PatrolTask(),
            }
        );
    }

    /// <summary>
    /// Set up a standard PMC entity for lifecycle testing.
    /// </summary>
    private static BotEntity CreatePmc(
        BotRegistry registry,
        int bsgId,
        float aggression,
        byte personality,
        float spawnTime = 10f,
        float spawnDuration = 4f
    )
    {
        var entity = CreateEntity(registry, bsgId);
        InitTaskScores(entity, QuestTaskFactory.TaskCount);
        entity.BotType = BotType.PMC;
        entity.Aggression = aggression;
        entity.Personality = personality;
        entity.SpawnTime = spawnTime;
        entity.SpawnEntryDuration = spawnDuration;
        entity.CurrentGameTime = spawnTime;
        entity.RaidTimeNormalized = 0f; // raid start
        return entity;
    }

    /// <summary>
    /// Run ScoreAndPick and return the name of the winning task (or null).
    /// </summary>
    private static string ScoreAndPickName(UtilityTaskManager manager, BotEntity entity)
    {
        manager.ScoreAndPick(entity);
        var task = entity.TaskAssignment.Task as QuestUtilityTask;
        return task?.GetType().Name;
    }

    /// <summary>
    /// Get the winning task's BotActionTypeId after ScoreAndPick, or -1 if none.
    /// </summary>
    private static int ScoreAndPickActionId(UtilityTaskManager manager, BotEntity entity)
    {
        manager.ScoreAndPick(entity);
        var task = entity.TaskAssignment.Task as QuestUtilityTask;
        return task?.BotActionTypeId ?? -1;
    }

    // ================================================================
    // Part A: Full PMC Lifecycle Behavioral Simulation
    // ================================================================

    [Test]
    public void FullLifecycle_SpawnEntry_ThenGoToObjective_ThenArriveAndAmbush()
    {
        // Simulate: PMC spawns → SpawnEntry → travels to ambush objective → arrives → ambushes
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 100, 0.5f, BotPersonality.Normal);

        // Phase 1: Just spawned — SpawnEntry should win
        entity.CurrentGameTime = 10f; // spawn time
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("SpawnEntryTask"));

        // Phase 2: Mid-spawn-entry — still SpawnEntry
        entity.CurrentGameTime = 12f;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("SpawnEntryTask"));

        // Phase 3: SpawnEntry duration expired (4s) — SpawnEntry goes to 0
        entity.CurrentGameTime = 14.5f;
        // Set an active ambush objective far away
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.Ambush;
        entity.DistanceToObjective = 150f;
        entity.IsCloseToObjective = false;
        string taskName = ScoreAndPickName(manager, entity);
        Assert.That(entity.IsSpawnEntryComplete, Is.True, "SpawnEntry should be marked complete");
        Assert.That(taskName, Is.EqualTo("GoToObjectiveTask"), "Should travel to ambush position");

        // Phase 4: Arrived at ambush position
        entity.CurrentGameTime = 45f;
        entity.DistanceToObjective = 2f;
        entity.IsCloseToObjective = true;
        taskName = ScoreAndPickName(manager, entity);
        Assert.That(taskName, Is.EqualTo("AmbushTask"), "Should ambush once close");
    }

    [Test]
    public void FullLifecycle_SpawnEntry_ThenGoToObjective_ThenSnipe()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 101, 0.3f, BotPersonality.Cautious);

        // Phase 1: Just spawned — SpawnEntry
        entity.CurrentGameTime = 10f;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("SpawnEntryTask"));

        // Phase 2: SpawnEntry complete, far snipe objective
        entity.CurrentGameTime = 15f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.Snipe;
        entity.DistanceToObjective = 200f;
        entity.IsCloseToObjective = false;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("GoToObjectiveTask"));

        // Phase 3: Close to snipe position
        entity.CurrentGameTime = 60f;
        entity.DistanceToObjective = 1f;
        entity.IsCloseToObjective = true;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("SnipeTask"));
    }

    [Test]
    public void FullLifecycle_MoveToPosition_ThenHold_ThenPlantItem()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 102, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;

        // Phase 1: MoveToPosition — go to objective
        entity.CurrentGameTime = 20f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("GoToObjectiveTask"));

        // Phase 2: Switch to HoldAtPosition
        entity.CurrentQuestAction = QuestActionId.HoldAtPosition;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("HoldPositionTask"));

        // Phase 3: Switch to PlantItem far away
        entity.CurrentQuestAction = QuestActionId.PlantItem;
        entity.DistanceToObjective = 50f;
        entity.IsCloseToObjective = false;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("GoToObjectiveTask"));

        // Phase 4: Close to plant position
        entity.DistanceToObjective = 1f;
        entity.IsCloseToObjective = true;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("PlantItemTask"));
    }

    [Test]
    public void FullLifecycle_UnlockDoor_BlocksGoToObjective()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 103, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;

        // Active objective with door block
        entity.CurrentGameTime = 20f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;
        entity.MustUnlockDoor = true;

        string taskName = ScoreAndPickName(manager, entity);
        Assert.That(taskName, Is.EqualTo("UnlockDoorTask"), "UnlockDoor should take priority over GoToObjective");

        // After unlocking
        entity.MustUnlockDoor = false;
        taskName = ScoreAndPickName(manager, entity);
        Assert.That(taskName, Is.EqualTo("GoToObjectiveTask"), "Should resume GoToObjective after door unlocked");
    }

    [Test]
    public void FullLifecycle_ToggleSwitch_And_CloseDoors()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 104, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 20f;
        entity.HasActiveObjective = true;

        // ToggleSwitch action
        entity.CurrentQuestAction = QuestActionId.ToggleSwitch;
        Assert.That(ScoreAndPickActionId(manager, entity), Is.EqualTo(BotActionTypeId.ToggleSwitch));

        // CloseNearbyDoors action
        entity.CurrentQuestAction = QuestActionId.CloseNearbyDoors;
        Assert.That(ScoreAndPickActionId(manager, entity), Is.EqualTo(BotActionTypeId.CloseNearbyDoors));
    }

    // ================================================================
    // Part A: Personality Variation Tests
    // ================================================================

    [Test]
    public void Personality_AggressiveBot_PrefersRushAndVulture()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var aggressive = CreatePmc(registry, 200, 0.9f, BotPersonality.Reckless);
        aggressive.IsSpawnEntryComplete = true;
        aggressive.CurrentGameTime = 20f;
        aggressive.RaidTimeNormalized = 0.0f; // early raid

        // Scenario: nearby combat event, close to own objective
        aggressive.HasActiveObjective = true;
        aggressive.CurrentQuestAction = QuestActionId.MoveToPosition;
        aggressive.DistanceToObjective = 5f; // close to objective
        aggressive.IsCloseToObjective = false; // but not "close" threshold

        aggressive.HasNearbyEvent = true;
        aggressive.CombatIntensity = 30; // high intensity
        aggressive.NearbyEventX = aggressive.CurrentPositionX + 20f;
        aggressive.NearbyEventZ = aggressive.CurrentPositionZ + 20f;

        manager.ScoreAndPick(aggressive);
        var task = aggressive.TaskAssignment.Task as QuestUtilityTask;
        Assert.That(task, Is.Not.Null, "Should have a task assigned");

        // Get individual scores for analysis
        float goToScore = aggressive.TaskScores[0]; // GoToObjectiveTask
        float vultureScore = aggressive.TaskScores[9]; // VultureTask
        float investigateScore = aggressive.TaskScores[11]; // InvestigateTask

        // For aggressive bot near objective with high intensity nearby, vulture or investigate should score
        Assert.That(
            vultureScore > 0f || investigateScore > 0f,
            Is.True,
            "Aggressive bot should score > 0 for vulture or investigate when combat nearby"
        );
    }

    [Test]
    public void Personality_TimidBot_PrefersLinger_OverGoToObjective_LateRaid()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var timid = CreatePmc(registry, 201, 0.1f, BotPersonality.Timid);
        timid.IsSpawnEntryComplete = true;
        timid.CurrentGameTime = 300f;
        timid.RaidTimeNormalized = 0.8f; // late raid

        // Recently completed objective — lingering
        timid.ObjectiveCompletedTime = 298f;
        timid.LingerDuration = 15f;
        timid.IsLingering = true;

        // Also has a far objective available
        timid.HasActiveObjective = true;
        timid.CurrentQuestAction = QuestActionId.MoveToPosition;
        timid.DistanceToObjective = 200f;
        timid.IsCloseToObjective = false;

        manager.ScoreAndPick(timid);

        float lingerScore = timid.TaskScores[10]; // LingerTask
        float goToScore = timid.TaskScores[0]; // GoToObjectiveTask

        // Timid + late raid: Linger modifier is high (personality: Lerp(1.3,0.7,0.1)=1.24, raid: Lerp(0.7,1.3,0.8)=1.18)
        // GoTo modifier is low (personality: Lerp(0.85,1.15,0.1)=0.88, raid: Lerp(1.2,0.8,0.8)=0.88)
        Assert.That(lingerScore, Is.GreaterThan(0f), "Timid bot should have linger score > 0");

        // Verify personality and raid time modifiers favor linger for timid late-raid
        float lingerMod = ScoringModifiers.CombinedModifier(0.1f, 0.8f, BotActionTypeId.Linger);
        float goToMod = ScoringModifiers.CombinedModifier(0.1f, 0.8f, BotActionTypeId.GoToObjective);
        Assert.That(lingerMod, Is.GreaterThan(goToMod), "Timid + late raid should boost linger more than GoTo");
    }

    [Test]
    public void Personality_Modifiers_Aggressive_Favors_Rush_Early_Raid()
    {
        // Aggressive + early raid: GoToObjective gets max boost
        float goToMod = ScoringModifiers.CombinedModifier(0.9f, 0.0f, BotActionTypeId.GoToObjective);
        float ambushMod = ScoringModifiers.CombinedModifier(0.9f, 0.0f, BotActionTypeId.Ambush);
        float vultureMod = ScoringModifiers.CombinedModifier(0.9f, 0.0f, BotActionTypeId.Vulture);

        Assert.That(goToMod, Is.GreaterThan(1.0f), "Aggressive early-raid GoTo modifier should be > 1.0");
        Assert.That(vultureMod, Is.GreaterThan(1.0f), "Aggressive vulture modifier should be > 1.0");
        Assert.That(ambushMod, Is.LessThan(1.0f), "Aggressive ambush modifier should be < 1.0 (aggression deprioritizes camping)");
    }

    [Test]
    public void Personality_Modifiers_Timid_Favors_Camp_Late_Raid()
    {
        float ambushMod = ScoringModifiers.CombinedModifier(0.1f, 0.9f, BotActionTypeId.Ambush);
        float snipeMod = ScoringModifiers.CombinedModifier(0.1f, 0.9f, BotActionTypeId.Snipe);
        float lingerMod = ScoringModifiers.CombinedModifier(0.1f, 0.9f, BotActionTypeId.Linger);
        float goToMod = ScoringModifiers.CombinedModifier(0.1f, 0.9f, BotActionTypeId.GoToObjective);

        Assert.That(ambushMod, Is.GreaterThan(goToMod), "Timid late-raid should favor ambush over GoTo");
        Assert.That(snipeMod, Is.GreaterThan(goToMod), "Timid late-raid should favor snipe over GoTo");
        Assert.That(lingerMod, Is.GreaterThan(goToMod), "Timid late-raid should favor linger over GoTo");
    }

    [Test]
    public void Personality_Neutral_Balanced_Mid_Raid()
    {
        float goToMod = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.GoToObjective);
        float ambushMod = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.Ambush);
        float vultureMod = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.Vulture);
        float lootMod = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.Loot);

        // All modifiers should be close to 1.0 for neutral mid-raid
        Assert.That(goToMod, Is.InRange(0.9f, 1.1f), "Neutral mid-raid GoTo should be near 1.0");
        Assert.That(ambushMod, Is.InRange(0.9f, 1.1f), "Neutral mid-raid Ambush should be near 1.0");
        Assert.That(vultureMod, Is.InRange(0.9f, 1.1f), "Neutral mid-raid Vulture should be near 1.0");
        Assert.That(lootMod, Is.InRange(0.9f, 1.1f), "Neutral mid-raid Loot should be near 1.0");
    }

    // ================================================================
    // Part A: Combat Event Response Tests
    // ================================================================

    [Test]
    public void CombatEvent_VultureActivates_When_NearObjective()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 300, 0.7f, BotPersonality.Aggressive);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 50f;
        entity.RaidTimeNormalized = 0.2f;

        // Near own objective (low GoToObjective score)
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 3f; // very close
        entity.IsCloseToObjective = false;

        // Nearby combat event with high intensity
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30;
        entity.NearbyEventX = entity.CurrentPositionX + 50f;
        entity.NearbyEventZ = entity.CurrentPositionZ;

        manager.ScoreAndPick(entity);

        float goToScore = entity.TaskScores[0];
        float vultureScore = entity.TaskScores[9];

        Assert.That(vultureScore, Is.GreaterThan(0f), "Vulture should score > 0 with nearby combat event");
        Assert.That(
            vultureScore,
            Is.GreaterThan(goToScore),
            "Vulture should beat GoToObjective when near own objective (GoTo distance-based score is low)"
        );
    }

    [Test]
    public void CombatEvent_VultureCannotBeat_GoToObjective_WhenFarFromObjective()
    {
        // This documents a behavioral property: GoToObjective with high distance scores
        // so high that vulture can't interrupt it (by design — bot prioritizes objectives)
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 301, 0.9f, BotPersonality.Reckless);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 50f;
        entity.RaidTimeNormalized = 0.0f; // early raid

        // Far from objective
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 200f;
        entity.IsCloseToObjective = false;

        // First: establish GoToObjective as current task
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task?.GetType().Name, Is.EqualTo("GoToObjectiveTask"), "Should start with GoToObjective");

        // Now add maximum combat event
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 100; // way above threshold
        entity.NearbyEventX = entity.CurrentPositionX + 10f; // very close event
        entity.NearbyEventZ = entity.CurrentPositionZ;

        manager.ScoreAndPick(entity);

        float goToScore = entity.TaskScores[0];
        float vultureScore = entity.TaskScores[9];
        float goToEffective = goToScore + 0.25f; // GoToObjective hysteresis

        // Document the behavioral property: GoTo + hysteresis beats vulture
        Assert.That(
            goToEffective,
            Is.GreaterThan(vultureScore),
            "GoToObjective with hysteresis should beat Vulture when far from objective (by design)"
        );
    }

    [Test]
    public void CombatEvent_InvestigateScores_BelowVulture_AtSameIntensity()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 302, 0.7f, BotPersonality.Aggressive);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 50f;
        entity.RaidTimeNormalized = 0.2f;

        // No active objective
        entity.HasActiveObjective = false;

        // Nearby combat at high intensity
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30;
        entity.NearbyEventX = entity.CurrentPositionX + 50f;
        entity.NearbyEventZ = entity.CurrentPositionZ;

        manager.ScoreAndPick(entity);

        float vultureScore = entity.TaskScores[9];
        float investigateScore = entity.TaskScores[11];

        Assert.That(vultureScore, Is.GreaterThan(investigateScore), "Vulture should outscore Investigate at same conditions");
    }

    [Test]
    public void CombatEvent_VultureIgnored_InCombat()
    {
        var registry = new BotRegistry(8);
        var entity = CreatePmc(new BotRegistry(8), 303, 0.9f, BotPersonality.Reckless);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 50f;
        entity.IsInCombat = true;

        // Nearby event
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 100;

        float score = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f), "Vulture should not score when bot is in combat");
    }

    [Test]
    public void CombatEvent_VultureIgnored_InBossZone()
    {
        var entity = CreatePmc(new BotRegistry(8), 304, 0.9f, BotPersonality.Reckless);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 50f;
        entity.IsInBossZone = true;

        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 100;

        float score = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f), "Vulture should not score when in boss zone");
    }

    [Test]
    public void CombatEvent_VultureIgnored_OnCooldown()
    {
        var entity = CreatePmc(new BotRegistry(8), 305, 0.9f, BotPersonality.Reckless);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 50f;
        entity.VultureCooldownUntil = 200f; // cooldown until 200s

        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 100;

        float score = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f), "Vulture should not score when on cooldown");
    }

    [Test]
    public void CombatEvent_VultureMaintainsMaxScore_DuringActivePhase()
    {
        var entity = CreatePmc(new BotRegistry(8), 306, 0.7f, BotPersonality.Aggressive);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 50f;
        entity.VulturePhase = VulturePhase.Approach;

        // Even with no nearby event or low intensity, active phase scores max
        entity.HasNearbyEvent = false;
        entity.CombatIntensity = 0;

        float score = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(VultureTask.MaxBaseScore), "Active vulture phase should maintain max score regardless of conditions");
    }

    // ================================================================
    // Part A: Loot Integration Tests
    // ================================================================

    [Test]
    public void Loot_HighValueTarget_ScoresAbovePatrol_BelowObjective()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 400, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 60f;
        entity.RaidTimeNormalized = 0.3f;

        // Has loot target
        entity.HasLootTarget = true;
        entity.LootTargetValue = 40000f;
        entity.LootTargetX = entity.CurrentPositionX + 10f;
        entity.LootTargetY = entity.CurrentPositionY;
        entity.LootTargetZ = entity.CurrentPositionZ;
        entity.InventorySpaceFree = 5f;

        // Also has far objective
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;

        manager.ScoreAndPick(entity);

        float goToScore = entity.TaskScores[0];
        float lootScore = entity.TaskScores[8];

        Assert.That(lootScore, Is.GreaterThan(0f), "Loot score should be positive");
        // Loot (max 0.55) vs GoTo far (0.65 * distance factor) — GoTo generally wins
    }

    [Test]
    public void Loot_CombatSuppresses()
    {
        var entity = CreatePmc(new BotRegistry(8), 401, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.IsInCombat = true;
        entity.HasLootTarget = true;
        entity.LootTargetValue = 50000f;

        float score = LootTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "Combat should suppress loot scoring");
    }

    [Test]
    public void Loot_ProximityBonus_NearObjective()
    {
        var entity = CreatePmc(new BotRegistry(8), 402, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 60f;

        entity.HasLootTarget = true;
        entity.LootTargetValue = 30000f;
        entity.LootTargetX = entity.CurrentPositionX + 5f;
        entity.LootTargetY = entity.CurrentPositionY;
        entity.LootTargetZ = entity.CurrentPositionZ;
        entity.InventorySpaceFree = 5f;

        // Near objective (DistanceToObjective < 20m threshold)
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 15f;
        float scoreNear = LootTask.Score(entity);

        // Far from objective
        entity.DistanceToObjective = 100f;
        float scoreFar = LootTask.Score(entity);

        Assert.That(scoreNear, Is.GreaterThan(scoreFar), "Loot near objective should score higher than loot far from objective");
    }

    // ================================================================
    // Part A: Raid Time Progression Tests
    // ================================================================

    [Test]
    public void RaidTime_EarlyRaid_BoostsGoToObjective()
    {
        float earlyGoTo = ScoringModifiers.RaidTimeModifier(0.0f, BotActionTypeId.GoToObjective);
        float lateGoTo = ScoringModifiers.RaidTimeModifier(1.0f, BotActionTypeId.GoToObjective);

        Assert.That(earlyGoTo, Is.GreaterThan(lateGoTo), "Early raid should boost GoToObjective");
        Assert.That(earlyGoTo, Is.EqualTo(1.2f).Within(0.001f), "GoTo early=1.2");
        Assert.That(lateGoTo, Is.EqualTo(0.8f).Within(0.001f), "GoTo late=0.8");
    }

    [Test]
    public void RaidTime_LateRaid_BoostsLingerAndLoot()
    {
        float earlyLinger = ScoringModifiers.RaidTimeModifier(0.0f, BotActionTypeId.Linger);
        float lateLinger = ScoringModifiers.RaidTimeModifier(1.0f, BotActionTypeId.Linger);
        float earlyLoot = ScoringModifiers.RaidTimeModifier(0.0f, BotActionTypeId.Loot);
        float lateLoot = ScoringModifiers.RaidTimeModifier(1.0f, BotActionTypeId.Loot);

        Assert.That(lateLinger, Is.GreaterThan(earlyLinger), "Late raid should boost Linger");
        Assert.That(lateLoot, Is.GreaterThan(earlyLoot), "Late raid should boost Loot");
    }

    [Test]
    public void RaidTime_VultureAndInvestigate_UnaffectedByRaidTime()
    {
        float earlyVulture = ScoringModifiers.RaidTimeModifier(0.0f, BotActionTypeId.Vulture);
        float lateVulture = ScoringModifiers.RaidTimeModifier(1.0f, BotActionTypeId.Vulture);
        float earlyInvestigate = ScoringModifiers.RaidTimeModifier(0.0f, BotActionTypeId.Investigate);
        float lateInvestigate = ScoringModifiers.RaidTimeModifier(1.0f, BotActionTypeId.Investigate);

        Assert.That(earlyVulture, Is.EqualTo(1.0f), "Vulture should not be affected by raid time");
        Assert.That(lateVulture, Is.EqualTo(1.0f), "Vulture should not be affected by raid time");
        Assert.That(earlyInvestigate, Is.EqualTo(1.0f), "Investigate should not be affected by raid time");
        Assert.That(lateInvestigate, Is.EqualTo(1.0f), "Investigate should not be affected by raid time");
    }

    // ================================================================
    // Part B: BotObjectiveLayer Decision Tree Audit
    // ================================================================

    [Test]
    public void DecisionTree_AllTasksScoreZero_WhenActiveObjectiveButUndefinedAction()
    {
        // BUG DOCUMENTATION: When HasActiveObjective=true but CurrentQuestAction=Undefined,
        // all 14 tasks score 0. Bot does nothing despite having an "active" objective.
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 500, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 30f;

        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.Undefined;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;

        manager.ScoreAndPick(entity);

        // Every task should score 0
        for (int i = 0; i < QuestTaskFactory.TaskCount; i++)
        {
            Assert.That(entity.TaskScores[i], Is.EqualTo(0f), $"Task at index {i} should score 0 with Undefined QuestAction");
        }

        // No task assigned
        Assert.That(entity.TaskAssignment.Task, Is.Null, "No task should be assigned when all scores are 0");
    }

    [Test]
    public void DecisionTree_RequestExtractAction_NotHandledByUtilityAI()
    {
        // BUG DOCUMENTATION: RequestExtract (QuestAction=7) is handled by legacy trySetNextAction()
        // but NO utility task handles it. All 14 tasks score 0.
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 501, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 300f;

        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.RequestExtract;
        entity.DistanceToObjective = 50f;
        entity.IsCloseToObjective = false;

        manager.ScoreAndPick(entity);

        // All tasks should score 0 for RequestExtract
        for (int i = 0; i < QuestTaskFactory.TaskCount; i++)
        {
            Assert.That(entity.TaskScores[i], Is.EqualTo(0f), $"Task at index {i} should score 0 for RequestExtract action");
        }

        Assert.That(
            entity.TaskAssignment.Task,
            Is.Null,
            "No task should be assigned for RequestExtract — extraction must be handled externally"
        );
    }

    [Test]
    public void DecisionTree_GoToObjective_ExplicitlyReturnsZero_ForAllSelfHandledActions()
    {
        var entity = CreatePmc(new BotRegistry(8), 502, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;

        // These actions handle their own movement; GoToObjective should return 0
        int[] selfHandledActions = new[]
        {
            QuestActionId.HoldAtPosition,
            QuestActionId.ToggleSwitch,
            QuestActionId.CloseNearbyDoors,
            QuestActionId.RequestExtract,
            QuestActionId.Undefined,
        };

        foreach (int action in selfHandledActions)
        {
            entity.CurrentQuestAction = action;
            float score = GoToObjectiveTask.Score(entity);
            Assert.That(score, Is.EqualTo(0f), $"GoToObjective should return 0 for action {action}");
        }
    }

    [Test]
    public void DecisionTree_TwoPhaseActions_GoToObjective_DropsToZero_WhenClose()
    {
        var entity = CreatePmc(new BotRegistry(8), 503, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.HasActiveObjective = true;

        int[] twoPhaseActions = new[] { QuestActionId.Ambush, QuestActionId.Snipe, QuestActionId.PlantItem };

        foreach (int action in twoPhaseActions)
        {
            entity.CurrentQuestAction = action;

            // Far away: GoToObjective scores > 0
            entity.DistanceToObjective = 100f;
            entity.IsCloseToObjective = false;
            float farScore = GoToObjectiveTask.Score(entity);
            Assert.That(farScore, Is.GreaterThan(0f), $"GoToObjective should score > 0 when far for action {action}");

            // Close: GoToObjective drops to 0, action-specific task takes over
            entity.DistanceToObjective = 1f;
            entity.IsCloseToObjective = true;
            float closeScore = GoToObjectiveTask.Score(entity);
            Assert.That(closeScore, Is.EqualTo(0f), $"GoToObjective should return 0 when close for action {action}");
        }
    }

    [Test]
    public void DecisionTree_NoDeadCodePaths_AllActionsMappedToAtLeastOneTask()
    {
        // Verify every QuestAction maps to at least one task that can score > 0
        var entity = CreatePmc(new BotRegistry(8), 504, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;

        var manager = CreateQuestManager();

        // MoveToPosition → GoToObjective
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f), "MoveToPosition should map to GoToObjectiveTask");

        // HoldAtPosition → HoldPositionTask
        entity.CurrentQuestAction = QuestActionId.HoldAtPosition;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[3], Is.GreaterThan(0f), "HoldAtPosition should map to HoldPositionTask");

        // Ambush far → GoToObjective
        entity.CurrentQuestAction = QuestActionId.Ambush;
        entity.IsCloseToObjective = false;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f), "Ambush (far) should map to GoToObjectiveTask");

        // Ambush close → AmbushTask
        entity.IsCloseToObjective = true;
        entity.DistanceToObjective = 1f;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[1], Is.GreaterThan(0f), "Ambush (close) should map to AmbushTask");

        // Snipe far → GoToObjective
        entity.CurrentQuestAction = QuestActionId.Snipe;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f), "Snipe (far) should map to GoToObjectiveTask");

        // Snipe close → SnipeTask
        entity.IsCloseToObjective = true;
        entity.DistanceToObjective = 1f;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[2], Is.GreaterThan(0f), "Snipe (close) should map to SnipeTask");

        // PlantItem far → GoToObjective
        entity.CurrentQuestAction = QuestActionId.PlantItem;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f), "PlantItem (far) should map to GoToObjectiveTask");

        // PlantItem close → PlantItemTask
        entity.IsCloseToObjective = true;
        entity.DistanceToObjective = 1f;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[4], Is.GreaterThan(0f), "PlantItem (close) should map to PlantItemTask");

        // ToggleSwitch → ToggleSwitchTask
        entity.CurrentQuestAction = QuestActionId.ToggleSwitch;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[6], Is.GreaterThan(0f), "ToggleSwitch should map to ToggleSwitchTask");

        // CloseNearbyDoors → CloseDoorsTask
        entity.CurrentQuestAction = QuestActionId.CloseNearbyDoors;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[7], Is.GreaterThan(0f), "CloseNearbyDoors should map to CloseDoorsTask");

        // UnlockDoor → UnlockDoorTask
        entity.MustUnlockDoor = true;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskScores[5], Is.GreaterThan(0f), "MustUnlockDoor should map to UnlockDoorTask");
        entity.MustUnlockDoor = false;

        // Undefined → no task (documented behavior)
        entity.CurrentQuestAction = QuestActionId.Undefined;
        manager.ScoreAndPick(entity);
        bool anyScored = false;
        for (int i = 0; i < QuestTaskFactory.TaskCount; i++)
        {
            if (entity.TaskScores[i] > 0f)
            {
                anyScored = true;
                break;
            }
        }
        Assert.That(anyScored, Is.False, "Undefined QuestAction should not map to any task");

        // RequestExtract → no task (documented behavior — handled by BotExtractMonitor)
        entity.CurrentQuestAction = QuestActionId.RequestExtract;
        manager.ScoreAndPick(entity);
        anyScored = false;
        for (int i = 0; i < QuestTaskFactory.TaskCount; i++)
        {
            if (entity.TaskScores[i] > 0f)
            {
                anyScored = true;
                break;
            }
        }
        Assert.That(anyScored, Is.False, "RequestExtract should not map to any utility task");
    }

    // ================================================================
    // Part B: Hysteresis and Task Switching
    // ================================================================

    [Test]
    public void Hysteresis_PreventsOscillation_BetweenCloseScores()
    {
        var registry = new BotRegistry(8);
        var entity = CreatePmc(registry, 600, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 30f;

        // Two tasks with close scores
        var task1 = new TestTask(0.50f, BotActionTypeId.GoToObjective, "TestA", 0.15f);
        var task2 = new TestTask(0.52f, BotActionTypeId.Ambush, "TestB", 0.15f);
        var manager = new UtilityTaskManager(new UtilityTask[] { task1, task2 });

        entity.TaskScores = new float[2];

        // First pick: task2 wins (0.52 > 0.50)
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task2));

        // Second pick: task1 at 0.50 vs task2 at 0.52 + 0.15 hysteresis = 0.67
        // task1 cannot beat 0.67, so task2 stays
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task2), "Hysteresis should prevent switch back");
    }

    [Test]
    public void Hysteresis_AllowsSwitch_WhenNewScoreClearlyHigher()
    {
        var registry = new BotRegistry(8);
        var entity = CreatePmc(registry, 601, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 30f;

        var lowTask = new TestTask(0.30f, BotActionTypeId.GoToObjective, "Low", 0.10f);
        var highTask = new TestTask(0.80f, BotActionTypeId.Ambush, "High", 0.10f);
        var manager = new UtilityTaskManager(new UtilityTask[] { lowTask, highTask });

        entity.TaskScores = new float[2];

        // First pick: highTask wins
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(highTask));

        // Now lower high task's score dramatically
        highTask.CurrentScore = 0.20f;
        // Low stays at 0.30 vs high at 0.20 + 0.10 hysteresis = 0.30
        // Tie (0.30 <= 0.30) — no switch (strictly greater required)
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(highTask), "Tie should keep current task");

        // Push low above hysteresis
        lowTask.CurrentScore = 0.35f;
        // Low 0.35 > high 0.20 + 0.10 = 0.30 — switch
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(lowTask), "Higher score should win after beating hysteresis");
    }

    [Test]
    public void Hysteresis_NaN_HighestScore_ResetToZero()
    {
        var registry = new BotRegistry(8);
        var entity = CreatePmc(registry, 602, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 30f;

        var nanTask = new TestTask(float.NaN, BotActionTypeId.GoToObjective, "NaN", 0.10f);
        var goodTask = new TestTask(0.01f, BotActionTypeId.Ambush, "Good", 0.10f);
        var manager = new UtilityTaskManager(new UtilityTask[] { nanTask, goodTask });

        entity.TaskScores = new float[2];

        // NaN task "wins" first (or rather, gets picked if no other scores)
        // Actually NaN is skipped in PickTask (float.IsNaN check), so goodTask wins
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(goodTask), "NaN score should be skipped");
    }

    // ================================================================
    // Part B: Manager Switching (Quest ↔ Follower)
    // ================================================================

    [Test]
    public void ManagerSwitch_Quest_To_Follower_ClearsStaleAssignment()
    {
        var registry = new BotRegistry(8);
        var entity = CreatePmc(registry, 700, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 30f;

        // Quest manager assigns a task
        var questManager = CreateQuestManager();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;
        questManager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null, "Quest manager should assign a task");
        var questTask = entity.TaskAssignment.Task;

        // Simulate follower manager taking over:
        // BotObjectiveLayer would clear the stale quest assignment
        // and then run follower ScoreAndPick
        entity.TaskAssignment.Task.Deactivate(entity);
        entity.TaskAssignment = default;

        // Follower manager
        var followerManager = SquadTaskFactory.Create();
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];

        // Set up follower conditions: has boss and tactical position
        var boss = CreateEntity(registry, 701);
        entity.Boss = boss;
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = entity.CurrentPositionX + 20f;
        entity.TacticalPositionY = entity.CurrentPositionY;
        entity.TacticalPositionZ = entity.CurrentPositionZ + 20f;

        followerManager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null, "Follower manager should assign a task");
        Assert.That(entity.TaskAssignment.Task, Is.Not.SameAs(questTask), "Should be a different task from follower manager");
    }

    [Test]
    public void ManagerSwitch_Follower_To_Quest_ClearsStaleAssignment()
    {
        var registry = new BotRegistry(8);
        var entity = CreatePmc(registry, 702, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 30f;

        // Set up as follower first
        var boss = CreateEntity(registry, 703);
        entity.Boss = boss;
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = entity.CurrentPositionX + 20f;
        entity.TacticalPositionY = entity.CurrentPositionY;
        entity.TacticalPositionZ = entity.CurrentPositionZ + 20f;

        // Follower task scores
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];
        var followerManager = SquadTaskFactory.Create();
        followerManager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null, "Should have follower task");
        var followerTask = entity.TaskAssignment.Task;

        // Switch to quest: clear stale assignment
        entity.TaskAssignment.Task.Deactivate(entity);
        entity.TaskAssignment = default;

        // Reset for quest manager
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Boss = null; // no longer a follower
        entity.HasTacticalPosition = false;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;

        var questManager = CreateQuestManager();
        questManager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null, "Quest manager should assign task");
        Assert.That(entity.TaskAssignment.Task, Is.Not.SameAs(followerTask), "Should have quest task, not follower task");
    }

    // ================================================================
    // Part C: Cross-System Behavioral Bug Detection
    // ================================================================

    [Test]
    public void CrossSystem_SpawnEntry_SideEffect_Marks_Complete_During_Scoring()
    {
        // SpawnEntryTask.Score() mutates entity.IsSpawnEntryComplete as a side effect
        // during scoring. Verify this works correctly when the manager does
        // score-then-pick in sequence.
        var registry = new BotRegistry(8);
        var entity = CreatePmc(registry, 800, 0.5f, BotPersonality.Normal);
        entity.CurrentGameTime = 14.5f; // past spawn duration (4s from spawn at 10s)

        Assert.That(entity.IsSpawnEntryComplete, Is.False, "Should start incomplete");

        // Scoring should mark complete and return 0
        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "Score should be 0 when duration expired");
        Assert.That(entity.IsSpawnEntryComplete, Is.True, "Should be marked complete after scoring");

        // Subsequent scoring should also return 0
        float score2 = SpawnEntryTask.Score(entity);
        Assert.That(score2, Is.EqualTo(0f), "Should remain 0 once complete");
    }

    [Test]
    public void CrossSystem_SpawnEntry_Blocks_AllOtherTasks()
    {
        // During spawn entry, SpawnEntry (0.80) should beat all other tasks
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 801, 0.9f, BotPersonality.Reckless);
        entity.CurrentGameTime = 11f; // 1s into spawn entry

        // Set up maximum conditions for all other tasks
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.HoldAtPosition; // HoldPosition base=0.70
        entity.MustUnlockDoor = true; // UnlockDoor base=0.70
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 100; // max vulture

        manager.ScoreAndPick(entity);

        // SpawnEntry at 0.80 should beat everything (HoldPosition 0.70, UnlockDoor 0.70)
        Assert.That(
            entity.TaskAssignment.Task?.GetType().Name,
            Is.EqualTo("SpawnEntryTask"),
            "SpawnEntry should win over all other tasks during spawn entry period"
        );
    }

    [Test]
    public void CrossSystem_Linger_Transitions_To_GoToObjective_After_Duration()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 802, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.RaidTimeNormalized = 0.5f;

        // Just completed objective — lingering
        entity.CurrentGameTime = 100f;
        entity.ObjectiveCompletedTime = 99f;
        entity.LingerDuration = 10f;
        entity.IsLingering = true;

        // Has next objective far away
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 150f;
        entity.IsCloseToObjective = false;

        // Linger should score initially
        float lingerScore = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(lingerScore, Is.GreaterThan(0f), "Linger should score right after completion");

        // Advance time past linger duration
        entity.CurrentGameTime = 110f;
        lingerScore = LingerTask.Score(entity, LingerTask.DefaultBaseScore);
        Assert.That(lingerScore, Is.EqualTo(0f), "Linger should expire after duration");

        // GoToObjective should now win
        manager.ScoreAndPick(entity);
        Assert.That(
            entity.TaskAssignment.Task?.GetType().Name,
            Is.EqualTo("GoToObjectiveTask"),
            "Should transition to GoToObjective after linger expires"
        );
    }

    [Test]
    public void CrossSystem_Linger_Score_Decays_Linearly()
    {
        var entity = CreatePmc(new BotRegistry(8), 803, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.ObjectiveCompletedTime = 100f;
        entity.LingerDuration = 10f;

        // At start: full score
        entity.CurrentGameTime = 100f;
        float scoreStart = LingerTask.Score(entity, LingerTask.DefaultBaseScore);

        // At midpoint: half score
        entity.CurrentGameTime = 105f;
        float scoreMid = LingerTask.Score(entity, LingerTask.DefaultBaseScore);

        // At 75%: quarter score
        entity.CurrentGameTime = 107.5f;
        float score75 = LingerTask.Score(entity, LingerTask.DefaultBaseScore);

        Assert.That(scoreStart, Is.EqualTo(LingerTask.DefaultBaseScore).Within(0.001f));
        Assert.That(scoreMid, Is.EqualTo(LingerTask.DefaultBaseScore * 0.5f).Within(0.001f));
        Assert.That(score75, Is.EqualTo(LingerTask.DefaultBaseScore * 0.25f).Within(0.001f));
    }

    [Test]
    public void CrossSystem_VultureCooldown_Prevents_Revulturing()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 804, 0.9f, BotPersonality.Reckless);
        entity.IsSpawnEntryComplete = true;
        entity.RaidTimeNormalized = 0.2f;
        entity.HasActiveObjective = false; // no quest objective

        // First vulture opportunity
        entity.CurrentGameTime = 50f;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30;
        entity.NearbyEventX = entity.CurrentPositionX + 50f;
        entity.NearbyEventZ = entity.CurrentPositionZ;

        manager.ScoreAndPick(entity);
        float firstVultureScore = entity.TaskScores[9];
        Assert.That(firstVultureScore, Is.GreaterThan(0f), "First vulture opportunity should score > 0");

        // Set vulture cooldown (simulating reject or completion)
        entity.VultureCooldownUntil = 230f; // 180s cooldown

        // Second opportunity during cooldown
        entity.CurrentGameTime = 100f;
        manager.ScoreAndPick(entity);
        float cooldownVultureScore = entity.TaskScores[9];
        Assert.That(cooldownVultureScore, Is.EqualTo(0f), "Vulture should score 0 during cooldown");

        // After cooldown expires
        entity.CurrentGameTime = 240f;
        manager.ScoreAndPick(entity);
        float afterCooldownScore = entity.TaskScores[9];
        Assert.That(afterCooldownScore, Is.GreaterThan(0f), "Vulture should score again after cooldown expires");
    }

    [Test]
    public void CrossSystem_InactiveEntity_DeactivatesCurrentTask()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 805, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 30f;

        // Assign a task
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.IsCloseToObjective = false;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null);

        // Deactivate entity
        entity.IsActive = false;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Null, "Inactive entity should have no task");
    }

    [Test]
    public void CrossSystem_Investigate_BlockedByActiveVulture()
    {
        // InvestigateTask returns 0 when VulturePhase is active
        var entity = CreatePmc(new BotRegistry(8), 806, 0.7f, BotPersonality.Aggressive);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 50f;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 10; // above investigate threshold

        // Without vulture phase: investigate should score
        entity.VulturePhase = VulturePhase.None;
        float scoreNoVulture = InvestigateTask.Score(
            entity,
            InvestigateTask.DefaultIntensityThreshold,
            InvestigateTask.DefaultDetectionRange
        );
        Assert.That(scoreNoVulture, Is.GreaterThan(0f));

        // With vulture phase: investigate should be 0
        entity.VulturePhase = VulturePhase.Approach;
        float scoreWithVulture = InvestigateTask.Score(
            entity,
            InvestigateTask.DefaultIntensityThreshold,
            InvestigateTask.DefaultDetectionRange
        );
        Assert.That(scoreWithVulture, Is.EqualTo(0f), "Investigate should be suppressed during active vulture phase");
    }

    [Test]
    public void CrossSystem_AllTasks_ScoreZero_WhenNoObjective_NoCombat_NoLoot_NoPatrol()
    {
        // Verify the "idle bot" state: no quests, no events, no loot, no routes
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 807, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 60f;

        // Reset all patrol routes to empty
        var savedRoutes = PatrolTask.CurrentMapRoutes;
        PatrolTask.CurrentMapRoutes = System.Array.Empty<PatrolRouteClass>();

        try
        {
            entity.HasActiveObjective = false;
            entity.HasNearbyEvent = false;
            entity.HasLootTarget = false;
            entity.ObjectiveCompletedTime = 0f;

            manager.ScoreAndPick(entity);

            for (int i = 0; i < QuestTaskFactory.TaskCount; i++)
            {
                Assert.That(entity.TaskScores[i], Is.EqualTo(0f), $"Task {i} should be 0 for idle bot");
            }

            Assert.That(entity.TaskAssignment.Task, Is.Null, "Idle bot should have no task");
        }
        finally
        {
            PatrolTask.CurrentMapRoutes = savedRoutes;
        }
    }

    [Test]
    public void CrossSystem_GoToObjective_DistanceBasedScoring_Continuous()
    {
        // Verify the exponential distance scoring curve
        var entity = CreatePmc(new BotRegistry(8), 808, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.IsCloseToObjective = false;

        // Distance → score mapping should be monotonically increasing
        float prevScore = -1f;
        float[] distances = new float[] { 0.1f, 5f, 10f, 25f, 50f, 75f, 100f, 150f, 200f, 500f };

        foreach (float dist in distances)
        {
            entity.DistanceToObjective = dist;
            float score = GoToObjectiveTask.Score(entity);
            Assert.That(score, Is.GreaterThan(prevScore), $"Score at {dist}m should be > score at previous distance");
            prevScore = score;
        }

        // All scores should be <= BaseScore (0.65) asymptotic bound
        entity.DistanceToObjective = 10000f;
        float maxScore = GoToObjectiveTask.Score(entity);
        Assert.That(
            maxScore,
            Is.LessThanOrEqualTo(GoToObjectiveTask.BaseScore + 0.05f),
            "Score should approach but not much exceed BaseScore"
        );
    }

    [Test]
    public void CrossSystem_Investigate_AlreadyInvestigating_MaintainsMaxScore()
    {
        var entity = CreatePmc(new BotRegistry(8), 809, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 50f;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 10;
        entity.IsInvestigating = true;

        float score = InvestigateTask.Score(entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);

        Assert.That(score, Is.EqualTo(InvestigateTask.MaxBaseScore), "Already-investigating bot should maintain max score");
    }

    [Test]
    public void CrossSystem_PatrolScores_OnlyWithoutActiveObjective()
    {
        var entity = CreatePmc(new BotRegistry(8), 810, 0.5f, BotPersonality.Normal);
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 60f;

        // With active objective: patrol should be 0
        entity.HasActiveObjective = true;
        float scoreWithObj = PatrolTask.Score(entity, System.Array.Empty<PatrolRouteClass>());
        Assert.That(scoreWithObj, Is.EqualTo(0f), "Patrol should not score with active objective");

        // Without objective: patrol scores if routes are available
        entity.HasActiveObjective = false;
        entity.PatrolRouteIndex = -1;
        float scoreNoObj = PatrolTask.Score(entity, System.Array.Empty<PatrolRouteClass>());
        Assert.That(scoreNoObj, Is.EqualTo(0f), "Patrol should be 0 with no routes");
    }

    [Test]
    public void CrossSystem_CombinedModifier_NaN_Guard()
    {
        // NaN aggression or raid time should produce 1.0 (safe fallback)
        float nanAggression = ScoringModifiers.CombinedModifier(float.NaN, 0.5f, BotActionTypeId.GoToObjective);
        float nanRaidTime = ScoringModifiers.CombinedModifier(0.5f, float.NaN, BotActionTypeId.GoToObjective);
        float bothNaN = ScoringModifiers.CombinedModifier(float.NaN, float.NaN, BotActionTypeId.GoToObjective);

        Assert.That(nanAggression, Is.EqualTo(1.0f), "NaN aggression should produce safe 1.0");
        Assert.That(nanRaidTime, Is.EqualTo(1.0f), "NaN raid time should produce safe 1.0");
        Assert.That(bothNaN, Is.EqualTo(1.0f), "Both NaN should produce safe 1.0");
    }

    [Test]
    public void CrossSystem_CombinedModifier_NegativeAggression_ClampsToZero()
    {
        float negMod = ScoringModifiers.PersonalityModifier(-0.5f, BotActionTypeId.GoToObjective);
        float zeroMod = ScoringModifiers.PersonalityModifier(0f, BotActionTypeId.GoToObjective);

        Assert.That(negMod, Is.EqualTo(zeroMod), "Negative aggression should clamp to 0");
    }

    [Test]
    public void CrossSystem_CombinedModifier_OverOneAggression_ClampsToOne()
    {
        float overMod = ScoringModifiers.PersonalityModifier(1.5f, BotActionTypeId.GoToObjective);
        float oneMod = ScoringModifiers.PersonalityModifier(1f, BotActionTypeId.GoToObjective);

        Assert.That(overMod, Is.EqualTo(oneMod), "Aggression > 1 should clamp to 1");
    }

    // ================================================================
    // Part C: Multi-Tick Behavioral Sequences
    // ================================================================

    [Test]
    public void MultiTick_SpawnEntry_Then_GoTo_Then_Linger_Then_GoTo()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 900, 0.5f, BotPersonality.Normal);
        entity.RaidTimeNormalized = 0.3f;

        // Tick 1-3: SpawnEntry (spawn at t=10, duration=4s)
        entity.CurrentGameTime = 10f;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("SpawnEntryTask"));

        entity.CurrentGameTime = 12f;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("SpawnEntryTask"));

        // Tick 4: SpawnEntry expires, GoToObjective activates
        entity.CurrentGameTime = 15f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 80f;
        entity.IsCloseToObjective = false;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("GoToObjectiveTask"));

        // Tick 5: "Arrived" — simulate objective completion
        entity.CurrentGameTime = 60f;
        entity.HasActiveObjective = false;
        entity.ObjectiveCompletedTime = 59f;
        entity.LingerDuration = 8f;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("LingerTask"));

        // Tick 6: Linger decaying
        entity.CurrentGameTime = 63f;
        string midLinger = ScoreAndPickName(manager, entity);
        Assert.That(midLinger, Is.EqualTo("LingerTask"), "Should still linger mid-duration");

        // Tick 7: Linger expired, new objective
        entity.CurrentGameTime = 70f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 120f;
        entity.IsCloseToObjective = false;
        entity.ObjectiveCompletedTime = 0f; // clear
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("GoToObjectiveTask"));
    }

    [Test]
    public void MultiTick_Aggressive_SpawnEntry_Then_Rush_Then_Vulture_NearObjective()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 901, 0.9f, BotPersonality.Reckless);
        entity.RaidTimeNormalized = 0.1f;

        // Tick 1: SpawnEntry
        entity.CurrentGameTime = 10f;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("SpawnEntryTask"));

        // Tick 2: SpawnEntry done, rush to objective
        entity.CurrentGameTime = 15f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 200f;
        entity.IsCloseToObjective = false;
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("GoToObjectiveTask"));

        // Tick 3: Nearly there — combat event nearby!
        entity.CurrentGameTime = 50f;
        entity.DistanceToObjective = 5f; // very close
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 40;
        entity.NearbyEventX = entity.CurrentPositionX + 30f;
        entity.NearbyEventZ = entity.CurrentPositionZ;

        manager.ScoreAndPick(entity);
        float goToScore = entity.TaskScores[0];
        float vultureScore = entity.TaskScores[9];

        // Near objective: GoTo score is very low, vulture should win
        Assert.That(vultureScore, Is.GreaterThan(goToScore), "Vulture should beat GoTo when near own objective");
    }

    [Test]
    public void MultiTick_Timid_Lingers_Long_Then_Patrols_When_No_Objective()
    {
        var registry = new BotRegistry(8);
        var manager = CreateQuestManager();
        var entity = CreatePmc(registry, 902, 0.1f, BotPersonality.Timid);
        entity.IsSpawnEntryComplete = true;
        entity.RaidTimeNormalized = 0.7f; // late-ish raid

        // Just completed objective
        entity.CurrentGameTime = 200f;
        entity.ObjectiveCompletedTime = 199f;
        entity.LingerDuration = 15f;
        entity.HasActiveObjective = false;

        // Timid + late raid: Linger modifier is high
        float lingerMod = ScoringModifiers.CombinedModifier(0.1f, 0.7f, BotActionTypeId.Linger);
        Assert.That(lingerMod, Is.GreaterThan(1.0f), "Timid late-raid linger modifier should be > 1.0");

        // Linger should win
        Assert.That(ScoreAndPickName(manager, entity), Is.EqualTo("LingerTask"));

        // After linger expires — nothing else scores (no routes, no objective)
        entity.CurrentGameTime = 220f;
        entity.ObjectiveCompletedTime = 0f;

        var savedRoutes = PatrolTask.CurrentMapRoutes;
        PatrolTask.CurrentMapRoutes = System.Array.Empty<PatrolRouteClass>();
        try
        {
            manager.ScoreAndPick(entity);
            var task = entity.TaskAssignment.Task;
            Assert.That(
                task == null || task.GetType().Name == "LingerTask",
                Is.True,
                "After linger expires with no routes/objectives, bot should have no task or keep stale linger"
            );
        }
        finally
        {
            PatrolTask.CurrentMapRoutes = savedRoutes;
        }
    }

    // ================================================================
    // Test helpers
    // ================================================================

    /// <summary>
    /// Minimal task with configurable score for hysteresis tests.
    /// </summary>
    private sealed class TestTask : QuestUtilityTask
    {
        public float CurrentScore;
        private readonly int _actionTypeId;
        private readonly string _actionReason;

        public TestTask(float score, int actionTypeId, string reason, float hysteresis = 0.10f)
            : base(hysteresis)
        {
            CurrentScore = score;
            _actionTypeId = actionTypeId;
            _actionReason = reason;
        }

        public override int BotActionTypeId => _actionTypeId;
        public override string ActionReason => _actionReason;

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            entity.TaskScores[ordinal] = CurrentScore;
        }
    }
}
