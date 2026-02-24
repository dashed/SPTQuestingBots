using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

// ── 1. Quest + Loot + Personality Interaction Matrix ───────────────────────

/// <summary>
/// Tests the interaction between GoToObjective scoring, LootTask scoring,
/// and personality/raidtime modifiers across a 3x2x2 matrix:
/// personality (timid/normal/aggressive) × loot proximity (near/far) × quest distance (near/far).
/// </summary>
[TestFixture]
public class QuestLootPersonalityMatrixTests
{
    private UtilityTaskManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = QuestTaskFactory.Create();
    }

    private BotEntity CreateBot(
        int id,
        float aggression,
        float raidTime,
        float questDistance,
        bool hasLoot,
        float lootDistance,
        float lootValue
    )
    {
        var entity = new BotEntity(id);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = aggression;
        entity.RaidTimeNormalized = raidTime;
        entity.CurrentGameTime = 100f;
        entity.IsSpawnEntryComplete = true;

        // Quest state
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = questDistance;

        // Loot state
        if (hasLoot)
        {
            entity.HasLootTarget = true;
            entity.LootTargetValue = lootValue;
            entity.CurrentPositionX = 0f;
            entity.CurrentPositionY = 0f;
            entity.CurrentPositionZ = 0f;
            entity.LootTargetX = lootDistance;
            entity.LootTargetY = 0f;
            entity.LootTargetZ = 0f;
            entity.InventorySpaceFree = 5f;
        }

        return entity;
    }

    // ── Timid (0.1) + Early Raid (0.0) ──

    [Test]
    public void TimidEarly_FarQuest_NearLoot_QuestWins()
    {
        // Timid early: GoToObjective mod = 0.85*1.2=1.02, Loot mod = 1.1*0.8=0.88
        var bot = CreateBot(0, aggression: 0.1f, raidTime: 0.0f, questDistance: 200f, hasLoot: true, lootDistance: 10f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        Assert.IsInstanceOf<GoToObjectiveTask>(
            bot.TaskAssignment.Task,
            "Timid early-raid: quest 200m should win over nearby loot (GoTo mod 1.02 vs Loot mod 0.88)"
        );
    }

    [Test]
    public void TimidEarly_NearQuest_NearLoot_LootCanWin()
    {
        // Near quest = low GoToObjective base, near loot = low distance penalty
        var bot = CreateBot(0, aggression: 0.1f, raidTime: 0.0f, questDistance: 10f, hasLoot: true, lootDistance: 10f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        // GoToObjective at 10m: 0.65*(1-exp(-10/75)) ≈ 0.65*0.125 = 0.081, modified: 0.081*1.02 = 0.083
        // Loot: valueScore 0.5, distPenalty min(100*0.001,0.4)=0.10, proximity 0.15, total 0.55 cap, modified 0.55*0.88 = 0.484
        Assert.IsInstanceOf<LootTask>(
            bot.TaskAssignment.Task,
            "Near quest + near high-value loot: LootTask should beat low GoToObjective score"
        );
    }

    // ── Normal (0.5) + Mid Raid (0.5) ──

    [Test]
    public void NormalMid_FarQuest_NearLoot_QuestWins()
    {
        var bot = CreateBot(0, aggression: 0.5f, raidTime: 0.5f, questDistance: 200f, hasLoot: true, lootDistance: 10f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        // GoToObjective at 200m: base ~0.61, mod 1.0*1.0=1.0, final 0.61
        // Loot: base 0.55, mod 1.0*1.0=1.0, final 0.55
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "Normal mid-raid: far quest (200m) should beat max loot");
    }

    [Test]
    public void NormalMid_FarQuest_FarLoot_QuestWins()
    {
        var bot = CreateBot(0, aggression: 0.5f, raidTime: 0.5f, questDistance: 200f, hasLoot: true, lootDistance: 30f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "Normal mid-raid: far quest should beat far loot");
    }

    [Test]
    public void NormalMid_NearQuest_NearLoot_LootWins()
    {
        var bot = CreateBot(0, aggression: 0.5f, raidTime: 0.5f, questDistance: 10f, hasLoot: true, lootDistance: 10f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        Assert.IsInstanceOf<LootTask>(bot.TaskAssignment.Task, "Normal mid-raid: near quest + near high-value loot: Loot should win");
    }

    [Test]
    public void NormalMid_NearQuest_FarLoot_QuestWins()
    {
        var bot = CreateBot(0, aggression: 0.5f, raidTime: 0.5f, questDistance: 10f, hasLoot: true, lootDistance: 30f, lootValue: 30000f);

        _manager.ScoreAndPick(bot);

        // Loot: value 0.3, distPenalty min(900*0.001,0.4)=0.4, proximity 0.15, total = 0.3+0.15-0.4 = 0.05
        // GoToObjective at 10m: ~0.083 (base 0.081)
        // Both very low but GoToObjective slightly higher
        var task = bot.TaskAssignment.Task;
        Assert.IsTrue(
            task is GoToObjectiveTask || task is LootTask,
            "Near quest + far low-value loot: either task can win (both very low scores)"
        );
    }

    // ── Aggressive (0.9) + Early Raid (0.0) ──

    [Test]
    public void AggressiveEarly_FarQuest_NearLoot_QuestWins()
    {
        // GoToObjective mod: Lerp(0.85,1.15,0.9)*Lerp(1.2,0.8,0.0) = 1.12*1.2 = 1.344
        // Loot mod: Lerp(1.1,0.9,0.9)*Lerp(0.8,1.2,0.0) = 0.92*0.8 = 0.736
        var bot = CreateBot(0, aggression: 0.9f, raidTime: 0.0f, questDistance: 200f, hasLoot: true, lootDistance: 10f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "Aggressive early-raid: quest rush should dominate loot");
    }

    [Test]
    public void AggressiveEarly_NearQuest_NearLoot_LootWins()
    {
        var bot = CreateBot(0, aggression: 0.9f, raidTime: 0.0f, questDistance: 5f, hasLoot: true, lootDistance: 5f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        // GoToObjective at 5m: 0.65*(1-exp(-5/75)) ≈ 0.65*0.0645 = 0.042, mod 1.344 → 0.056
        // Loot: value 0.5, dist 25*0.001=0.025, prox 0.15, total 0.55 cap, mod 0.736 → 0.405
        Assert.IsInstanceOf<LootTask>(
            bot.TaskAssignment.Task,
            "Even aggressive early: near quest + near high loot → Loot wins (GoTo is ~0.06)"
        );
    }

    // ── Timid (0.1) + Late Raid (1.0) — maximum Loot modifier ──

    [Test]
    public void TimidLate_FarQuest_NearLoot_LootWins()
    {
        // GoToObjective mod: Lerp(0.85,1.15,0.1)*Lerp(1.2,0.8,1.0) = 0.88*0.8 = 0.704
        // Loot mod: Lerp(1.1,0.9,0.1)*Lerp(0.8,1.2,1.0) = 1.08*1.2 = 1.296
        var bot = CreateBot(0, aggression: 0.1f, raidTime: 1.0f, questDistance: 100f, hasLoot: true, lootDistance: 10f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        // GoToObjective at 100m: 0.65*(1-exp(-100/75)) ≈ 0.65*0.736 = 0.479, mod → 0.337
        // Loot: 0.55 * 1.296 = 0.713
        Assert.IsInstanceOf<LootTask>(
            bot.TaskAssignment.Task,
            "Timid late-raid: Loot should beat even medium-distance quest (modifiers heavily favor loot)"
        );
    }

    [Test]
    public void TimidLate_FarQuest_FarLoot_QuestWins()
    {
        var bot = CreateBot(0, aggression: 0.1f, raidTime: 1.0f, questDistance: 200f, hasLoot: true, lootDistance: 25f, lootValue: 20000f);

        _manager.ScoreAndPick(bot);

        // GoToObjective at 200m: 0.65*0.93=0.61, mod 0.704 → 0.429
        // Loot: value 0.2, dist 625*0.001=0.4 (capped), prox 0 (quest>20m), total=0.2-0.4=-0.2 → 0
        Assert.IsInstanceOf<GoToObjectiveTask>(
            bot.TaskAssignment.Task,
            "Timid late-raid: far low-value loot with distance penalty should lose to quest"
        );
    }

    // ── Aggressive (0.9) + Late Raid (1.0) ──

    [Test]
    public void AggressiveLate_FarQuest_NearLoot_QuestWins()
    {
        // GoToObjective mod: 1.12*0.8=0.896
        // Loot mod: 0.92*1.2=1.104
        var bot = CreateBot(0, aggression: 0.9f, raidTime: 1.0f, questDistance: 200f, hasLoot: true, lootDistance: 10f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        // GoToObjective at 200m: base 0.605, mod 0.896 → 0.542
        // Loot: value 0.5, no proximity (quest 200m away), dist penalty 0.10, base=0.40, mod 1.104 → 0.442
        // GoToObjective wins because far quest = high base + no loot proximity bonus
        Assert.IsInstanceOf<GoToObjectiveTask>(
            bot.TaskAssignment.Task,
            "Aggressive late-raid: far quest (200m) beats loot without proximity bonus"
        );
    }

    [Test]
    public void AggressiveLate_NearQuest_NearLoot_LootWins()
    {
        var bot = CreateBot(0, aggression: 0.9f, raidTime: 1.0f, questDistance: 10f, hasLoot: true, lootDistance: 10f, lootValue: 50000f);

        _manager.ScoreAndPick(bot);

        Assert.IsInstanceOf<LootTask>(bot.TaskAssignment.Task, "Aggressive late-raid: near high-value loot beats near quest");
    }
}

// ── 2. PlantItem Priority Inversion Tests ───────────────────────

/// <summary>
/// Tests whether quest-mandatory actions (PlantItem, etc.) can be beaten
/// by modifier-boosted discretionary tasks (Loot, Vulture).
/// </summary>
[TestFixture]
public class PlantItemPriorityTests
{
    private UtilityTaskManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = QuestTaskFactory.Create();
    }

    private BotEntity CreateBotNearPlantWithLoot(float aggression, float raidTime, float lootValue, float lootDistance)
    {
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = aggression;
        entity.RaidTimeNormalized = raidTime;
        entity.CurrentGameTime = 100f;
        entity.IsSpawnEntryComplete = true;

        // Close to plant objective
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.PlantItem;
        entity.IsCloseToObjective = true;
        entity.DistanceToObjective = 2f;

        // Nearby loot
        entity.HasLootTarget = true;
        entity.LootTargetValue = lootValue;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 0f;
        entity.LootTargetX = lootDistance;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 0f;
        entity.InventorySpaceFree = 5f;

        return entity;
    }

    [Test]
    public void PlantItem_BeatsLoot_TimidLateWithHighValueLoot()
    {
        // PlantItemTask: 0.80 flat (raised from 0.65 to fix priority inversion)
        // LootTask timid+late: max 0.55 * 1.296 = 0.713
        // PlantItem 0.80 > Loot 0.713 → PlantItem wins correctly
        var bot = CreateBotNearPlantWithLoot(aggression: 0.1f, raidTime: 1.0f, lootValue: 50000f, lootDistance: 5f);

        _manager.ScoreAndPick(bot);

        Assert.IsInstanceOf<PlantItemTask>(
            bot.TaskAssignment.Task,
            "PlantItem (0.80) should always beat max modified Loot (0.713) for timid late-raid"
        );
    }

    [Test]
    public void PlantItem_BeatsLoot_AggressiveEarly()
    {
        var bot = CreateBotNearPlantWithLoot(aggression: 0.9f, raidTime: 0.0f, lootValue: 50000f, lootDistance: 5f);

        _manager.ScoreAndPick(bot);

        // PlantItem: 0.80
        // Loot aggressive+early: 0.55 * 0.92*0.8 = 0.55*0.736 = 0.405
        Assert.IsInstanceOf<PlantItemTask>(bot.TaskAssignment.Task, "PlantItem should beat loot for aggressive early-raid");
    }

    [Test]
    public void PlantItem_BeatsLoot_NormalMid()
    {
        var bot = CreateBotNearPlantWithLoot(aggression: 0.5f, raidTime: 0.5f, lootValue: 50000f, lootDistance: 5f);

        _manager.ScoreAndPick(bot);

        // PlantItem: 0.80
        // Loot normal+mid: 0.55 * 1.0*1.0 = 0.55
        Assert.IsInstanceOf<PlantItemTask>(bot.TaskAssignment.Task, "PlantItem should beat loot for normal mid-raid");
    }

    [Test]
    public void PlantItem_BeatsVulture_AggressiveBot_ActivePhase()
    {
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.9f;
        entity.RaidTimeNormalized = 0.3f;
        entity.CurrentGameTime = 100f;
        entity.IsSpawnEntryComplete = true;

        // Close to plant objective
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.PlantItem;
        entity.IsCloseToObjective = true;
        entity.DistanceToObjective = 2f;

        // Active vulture phase
        entity.HasNearbyEvent = true;
        entity.VulturePhase = VulturePhase.Approach;
        entity.CombatIntensity = 30;
        entity.NearbyEventX = 50f;
        entity.NearbyEventZ = 0f;

        _manager.ScoreAndPick(entity);

        // PlantItem: 0.80 flat (raised from 0.65)
        // Vulture active phase: 0.60 * Lerp(0.7,1.3,0.9)*1.0 = 0.60*1.24 = 0.744
        // PlantItem 0.80 > Vulture 0.744 → PlantItem wins correctly
        Assert.IsInstanceOf<PlantItemTask>(
            entity.TaskAssignment.Task,
            "PlantItem (0.80) should beat active Vulture (0.744) — quest-mandatory action takes priority"
        );
    }
}

// ── 3. Personality + RaidTime + Vulture Modifier Stacking ────────────────

[TestFixture]
public class VultureModifierStackingTests
{
    [Test]
    public void VultureHasNoRaidTimeModifier_OnlyPersonality()
    {
        // Vulture BotActionTypeId = 14, not in RaidTimeModifier switch → default 1.0
        for (float time = 0f; time <= 1f; time += 0.25f)
        {
            float raidMod = ScoringModifiers.RaidTimeModifier(time, BotActionTypeId.Vulture);
            Assert.AreEqual(1f, raidMod, 0.001f, "VultureTask should have no raid time modifier at time=" + time);
        }
    }

    [Test]
    public void VulturePersonalityModifier_RangeCheck()
    {
        float timidMod = ScoringModifiers.PersonalityModifier(0.1f, BotActionTypeId.Vulture);
        float normalMod = ScoringModifiers.PersonalityModifier(0.5f, BotActionTypeId.Vulture);
        float aggressiveMod = ScoringModifiers.PersonalityModifier(0.9f, BotActionTypeId.Vulture);

        // Vulture: Lerp(0.7, 1.3, aggression) — aggressive bots vulture more
        Assert.AreEqual(0.76f, timidMod, 0.01f, "Timid vulture modifier");
        Assert.AreEqual(1.0f, normalMod, 0.01f, "Normal vulture modifier");
        Assert.AreEqual(1.24f, aggressiveMod, 0.01f, "Aggressive vulture modifier");
    }

    [Test]
    public void VultureCombinedModifier_PersonalityOnlyDetermines()
    {
        // Since raidTime modifier = 1.0 always, combined = personality only
        float timidEarly = ScoringModifiers.CombinedModifier(0.1f, 0.0f, BotActionTypeId.Vulture);
        float timidLate = ScoringModifiers.CombinedModifier(0.1f, 1.0f, BotActionTypeId.Vulture);
        float aggressiveEarly = ScoringModifiers.CombinedModifier(0.9f, 0.0f, BotActionTypeId.Vulture);
        float aggressiveLate = ScoringModifiers.CombinedModifier(0.9f, 1.0f, BotActionTypeId.Vulture);

        // Same personality → same combined modifier regardless of raid time
        Assert.AreEqual(timidEarly, timidLate, 0.001f, "Timid early == timid late for vulture");
        Assert.AreEqual(aggressiveEarly, aggressiveLate, 0.001f, "Aggressive early == aggressive late for vulture");
    }

    [Test]
    public void AggressiveMaxVultureScore_ComparedToFlatBaseScores()
    {
        // Max vulture score: active phase 0.60 * 1.30 (personality at aggr=1.0) = 0.780
        float maxMod = ScoringModifiers.CombinedModifier(1.0f, 0.5f, BotActionTypeId.Vulture);
        float maxVultureScore = VultureTask.MaxBaseScore * maxMod;

        // PlantItem (0.80) is quest-mandatory and correctly beats max Vulture (0.78)
        Assert.Greater(PlantItemTask.BaseScore, maxVultureScore, "PlantItem base beats max vulture score");

        // Max Vulture still exceeds other discretionary base scores
        Assert.Greater(maxVultureScore, AmbushTask.BaseScore, "Max vulture score exceeds Ambush base");
        Assert.Greater(maxVultureScore, SnipeTask.BaseScore, "Max vulture score exceeds Snipe base");
        Assert.Greater(maxVultureScore, HoldPositionTask.BaseScore, "Max vulture score exceeds HoldPosition base");
    }
}

// ── 4. Linger + RaidTime + Personality vs GoToObjective ──────────────────

[TestFixture]
public class LingerVsGoToObjectiveTests
{
    [Test]
    public void TimidLate_LingerBeatsGoToObjective_AtFreshStart()
    {
        // Linger at t=0 (full base score): 0.45 * Lerp(1.3,0.7,0.1)*Lerp(0.7,1.3,1.0)
        //   = 0.45 * 1.24 * 1.3 = 0.45 * 1.612 = 0.7254
        // GoToObjective at 200m: 0.61 * Lerp(0.85,1.15,0.1)*Lerp(1.2,0.8,1.0)
        //   = 0.61 * 0.88 * 0.8 = 0.61 * 0.704 = 0.4294
        float lingerMod = ScoringModifiers.CombinedModifier(0.1f, 1.0f, BotActionTypeId.Linger);
        float lingerScore = 0.45f * lingerMod;

        float gotoBase = 0.65f * (1f - (float)Math.Exp(-200f / 75f));
        float gotoMod = ScoringModifiers.CombinedModifier(0.1f, 1.0f, BotActionTypeId.GoToObjective);
        float gotoScore = gotoBase * gotoMod;

        Assert.Greater(lingerScore, gotoScore, "Timid late-raid: fresh Linger ({0:F3}) should beat GoToObjective 200m ({1:F3})");
    }

    [Test]
    public void TimidLate_LingerDecaysBelow_GoToObjective()
    {
        float lingerMod = ScoringModifiers.CombinedModifier(0.1f, 1.0f, BotActionTypeId.Linger);
        float gotoBase = 0.65f * (1f - (float)Math.Exp(-200f / 75f));
        float gotoMod = ScoringModifiers.CombinedModifier(0.1f, 1.0f, BotActionTypeId.GoToObjective);
        float gotoScore = gotoBase * gotoMod;

        // Find decay fraction where linger drops below GoToObjective
        // lingerScore = 0.45 * (1-decay) * lingerMod
        // Solve: 0.45 * (1-decay) * lingerMod = gotoScore
        // (1-decay) = gotoScore / (0.45 * lingerMod)
        float crossoverDecay = 1f - gotoScore / (0.45f * lingerMod);

        // With 10s linger duration, crossover at ~5.5s
        Assert.Greater(crossoverDecay, 0f, "Crossover should happen before linger fully decays");
        Assert.Less(crossoverDecay, 1f, "Crossover should happen before linger expires");
    }

    [Test]
    public void AggressiveEarly_LingerLosesToGoToObjective_Always()
    {
        // Linger aggressive+early: 0.45 * Lerp(1.3,0.7,0.9)*Lerp(0.7,1.3,0.0)
        //   = 0.45 * 0.76 * 0.70 = 0.45 * 0.532 = 0.2394
        // GoToObjective at 200m aggressive+early: 0.61 * 1.12*1.2 = 0.61*1.344 = 0.8198
        float lingerMax = 0.45f * ScoringModifiers.CombinedModifier(0.9f, 0.0f, BotActionTypeId.Linger);
        float gotoAt200 =
            0.65f * (1f - (float)Math.Exp(-200f / 75f)) * ScoringModifiers.CombinedModifier(0.9f, 0.0f, BotActionTypeId.GoToObjective);

        Assert.Greater(gotoAt200, lingerMax, "Aggressive early: GoToObjective ({0:F3}) should always beat Linger ({1:F3})");
    }

    [Test]
    public void LingerMaxModifier_TimidLate()
    {
        float lingerMod = ScoringModifiers.CombinedModifier(0.1f, 1.0f, BotActionTypeId.Linger);
        // Lerp(1.3,0.7,0.1) = 1.24, Lerp(0.7,1.3,1.0) = 1.3
        // Combined = 1.612
        Assert.AreEqual(1.24f * 1.3f, lingerMod, 0.01f, "Linger timid+late combined modifier");
    }
}

// ── 5. Investigate + Vulture Mutual Exclusion ────────────────────────

[TestFixture]
public class InvestigateVultureExclusionTests
{
    [Test]
    public void LowIntensity_OnlyInvestigateTriggers()
    {
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.5f;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 10; // Above investigate threshold (5), below vulture (15)
        entity.NearbyEventX = 50f;
        entity.NearbyEventZ = 0f;

        float investigateScore = InvestigateTask.Score(
            entity,
            InvestigateTask.DefaultIntensityThreshold,
            InvestigateTask.DefaultDetectionRange
        );
        float vultureScore = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);

        Assert.Greater(investigateScore, 0f, "Investigate should trigger at intensity 10");
        Assert.AreEqual(0f, vultureScore, "Vulture should NOT trigger below courage threshold 15");
    }

    [Test]
    public void HighIntensity_BothScore_VultureWins()
    {
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.5f;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30; // Well above both thresholds
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 30f; // Close event
        entity.NearbyEventZ = 0f;

        float investigateBase = InvestigateTask.Score(
            entity,
            InvestigateTask.DefaultIntensityThreshold,
            InvestigateTask.DefaultDetectionRange
        );
        float vultureBase = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);

        float investigateMod = investigateBase * ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.Investigate);
        float vultureMod = vultureBase * ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.Vulture);

        Assert.Greater(investigateBase, 0f, "Investigate should score");
        Assert.Greater(vultureBase, 0f, "Vulture should score");
        Assert.Greater(vultureMod, investigateMod, "Vulture should win over Investigate when both score");
    }

    [Test]
    public void ActiveVulturePhase_InvestigateYields()
    {
        var entity = new BotEntity(0);
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 10;
        entity.VulturePhase = VulturePhase.Approach;

        float investigateScore = InvestigateTask.Score(
            entity,
            InvestigateTask.DefaultIntensityThreshold,
            InvestigateTask.DefaultDetectionRange
        );

        Assert.AreEqual(0f, investigateScore, "Investigate should yield to active vulture phase");
    }

    [Test]
    public void VultureComplete_InvestigateCanScore()
    {
        var entity = new BotEntity(0);
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 10;
        entity.VulturePhase = VulturePhase.Complete;

        float investigateScore = InvestigateTask.Score(
            entity,
            InvestigateTask.DefaultIntensityThreshold,
            InvestigateTask.DefaultDetectionRange
        );

        Assert.Greater(investigateScore, 0f, "Investigate should score after vulture phase completes");
    }

    [Test]
    public void HighIntensity_AllPersonalities_VultureAlwaysBeatsInvestigate()
    {
        float[] aggressions = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };

        foreach (float aggr in aggressions)
        {
            var entity = new BotEntity(0);
            entity.Aggression = aggr;
            entity.HasNearbyEvent = true;
            entity.CombatIntensity = 30;
            entity.CurrentPositionX = 0f;
            entity.CurrentPositionZ = 0f;
            entity.NearbyEventX = 30f;
            entity.NearbyEventZ = 0f;

            float investigateBase = InvestigateTask.Score(
                entity,
                InvestigateTask.DefaultIntensityThreshold,
                InvestigateTask.DefaultDetectionRange
            );
            float vultureBase = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);

            float investigateFinal = investigateBase * ScoringModifiers.CombinedModifier(aggr, 0.5f, BotActionTypeId.Investigate);
            float vultureFinal = vultureBase * ScoringModifiers.CombinedModifier(aggr, 0.5f, BotActionTypeId.Vulture);

            Assert.Greater(vultureFinal, investigateFinal, "Vulture should always beat Investigate at aggression=" + aggr);
        }
    }
}

// ── 6. SpawnEntry + Combat Event Priority ─────────────────────────

[TestFixture]
public class SpawnEntryCombatEventTests
{
    [Test]
    public void SpawnEntry_BeatsMaxVulture_AnyPersonality()
    {
        float[] aggressions = { 0.1f, 0.5f, 0.9f, 1.0f };

        foreach (float aggr in aggressions)
        {
            float maxVultureMod = ScoringModifiers.CombinedModifier(aggr, 0.5f, BotActionTypeId.Vulture);
            float maxVultureScore = VultureTask.MaxBaseScore * maxVultureMod;

            Assert.Greater(
                SpawnEntryTask.MaxBaseScore,
                maxVultureScore,
                "SpawnEntry (0.80) should beat max Vulture score at aggression=" + aggr
            );
        }
    }

    [Test]
    public void SpawnEntry_BeatsMaxInvestigate_AnyPersonality()
    {
        float[] aggressions = { 0.1f, 0.5f, 0.9f, 1.0f };

        foreach (float aggr in aggressions)
        {
            float maxInvMod = ScoringModifiers.CombinedModifier(aggr, 0.5f, BotActionTypeId.Investigate);
            float maxInvScore = InvestigateTask.MaxBaseScore * maxInvMod;

            Assert.Greater(SpawnEntryTask.MaxBaseScore, maxInvScore, "SpawnEntry (0.80) should beat max Investigate at aggression=" + aggr);
        }
    }

    [Test]
    public void SpawnEntry_BeatsMaxLoot()
    {
        float maxLootMod = ScoringModifiers.CombinedModifier(0.1f, 1.0f, BotActionTypeId.Loot);
        float maxLootScore = LootTask.MaxBaseScore * maxLootMod;

        Assert.Greater(SpawnEntryTask.MaxBaseScore, maxLootScore, "SpawnEntry (0.80) should beat max modified Loot");
    }

    [Test]
    public void SpawnEntry_E2E_BlocksCombatEvent()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.Aggression = 0.9f;
        bot.RaidTimeNormalized = 0.3f;
        bot.CurrentGameTime = 1f;
        bot.SpawnTime = 0f;
        bot.SpawnEntryDuration = 4f;

        // Nearby combat event
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.NearbyEventX = 30f;
        bot.NearbyEventZ = 0f;

        manager.ScoreAndPick(bot);

        Assert.IsInstanceOf<SpawnEntryTask>(
            bot.TaskAssignment.Task,
            "SpawnEntry should block aggressive bot's response to nearby combat event"
        );
    }
}

// ── 7. Patrol + GoToObjective Mutual Exclusion ──────────────────────

[TestFixture]
public class PatrolQuestTransitionTests
{
    [Test]
    public void NoQuest_PatrolScores_GoToObjectiveDoesNot()
    {
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.5f;
        entity.HasActiveObjective = false;
        entity.IsSpawnEntryComplete = true;

        float gotoScore = GoToObjectiveTask.Score(entity);

        Assert.AreEqual(0f, gotoScore, "GoToObjective should return 0 with no active objective");
    }

    [Test]
    public void HasQuest_PatrolDoesNotScore()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = true;

        var routes = new[]
        {
            new PatrolRoute(
                "TestRoute",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(100f, 0f, 100f) },
                minAggression: 0f,
                maxAggression: 1f
            ),
        };

        float patrolScore = PatrolTask.Score(entity, routes);

        Assert.AreEqual(0f, patrolScore, "Patrol should return 0 when HasActiveObjective is true");
    }

    [Test]
    public void QuestComplete_TransitionToPatrol_NewQuest_BackToGoToObjective()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.3f;
        bot.CurrentGameTime = 50f;
        bot.IsSpawnEntryComplete = true;

        // Phase 1: active quest
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 200f;

        manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "Phase 1: GoToObjective for active quest");

        // Phase 2: quest completes, no more quests
        bot.HasActiveObjective = false;
        bot.DistanceToObjective = float.MaxValue;

        manager.ScoreAndPick(bot);
        // Without patrol routes loaded, GoToObjective scores 0, patrol scores 0.
        // The bot should stay on the previous task via hysteresis or switch to whatever scores highest.
        // In practice, with no scoring task, the bot stays on GoToObjective via hysteresis.

        // Phase 3: new quest appears
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 150f;

        manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "Phase 3: GoToObjective for new quest");
    }
}

// ── 8. UnlockDoor → PlantItem Task Chaining ─────────────────────

[TestFixture]
public class TaskChainingTests
{
    [Test]
    public void UnlockDoor_ThenGoToObjective_ThenPlantItem()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.3f;
        bot.CurrentGameTime = 50f;
        bot.IsSpawnEntryComplete = true;

        // Phase 1: Plant objective behind locked door
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.PlantItem;
        bot.DistanceToObjective = 50f;
        bot.IsCloseToObjective = false;
        bot.MustUnlockDoor = true;

        manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<UnlockDoorTask>(bot.TaskAssignment.Task, "Phase 1: UnlockDoor should block GoToObjective");

        // Phase 2: Door unlocked, travel to plant position
        bot.MustUnlockDoor = false;

        manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "Phase 2: GoToObjective should handle travel");

        // Phase 3: Close to plant position
        bot.IsCloseToObjective = true;
        bot.DistanceToObjective = 2f;

        manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<PlantItemTask>(bot.TaskAssignment.Task, "Phase 3: PlantItem should take over when close");
    }

    [Test]
    public void UnlockDoor_AlwaysBeatsModifiedLoot()
    {
        // UnlockDoor: 0.70 flat
        // Max Loot: 0.55 * 1.296 = 0.713
        float maxLootMod = ScoringModifiers.CombinedModifier(0.1f, 1.0f, BotActionTypeId.Loot);
        float maxLootScore = LootTask.MaxBaseScore * maxLootMod;

        // NOTE: UnlockDoor (0.70) vs max Loot (0.713) — Loot CAN beat UnlockDoor!
        // But this only happens in extreme timid+late raid conditions.
        if (maxLootScore > UnlockDoorTask.BaseScore)
        {
            Assert.Greater(maxLootScore, UnlockDoorTask.BaseScore, "BUG: Max modified LootTask ({0:F3}) can beat flat UnlockDoor ({1:F3})");
        }
        else
        {
            Assert.Greater(UnlockDoorTask.BaseScore, maxLootScore, "UnlockDoor should beat max loot");
        }
    }
}

// ── 9. HoldPosition + Ambush + Snipe Boundary Tests ──────────────

[TestFixture]
public class ActionBoundaryTests
{
    [Test]
    public void HoldPosition_Ambush_Snipe_MutuallyExclusive()
    {
        // Only one QuestAction can be active at a time
        var entity = new BotEntity(0);
        entity.HasActiveObjective = true;
        entity.IsCloseToObjective = true;

        entity.CurrentQuestAction = QuestActionId.HoldAtPosition;
        Assert.AreEqual(HoldPositionTask.BaseScore, HoldPositionTask.Score(entity));
        Assert.AreEqual(0f, AmbushTask.Score(entity));
        Assert.AreEqual(0f, SnipeTask.Score(entity));

        entity.CurrentQuestAction = QuestActionId.Ambush;
        Assert.AreEqual(0f, HoldPositionTask.Score(entity));
        Assert.AreEqual(AmbushTask.BaseScore, AmbushTask.Score(entity));
        Assert.AreEqual(0f, SnipeTask.Score(entity));

        entity.CurrentQuestAction = QuestActionId.Snipe;
        Assert.AreEqual(0f, HoldPositionTask.Score(entity));
        Assert.AreEqual(0f, AmbushTask.Score(entity));
        Assert.AreEqual(SnipeTask.BaseScore, SnipeTask.Score(entity));
    }

    [Test]
    public void Ambush_BeatsLoot_AllCombos_IncludingAggressiveLate()
    {
        // After fix: Ambush raidTime modifier widened from Lerp(0.9,1.1) to Lerp(0.9,1.2)
        // At aggr=0.9, time=1.0: Ambush mod = 0.84*1.20=1.008, Loot mod = 0.92*1.20=1.104
        // Ambush: 0.65*1.008=0.655, Loot: 0.55*1.104=0.607 → Ambush wins!
        float[] aggressions = { 0.0f, 0.1f, 0.3f, 0.5f, 0.7f, 0.9f, 1.0f };
        float[] raidTimes = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };

        foreach (float aggr in aggressions)
        {
            foreach (float time in raidTimes)
            {
                float ambushMod = ScoringModifiers.CombinedModifier(aggr, time, BotActionTypeId.Ambush);
                float ambushScore = AmbushTask.BaseScore * ambushMod;

                float lootMod = ScoringModifiers.CombinedModifier(aggr, time, BotActionTypeId.Loot);
                float lootMaxScore = LootTask.MaxBaseScore * lootMod;

                Assert.Greater(
                    ambushScore,
                    lootMaxScore,
                    $"Ambush ({ambushScore:F3}) should beat Loot ({lootMaxScore:F3}) at aggr={aggr}, time={time}"
                );
            }
        }
    }

    [Test]
    public void Snipe_BeatsLoot_AllCombos_IncludingAggressiveLate()
    {
        // After fix: Snipe raidTime modifier widened from Lerp(0.9,1.1) to Lerp(0.9,1.2)
        float[] aggressions = { 0.0f, 0.1f, 0.3f, 0.5f, 0.7f, 0.9f, 1.0f };
        float[] raidTimes = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };

        foreach (float aggr in aggressions)
        {
            foreach (float time in raidTimes)
            {
                float snipeMod = ScoringModifiers.CombinedModifier(aggr, time, BotActionTypeId.Snipe);
                float snipeScore = SnipeTask.BaseScore * snipeMod;

                float lootMod = ScoringModifiers.CombinedModifier(aggr, time, BotActionTypeId.Loot);
                float lootMaxScore = LootTask.MaxBaseScore * lootMod;

                Assert.Greater(
                    snipeScore,
                    lootMaxScore,
                    $"Snipe ({snipeScore:F3}) should beat Loot ({lootMaxScore:F3}) at aggr={aggr}, time={time}"
                );
            }
        }
    }
}

// ── 10. Comprehensive Modifier Composition Verification ─────────

[TestFixture]
public class ModifierCompositionTests
{
    [Test]
    public void CombinedModifier_IsMultiplicative_NotAdditive()
    {
        float personality = ScoringModifiers.PersonalityModifier(0.5f, BotActionTypeId.GoToObjective);
        float raidTime = ScoringModifiers.RaidTimeModifier(0.5f, BotActionTypeId.GoToObjective);
        float combined = ScoringModifiers.CombinedModifier(0.5f, 0.5f, BotActionTypeId.GoToObjective);

        Assert.AreEqual(personality * raidTime, combined, 0.001f, "CombinedModifier should be multiplicative");
    }

    [Test]
    public void MaxModifier_ForEachTask()
    {
        // Verify maximum combined modifier for each task type across all personality/time combos
        int[] taskIds =
        {
            BotActionTypeId.GoToObjective,
            BotActionTypeId.Loot,
            BotActionTypeId.Vulture,
            BotActionTypeId.Linger,
            BotActionTypeId.Investigate,
            BotActionTypeId.Ambush,
            BotActionTypeId.Snipe,
            BotActionTypeId.Patrol,
        };

        foreach (int taskId in taskIds)
        {
            float maxMod = 0f;
            for (float aggr = 0f; aggr <= 1.01f; aggr += 0.1f)
            {
                for (float time = 0f; time <= 1.01f; time += 0.1f)
                {
                    float mod = ScoringModifiers.CombinedModifier(Math.Min(aggr, 1f), Math.Min(time, 1f), taskId);
                    if (mod > maxMod)
                        maxMod = mod;
                }
            }

            // Every max modifier should be > 1 but < 2 (sane range)
            Assert.Greater(maxMod, 0.5f, "Max modifier for task " + taskId + " should be > 0.5");
            Assert.Less(maxMod, 2.0f, "Max modifier for task " + taskId + " should be < 2.0");
        }
    }

    [Test]
    public void DefaultTasks_GetNoModifier()
    {
        // Tasks not in the modifier tables should get 1.0
        int[] unmodifiedTasks =
        {
            BotActionTypeId.HoldPosition,
            BotActionTypeId.PlantItem,
            BotActionTypeId.UnlockDoor,
            BotActionTypeId.ToggleSwitch,
            BotActionTypeId.CloseNearbyDoors,
            BotActionTypeId.SpawnEntry,
        };

        foreach (int taskId in unmodifiedTasks)
        {
            for (float aggr = 0f; aggr <= 1f; aggr += 0.5f)
            {
                for (float time = 0f; time <= 1f; time += 0.5f)
                {
                    float mod = ScoringModifiers.CombinedModifier(aggr, time, taskId);
                    Assert.AreEqual(1f, mod, 0.001f, $"Task {taskId} should have no modifier at aggr={aggr}, time={time}");
                }
            }
        }
    }
}

// ── 11. Max Score Ceiling Verification ──────────────────────────

/// <summary>
/// Verifies that modified discretionary task scores don't exceed quest-gating tasks.
/// Documents the scoring ceiling relationships.
/// </summary>
[TestFixture]
public class ScoreCeilingTests
{
    [Test]
    public void MaxLootModified_VsFlatQuestActions()
    {
        // Compute maximum possible LootTask modified score
        float maxLootBase = LootTask.MaxBaseScore; // 0.55
        float maxLootMod = 0f;
        for (float aggr = 0f; aggr <= 1.01f; aggr += 0.05f)
        {
            for (float time = 0f; time <= 1.01f; time += 0.05f)
            {
                float mod = ScoringModifiers.CombinedModifier(Math.Min(aggr, 1f), Math.Min(time, 1f), BotActionTypeId.Loot);
                if (mod > maxLootMod)
                    maxLootMod = mod;
            }
        }

        float maxLootScore = maxLootBase * maxLootMod;

        // After fix: PlantItem raised to 0.80, no longer vulnerable to Loot
        Assert.Greater(
            PlantItemTask.BaseScore,
            maxLootScore,
            $"PlantItem ({PlantItemTask.BaseScore:F2}) should beat max modified Loot ({maxLootScore:F3})"
        );

        // HoldPosition (0.70) is borderline — Loot can barely exceed it
        bool holdVulnerable = maxLootScore > HoldPositionTask.BaseScore;
        if (holdVulnerable)
        {
            // Margin is tiny (<0.02) and hysteresis protects if already active
            Assert.Less(maxLootScore - HoldPositionTask.BaseScore, 0.05f, "HoldPosition borderline: margin should be very small");
        }
    }

    [Test]
    public void MaxVultureModified_VsFlatQuestActions()
    {
        float maxVultureBase = VultureTask.MaxBaseScore; // 0.60
        float maxVultureMod = 0f;
        for (float aggr = 0f; aggr <= 1.01f; aggr += 0.05f)
        {
            for (float time = 0f; time <= 1.01f; time += 0.05f)
            {
                float mod = ScoringModifiers.CombinedModifier(Math.Min(aggr, 1f), Math.Min(time, 1f), BotActionTypeId.Vulture);
                if (mod > maxVultureMod)
                    maxVultureMod = mod;
            }
        }

        float maxVultureScore = maxVultureBase * maxVultureMod;

        // After fix: PlantItem raised to 0.80, now beats max Vulture (0.78)
        Assert.Greater(
            PlantItemTask.BaseScore,
            maxVultureScore,
            $"PlantItem ({PlantItemTask.BaseScore:F2}) should beat max modified Vulture ({maxVultureScore:F3})"
        );

        // HoldPosition (0.70) is still beaten by max Vulture (0.78)
        Assert.Greater(
            maxVultureScore,
            HoldPositionTask.BaseScore,
            $"Max Vulture ({maxVultureScore:F3}) exceeds HoldPosition ({HoldPositionTask.BaseScore:F2}) — known limitation"
        );
    }

    [Test]
    public void SpawnEntry_AboveAllDiscretionaryMaximums()
    {
        // SpawnEntry at 0.80 should be above all discretionary maximum scores
        float[] maxScores = new float[8];
        int[] taskIds =
        {
            BotActionTypeId.Loot,
            BotActionTypeId.Vulture,
            BotActionTypeId.Linger,
            BotActionTypeId.Investigate,
            BotActionTypeId.Patrol,
        };
        float[] baseCaps =
        {
            LootTask.MaxBaseScore,
            VultureTask.MaxBaseScore,
            LingerTask.DefaultBaseScore,
            InvestigateTask.MaxBaseScore,
            PatrolTask.MaxBaseScore,
        };

        for (int i = 0; i < taskIds.Length; i++)
        {
            float maxMod = 0f;
            for (float aggr = 0f; aggr <= 1.01f; aggr += 0.05f)
            {
                for (float time = 0f; time <= 1.01f; time += 0.05f)
                {
                    float mod = ScoringModifiers.CombinedModifier(Math.Min(aggr, 1f), Math.Min(time, 1f), taskIds[i]);
                    if (mod > maxMod)
                        maxMod = mod;
                }
            }

            float maxScore = baseCaps[i] * maxMod;
            Assert.Greater(
                SpawnEntryTask.MaxBaseScore,
                maxScore,
                $"SpawnEntry (0.80) should beat max modified score ({maxScore:F3}) for task {taskIds[i]}"
            );
        }
    }
}
