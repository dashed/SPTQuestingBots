using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

// ── Shared helper ───────────────────────────────────────────────────────

/// <summary>
/// Helpers for multi-bot simulation tests: entity creation, time stepping,
/// combat event setup, and snapshot utilities.
/// </summary>
internal static class SimHelper
{
    /// <summary>Create a bot entity with task scores sized for the quest manager.</summary>
    internal static BotEntity CreateBot(
        int id,
        float aggression = 0.5f,
        float raidTime = 0.3f,
        float gameTime = 10f,
        float posX = 0f,
        float posZ = 0f
    )
    {
        var entity = new BotEntity(id);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = aggression;
        entity.RaidTimeNormalized = raidTime;
        entity.CurrentGameTime = gameTime;
        entity.CurrentPositionX = posX;
        entity.CurrentPositionZ = posZ;
        return entity;
    }

    /// <summary>Advance game time for all bots and re-score.</summary>
    internal static void AdvanceAndScore(UtilityTaskManager mgr, BotEntity[] bots, float newTime)
    {
        foreach (var bot in bots)
        {
            bot.CurrentGameTime = newTime;
            mgr.ScoreAndPick(bot);
        }
    }

    /// <summary>Set up combat event scanner fields on a single entity from the static registry.</summary>
    internal static void ScanCombatEvents(
        BotEntity entity,
        float currentTime,
        float maxEventAge = 120f,
        float detectionRange = 200f,
        float intensityRadius = 100f,
        float intensityWindow = 60f,
        float bossAvoidanceRadius = 80f,
        float bossZoneDecay = 120f
    )
    {
        CombatEventScanner.UpdateEntity(
            entity,
            currentTime,
            maxEventAge,
            detectionRange,
            intensityRadius,
            intensityWindow,
            bossAvoidanceRadius,
            bossZoneDecay
        );
    }

    /// <summary>Set up combat event scanner fields on multiple entities.</summary>
    internal static void ScanCombatEventsAll(
        BotEntity[] bots,
        float currentTime,
        float maxEventAge = 120f,
        float detectionRange = 200f,
        float intensityRadius = 100f,
        float intensityWindow = 60f
    )
    {
        foreach (var bot in bots)
        {
            ScanCombatEvents(bot, currentTime, maxEventAge, detectionRange, intensityRadius, intensityWindow);
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 1. Staggered Spawn Raid — 3 bots, different spawn times + personalities
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class StaggeredSpawnRaidTests
{
    private UtilityTaskManager _mgr;

    [SetUp]
    public void SetUp()
    {
        _mgr = QuestTaskFactory.Create();
        CombatEventRegistry.Initialize(64);
    }

    [TearDown]
    public void TearDown()
    {
        CombatEventRegistry.Clear();
    }

    [Test]
    public void ThreeBotsSpawnStaggered_EachGetsIndependentSpawnEntry()
    {
        // Bot A spawns t=0 (aggressive), Bot B spawns t=60 (timid), Bot C t=120 (normal)
        var botA = SimHelper.CreateBot(0, aggression: 0.9f, gameTime: 0f);
        botA.SpawnTime = 0f;
        botA.SpawnEntryDuration = 4f;
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 200f;

        var botB = SimHelper.CreateBot(1, aggression: 0.1f, gameTime: 0f);
        // Bot B hasn't spawned yet at t=0

        var botC = SimHelper.CreateBot(2, aggression: 0.5f, gameTime: 0f);
        // Bot C hasn't spawned yet at t=0

        // t=1: Only Bot A is active with spawn entry
        botA.CurrentGameTime = 1f;
        _mgr.ScoreAndPick(botA);
        Assert.IsInstanceOf<SpawnEntryTask>(botA.TaskAssignment.Task, "Bot A should be in SpawnEntry at t=1");

        // t=5: Bot A spawn entry expires, starts questing
        botA.CurrentGameTime = 5f;
        _mgr.ScoreAndPick(botA);
        Assert.IsInstanceOf<GoToObjectiveTask>(botA.TaskAssignment.Task, "Bot A should quest after spawn entry at t=5");

        // t=60: Bot B spawns
        botB.SpawnTime = 60f;
        botB.SpawnEntryDuration = 4f;
        botB.HasActiveObjective = true;
        botB.CurrentQuestAction = QuestActionId.MoveToPosition;
        botB.DistanceToObjective = 150f;
        botB.CurrentGameTime = 61f;
        _mgr.ScoreAndPick(botB);
        Assert.IsInstanceOf<SpawnEntryTask>(botB.TaskAssignment.Task, "Bot B should be in SpawnEntry at t=61");

        // Bot A still questing at t=61
        botA.CurrentGameTime = 61f;
        _mgr.ScoreAndPick(botA);
        Assert.IsInstanceOf<GoToObjectiveTask>(botA.TaskAssignment.Task, "Bot A should still quest at t=61");

        // t=65: Bot B spawn entry expires
        botB.CurrentGameTime = 65f;
        _mgr.ScoreAndPick(botB);
        Assert.IsInstanceOf<GoToObjectiveTask>(botB.TaskAssignment.Task, "Bot B should quest after spawn entry at t=65");

        // t=120: Bot C spawns
        botC.SpawnTime = 120f;
        botC.SpawnEntryDuration = 4f;
        botC.HasActiveObjective = true;
        botC.CurrentQuestAction = QuestActionId.Ambush;
        botC.DistanceToObjective = 100f;
        botC.CurrentGameTime = 121f;
        _mgr.ScoreAndPick(botC);
        Assert.IsInstanceOf<SpawnEntryTask>(botC.TaskAssignment.Task, "Bot C should be in SpawnEntry at t=121");
    }

    [Test]
    public void CombatEvent_DivergentPersonalityResponse()
    {
        // All bots questing, then combat event fires
        var botA = SimHelper.CreateBot(0, aggression: 0.9f, gameTime: 180f, posX: 100f, posZ: 100f);
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 200f;
        botA.IsSpawnEntryComplete = true;

        var botB = SimHelper.CreateBot(1, aggression: 0.1f, gameTime: 180f, posX: 120f, posZ: 100f);
        botB.HasActiveObjective = true;
        botB.CurrentQuestAction = QuestActionId.MoveToPosition;
        botB.DistanceToObjective = 200f;
        botB.IsSpawnEntryComplete = true;

        var botC = SimHelper.CreateBot(2, aggression: 0.5f, gameTime: 180f, posX: 110f, posZ: 100f);
        botC.HasActiveObjective = true;
        botC.CurrentQuestAction = QuestActionId.MoveToPosition;
        botC.DistanceToObjective = 200f;
        botC.IsSpawnEntryComplete = true;

        var bots = new[] { botA, botB, botC };

        // Before event: all should be GoToObjective
        foreach (var bot in bots)
        {
            _mgr.ScoreAndPick(bot);
        }

        Assert.IsInstanceOf<GoToObjectiveTask>(botA.TaskAssignment.Task);
        Assert.IsInstanceOf<GoToObjectiveTask>(botB.TaskAssignment.Task);
        Assert.IsInstanceOf<GoToObjectiveTask>(botC.TaskAssignment.Task);

        // Record 20 gunshots at (150, 0, 100) to produce high intensity
        for (int i = 0; i < 20; i++)
        {
            CombatEventRegistry.RecordEvent(150f, 0f, 100f, 179f + i * 0.1f, 100f, CombatEventType.Gunshot, false);
        }

        // Scan combat events for all bots
        SimHelper.ScanCombatEventsAll(bots, 180f);

        // Verify each bot detected the event
        Assert.IsTrue(botA.HasNearbyEvent, "Bot A should detect nearby event");
        Assert.IsTrue(botB.HasNearbyEvent, "Bot B should detect nearby event");
        Assert.IsTrue(botC.HasNearbyEvent, "Bot C should detect nearby event");

        // Re-score — personality modifiers should create divergence
        foreach (var bot in bots)
        {
            _mgr.ScoreAndPick(bot);
        }

        // Aggressive bot (A): vulture modifier = Lerp(0.7, 1.3, 0.9) = 1.24
        // Timid bot (B): vulture modifier = Lerp(0.7, 1.3, 0.1) = 0.76
        // Normal bot (C): vulture modifier = Lerp(0.7, 1.3, 0.5) = 1.0

        // Verify vulture scores diverge by personality
        int vultureOrdinal = -1;
        int investigateOrdinal = -1;
        for (int i = 0; i < _mgr.Tasks.Length; i++)
        {
            if (_mgr.Tasks[i] is VultureTask)
                vultureOrdinal = i;
            if (_mgr.Tasks[i] is InvestigateTask)
                investigateOrdinal = i;
        }

        Assert.GreaterOrEqual(vultureOrdinal, 0);
        Assert.GreaterOrEqual(investigateOrdinal, 0);

        // Aggressive bot should have higher vulture score than timid bot
        Assert.Greater(
            botA.TaskScores[vultureOrdinal],
            botB.TaskScores[vultureOrdinal],
            "Aggressive bot should have higher vulture score than timid"
        );

        // Aggressive bot should have higher investigate score than timid bot
        Assert.Greater(
            botA.TaskScores[investigateOrdinal],
            botB.TaskScores[investigateOrdinal],
            "Aggressive bot should have higher investigate score than timid"
        );
    }

    [Test]
    public void CombatEventExpires_AllReturnToQuesting()
    {
        // Start without objectives so there's no GoToObjective hysteresis blocking
        // vulture/investigate activation
        var botA = SimHelper.CreateBot(0, aggression: 0.9f, gameTime: 180f, posX: 140f, posZ: 100f);
        botA.HasActiveObjective = false;
        botA.IsSpawnEntryComplete = true;

        var botB = SimHelper.CreateBot(1, aggression: 0.7f, gameTime: 180f, posX: 145f, posZ: 100f);
        botB.HasActiveObjective = false;
        botB.IsSpawnEntryComplete = true;

        var bots = new[] { botA, botB };

        // Record enough events to exceed both investigate (5) and vulture (15) thresholds
        for (int i = 0; i < 25; i++)
        {
            CombatEventRegistry.RecordEvent(150f, 0f, 100f, 179f + i * 0.1f, 100f, CombatEventType.Gunshot, false);
        }

        SimHelper.ScanCombatEventsAll(bots, 180f);
        foreach (var bot in bots)
        {
            _mgr.ScoreAndPick(bot);
        }

        // At least one bot should be on vulture or investigate
        bool anyEventTask = bots.Any(b => b.TaskAssignment.Task is VultureTask || b.TaskAssignment.Task is InvestigateTask);
        Assert.IsTrue(anyEventTask, "At least one bot should react to combat event");

        // t=300: Events expire (maxAge=60s default, events were at t~179)
        CombatEventRegistry.CleanupExpired(300f, 60f);
        SimHelper.ScanCombatEventsAll(bots, 300f);
        foreach (var bot in bots)
        {
            bot.CurrentGameTime = 300f;
            // Clear vulture phase and investigation to simulate completion
            bot.VulturePhase = VulturePhase.Complete;
            bot.IsInvestigating = false;
            // Give them objectives now so they have something to switch to
            bot.HasActiveObjective = true;
            bot.CurrentQuestAction = QuestActionId.MoveToPosition;
            bot.DistanceToObjective = 200f;
            _mgr.ScoreAndPick(bot);
        }

        // All bots should return to GoToObjective
        Assert.IsInstanceOf<GoToObjectiveTask>(botA.TaskAssignment.Task, "Bot A should return to GoToObjective after event expires");
        Assert.IsInstanceOf<GoToObjectiveTask>(botB.TaskAssignment.Task, "Bot B should return to GoToObjective after event expires");
    }

    [Test]
    public void LateRaid_ScoringModifiersAffectAllBots()
    {
        // Early raid
        var botA = SimHelper.CreateBot(0, aggression: 0.5f, raidTime: 0.1f, gameTime: 100f);
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 200f;
        botA.IsSpawnEntryComplete = true;

        var botB = SimHelper.CreateBot(1, aggression: 0.5f, raidTime: 0.1f, gameTime: 100f);
        botB.HasActiveObjective = true;
        botB.CurrentQuestAction = QuestActionId.MoveToPosition;
        botB.DistanceToObjective = 200f;
        botB.IsSpawnEntryComplete = true;

        var bots = new[] { botA, botB };
        foreach (var bot in bots)
        {
            _mgr.ScoreAndPick(bot);
        }

        int gotoOrdinal = -1;
        for (int i = 0; i < _mgr.Tasks.Length; i++)
        {
            if (_mgr.Tasks[i] is GoToObjectiveTask)
            {
                gotoOrdinal = i;
                break;
            }
        }

        float earlyScoreA = botA.TaskScores[gotoOrdinal];
        float earlyScoreB = botB.TaskScores[gotoOrdinal];

        // Late raid
        botA.RaidTimeNormalized = 0.9f;
        botB.RaidTimeNormalized = 0.9f;

        foreach (var bot in bots)
        {
            _mgr.ScoreAndPick(bot);
        }

        float lateScoreA = botA.TaskScores[gotoOrdinal];
        float lateScoreB = botB.TaskScores[gotoOrdinal];

        // GoToObjective should have lower score in late raid (Lerp(1.2, 0.8, 0.9) = 0.84)
        Assert.Less(lateScoreA, earlyScoreA, "GoToObjective score should decrease in late raid for Bot A");
        Assert.Less(lateScoreB, earlyScoreB, "GoToObjective score should decrease in late raid for Bot B");

        // Scores should be equal for bots with same personality and raid time
        Assert.AreEqual(lateScoreA, lateScoreB, 0.001f, "Same personality+raidtime bots should get same score");
    }

    [Test]
    public void ThreeBots_IndependentScoreArrays_NoLeakage()
    {
        // Verify each bot has its own TaskScores array — no shared references
        var botA = SimHelper.CreateBot(0, aggression: 0.9f, gameTime: 10f);
        var botB = SimHelper.CreateBot(1, aggression: 0.1f, gameTime: 10f);
        var botC = SimHelper.CreateBot(2, aggression: 0.5f, gameTime: 10f);

        Assert.AreNotSame(botA.TaskScores, botB.TaskScores, "Bots should not share TaskScores arrays");
        Assert.AreNotSame(botB.TaskScores, botC.TaskScores, "Bots should not share TaskScores arrays");

        // Set unique states
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 200f;
        botA.IsSpawnEntryComplete = true;

        botB.HasActiveObjective = false;
        botB.IsSpawnEntryComplete = true;

        botC.HasActiveObjective = true;
        botC.CurrentQuestAction = QuestActionId.Ambush;
        botC.DistanceToObjective = 3f;
        botC.IsCloseToObjective = true;
        botC.IsSpawnEntryComplete = true;

        _mgr.ScoreAndPick(botA);
        _mgr.ScoreAndPick(botB);
        _mgr.ScoreAndPick(botC);

        // A: GoToObjective, B: no objective so patrol/0, C: Ambush (close)
        Assert.IsInstanceOf<GoToObjectiveTask>(botA.TaskAssignment.Task, "A should GoToObjective");
        Assert.IsInstanceOf<AmbushTask>(botC.TaskAssignment.Task, "C should Ambush (close to position)");

        // Modifying A's scores should not affect B or C
        botA.TaskScores[0] = 999f;
        Assert.AreNotEqual(999f, botB.TaskScores[0], "B's score array should be independent");
        Assert.AreNotEqual(999f, botC.TaskScores[0], "C's score array should be independent");
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 2. Quest Contention — bots competing for objectives
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class QuestContentionTests
{
    private UtilityTaskManager _mgr;

    [SetUp]
    public void SetUp()
    {
        _mgr = QuestTaskFactory.Create();
        CombatEventRegistry.Initialize(64);
    }

    [TearDown]
    public void TearDown()
    {
        CombatEventRegistry.Clear();
    }

    [Test]
    public void ThreeBots_TwoWithObjectives_OneBlocked_DifferentTasks()
    {
        // Bot A and B have objectives, Bot C has none
        var botA = SimHelper.CreateBot(0, aggression: 0.7f, gameTime: 100f);
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 200f;
        botA.IsSpawnEntryComplete = true;

        var botB = SimHelper.CreateBot(1, aggression: 0.3f, gameTime: 100f);
        botB.HasActiveObjective = true;
        botB.CurrentQuestAction = QuestActionId.Ambush;
        botB.DistanceToObjective = 150f;
        botB.IsSpawnEntryComplete = true;

        var botC = SimHelper.CreateBot(2, aggression: 0.5f, gameTime: 100f);
        botC.HasActiveObjective = false;
        botC.IsSpawnEntryComplete = true;

        _mgr.ScoreAndPick(botA);
        _mgr.ScoreAndPick(botB);
        _mgr.ScoreAndPick(botC);

        Assert.IsInstanceOf<GoToObjectiveTask>(botA.TaskAssignment.Task, "Bot A should GoToObjective");
        Assert.IsInstanceOf<GoToObjectiveTask>(botB.TaskAssignment.Task, "Bot B should GoToObjective (far from ambush)");

        // Bot C has no objective: should not select GoToObjective
        Assert.IsNotInstanceOf<GoToObjectiveTask>(botC.TaskAssignment.Task, "Bot C should not GoToObjective (no objective)");
    }

    [Test]
    public void BlockedBot_GetsObjective_SwitchesToGoToObjective()
    {
        var botC = SimHelper.CreateBot(2, aggression: 0.5f, gameTime: 100f);
        botC.HasActiveObjective = false;
        botC.IsSpawnEntryComplete = true;

        _mgr.ScoreAndPick(botC);
        var taskBeforeObjective = botC.TaskAssignment.Task;
        Assert.IsNotInstanceOf<GoToObjectiveTask>(taskBeforeObjective, "No objective = not GoToObjective");

        // Dynamic objective arrives — simulate objective assignment
        botC.HasActiveObjective = true;
        botC.CurrentQuestAction = QuestActionId.MoveToPosition;
        botC.DistanceToObjective = 100f;

        _mgr.ScoreAndPick(botC);
        Assert.IsInstanceOf<GoToObjectiveTask>(botC.TaskAssignment.Task, "Bot C should switch to GoToObjective after receiving objective");
    }

    [Test]
    public void MultipleBots_DifferentObjectiveTypes_IndependentExecution()
    {
        var botA = SimHelper.CreateBot(0, aggression: 0.5f, gameTime: 50f);
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.PlantItem;
        botA.DistanceToObjective = 3f;
        botA.IsCloseToObjective = true;
        botA.IsSpawnEntryComplete = true;

        var botB = SimHelper.CreateBot(1, aggression: 0.5f, gameTime: 50f);
        botB.HasActiveObjective = true;
        botB.CurrentQuestAction = QuestActionId.Snipe;
        botB.DistanceToObjective = 2f;
        botB.IsCloseToObjective = true;
        botB.IsSpawnEntryComplete = true;

        var botC = SimHelper.CreateBot(2, aggression: 0.5f, gameTime: 50f);
        botC.HasActiveObjective = true;
        botC.CurrentQuestAction = QuestActionId.HoldAtPosition;
        botC.DistanceToObjective = 5f;
        botC.IsSpawnEntryComplete = true;

        _mgr.ScoreAndPick(botA);
        _mgr.ScoreAndPick(botB);
        _mgr.ScoreAndPick(botC);

        Assert.IsInstanceOf<PlantItemTask>(botA.TaskAssignment.Task, "Bot A should PlantItem (close)");
        Assert.IsInstanceOf<SnipeTask>(botB.TaskAssignment.Task, "Bot B should Snipe (close)");
        Assert.IsInstanceOf<HoldPositionTask>(botC.TaskAssignment.Task, "Bot C should HoldPosition");
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 3. Squad Split — boss+followers with loot interactions
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class SquadSplitTests
{
    private SquadRegistry _squadReg;
    private BotRegistry _botReg;
    private LootClaimRegistry _claims;

    [SetUp]
    public void SetUp()
    {
        _squadReg = new SquadRegistry();
        _botReg = new BotRegistry();
        _claims = new LootClaimRegistry();
    }

    private (SquadEntity squad, BotEntity boss, BotEntity follower1, BotEntity follower2) CreateSquad()
    {
        var boss = _botReg.Add(100);
        boss.TaskScores = new float[QuestTaskFactory.TaskCount];
        boss.Aggression = 0.7f;
        boss.IsActive = true;
        boss.CurrentPositionX = 50f;
        boss.CurrentPositionY = 0f;
        boss.CurrentPositionZ = 50f;

        var f1 = _botReg.Add(101);
        f1.TaskScores = new float[QuestTaskFactory.TaskCount];
        f1.Aggression = 0.5f;
        f1.IsActive = true;
        f1.CurrentPositionX = 55f;
        f1.CurrentPositionY = 0f;
        f1.CurrentPositionZ = 50f;

        var f2 = _botReg.Add(102);
        f2.TaskScores = new float[QuestTaskFactory.TaskCount];
        f2.Aggression = 0.4f;
        f2.IsActive = true;
        f2.CurrentPositionX = 45f;
        f2.CurrentPositionY = 0f;
        f2.CurrentPositionZ = 50f;

        var squad = _squadReg.Add(2, 3);
        _squadReg.AddMember(squad, boss);
        _squadReg.AddMember(squad, f1);
        _squadReg.AddMember(squad, f2);

        // Set up hierarchy
        boss.Followers.Add(f1);
        boss.Followers.Add(f2);
        f1.Boss = boss;
        f2.Boss = boss;

        return (squad, boss, f1, f2);
    }

    [Test]
    public void BossPriorityClaim_BossGetsHighestValueLoot()
    {
        var (squad, boss, f1, f2) = CreateSquad();

        var results = new[]
        {
            new LootScanResult
            {
                Id = 1,
                Value = 5000f,
                X = 60f,
                Y = 0f,
                Z = 55f,
            },
            new LootScanResult
            {
                Id = 2,
                Value = 25000f,
                X = 55f,
                Y = 0f,
                Z = 50f,
            },
            new LootScanResult
            {
                Id = 3,
                Value = 8000f,
                X = 45f,
                Y = 0f,
                Z = 55f,
            },
        };

        int claimed = SquadLootCoordinator.BossPriorityClaim(results, 3, _claims, boss.Id);
        Assert.AreEqual(1, claimed, "Boss should claim index 1 (highest value=25000)");
        Assert.IsTrue(_claims.IsClaimedByOther(f1.Id, 2), "Follower 1 should see loot 2 as claimed by boss");
        Assert.IsFalse(_claims.IsClaimedByOther(f1.Id, 1), "Follower 1 should be able to claim loot 1");
        Assert.IsFalse(_claims.IsClaimedByOther(f1.Id, 3), "Follower 1 should be able to claim loot 3");
    }

    [Test]
    public void ShouldFollowerLoot_BossNotAtObjective_Denied()
    {
        var (squad, boss, f1, f2) = CreateSquad();
        float commRange = 50f;
        float commRangeSqr = commRange * commRange;

        // Boss not at objective, not looting
        boss.IsCloseToObjective = false;
        boss.HasActiveObjective = true;
        boss.IsLooting = false;

        bool allowed = SquadLootCoordinator.ShouldFollowerLoot(f1, boss, commRangeSqr);
        Assert.IsFalse(allowed, "Follower should not loot while boss is traveling to objective");
    }

    [Test]
    public void ShouldFollowerLoot_BossAtObjective_Allowed()
    {
        var (squad, boss, f1, f2) = CreateSquad();
        float commRangeSqr = 50f * 50f;

        boss.IsCloseToObjective = true;
        boss.HasActiveObjective = true;
        boss.IsLooting = false;

        bool allowed = SquadLootCoordinator.ShouldFollowerLoot(f1, boss, commRangeSqr);
        Assert.IsTrue(allowed, "Follower should loot when boss is at objective");
    }

    [Test]
    public void ShouldFollowerLoot_BossLooting_Allowed()
    {
        var (squad, boss, f1, f2) = CreateSquad();
        float commRangeSqr = 50f * 50f;

        boss.IsLooting = true;
        boss.IsCloseToObjective = false;
        boss.HasActiveObjective = false;

        bool allowed = SquadLootCoordinator.ShouldFollowerLoot(f1, boss, commRangeSqr);
        Assert.IsTrue(allowed, "Follower should loot when boss is looting");
    }

    [Test]
    public void ShouldFollowerLoot_OutOfCommRange_Denied()
    {
        var (squad, boss, f1, f2) = CreateSquad();
        float commRangeSqr = 3f * 3f; // Very short range

        // Boss is at objective but follower is far away
        boss.IsCloseToObjective = true;
        boss.HasActiveObjective = true;
        f1.CurrentPositionX = 200f; // Far away

        bool allowed = SquadLootCoordinator.ShouldFollowerLoot(f1, boss, commRangeSqr);
        Assert.IsFalse(allowed, "Follower out of comm range should be denied loot");
    }

    [Test]
    public void ShouldFollowerLoot_InCombat_AlwaysDenied()
    {
        var (squad, boss, f1, f2) = CreateSquad();
        float commRangeSqr = 50f * 50f;

        // Boss at objective, but follower in combat
        boss.IsCloseToObjective = true;
        boss.HasActiveObjective = true;
        f1.IsInCombat = true;

        bool allowed = SquadLootCoordinator.ShouldFollowerLoot(f1, boss, commRangeSqr);
        Assert.IsFalse(allowed, "Follower in combat should never loot");

        // Also test boss in combat
        f1.IsInCombat = false;
        boss.IsInCombat = true;

        allowed = SquadLootCoordinator.ShouldFollowerLoot(f1, boss, commRangeSqr);
        Assert.IsFalse(allowed, "Should not loot when boss is in combat");
    }

    [Test]
    public void LootClaimDeconfliction_MultipleBots_NoDoubleClaimOnSameItem()
    {
        var (squad, boss, f1, f2) = CreateSquad();

        // Boss claims loot 42
        Assert.IsTrue(_claims.TryClaim(boss.Id, 42));

        // Follower 1 tries to claim same loot
        Assert.IsFalse(_claims.TryClaim(f1.Id, 42), "Should not double-claim");
        Assert.IsTrue(_claims.IsClaimedByOther(f1.Id, 42));

        // Follower 2 also denied
        Assert.IsFalse(_claims.TryClaim(f2.Id, 42));

        // Follower 1 claims different loot
        Assert.IsTrue(_claims.TryClaim(f1.Id, 43));
        Assert.IsTrue(_claims.IsClaimedByOther(f2.Id, 43), "F2 should see loot 43 claimed by F1");
        Assert.IsFalse(_claims.IsClaimedByOther(f1.Id, 43), "F1 should see its own claim as valid");
    }

    [Test]
    public void SharedScanResults_FollowersGetDifferentTargets()
    {
        var (squad, boss, f1, f2) = CreateSquad();

        var bossResults = new[]
        {
            new LootScanResult
            {
                Id = 10,
                Value = 30000f,
                X = 55f,
            },
            new LootScanResult
            {
                Id = 11,
                Value = 15000f,
                X = 60f,
            },
            new LootScanResult
            {
                Id = 12,
                Value = 8000f,
                X = 65f,
            },
        };

        // Boss claims best
        int bossIndex = SquadLootCoordinator.BossPriorityClaim(bossResults, 3, _claims, boss.Id);
        Assert.AreEqual(0, bossIndex, "Boss claims index 0 (value=30000)");

        // Share with squad
        SquadLootCoordinator.ShareScanResults(squad, bossResults, 3);

        // Follower 1 picks from shared (should skip boss target 10)
        int f1Index = SquadLootCoordinator.PickSharedTargetForFollower(squad, f1.Id, bossResults[bossIndex].Id, _claims);
        Assert.GreaterOrEqual(f1Index, 0, "F1 should find a shared target");
        Assert.AreNotEqual(0, f1Index, "F1 should not pick boss's target");

        // F1 claims
        _claims.TryClaim(f1.Id, squad.SharedLootIds[f1Index]);

        // Follower 2 picks (should skip both boss and F1 targets)
        int f2Index = SquadLootCoordinator.PickSharedTargetForFollower(squad, f2.Id, bossResults[bossIndex].Id, _claims);
        Assert.GreaterOrEqual(f2Index, 0, "F2 should find a shared target");
        Assert.AreNotEqual(f1Index, f2Index, "F2 should pick different target than F1");
    }

    [Test]
    public void BossDies_LeaderReassigned_SquadContinues()
    {
        var (squad, boss, f1, f2) = CreateSquad();

        Assert.AreEqual(boss, squad.Leader, "Boss should be leader");
        Assert.AreEqual(SquadRole.Leader, boss.SquadRole);
        Assert.AreEqual(SquadRole.Guard, f1.SquadRole);

        // Boss dies — remove from squad
        _squadReg.RemoveMember(squad, boss);

        // Verify leadership transferred
        Assert.AreEqual(f1, squad.Leader, "F1 should become new leader (first active member)");
        Assert.AreEqual(SquadRole.Leader, f1.SquadRole, "F1 should have Leader role");
        Assert.IsNull(boss.Squad, "Dead boss should have no squad");
        Assert.AreEqual(SquadRole.None, boss.SquadRole, "Dead boss should have no role");
        Assert.AreEqual(2, squad.Members.Count, "Squad should have 2 remaining members");
    }

    [Test]
    public void BossDies_LootClaimsReleased_FollowerCanClaimBossLoot()
    {
        var (squad, boss, f1, f2) = CreateSquad();

        // Boss claims valuable loot
        Assert.IsTrue(_claims.TryClaim(boss.Id, 42));
        Assert.IsTrue(_claims.IsClaimedByOther(f1.Id, 42));

        // Boss dies — release all claims
        _claims.ReleaseAll(boss.Id);

        // Follower can now claim
        Assert.IsFalse(_claims.IsClaimedByOther(f1.Id, 42), "Loot should be unclaimed after boss death");
        Assert.IsTrue(_claims.TryClaim(f1.Id, 42), "F1 should claim boss's former loot");
    }

    [Test]
    public void FollowerAtTacticalPosition_AllowedToLoot()
    {
        var (squad, boss, f1, _) = CreateSquad();
        float commRangeSqr = 50f * 50f;

        // Follower at tactical position
        f1.HasTacticalPosition = true;
        f1.TacticalPositionX = 55f;
        f1.TacticalPositionZ = 50f;
        f1.CurrentPositionX = 56f;
        f1.CurrentPositionZ = 50f;
        f1.IsApproachingLoot = false;

        // Boss not at objective, not looting (would normally deny)
        boss.IsCloseToObjective = false;
        boss.HasActiveObjective = true;
        boss.IsLooting = false;

        bool allowed = SquadLootCoordinator.ShouldFollowerLoot(f1, boss, commRangeSqr);
        Assert.IsTrue(allowed, "Follower at tactical position should be allowed to loot");
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 4. Cascading Combat Events — bots reacting to changing combat landscape
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class CascadingCombatEventsTests
{
    private UtilityTaskManager _mgr;

    [SetUp]
    public void SetUp()
    {
        _mgr = QuestTaskFactory.Create();
        CombatEventRegistry.Initialize(64);
    }

    [TearDown]
    public void TearDown()
    {
        CombatEventRegistry.Clear();
    }

    [Test]
    public void TwoEvents_BotsSeeNearestEvent()
    {
        // Event 1 at (100, 0, 100), Event 2 at (300, 0, 100)
        for (int i = 0; i < 10; i++)
        {
            CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f + i * 0.1f, 100f, CombatEventType.Gunshot, false);
            CombatEventRegistry.RecordEvent(300f, 0f, 100f, 10f + i * 0.1f, 100f, CombatEventType.Gunshot, false);
        }

        // Bot near event 1
        var botNear1 = SimHelper.CreateBot(0, posX: 120f, posZ: 100f, gameTime: 15f);
        // Bot near event 2
        var botNear2 = SimHelper.CreateBot(1, posX: 280f, posZ: 100f, gameTime: 15f);
        // Bot equidistant
        var botMid = SimHelper.CreateBot(2, posX: 200f, posZ: 100f, gameTime: 15f);

        SimHelper.ScanCombatEvents(botNear1, 15f);
        SimHelper.ScanCombatEvents(botNear2, 15f);
        SimHelper.ScanCombatEvents(botMid, 15f);

        // Bot near event 1 should see event 1 as nearest
        Assert.IsTrue(botNear1.HasNearbyEvent);
        Assert.AreEqual(100f, botNear1.NearbyEventX, 1f, "Bot near event 1 should see event at x=100");

        // Bot near event 2 should see event 2 as nearest
        Assert.IsTrue(botNear2.HasNearbyEvent);
        Assert.AreEqual(300f, botNear2.NearbyEventX, 1f, "Bot near event 2 should see event at x=300");

        // Bot in middle should see one of them (nearest by distance)
        Assert.IsTrue(botMid.HasNearbyEvent);
    }

    [Test]
    public void NewHigherIntensityEvent_OverridesOldNearest()
    {
        // Event 1 at (100, 0, 0) with low intensity (2 shots)
        CombatEventRegistry.RecordEvent(100f, 0f, 0f, 10f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(100f, 0f, 0f, 10.1f, 100f, CombatEventType.Gunshot, false);

        var bot = SimHelper.CreateBot(0, posX: 80f, posZ: 0f, gameTime: 15f);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 200f;
        bot.IsSpawnEntryComplete = true;

        SimHelper.ScanCombatEvents(bot, 15f);
        Assert.AreEqual(100f, bot.NearbyEventX, 1f, "Initial event at x=100");

        // New event at (90, 0, 0) — closer to bot, higher intensity
        for (int i = 0; i < 20; i++)
        {
            CombatEventRegistry.RecordEvent(90f, 0f, 0f, 14f + i * 0.1f, 100f, CombatEventType.Gunshot, false);
        }

        SimHelper.ScanCombatEvents(bot, 16f);

        // Bot should now see the closer event at x=90
        Assert.IsTrue(bot.HasNearbyEvent);
        Assert.AreEqual(90f, bot.NearbyEventX, 1f, "Bot should see closer event at x=90");

        // Intensity should be higher now
        Assert.Greater(bot.CombatIntensity, 2, "Intensity should be higher with more events");
    }

    [Test]
    public void AllEventsExpire_AllBotsReturnToDefault()
    {
        for (int i = 0; i < 10; i++)
        {
            CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, false);
        }

        var botA = SimHelper.CreateBot(0, posX: 110f, posZ: 100f, gameTime: 15f);
        var botB = SimHelper.CreateBot(1, posX: 120f, posZ: 100f, gameTime: 15f);

        SimHelper.ScanCombatEvents(botA, 15f);
        SimHelper.ScanCombatEvents(botB, 15f);
        Assert.IsTrue(botA.HasNearbyEvent);
        Assert.IsTrue(botB.HasNearbyEvent);

        // Expire all events
        CombatEventRegistry.CleanupExpired(200f, 60f);

        SimHelper.ScanCombatEvents(botA, 200f);
        SimHelper.ScanCombatEvents(botB, 200f);

        Assert.IsFalse(botA.HasNearbyEvent, "A should have no event after expiry");
        Assert.IsFalse(botB.HasNearbyEvent, "B should have no event after expiry");
        Assert.AreEqual(0, botA.CombatIntensity, "A intensity should be 0");
        Assert.AreEqual(0, botB.CombatIntensity, "B intensity should be 0");
    }

    [Test]
    public void BossEvent_TriggersAvoidanceForNearbyBots()
    {
        // Boss event with IsBoss=true
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 150f, CombatEventType.Gunshot, true);

        var botNear = SimHelper.CreateBot(0, posX: 110f, posZ: 100f, gameTime: 15f);
        var botFar = SimHelper.CreateBot(1, posX: 500f, posZ: 500f, gameTime: 15f);

        SimHelper.ScanCombatEvents(botNear, 15f);
        SimHelper.ScanCombatEvents(botFar, 15f);

        Assert.IsTrue(botNear.IsInBossZone, "Bot near boss event should detect boss zone");
        Assert.IsFalse(botFar.IsInBossZone, "Bot far from boss event should not detect boss zone");
    }

    [Test]
    public void ExplosionEvent_CountsAsTripleIntensity()
    {
        // 1 explosion = intensity 3 (1 + 2 extra)
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 150f, CombatEventType.Explosion, false);

        var bot = SimHelper.CreateBot(0, posX: 105f, posZ: 100f, gameTime: 15f);
        SimHelper.ScanCombatEvents(bot, 15f, intensityRadius: 100f, intensityWindow: 60f);

        Assert.AreEqual(3, bot.CombatIntensity, "Explosion should count as 3 intensity");
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 5. Extraction Race — bots independently extracting
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class ExtractionRaceTests
{
    private UtilityTaskManager _mgr;

    [SetUp]
    public void SetUp()
    {
        _mgr = QuestTaskFactory.Create();
    }

    [Test]
    public void TwoBots_SameState_GetSameScores()
    {
        // Two bots with identical state should get identical scores
        var botA = SimHelper.CreateBot(0, aggression: 0.5f, raidTime: 0.8f, gameTime: 500f);
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 100f;
        botA.IsSpawnEntryComplete = true;

        var botB = SimHelper.CreateBot(1, aggression: 0.5f, raidTime: 0.8f, gameTime: 500f);
        botB.HasActiveObjective = true;
        botB.CurrentQuestAction = QuestActionId.MoveToPosition;
        botB.DistanceToObjective = 100f;
        botB.IsSpawnEntryComplete = true;

        _mgr.ScoreAndPick(botA);
        _mgr.ScoreAndPick(botB);

        // All scores should be identical
        for (int i = 0; i < QuestTaskFactory.TaskCount; i++)
        {
            Assert.AreEqual(botA.TaskScores[i], botB.TaskScores[i], 0.0001f, $"Task {i} score should match for identical bots");
        }

        // Same task should be selected
        Assert.AreEqual(
            botA.TaskAssignment.Task.GetType(),
            botB.TaskAssignment.Task.GetType(),
            "Identical bots should select same task type"
        );
    }

    [Test]
    public void TwoBots_MutatingOneDoesNotAffectOther()
    {
        var botA = SimHelper.CreateBot(0, aggression: 0.5f, gameTime: 100f);
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 100f;
        botA.IsSpawnEntryComplete = true;

        var botB = SimHelper.CreateBot(1, aggression: 0.5f, gameTime: 100f);
        botB.HasActiveObjective = true;
        botB.CurrentQuestAction = QuestActionId.MoveToPosition;
        botB.DistanceToObjective = 100f;
        botB.IsSpawnEntryComplete = true;

        _mgr.ScoreAndPick(botA);
        _mgr.ScoreAndPick(botB);

        // Now mutate bot A's state
        botA.HasActiveObjective = false;
        botA.IsInCombat = true;

        // Re-score only bot A
        _mgr.ScoreAndPick(botA);

        // Bot B should be unaffected
        Assert.IsInstanceOf<GoToObjectiveTask>(botB.TaskAssignment.Task, "Bot B should not be affected by Bot A's state change");
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 6. Bot Registry Lifecycle — add/remove/recycle with task cleanup
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class BotRegistryLifecycleTests
{
    private UtilityTaskManager _mgr;
    private BotRegistry _reg;

    [SetUp]
    public void SetUp()
    {
        _mgr = QuestTaskFactory.Create();
        _reg = new BotRegistry();
    }

    [Test]
    public void BotRemoval_DeactivatesActiveTask()
    {
        var entity = _reg.Add(10);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 200f;
        entity.IsSpawnEntryComplete = true;
        entity.CurrentGameTime = 100f;

        _mgr.ScoreAndPick(entity);
        Assert.IsInstanceOf<GoToObjectiveTask>(entity.TaskAssignment.Task);

        // Check task has entity in active list
        var task = entity.TaskAssignment.Task;
        Assert.AreEqual(1, task.ActiveEntityCount, "Task should have 1 active entity");

        // Remove entity from task manager
        _mgr.RemoveEntity(entity);
        Assert.AreEqual(0, task.ActiveEntityCount, "Task should have 0 active entities after removal");
        Assert.IsNull(entity.TaskAssignment.Task, "Entity should have no task after removal");
    }

    [Test]
    public void AddRemoveAdd_RecycledId_NoStaleState()
    {
        var entity1 = _reg.Add(10);
        entity1.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity1.HasActiveObjective = true;
        entity1.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity1.DistanceToObjective = 200f;
        entity1.IsSpawnEntryComplete = true;
        entity1.CurrentGameTime = 50f;
        entity1.VulturePhase = VulturePhase.Approach;

        _mgr.ScoreAndPick(entity1);
        int originalId = entity1.Id;

        // Remove
        _mgr.RemoveEntity(entity1);
        _reg.Remove(entity1);

        // Add new entity — may get recycled ID
        var entity2 = _reg.Add(20);
        entity2.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity2.IsSpawnEntryComplete = true;
        entity2.CurrentGameTime = 100f;

        // entity2 is a fresh BotEntity — should not have entity1's vulture phase
        Assert.AreEqual(VulturePhase.None, entity2.VulturePhase, "New entity should not inherit old vulture phase");
        Assert.IsFalse(entity2.HasActiveObjective, "New entity should not inherit old objective");
        Assert.AreEqual(20, entity2.BsgId, "New entity BsgId should be 20 (set by Add(20))");

        // Verify the new entity functions correctly
        entity2.HasActiveObjective = true;
        entity2.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity2.DistanceToObjective = 100f;
        _mgr.ScoreAndPick(entity2);

        Assert.IsInstanceOf<GoToObjectiveTask>(entity2.TaskAssignment.Task, "New entity should function independently");
    }

    [Test]
    public void MultipleBotsInRegistry_IterationCorrect()
    {
        var entities = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            entities[i] = _reg.Add(i + 100);
            entities[i].TaskScores = new float[QuestTaskFactory.TaskCount];
            entities[i].IsActive = true;
        }

        Assert.AreEqual(5, _reg.Count);

        // Remove middle entity
        _reg.Remove(entities[2]);
        Assert.AreEqual(4, _reg.Count);

        // Verify remaining entities are all accessible
        for (int i = 0; i < _reg.Count; i++)
        {
            Assert.IsTrue(_reg.Entities[i].IsActive, $"Entity at index {i} should be active");
        }

        // Verify BSG ID lookup still works
        Assert.IsNotNull(_reg.GetByBsgId(100), "BsgId 100 should still be found");
        Assert.IsNull(_reg.GetByBsgId(102), "BsgId 102 should be null after removal");
        Assert.IsNotNull(_reg.GetByBsgId(103), "BsgId 103 should still be found");
    }

    [Test]
    public void LootClaimRegistry_BotDeath_ClaimsReleased_OthersCanClaim()
    {
        var claims = new LootClaimRegistry();
        var botA = _reg.Add(0);
        var botB = _reg.Add(1);

        // Bot A claims several items
        claims.TryClaim(botA.Id, 100);
        claims.TryClaim(botA.Id, 101);
        claims.TryClaim(botA.Id, 102);

        Assert.IsTrue(claims.IsClaimedByOther(botB.Id, 100));
        Assert.IsTrue(claims.IsClaimedByOther(botB.Id, 101));
        Assert.IsTrue(claims.IsClaimedByOther(botB.Id, 102));

        // Bot A dies — release all
        claims.ReleaseAll(botA.Id);

        Assert.IsFalse(claims.IsClaimedByOther(botB.Id, 100), "100 should be free");
        Assert.IsFalse(claims.IsClaimedByOther(botB.Id, 101), "101 should be free");
        Assert.IsFalse(claims.IsClaimedByOther(botB.Id, 102), "102 should be free");

        // Bot B can claim them
        Assert.IsTrue(claims.TryClaim(botB.Id, 100));
        Assert.IsTrue(claims.TryClaim(botB.Id, 101));
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 7. Full Raid Timeline — time-stepped simulation through an entire raid
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class FullRaidTimelineTests
{
    private UtilityTaskManager _mgr;

    [SetUp]
    public void SetUp()
    {
        _mgr = QuestTaskFactory.Create();
        CombatEventRegistry.Initialize(128);
    }

    [TearDown]
    public void TearDown()
    {
        CombatEventRegistry.Clear();
    }

    [Test]
    public void ThreeBotRaid_SpawnQuestCombatLateRaid_FullProgression()
    {
        // === PHASE 1: Spawn (t=0..5) ===
        // Use raidTime=0.3 so GoToObjective's combined modifier stays below SpawnEntry's 0.80
        var botA = SimHelper.CreateBot(0, aggression: 0.9f, raidTime: 0.3f, gameTime: 0f, posX: 10f, posZ: 10f);
        botA.SpawnTime = 0f;
        botA.SpawnEntryDuration = 3f;
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 100f;

        var botB = SimHelper.CreateBot(1, aggression: 0.1f, raidTime: 0.3f, gameTime: 0f, posX: 200f, posZ: 200f);
        botB.SpawnTime = 0f;
        botB.SpawnEntryDuration = 5f;
        botB.HasActiveObjective = true;
        botB.CurrentQuestAction = QuestActionId.MoveToPosition;
        botB.DistanceToObjective = 100f;

        var botC = SimHelper.CreateBot(2, aggression: 0.5f, raidTime: 0.3f, gameTime: 0f, posX: 150f, posZ: 50f);
        botC.SpawnTime = 0f;
        botC.SpawnEntryDuration = 4f;
        botC.HasActiveObjective = true;
        botC.CurrentQuestAction = QuestActionId.MoveToPosition;
        botC.DistanceToObjective = 100f;

        var bots = new[] { botA, botB, botC };

        // t=1: All in SpawnEntry
        SimHelper.AdvanceAndScore(_mgr, bots, 1f);
        Assert.IsInstanceOf<SpawnEntryTask>(botA.TaskAssignment.Task, "A: SpawnEntry at t=1");
        Assert.IsInstanceOf<SpawnEntryTask>(botB.TaskAssignment.Task, "B: SpawnEntry at t=1");
        Assert.IsInstanceOf<SpawnEntryTask>(botC.TaskAssignment.Task, "C: SpawnEntry at t=1");

        // === PHASE 2: Questing (t=4..50) ===
        // A's entry finishes at t=3, C at t=4, B at t=5
        SimHelper.AdvanceAndScore(_mgr, bots, 4f);
        Assert.IsInstanceOf<GoToObjectiveTask>(botA.TaskAssignment.Task, "A: GoToObjective at t=4");
        Assert.IsInstanceOf<SpawnEntryTask>(botB.TaskAssignment.Task, "B: still SpawnEntry at t=4");
        Assert.IsInstanceOf<GoToObjectiveTask>(botC.TaskAssignment.Task, "C: GoToObjective at t=4");

        SimHelper.AdvanceAndScore(_mgr, bots, 6f);
        Assert.IsInstanceOf<GoToObjectiveTask>(botA.TaskAssignment.Task, "A: GoToObjective at t=6");
        Assert.IsInstanceOf<GoToObjectiveTask>(botB.TaskAssignment.Task, "B: GoToObjective at t=6");
        Assert.IsInstanceOf<GoToObjectiveTask>(botC.TaskAssignment.Task, "C: GoToObjective at t=6");

        // === PHASE 3: Combat Event (t=50) ===
        for (int i = 0; i < 20; i++)
        {
            CombatEventRegistry.RecordEvent(120f, 0f, 50f, 49f + i * 0.1f, 100f, CombatEventType.Gunshot, false);
        }

        SimHelper.ScanCombatEventsAll(bots, 51f);
        SimHelper.AdvanceAndScore(_mgr, bots, 51f);

        // Aggressive bot A: should react to combat (vulture/investigate)
        // The exact task depends on whether intensity meets vulture threshold (15)
        // With 20 events at same position, intensity should be high
        bool botAReacted = botA.TaskAssignment.Task is VultureTask || botA.TaskAssignment.Task is InvestigateTask;
        // Timid bot B (aggression=0.1): vulture modifier = 0.76, so lower scores
        // Still may investigate due to high intensity but with lower chance
        // The actual task depends on score vs GoToObjective with hysteresis

        // Verify at least aggressive bot has higher vulture/investigate scores
        int vultureOrd = Array.FindIndex(_mgr.Tasks, t => t is VultureTask);
        int investOrd = Array.FindIndex(_mgr.Tasks, t => t is InvestigateTask);
        float aVulture = botA.TaskScores[vultureOrd];
        float bVulture = botB.TaskScores[vultureOrd];
        Assert.Greater(aVulture, bVulture, "Aggressive bot should have higher vulture score");

        // === PHASE 4: Post-combat linger (simulate objective completion) ===
        // Remove botA from current task to clear hysteresis, simulating the
        // objective completion path that would call RemoveEntity internally
        _mgr.RemoveEntity(botA);
        botA.HasActiveObjective = false;
        botA.ObjectiveCompletedTime = 54f;
        botA.LingerDuration = 15f;
        botA.VulturePhase = VulturePhase.Complete;
        botA.IsSpawnEntryComplete = true;

        CombatEventRegistry.CleanupExpired(100f, 30f); // Events from t~49 expire
        SimHelper.ScanCombatEventsAll(bots, 55f);
        SimHelper.AdvanceAndScore(_mgr, bots, 55f);

        // Bot A just completed objective (1s ago) — linger score = 0.45 * (1 - 1/15) * modifier
        // With no previous task (hysteresis cleared), LingerTask should win
        Assert.IsInstanceOf<LingerTask>(botA.TaskAssignment.Task, "A: should Linger after completing objective");

        // === PHASE 5: Late raid (t=500, raidTime=0.85) ===
        foreach (var bot in bots)
        {
            bot.RaidTimeNormalized = 0.85f;
            bot.HasActiveObjective = true;
            bot.CurrentQuestAction = QuestActionId.MoveToPosition;
            bot.DistanceToObjective = 100f;
            bot.ObjectiveCompletedTime = 0f;
            bot.IsLingering = false;
            bot.VulturePhase = VulturePhase.None;
        }

        SimHelper.AdvanceAndScore(_mgr, bots, 500f);

        // Late raid: GoToObjective modifier = Lerp(1.2, 0.8, 0.85) = 0.86
        // All should still GoToObjective but scores should be lower than early raid
        Assert.IsInstanceOf<GoToObjectiveTask>(botA.TaskAssignment.Task, "A: GoToObjective in late raid");
        Assert.IsInstanceOf<GoToObjectiveTask>(botB.TaskAssignment.Task, "B: GoToObjective in late raid");
        Assert.IsInstanceOf<GoToObjectiveTask>(botC.TaskAssignment.Task, "C: GoToObjective in late raid");
    }

    [Test]
    public void MultipleCombatEvents_BotsTrackDifferentEvents_ByProximity()
    {
        var botA = SimHelper.CreateBot(0, aggression: 0.7f, gameTime: 100f, posX: 50f, posZ: 50f);
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 200f;
        botA.IsSpawnEntryComplete = true;

        var botB = SimHelper.CreateBot(1, aggression: 0.7f, gameTime: 100f, posX: 500f, posZ: 500f);
        botB.HasActiveObjective = true;
        botB.CurrentQuestAction = QuestActionId.MoveToPosition;
        botB.DistanceToObjective = 200f;
        botB.IsSpawnEntryComplete = true;

        // Event 1 near bot A
        for (int i = 0; i < 20; i++)
        {
            CombatEventRegistry.RecordEvent(70f, 0f, 50f, 99f + i * 0.05f, 100f, CombatEventType.Gunshot, false);
        }
        // Event 2 near bot B
        for (int i = 0; i < 20; i++)
        {
            CombatEventRegistry.RecordEvent(480f, 0f, 500f, 99f + i * 0.05f, 100f, CombatEventType.Gunshot, false);
        }

        SimHelper.ScanCombatEvents(botA, 100f, detectionRange: 200f);
        SimHelper.ScanCombatEvents(botB, 100f, detectionRange: 200f);

        Assert.IsTrue(botA.HasNearbyEvent);
        Assert.IsTrue(botB.HasNearbyEvent);

        // Bot A should see event 1, Bot B should see event 2
        Assert.AreEqual(70f, botA.NearbyEventX, 1f, "Bot A should see event near x=70");
        Assert.AreEqual(480f, botB.NearbyEventX, 1f, "Bot B should see event near x=480");

        // They should have independent intensity values
        // Both have 20 events near them
        Assert.Greater(botA.CombatIntensity, 0, "Bot A should have non-zero intensity");
        Assert.Greater(botB.CombatIntensity, 0, "Bot B should have non-zero intensity");
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 8. Task Manager Multi-Bot Consistency — stress tests
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class TaskManagerMultiBotConsistencyTests
{
    private UtilityTaskManager _mgr;

    [SetUp]
    public void SetUp()
    {
        _mgr = QuestTaskFactory.Create();
    }

    [Test]
    public void TenBots_BatchScoring_MatchesIndividualScoring()
    {
        // Create 10 bots with varying states
        var bots = new BotEntity[10];
        for (int i = 0; i < 10; i++)
        {
            bots[i] = SimHelper.CreateBot(i, aggression: i * 0.1f, raidTime: 0.3f, gameTime: 100f);
            bots[i].HasActiveObjective = i % 2 == 0;
            bots[i].CurrentQuestAction = i % 2 == 0 ? QuestActionId.MoveToPosition : QuestActionId.Undefined;
            bots[i].DistanceToObjective = i % 2 == 0 ? 100f + i * 10f : float.MaxValue;
            bots[i].IsSpawnEntryComplete = true;
        }

        // Score individually
        var individualScores = new float[10][];
        for (int i = 0; i < 10; i++)
        {
            _mgr.ScoreAndPick(bots[i]);
            individualScores[i] = new float[QuestTaskFactory.TaskCount];
            Array.Copy(bots[i].TaskScores, individualScores[i], QuestTaskFactory.TaskCount);
        }

        // Reset tasks
        for (int i = 0; i < 10; i++)
        {
            _mgr.RemoveEntity(bots[i]);
        }

        // Create fresh bots with same parameters
        var bots2 = new BotEntity[10];
        for (int i = 0; i < 10; i++)
        {
            bots2[i] = SimHelper.CreateBot(i + 10, aggression: i * 0.1f, raidTime: 0.3f, gameTime: 100f);
            bots2[i].HasActiveObjective = i % 2 == 0;
            bots2[i].CurrentQuestAction = i % 2 == 0 ? QuestActionId.MoveToPosition : QuestActionId.Undefined;
            bots2[i].DistanceToObjective = i % 2 == 0 ? 100f + i * 10f : float.MaxValue;
            bots2[i].IsSpawnEntryComplete = true;
        }

        // Score via batch (UpdateScores + PickTasks)
        var botList = new List<BotEntity>(bots2);
        _mgr.UpdateScores(botList);
        _mgr.PickTasks(botList);

        // Verify scores match
        for (int i = 0; i < 10; i++)
        {
            for (int j = 0; j < QuestTaskFactory.TaskCount; j++)
            {
                Assert.AreEqual(
                    individualScores[i][j],
                    bots2[i].TaskScores[j],
                    0.0001f,
                    $"Bot {i} task {j} score mismatch (individual vs batch)"
                );
            }
        }
    }

    [Test]
    public void HysteresisPreventsThrashing_MultipleBots()
    {
        // Two bots with similar scores — hysteresis should prevent thrashing
        var botA = SimHelper.CreateBot(0, aggression: 0.5f, gameTime: 100f);
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 200f;
        botA.IsSpawnEntryComplete = true;

        // Also give loot target with moderate value
        botA.HasLootTarget = true;
        botA.LootTargetValue = 20000f;
        botA.LootTargetX = 10f;
        botA.LootTargetY = 0f;
        botA.LootTargetZ = 10f;
        botA.InventorySpaceFree = 10f;

        _mgr.ScoreAndPick(botA);
        var firstTask = botA.TaskAssignment.Task;
        string firstTaskType = firstTask.GetType().Name;

        // Re-score 10 times without changing state — task should not change
        for (int tick = 0; tick < 10; tick++)
        {
            _mgr.ScoreAndPick(botA);
            Assert.AreEqual(firstTaskType, botA.TaskAssignment.Task.GetType().Name, $"Task should be stable on tick {tick} (hysteresis)");
        }
    }

    [Test]
    public void InactiveBot_TaskDeactivated_NotScored()
    {
        var botA = SimHelper.CreateBot(0, gameTime: 100f);
        botA.HasActiveObjective = true;
        botA.CurrentQuestAction = QuestActionId.MoveToPosition;
        botA.DistanceToObjective = 200f;
        botA.IsSpawnEntryComplete = true;

        _mgr.ScoreAndPick(botA);
        Assert.IsNotNull(botA.TaskAssignment.Task, "Active bot should have task");

        // Deactivate
        botA.IsActive = false;
        _mgr.ScoreAndPick(botA);
        Assert.IsNull(botA.TaskAssignment.Task, "Inactive bot should have no task");
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 9. Squad Registry Multi-Bot Stress — edge cases
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class SquadRegistryMultiBotTests
{
    [Test]
    public void MultipleSquads_IndependentState()
    {
        var reg = new SquadRegistry();
        var botReg = new BotRegistry();

        // Squad 1: 3 members
        var s1 = reg.Add(2, 3);
        var s1b = botReg.Add(0);
        var s1f1 = botReg.Add(1);
        var s1f2 = botReg.Add(2);
        reg.AddMember(s1, s1b);
        reg.AddMember(s1, s1f1);
        reg.AddMember(s1, s1f2);

        // Squad 2: 2 members
        var s2 = reg.Add(2, 2);
        var s2b = botReg.Add(3);
        var s2f1 = botReg.Add(4);
        reg.AddMember(s2, s2b);
        reg.AddMember(s2, s2f1);

        Assert.AreEqual(2, reg.Count);
        Assert.AreEqual(3, s1.Members.Count);
        Assert.AreEqual(2, s2.Members.Count);

        // Remove leader of squad 1
        reg.RemoveMember(s1, s1b);
        Assert.AreEqual(2, s1.Members.Count);
        Assert.AreEqual(s1f1, s1.Leader, "S1 leader should be reassigned to F1");

        // Squad 2 unaffected
        Assert.AreEqual(2, s2.Members.Count);
        Assert.AreEqual(s2b, s2.Leader, "S2 leader should be unchanged");
    }

    [Test]
    public void SquadRemoval_MembersGetClearedReferences()
    {
        var reg = new SquadRegistry();
        var botReg = new BotRegistry();

        var squad = reg.Add(2, 3);
        var boss = botReg.Add(0);
        var f1 = botReg.Add(1);
        reg.AddMember(squad, boss);
        reg.AddMember(squad, f1);

        Assert.AreEqual(squad, boss.Squad);
        Assert.AreEqual(squad, f1.Squad);

        // Remove entire squad
        reg.Remove(squad);

        Assert.IsNull(boss.Squad, "Boss should have no squad after squad removal");
        Assert.IsNull(f1.Squad, "F1 should have no squad after squad removal");
        Assert.AreEqual(SquadRole.None, boss.SquadRole);
        Assert.AreEqual(SquadRole.None, f1.SquadRole);
        Assert.IsFalse(boss.HasTacticalPosition);
        Assert.IsFalse(f1.HasTacticalPosition);
    }

    [Test]
    public void TransferBetweenSquads_CleanHandoff()
    {
        var reg = new SquadRegistry();
        var botReg = new BotRegistry();

        var squad1 = reg.Add(2, 3);
        var squad2 = reg.Add(2, 3);

        var boss1 = botReg.Add(0);
        var f1 = botReg.Add(1);
        var boss2 = botReg.Add(2);

        reg.AddMember(squad1, boss1);
        reg.AddMember(squad1, f1);
        reg.AddMember(squad2, boss2);

        Assert.AreEqual(squad1, f1.Squad);
        Assert.AreEqual(2, squad1.Members.Count);

        // Transfer f1 from squad1 to squad2
        reg.AddMember(squad2, f1);

        Assert.AreEqual(squad2, f1.Squad, "F1 should be in squad2 after transfer");
        Assert.AreEqual(1, squad1.Members.Count, "Squad1 should have 1 member after transfer");
        Assert.AreEqual(2, squad2.Members.Count, "Squad2 should have 2 members after transfer");
        Assert.IsFalse(squad1.Members.Contains(f1), "F1 should not be in squad1's member list");
        Assert.IsTrue(squad2.Members.Contains(f1), "F1 should be in squad2's member list");
    }

    [Test]
    public void AllMembersRemoved_SquadBecomesLeaderless()
    {
        var reg = new SquadRegistry();
        var botReg = new BotRegistry();

        var squad = reg.Add(2, 2);
        var boss = botReg.Add(0);
        var f1 = botReg.Add(1);

        reg.AddMember(squad, boss);
        reg.AddMember(squad, f1);

        reg.RemoveMember(squad, boss);
        reg.RemoveMember(squad, f1);

        Assert.AreEqual(0, squad.Members.Count, "Squad should have 0 members");
        Assert.IsNull(squad.Leader, "Squad should have no leader");
    }

    [Test]
    public void LeaderReassignment_PrefersActiveMembers()
    {
        var reg = new SquadRegistry();
        var botReg = new BotRegistry();

        var squad = reg.Add(2, 4);
        var boss = botReg.Add(0);
        var f1 = botReg.Add(1);
        f1.IsActive = false; // Inactive
        var f2 = botReg.Add(2);
        f2.IsActive = true; // Active

        reg.AddMember(squad, boss);
        reg.AddMember(squad, f1);
        reg.AddMember(squad, f2);

        // Remove boss — should pick active f2 over inactive f1
        reg.RemoveMember(squad, boss);

        Assert.AreEqual(f2, squad.Leader, "Should prefer active member as new leader");
        Assert.AreEqual(SquadRole.Leader, f2.SquadRole);
    }

    [Test]
    public void LeaderReassignment_AllInactive_FallsBackToFirst()
    {
        var reg = new SquadRegistry();
        var botReg = new BotRegistry();

        var squad = reg.Add(2, 3);
        var boss = botReg.Add(0);
        var f1 = botReg.Add(1);
        f1.IsActive = false;
        var f2 = botReg.Add(2);
        f2.IsActive = false;

        reg.AddMember(squad, boss);
        reg.AddMember(squad, f1);
        reg.AddMember(squad, f2);

        // Remove boss — all remaining are inactive, should fall back to first
        reg.RemoveMember(squad, boss);

        Assert.AreEqual(f1, squad.Leader, "Should fall back to first member when all inactive");
        Assert.AreEqual(SquadRole.Leader, f1.SquadRole);
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 10. Combat Event Registry Concurrency — static state edge cases
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class CombatEventRegistryConcurrencyTests
{
    [SetUp]
    public void SetUp()
    {
        CombatEventRegistry.Initialize(128);
    }

    [TearDown]
    public void TearDown()
    {
        CombatEventRegistry.Clear();
    }

    [Test]
    public void RingBufferOverflow_OldEventsOverwritten()
    {
        CombatEventRegistry.Initialize(4); // Small buffer

        // Write 6 events — buffer is 4, so 2 oldest should be overwritten
        for (int i = 0; i < 6; i++)
        {
            CombatEventRegistry.RecordEvent(i * 10f, 0f, 0f, i * 1f, 100f, CombatEventType.Gunshot, false);
        }

        Assert.AreEqual(4, CombatEventRegistry.Count, "Buffer should cap at capacity");

        // Only events at x=20,30,40,50 should be findable (events at x=0,10 overwritten)
        // Bot at x=0 with range 15 — should NOT find overwritten events
        bool found = CombatEventRegistry.GetNearestEvent(0f, 0f, 15f, 10f, 60f, out var nearest);
        // Events 0 and 1 (at x=0 and x=10) were overwritten
        // The bot at x=0 is within range 15 of x=10, but that was overwritten
        // Nearest active event would be at x=20 (distance=20, > range 15)
        Assert.IsFalse(found, "Overwritten events should not be found");

        // Bot at x=25 should find event at x=20 or x=30
        found = CombatEventRegistry.GetNearestEvent(25f, 0f, 15f, 10f, 60f, out nearest);
        Assert.IsTrue(found, "Should find recent event within range");
    }

    [Test]
    public void CleanupExpired_OnlyMarksOldEvents()
    {
        CombatEventRegistry.RecordEvent(0f, 0f, 0f, 10f, 100f, CombatEventType.Gunshot, false); // Old
        CombatEventRegistry.RecordEvent(50f, 0f, 0f, 90f, 100f, CombatEventType.Gunshot, false); // Recent

        CombatEventRegistry.CleanupExpired(100f, 30f); // maxAge=30 → events older than t=70 expire

        // Old event (t=10) should be expired
        // Recent event (t=90) should still be active
        bool found = CombatEventRegistry.GetNearestEvent(50f, 0f, 100f, 100f, 30f, out var nearest);
        Assert.IsTrue(found, "Recent event should still be found after cleanup");
        Assert.AreEqual(50f, nearest.X, 1f, "Should find the recent event");

        // Old event should not be found
        found = CombatEventRegistry.GetNearestEvent(0f, 0f, 10f, 100f, 30f, out nearest);
        Assert.IsFalse(found, "Old event should not be found after cleanup");
    }

    [Test]
    public void GatherCombatPull_LinearDecay_CorrectStrength()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 80f, 100f, CombatEventType.Gunshot, false);

        var buffer = new SPTQuestingBots.ZoneMovement.Core.CombatPullPoint[128];
        int count = CombatEventRegistry.GatherCombatPull(buffer, 90f, 20f, 1.0f);

        Assert.AreEqual(1, count, "Should gather 1 event");
        // Age = 90 - 80 = 10s, maxAge = 20s → decay = 1 - 10/20 = 0.5
        // Strength = 0.5 * (100/100) * 1.0 = 0.5
        Assert.AreEqual(0.5f, buffer[0].Strength, 0.01f, "Decay should be linear");
    }

    [Test]
    public void GatherActiveEvents_ReturnsOnlyWithinAge()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 10f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 50f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(30f, 0f, 30f, 90f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatEvent[128];
        int count = CombatEventRegistry.GatherActiveEvents(buffer, 100f, 20f);

        // Only event at t=90 is within 20s of t=100
        Assert.AreEqual(1, count, "Only 1 event within maxAge=20s");
        Assert.AreEqual(30f, buffer[0].X, 1f, "Should be the most recent event");
    }

    [Test]
    public void CombatIntensity_ExcludesExpiredEvents()
    {
        // Old event
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, false);
        // Recent events
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 95f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 96f, 100f, CombatEventType.Gunshot, false);

        // Intensity with 30s window at t=100 should only count events at t=95 and t=96
        int intensity = CombatEventRegistry.GetIntensity(100f, 100f, 50f, 30f, 100f);
        Assert.AreEqual(2, intensity, "Should only count events within time window");
    }
}

// ═══════════════════════════════════════════════════════════════════════
// 11. Scoring Modifiers — multi-bot personality/time divergence
// ═══════════════════════════════════════════════════════════════════════

[TestFixture]
public class ScoringModifierMultiBotTests
{
    [Test]
    public void FivePersonalities_GoToObjective_MonotonicWithAggression()
    {
        // GoToObjective: Lerp(0.85, 1.15, aggression) — should increase with aggression
        float[] aggressions = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };
        float[] modifiers = new float[5];

        for (int i = 0; i < 5; i++)
        {
            modifiers[i] = ScoringModifiers.PersonalityModifier(aggressions[i], BotActionTypeId.GoToObjective);
        }

        for (int i = 1; i < 5; i++)
        {
            Assert.Greater(
                modifiers[i],
                modifiers[i - 1],
                $"GoToObjective modifier should increase with aggression ({aggressions[i]} vs {aggressions[i - 1]})"
            );
        }
    }

    [Test]
    public void FivePersonalities_Vulture_MonotonicWithAggression()
    {
        // Vulture: Lerp(0.7, 1.3, aggression) — should increase with aggression
        float[] aggressions = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };
        float[] modifiers = new float[5];

        for (int i = 0; i < 5; i++)
        {
            modifiers[i] = ScoringModifiers.PersonalityModifier(aggressions[i], BotActionTypeId.Vulture);
        }

        for (int i = 1; i < 5; i++)
        {
            Assert.Greater(
                modifiers[i],
                modifiers[i - 1],
                $"Vulture modifier should increase with aggression ({aggressions[i]} vs {aggressions[i - 1]})"
            );
        }
    }

    [Test]
    public void FivePersonalities_Linger_DecreasesWithAggression()
    {
        // Linger: Lerp(1.3, 0.7, aggression) — should decrease with aggression
        float[] aggressions = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };
        float[] modifiers = new float[5];

        for (int i = 0; i < 5; i++)
        {
            modifiers[i] = ScoringModifiers.PersonalityModifier(aggressions[i], BotActionTypeId.Linger);
        }

        for (int i = 1; i < 5; i++)
        {
            Assert.Less(
                modifiers[i],
                modifiers[i - 1],
                $"Linger modifier should decrease with aggression ({aggressions[i]} vs {aggressions[i - 1]})"
            );
        }
    }

    [Test]
    public void RaidTimeProgression_GoToObjective_DecreasesOverTime()
    {
        // GoToObjective: Lerp(1.2, 0.8, raidTime) — should decrease with time
        float[] times = { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };
        float[] modifiers = new float[5];

        for (int i = 0; i < 5; i++)
        {
            modifiers[i] = ScoringModifiers.RaidTimeModifier(times[i], BotActionTypeId.GoToObjective);
        }

        for (int i = 1; i < 5; i++)
        {
            Assert.Less(modifiers[i], modifiers[i - 1], $"GoToObjective time modifier should decrease ({times[i]} vs {times[i - 1]})");
        }
    }

    [Test]
    public void CombinedModifier_ProductOfTwoModifiers()
    {
        float aggression = 0.6f;
        float raidTime = 0.4f;
        int actionId = BotActionTypeId.GoToObjective;

        float personality = ScoringModifiers.PersonalityModifier(aggression, actionId);
        float time = ScoringModifiers.RaidTimeModifier(raidTime, actionId);
        float combined = ScoringModifiers.CombinedModifier(aggression, raidTime, actionId);

        Assert.AreEqual(personality * time, combined, 0.0001f, "Combined should be product of personality and time");
    }

    [Test]
    public void NaNInput_CombinedModifier_Returns1()
    {
        float result = ScoringModifiers.CombinedModifier(float.NaN, 0.5f, BotActionTypeId.GoToObjective);
        Assert.AreEqual(1.0f, result, "NaN aggression should produce 1.0 combined modifier");
    }
}
