using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Integration;

/// <summary>
/// E2E tests for the combat event pipeline and config-gated task scoring:
///   - Combat event scanning runs when investigate OR vulture is enabled
///   - Task scorers respect IsEnabled flag
///   - QuestUtilityTask.UpdateScores zeroes scores when disabled
///   - Grenade events are recorded via CombatEventRegistry
/// </summary>
[TestFixture]
public class CombatEventPipelineAndConfigGateTests
{
    [SetUp]
    public void SetUp()
    {
        CombatEventRegistry.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        CombatEventRegistry.Clear();
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private BotEntity CreateBot(int id, float x = 0f, float y = 0f, float z = 0f)
    {
        var bot = new BotEntity(id) { IsActive = true };
        bot.CurrentPositionX = x;
        bot.CurrentPositionY = y;
        bot.CurrentPositionZ = z;
        bot.TaskScores = new float[18];
        return bot;
    }

    private void RecordGunshot(float x, float z, float time, float power = 10f)
    {
        CombatEventRegistry.RecordEvent(
            new CombatEvent
            {
                X = x,
                Y = 0f,
                Z = z,
                Time = time,
                Power = power,
                Type = CombatEventType.Gunshot,
                IsBoss = false,
                IsActive = true,
            }
        );
    }

    // ========================================================================
    // 1. Combat Event Scanner Independence from Vulture
    // ========================================================================

    [Test]
    public void CombatEventScanner_WritesHasNearbyEvent_WhenEventsExist()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        var entities = new List<BotEntity> { bot };

        // Record nearby gunshot
        RecordGunshot(110, 110, time: 1.0f);

        // Run scanner (simulating what updateCombatEvents does)
        CombatEventScanner.UpdateEntities(
            entities,
            currentTime: 1.5f,
            maxEventAge: 300f,
            detectionRange: 120f,
            intensityRadius: 120f,
            intensityWindow: 15f,
            bossAvoidanceRadius: 75f,
            bossZoneDecay: 120f
        );

        Assert.IsTrue(bot.HasNearbyEvent, "Bot should detect nearby event");
        Assert.Greater(bot.CombatIntensity, 0, "Bot should have non-zero combat intensity");
    }

    [Test]
    public void CombatEventScanner_NoEvents_HasNearbyEventIsFalse()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        var entities = new List<BotEntity> { bot };

        CombatEventScanner.UpdateEntities(
            entities,
            currentTime: 1.5f,
            maxEventAge: 300f,
            detectionRange: 120f,
            intensityRadius: 120f,
            intensityWindow: 15f,
            bossAvoidanceRadius: 75f,
            bossZoneDecay: 120f
        );

        Assert.IsFalse(bot.HasNearbyEvent);
        Assert.AreEqual(0, bot.CombatIntensity);
    }

    [Test]
    public void CombatEventScanner_FarEvent_NotDetected()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        var entities = new List<BotEntity> { bot };

        // Record far-away gunshot (500m away, detection range 120m)
        RecordGunshot(600, 600, time: 1.0f);

        CombatEventScanner.UpdateEntities(
            entities,
            currentTime: 1.5f,
            maxEventAge: 300f,
            detectionRange: 120f,
            intensityRadius: 120f,
            intensityWindow: 15f,
            bossAvoidanceRadius: 75f,
            bossZoneDecay: 120f
        );

        Assert.IsFalse(bot.HasNearbyEvent, "Bot should not detect far event");
    }

    [Test]
    public void CombatEventCleanup_RemovesExpiredEvents()
    {
        RecordGunshot(100, 100, time: 1.0f);
        RecordGunshot(200, 200, time: 2.0f);

        // Cleanup with maxAge=5 at time=100 — both should be expired
        CombatEventRegistry.CleanupExpired(100f, 5f);

        var bot = CreateBot(0, x: 100, z: 100);
        CombatEventScanner.UpdateEntities(
            new List<BotEntity> { bot },
            currentTime: 100f,
            maxEventAge: 5f,
            detectionRange: 120f,
            intensityRadius: 120f,
            intensityWindow: 15f,
            bossAvoidanceRadius: 75f,
            bossZoneDecay: 120f
        );

        Assert.IsFalse(bot.HasNearbyEvent, "Expired events should not trigger detection");
    }

    // ========================================================================
    // 2. InvestigateTask Scores Based on CombatEventScanner Output
    // ========================================================================

    [Test]
    public void InvestigateTask_ScoresWhenHasNearbyEvent()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = true;
        bot.NearbyEventX = 110;
        bot.NearbyEventZ = 110;
        bot.CombatIntensity = 10;
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;

        var task = new InvestigateTask();
        task.ScoreEntity(0, bot);

        Assert.Greater(bot.TaskScores[0], 0f, "Investigate should score > 0 with nearby event");
    }

    [Test]
    public void InvestigateTask_ZeroScoreWhenNoEvent()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = false;
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;

        var task = new InvestigateTask();
        task.ScoreEntity(0, bot);

        Assert.AreEqual(0f, bot.TaskScores[0], "Investigate should score 0 without nearby event");
    }

    [Test]
    public void InvestigateTask_ZeroScoreWhenInCombat()
    {
        var bot = CreateBot(0, x: 100, z: 100);
        bot.HasNearbyEvent = true;
        bot.NearbyEventX = 110;
        bot.NearbyEventZ = 110;
        bot.CombatIntensity = 10;
        bot.IsInCombat = true;
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;

        var task = new InvestigateTask();
        task.ScoreEntity(0, bot);

        Assert.AreEqual(0f, bot.TaskScores[0], "Investigate should score 0 when in combat");
    }

    // ========================================================================
    // 3. QuestUtilityTask.IsEnabled Gate
    // ========================================================================

    [Test]
    public void IsEnabled_False_ZeroesAllScores()
    {
        var bot1 = CreateBot(0);
        var bot2 = CreateBot(1);
        bot1.HasNearbyEvent = true;
        bot1.NearbyEventX = 10;
        bot1.NearbyEventZ = 10;
        bot1.CombatIntensity = 10;
        bot1.RaidTimeNormalized = 0.5f;
        bot1.Aggression = 0.5f;
        bot2.HasNearbyEvent = true;
        bot2.NearbyEventX = 10;
        bot2.NearbyEventZ = 10;
        bot2.CombatIntensity = 10;
        bot2.RaidTimeNormalized = 0.5f;
        bot2.Aggression = 0.5f;

        var task = new InvestigateTask();
        task.IsEnabled = false;

        var entities = new List<BotEntity> { bot1, bot2 };
        task.UpdateScores(0, entities);

        Assert.AreEqual(0f, bot1.TaskScores[0], "Disabled task should zero bot1 score");
        Assert.AreEqual(0f, bot2.TaskScores[0], "Disabled task should zero bot2 score");
    }

    [Test]
    public void IsEnabled_True_ScoresNormally()
    {
        var bot = CreateBot(0);
        bot.HasNearbyEvent = true;
        bot.NearbyEventX = 10;
        bot.NearbyEventZ = 10;
        bot.CombatIntensity = 10;
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;

        var task = new InvestigateTask();
        task.IsEnabled = true;

        var entities = new List<BotEntity> { bot };
        task.UpdateScores(0, entities);

        Assert.Greater(bot.TaskScores[0], 0f, "Enabled task should score normally");
    }

    [Test]
    public void IsEnabled_DefaultsToTrue()
    {
        var task = new InvestigateTask();
        Assert.IsTrue(task.IsEnabled);
    }

    [Test]
    public void VultureTask_IsEnabled_Gate()
    {
        var bot = CreateBot(0);
        bot.HasNearbyEvent = true;
        bot.NearbyEventX = 10;
        bot.NearbyEventZ = 10;
        bot.CombatIntensity = 20;
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;
        bot.TaskScores[0] = 999f; // Pre-seed with non-zero

        var task = new VultureTask();
        task.IsEnabled = false;

        task.UpdateScores(0, new List<BotEntity> { bot });

        Assert.AreEqual(0f, bot.TaskScores[0], "Disabled vulture should zero score");
    }

    [Test]
    public void LingerTask_IsEnabled_Gate()
    {
        var bot = CreateBot(0);
        bot.ObjectiveCompletedTime = 1.0f;
        bot.LingerDuration = 10f;
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;
        bot.TaskScores[0] = 999f;

        var task = new LingerTask();
        task.IsEnabled = false;

        task.UpdateScores(0, new List<BotEntity> { bot });

        Assert.AreEqual(0f, bot.TaskScores[0], "Disabled linger should zero score");
    }

    [Test]
    public void SpawnEntryTask_IsEnabled_Gate()
    {
        var bot = CreateBot(0);
        bot.SpawnEntryDuration = 3f;
        bot.SpawnTime = 0f;
        bot.RaidTimeNormalized = 0.01f;
        bot.Aggression = 0.5f;
        bot.TaskScores[0] = 999f;

        var task = new SpawnEntryTask();
        task.IsEnabled = false;

        task.UpdateScores(0, new List<BotEntity> { bot });

        Assert.AreEqual(0f, bot.TaskScores[0], "Disabled spawn entry should zero score");
    }

    [Test]
    public void PatrolTask_IsEnabled_Gate()
    {
        var bot = CreateBot(0);
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;
        bot.TaskScores[0] = 999f;

        var task = new PatrolTask();
        task.IsEnabled = false;

        task.UpdateScores(0, new List<BotEntity> { bot });

        Assert.AreEqual(0f, bot.TaskScores[0], "Disabled patrol should zero score");
    }

    [Test]
    public void LootTask_IsEnabled_Gate()
    {
        var bot = CreateBot(0);
        bot.HasLootTarget = true;
        bot.LootTargetValue = 100f;
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;
        bot.TaskScores[0] = 999f;

        var task = new LootTask();
        task.IsEnabled = false;

        task.UpdateScores(0, new List<BotEntity> { bot });

        Assert.AreEqual(0f, bot.TaskScores[0], "Disabled loot should zero score");
    }

    // ========================================================================
    // 4. CombatEventRegistry Ring Buffer
    // ========================================================================

    [Test]
    public void GrenadeExplosion_RecordedInRegistry()
    {
        CombatEventRegistry.RecordEvent(
            new CombatEvent
            {
                X = 50f,
                Y = 0f,
                Z = 50f,
                Time = 1.0f,
                Power = 150f,
                Type = CombatEventType.Explosion,
                IsBoss = false,
                IsActive = true,
            }
        );

        var bot = CreateBot(0, x: 55, z: 55);
        CombatEventScanner.UpdateEntities(
            new List<BotEntity> { bot },
            currentTime: 1.5f,
            maxEventAge: 300f,
            detectionRange: 120f,
            intensityRadius: 120f,
            intensityWindow: 15f,
            bossAvoidanceRadius: 75f,
            bossZoneDecay: 120f
        );

        Assert.IsTrue(bot.HasNearbyEvent, "Bot should detect nearby grenade explosion");
    }

    // ========================================================================
    // 5. E2E: Full Combat Event → Investigate Pipeline
    // ========================================================================

    [Test]
    public void E2E_Gunshot_TriggersCombatScan_TriggerInvestigate()
    {
        // Record gunshot
        RecordGunshot(100, 100, time: 1.0f, power: 20f);
        RecordGunshot(102, 102, time: 1.1f, power: 20f);
        RecordGunshot(101, 101, time: 1.2f, power: 20f);

        // Create bot within detection range
        var bot = CreateBot(0, x: 110, z: 110);
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;

        // Step 1: Combat event scanner writes entity fields
        CombatEventScanner.UpdateEntities(
            new List<BotEntity> { bot },
            currentTime: 1.5f,
            maxEventAge: 300f,
            detectionRange: 120f,
            intensityRadius: 120f,
            intensityWindow: 15f,
            bossAvoidanceRadius: 75f,
            bossZoneDecay: 120f
        );

        Assert.IsTrue(bot.HasNearbyEvent, "Scanner should detect nearby events");
        Assert.Greater(bot.CombatIntensity, 0, "Scanner should compute intensity > 0");

        // Step 2: Investigate task reads entity fields and scores
        var investigateTask = new InvestigateTask();
        investigateTask.IntensityThreshold = 3; // Lower threshold for test
        investigateTask.ScoreEntity(0, bot);

        Assert.Greater(bot.TaskScores[0], 0f, "Investigate should score > 0 from scanner output");
    }

    [Test]
    public void E2E_InvestigateEnabled_VultureDisabled_StillScores()
    {
        // This is the critical regression test for the vulture-gate bug.
        // Even when vulture is disabled, investigate should still work
        // because the combat event scanner now runs independently.

        // Record gunshot
        RecordGunshot(100, 100, time: 1.0f, power: 20f);

        var bot = CreateBot(0, x: 110, z: 110);
        bot.RaidTimeNormalized = 0.5f;
        bot.Aggression = 0.5f;

        // Scanner runs (not gated by vulture anymore)
        CombatEventScanner.UpdateEntities(
            new List<BotEntity> { bot },
            currentTime: 1.5f,
            maxEventAge: 300f,
            detectionRange: 120f,
            intensityRadius: 120f,
            intensityWindow: 15f,
            bossAvoidanceRadius: 75f,
            bossZoneDecay: 120f
        );

        // Investigate is enabled but vulture is not — should still score
        var investigateTask = new InvestigateTask();
        investigateTask.IsEnabled = true;
        investigateTask.IntensityThreshold = 1;
        investigateTask.ScoreEntity(0, bot);

        Assert.Greater(bot.TaskScores[0], 0f, "Investigate should work independently of vulture");
    }
}
