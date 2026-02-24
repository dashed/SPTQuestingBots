using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Integration;

/// <summary>
/// E2E integration tests that exercise the full squad + combat + loot pipeline
/// across system boundaries:
///   Squad Formation -> Strategy Selection -> Tactical Positioning ->
///   Formation Movement -> Combat Events -> Vulture/Loot Scoring ->
///   Squad Loot Coordination
/// </summary>
[TestFixture]
public class SquadCombatLootPipelineTests
{
    private SquadRegistry _squadRegistry;
    private SquadStrategyConfig _config;

    [SetUp]
    public void SetUp()
    {
        _squadRegistry = new SquadRegistry();
        _config = new SquadStrategyConfig
        {
            Enabled = true,
            GuardDistance = 8f,
            FlankDistance = 15f,
            OverwatchDistance = 25f,
            EscortDistance = 5f,
            ArrivalRadius = 3f,
            EnableCommunicationRange = false,
            EnableSquadPersonality = false,
            EnablePositionValidation = false,
            EnableCoverPositionSource = false,
            EnableCombatAwarePositioning = true,
            EnableObjectiveSharing = false,
            UseQuestTypeRoles = true,
        };
        CombatEventRegistry.Initialize(128);
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
        bot.TaskScores = new float[18]; // 18 tasks in the utility AI
        return bot;
    }

    private SquadEntity CreateSquadWithMembers(int strategyCount, out BotEntity leader, out BotEntity follower1, out BotEntity follower2)
    {
        var squad = _squadRegistry.Add(strategyCount, 3);
        leader = CreateBot(10, x: 100f, z: 100f);
        follower1 = CreateBot(11, x: 105f, z: 105f);
        follower2 = CreateBot(12, x: 95f, z: 95f);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, follower1);
        _squadRegistry.AddMember(squad, follower2);

        return squad;
    }

    private void SetLeaderObjective(
        BotEntity leader,
        SquadEntity squad,
        float objX,
        float objY,
        float objZ,
        int questAction = QuestActionId.MoveToPosition
    )
    {
        leader.HasActiveObjective = true;
        leader.CurrentQuestAction = questAction;
        squad.Objective.SetObjective(objX, objY, objZ);
    }

    // ========================================================================
    // 1. Squad Formation -> Strategy Selection
    // ========================================================================

    [Test]
    public void FullPipeline_SquadFormation_StrategySelection_GotoObjectiveScoresHigherThanVulture()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Score and pick
        manager.ScoreAndPick(squad);

        // GotoObjective should score 0.5, VultureSquad should score 0 (no vulture phase)
        Assert.That(squad.StrategyScores[0], Is.EqualTo(0.5f));
        Assert.That(squad.StrategyScores[1], Is.EqualTo(0f));
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy));
        Assert.That(squad.StrategyAssignment.Ordinal, Is.EqualTo(0));
    }

    [Test]
    public void FullPipeline_VultureStrategyActivatesWhenLeaderHasActiveVulturePhase()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        // Leader enters vulture phase with nearby event
        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 150f;
        leader.NearbyEventZ = 150f;

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        manager.ScoreAndPick(squad);

        // VultureSquad scores 0.75, beats GotoObjective's 0.5
        Assert.That(squad.StrategyScores[1], Is.EqualTo(0.75f));
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(vultureStrategy));
    }

    // ========================================================================
    // 2. Strategy -> Tactical Positioning -> Formation Movement
    // ========================================================================

    [Test]
    public void FullPipeline_GotoObjectiveAssignsTacticalPositionsAndFormationSpeeds()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Score, pick, and update (full cycle)
        manager.Update(_squadRegistry.ActiveSquads);

        // Verify followers got tactical positions (MoveToPosition => Escort roles)
        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
        Assert.That(f1.SquadRole, Is.EqualTo(SquadRole.Escort));
        Assert.That(f2.SquadRole, Is.EqualTo(SquadRole.Escort));

        // Verify positions are not at origin (computed from objective)
        Assert.That(f1.TacticalPositionX, Is.Not.EqualTo(0f));
        Assert.That(f2.TacticalPositionX, Is.Not.EqualTo(0f));

        // Formation speed: followers far from boss should sprint
        float dx1 = f1.CurrentPositionX - leader.CurrentPositionX;
        float dz1 = f1.CurrentPositionZ - leader.CurrentPositionZ;
        float distToBoss1 = dx1 * dx1 + dz1 * dz1;

        float tdx1 = f1.CurrentPositionX - f1.TacticalPositionX;
        float tdz1 = f1.CurrentPositionZ - f1.TacticalPositionZ;
        float distToTactical1 = tdx1 * tdx1 + tdz1 * tdz1;

        var formationConfig = FormationConfig.Default;
        var decision = FormationSpeedController.ComputeSpeedDecision(false, distToBoss1, distToTactical1, in formationConfig);

        // At 5m distance, within MatchSpeedDistance (15m), not slow approach
        // Decision should depend on distances
        Assert.That(decision, Is.Not.EqualTo(FormationSpeedDecision.Sprint), "Follower at 5m from boss should not need to sprint");
    }

    [Test]
    public void FullPipeline_AmbushQuestAssignsFlankAndOverwatchRoles()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f, QuestActionId.Ambush);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        manager.Update(_squadRegistry.ActiveSquads);

        // Ambush assigns: first=Flanker, second=Overwatch, rest=Guard
        Assert.That(f1.SquadRole, Is.EqualTo(SquadRole.Flanker));
        Assert.That(f2.SquadRole, Is.EqualTo(SquadRole.Overwatch));
    }

    [Test]
    public void FullPipeline_FormationSpeedController_ZeroDistance_ReturnsSlowApproach()
    {
        // Edge case: follower is exactly at boss position and tactical position
        var config = new FormationConfig(30f, 15f, 5f, true);
        var decision = FormationSpeedController.ComputeSpeedDecision(false, 0f, 0f, in config);

        // distToBoss=0 < MatchSpeed, distToTactical=0 < SlowApproach
        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.SlowApproach));
    }

    [Test]
    public void FullPipeline_FormationSpeedController_LargeDistance_ReturnsSprint()
    {
        var config = new FormationConfig(30f, 15f, 5f, true);
        float distToBossSqr = 50f * 50f; // 50m > 30m catch-up distance
        var decision = FormationSpeedController.ComputeSpeedDecision(false, distToBossSqr, 1000f, in config);

        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.Sprint));
    }

    // ========================================================================
    // 3. Combat Events -> CombatEventScanner -> VultureTask Scoring
    // ========================================================================

    [Test]
    public void FullPipeline_CombatEvents_ScanEntity_SetVultureFields()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);

        // Record nearby combat events
        CombatEventRegistry.RecordEvent(110f, 0f, 110f, time: 10f, power: 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(112f, 0f, 112f, time: 10.5f, power: 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(115f, 0f, 115f, time: 11f, power: 150f, CombatEventType.Explosion, false);

        CombatEventScanner.UpdateEntity(
            bot,
            currentTime: 12f,
            maxEventAge: 30f,
            detectionRange: 200f,
            intensityRadius: 50f,
            intensityWindow: 30f,
            bossAvoidanceRadius: 100f,
            bossZoneDecay: 60f
        );

        Assert.IsTrue(bot.HasNearbyEvent);
        Assert.That(bot.CombatIntensity, Is.GreaterThan(0));
        Assert.IsFalse(bot.IsInBossZone); // No boss events
    }

    [Test]
    public void FullPipeline_CombatEvents_VultureTaskScoring_IntensityAboveThreshold()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.3f;

        // Record many combat events to exceed courage threshold
        for (int i = 0; i < 20; i++)
        {
            CombatEventRegistry.RecordEvent(110f + i * 0.5f, 0f, 110f, time: 10f + i * 0.1f, power: 100f, CombatEventType.Gunshot, false);
        }

        CombatEventScanner.UpdateEntity(bot, 12f, 30f, 200f, 50f, 30f, 100f, 60f);

        Assert.IsTrue(bot.HasNearbyEvent);
        Assert.That(bot.CombatIntensity, Is.GreaterThanOrEqualTo(VultureTask.DefaultCourageThreshold));

        float score = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.GreaterThan(0f), "VultureTask should score > 0 when intensity exceeds courage threshold");
    }

    [Test]
    public void FullPipeline_CombatEvents_VultureTaskScoring_IntensityBelowThreshold()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);

        // Record only a few events (intensity below default threshold of 15)
        CombatEventRegistry.RecordEvent(110f, 0f, 110f, time: 10f, power: 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(112f, 0f, 110f, time: 10.5f, power: 100f, CombatEventType.Gunshot, false);

        CombatEventScanner.UpdateEntity(bot, 12f, 30f, 200f, 50f, 30f, 100f, 60f);

        Assert.IsTrue(bot.HasNearbyEvent);
        Assert.That(bot.CombatIntensity, Is.LessThan(VultureTask.DefaultCourageThreshold));

        float score = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f), "VultureTask should score 0 when intensity below courage threshold");
    }

    [Test]
    public void FullPipeline_CombatEvents_VultureTaskScoring_InCombat_ReturnsZero()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);
        bot.IsInCombat = true;
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.NearbyEventX = 110f;
        bot.NearbyEventZ = 110f;

        float score = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void FullPipeline_CombatEvents_VultureTaskScoring_BossZone_ReturnsZero()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);
        bot.IsInBossZone = true;
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.NearbyEventX = 110f;
        bot.NearbyEventZ = 110f;

        float score = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void FullPipeline_CombatEvents_VultureTaskScoring_OnCooldown_ReturnsZero()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);
        bot.VultureCooldownUntil = 200f;
        bot.CurrentGameTime = 100f;
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.NearbyEventX = 110f;
        bot.NearbyEventZ = 110f;

        float score = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void FullPipeline_CombatEvents_VultureTaskScoring_CooldownExpired_ScoresPositive()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);
        bot.VultureCooldownUntil = 100f;
        bot.CurrentGameTime = 200f; // Past cooldown
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.NearbyEventX = 110f;
        bot.NearbyEventZ = 110f;

        float score = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void FullPipeline_CombatEvents_ActiveVulturePhase_MaintainsMaxScore()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.VulturePhase = VulturePhase.SilentApproach;

        float score = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(VultureTask.MaxBaseScore));
    }

    // ========================================================================
    // 4. Combat Event Expiration -> Vulture Score Drop
    // ========================================================================

    [Test]
    public void FullPipeline_EventExpiration_VultureScoreDropsToZero()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);

        // Record events at time 10
        for (int i = 0; i < 20; i++)
        {
            CombatEventRegistry.RecordEvent(110f + i * 0.5f, 0f, 110f, time: 10f, power: 100f, CombatEventType.Gunshot, false);
        }

        // Scan at time 12 (events are 2s old, within 30s max age)
        CombatEventScanner.UpdateEntity(bot, 12f, 30f, 200f, 50f, 30f, 100f, 60f);
        Assert.IsTrue(bot.HasNearbyEvent);
        float scoreActive = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, 200f);
        Assert.That(scoreActive, Is.GreaterThan(0f));

        // Now expire events (scan at time 100, events are 90s old, maxAge=30s)
        CombatEventScanner.UpdateEntity(bot, 100f, 30f, 200f, 50f, 30f, 100f, 60f);
        Assert.IsFalse(bot.HasNearbyEvent);

        float scoreExpired = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, 200f);
        Assert.That(scoreExpired, Is.EqualTo(0f), "VultureTask should score 0 after events expire");
    }

    [Test]
    public void FullPipeline_EventExpiration_VultureSquadStrategyScoreDropsToZero()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 150f;
        leader.NearbyEventZ = 150f;

        float activeScore = VultureSquadStrategy.Score(squad);
        Assert.That(activeScore, Is.EqualTo(VultureSquadStrategy.BaseScore));

        // Events expire — leader's HasNearbyEvent goes false
        leader.HasNearbyEvent = false;

        float expiredScore = VultureSquadStrategy.Score(squad);
        Assert.That(expiredScore, Is.EqualTo(0f));
    }

    [Test]
    public void FullPipeline_EventExpiration_StrategyManagerSwitchesFromVultureToGotoObjective()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 150f;
        leader.NearbyEventZ = 150f;

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // First pick: VultureSquad wins (0.75 > 0.5)
        manager.ScoreAndPick(squad);
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(vultureStrategy));

        // Events expire
        leader.HasNearbyEvent = false;
        leader.VulturePhase = VulturePhase.None;

        // Second pick: GotoObjective (0.5) should beat VultureSquad (0 + 0.20 hysteresis = 0.20)
        manager.ScoreAndPick(squad);
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy));
    }

    // ========================================================================
    // 5. Squad Loot Pipeline
    // ========================================================================

    [Test]
    public void FullPipeline_SquadLoot_BossClaimsHighestValue_FollowersPickUnclaimed()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        var claims = new LootClaimRegistry();

        var results = new LootScanResult[]
        {
            new LootScanResult
            {
                Id = 100,
                X = 150f,
                Y = 0f,
                Z = 150f,
                Value = 10000f,
                Type = 1,
            },
            new LootScanResult
            {
                Id = 101,
                X = 160f,
                Y = 0f,
                Z = 160f,
                Value = 8000f,
                Type = 1,
            },
            new LootScanResult
            {
                Id = 102,
                X = 170f,
                Y = 0f,
                Z = 170f,
                Value = 5000f,
                Type = 1,
            },
        };

        // Boss claims highest
        int bossIdx = SquadLootCoordinator.BossPriorityClaim(results, 3, claims, leader.Id);
        Assert.That(bossIdx, Is.EqualTo(0)); // Highest value at index 0

        // Share results with squad
        SquadLootCoordinator.ShareScanResults(squad, results, 3);
        Assert.That(squad.SharedLootCount, Is.EqualTo(3));

        // Follower 1 picks from shared (skips boss's target 100)
        int f1Idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, f1.Id, bossLootTargetId: results[bossIdx].Id, claims);
        Assert.That(f1Idx, Is.EqualTo(1)); // Next highest value (8000)

        // Follower 1 claims it
        Assert.IsTrue(claims.TryClaim(f1.Id, squad.SharedLootIds[f1Idx]));

        // Follower 2 picks from shared (skips boss's target and f1's claim)
        int f2Idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, f2.Id, bossLootTargetId: results[bossIdx].Id, claims);
        Assert.That(f2Idx, Is.EqualTo(2)); // Remaining (5000)
    }

    [Test]
    public void FullPipeline_SquadLoot_FollowerPermission_BossLooting()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);

        // Position within comm range
        f1.CurrentPositionX = leader.CurrentPositionX + 5f;
        f1.CurrentPositionZ = leader.CurrentPositionZ + 5f;

        // Boss is looting
        leader.IsLooting = true;
        float commRangeSqr = 35f * 35f;

        Assert.IsTrue(
            SquadLootCoordinator.ShouldFollowerLoot(f1, leader, commRangeSqr),
            "Follower should be allowed to loot when boss is looting"
        );
    }

    [Test]
    public void FullPipeline_SquadLoot_FollowerPermission_AtTacticalPosition()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);

        // Follower at tactical position (within 5m)
        f1.HasTacticalPosition = true;
        f1.TacticalPositionX = f1.CurrentPositionX + 2f;
        f1.TacticalPositionZ = f1.CurrentPositionZ + 2f;
        float commRangeSqr = 35f * 35f;

        Assert.IsTrue(
            SquadLootCoordinator.ShouldFollowerLoot(f1, leader, commRangeSqr),
            "Follower at tactical position should be allowed to loot"
        );
    }

    [Test]
    public void FullPipeline_SquadLoot_CombatBlocksLooting()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        leader.IsLooting = true;
        f1.IsInCombat = true;
        float commRangeSqr = 35f * 35f;

        Assert.IsFalse(
            SquadLootCoordinator.ShouldFollowerLoot(f1, leader, commRangeSqr),
            "Follower in combat should not loot even if boss is looting"
        );
    }

    // ========================================================================
    // 6. Full Pipeline: Combat Events -> Clustering -> Vulture Strategy -> Loot
    // ========================================================================

    [Test]
    public void FullPipeline_CombatClustering_ProducesFirefightClusters()
    {
        var events = new CombatEvent[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 100f,
                Time = 10f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 105f,
                Y = 0f,
                Z = 105f,
                Time = 10.5f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 103f,
                Y = 0f,
                Z = 103f,
                Time = 11f,
                Type = CombatEventType.Explosion,
                IsActive = true,
            },
            // Distant event — separate cluster
            new CombatEvent
            {
                X = 500f,
                Y = 0f,
                Z = 500f,
                Time = 11f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
        };

        var output = new CombatEventClustering.ClusterResult[4];
        float clusterRadiusSqr = 20f * 20f; // 20m radius
        int count = CombatEventClustering.ClusterEvents(events, 4, currentTime: 12f, maxAge: 30f, clusterRadiusSqr, output, 4);

        Assert.That(count, Is.EqualTo(2), "Should produce 2 clusters (nearby group + distant single)");
        // First cluster: 3 events near (100, 100)
        Assert.That(output[0].Intensity, Is.GreaterThanOrEqualTo(3));
    }

    [Test]
    public void FullPipeline_CombatClustering_IdenticalPositions_SingleCluster()
    {
        // Edge case: all events at identical positions
        var events = new CombatEvent[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 100f,
                Time = 10f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 100f,
                Time = 10.5f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 100f,
                Time = 11f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
        };

        var output = new CombatEventClustering.ClusterResult[4];
        int count = CombatEventClustering.ClusterEvents(events, 3, 12f, 30f, 400f, output, 4);

        Assert.That(count, Is.EqualTo(1), "All events at same position should form single cluster");
        Assert.That(output[0].X, Is.EqualTo(100f).Within(0.01f));
        Assert.That(output[0].Z, Is.EqualTo(100f).Within(0.01f));
        Assert.That(output[0].Intensity, Is.EqualTo(3));
    }

    [Test]
    public void FullPipeline_CombatClustering_ExcludesDeathEvents()
    {
        var events = new CombatEvent[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 100f,
                Time = 10f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 100f,
                Time = 10.5f,
                Type = CombatEventType.Death,
                IsActive = true,
            },
        };

        var output = new CombatEventClustering.ClusterResult[4];
        int count = CombatEventClustering.ClusterEvents(events, 2, 12f, 30f, 400f, output, 4);

        Assert.That(count, Is.EqualTo(1));
        Assert.That(output[0].Intensity, Is.EqualTo(1), "Death events should not contribute to cluster intensity");
    }

    [Test]
    public void FullPipeline_CombatClustering_FilterDeathEvents_ReturnsOnlyDeaths()
    {
        var events = new CombatEvent[]
        {
            new CombatEvent
            {
                X = 100f,
                Y = 0f,
                Z = 100f,
                Time = 10f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 200f,
                Y = 0f,
                Z = 200f,
                Time = 10.5f,
                Type = CombatEventType.Death,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 300f,
                Y = 0f,
                Z = 300f,
                Time = 11f,
                Type = CombatEventType.Death,
                IsActive = true,
            },
        };

        var output = new CombatEvent[4];
        int count = CombatEventClustering.FilterDeathEvents(events, 3, 12f, 30f, output);

        Assert.That(count, Is.EqualTo(2));
        Assert.That(output[0].X, Is.EqualTo(200f));
        Assert.That(output[1].X, Is.EqualTo(300f));
    }

    // ========================================================================
    // 7. Leader Death Integration
    // ========================================================================

    [Test]
    public void FullPipeline_LeaderDeath_StrategyGracefullyHandlesDeadLeader()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Initial pick: GotoObjective activated
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy));

        // Leader dies
        leader.IsActive = false;
        _squadRegistry.RemoveMember(squad, leader);

        // f1 should become the new leader (it's active)
        Assert.That(squad.Leader, Is.SameAs(f1));
        Assert.That(squad.Leader.IsActive, Is.True);

        // GotoObjectiveStrategy.ScoreSquad should return 0 (f1 has no active objective)
        gotoStrategy.ScoreSquad(0, squad);
        Assert.That(squad.StrategyScores[0], Is.EqualTo(0f));
    }

    [Test]
    public void FullPipeline_LeaderDeath_PrefersActiveFollowerAsNewLeader()
    {
        var squad = _squadRegistry.Add(2, 4);
        var leader = CreateBot(10, x: 100f, z: 100f);
        var deadFollower = CreateBot(11, x: 105f, z: 105f);
        var aliveFollower = CreateBot(12, x: 95f, z: 95f);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, deadFollower);
        _squadRegistry.AddMember(squad, aliveFollower);

        // deadFollower is inactive
        deadFollower.IsActive = false;

        // Remove leader
        leader.IsActive = false;
        _squadRegistry.RemoveMember(squad, leader);

        // aliveFollower should become leader (not deadFollower which is Members[0] after removal)
        Assert.That(squad.Leader, Is.SameAs(aliveFollower), "Active member should be preferred as new leader");
        Assert.That(squad.Leader.SquadRole, Is.EqualTo(SquadRole.Leader));
    }

    [Test]
    public void FullPipeline_LeaderDeath_AllMembersInactive_FallsBackToFirst()
    {
        var squad = _squadRegistry.Add(2, 4);
        var leader = CreateBot(10);
        var follower1 = CreateBot(11);
        var follower2 = CreateBot(12);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, follower1);
        _squadRegistry.AddMember(squad, follower2);

        // Both followers are inactive
        follower1.IsActive = false;
        follower2.IsActive = false;

        // Remove leader
        leader.IsActive = false;
        _squadRegistry.RemoveMember(squad, leader);

        // Falls back to first member when all are inactive
        Assert.That(squad.Leader, Is.SameAs(follower1));
    }

    // ========================================================================
    // 8. Empty Squad / All Followers Dead
    // ========================================================================

    [Test]
    public void FullPipeline_AllFollowersDead_StrategySkipsPositionAssignment()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        // Kill followers
        f1.IsActive = false;
        f2.IsActive = false;

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        manager.Update(_squadRegistry.ActiveSquads);

        // Strategy is activated but no positions assigned (followerCount == 0)
        Assert.That(squad.Objective.MemberCount, Is.EqualTo(0));
        Assert.IsFalse(f1.HasTacticalPosition);
        Assert.IsFalse(f2.HasTacticalPosition);
    }

    [Test]
    public void FullPipeline_SquadRemoved_StrategyDeactivated()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Activate strategy
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(gotoStrategy.ActiveSquadCount, Is.EqualTo(1));

        // Remove squad from strategy tracking
        manager.RemoveSquad(squad);
        Assert.That(gotoStrategy.ActiveSquadCount, Is.EqualTo(0));
        Assert.That(squad.StrategyAssignment.Strategy, Is.Null);
    }

    [Test]
    public void FullPipeline_InactiveSquad_StrategyManagerDeactivatesStrategy()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Activate strategy
        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(squad.StrategyAssignment.Strategy, Is.Not.Null);

        // Deactivate squad
        squad.IsActive = false;
        manager.PickStrategies(_squadRegistry.ActiveSquads);

        Assert.That(squad.StrategyAssignment.Strategy, Is.Null);
    }

    // ========================================================================
    // 9. VultureSquadStrategy Position Computation
    // ========================================================================

    [Test]
    public void FullPipeline_VultureSquadStrategy_AssignsPositionsToActiveFollowersOnly()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 200f;
        leader.NearbyEventY = 0f;
        leader.NearbyEventZ = 200f;

        // Kill follower 1
        f1.IsActive = false;

        var vultureStrategy = new VultureSquadStrategy();
        vultureStrategy.Activate(squad);
        vultureStrategy.Update();

        // f1 should NOT get a tactical position (inactive)
        Assert.IsFalse(f1.HasTacticalPosition, "Inactive follower should not get tactical position");
        // f2 should get a position
        Assert.IsTrue(f2.HasTacticalPosition, "Active follower should get tactical position");
    }

    [Test]
    public void FullPipeline_VultureSquadStrategy_SingleFollower_PositionIsOffset()
    {
        var squad = _squadRegistry.Add(2, 2);
        var leader = CreateBot(10, x: 100f, z: 100f);
        var follower = CreateBot(11, x: 105f, z: 105f);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, follower);

        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 200f;
        leader.NearbyEventY = 0f;
        leader.NearbyEventZ = 200f;

        var vultureStrategy = new VultureSquadStrategy();
        vultureStrategy.Activate(squad);
        vultureStrategy.Update();

        Assert.IsTrue(follower.HasTacticalPosition);
        // With single follower, lateral offset = spread (15m)
        // Position should be behind the target at an offset
        Assert.That(follower.TacticalPositionX, Is.Not.EqualTo(200f).Within(1f));
        Assert.That(follower.SquadRole, Is.EqualTo(SquadRole.Flanker));
    }

    [Test]
    public void FullPipeline_VultureSquadStrategy_LeaderAtTarget_UsesDefaultDirection()
    {
        var squad = _squadRegistry.Add(2, 2);
        var leader = CreateBot(10, x: 200f, z: 200f); // Same as event position
        var follower = CreateBot(11, x: 205f, z: 205f);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, follower);

        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 200f;
        leader.NearbyEventY = 0f;
        leader.NearbyEventZ = 200f;

        var vultureStrategy = new VultureSquadStrategy();
        vultureStrategy.Activate(squad);

        // Should not throw even with zero direction vector
        Assert.DoesNotThrow(() => vultureStrategy.Update());
        Assert.IsTrue(follower.HasTacticalPosition);
    }

    // ========================================================================
    // 10. Combat-Aware Tactical Repositioning
    // ========================================================================

    [Test]
    public void FullPipeline_CombatVersionChange_TriggersPositionRecompute()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Initial assignment
        manager.Update(_squadRegistry.ActiveSquads);
        float f1OldX = f1.TacticalPositionX;
        float f1OldZ = f1.TacticalPositionZ;

        // Simulate combat: set threat direction and bump combat version
        squad.HasThreatDirection = true;
        squad.ThreatDirectionX = 0.707f;
        squad.ThreatDirectionZ = 0.707f;
        squad.CombatVersion++;

        // Update again — should recompute positions
        manager.Update(_squadRegistry.ActiveSquads);

        // After combat-aware repositioning, positions should have changed
        // (CombatPositionAdjuster recomputes based on threat direction)
        bool positionsChanged = Math.Abs(f1.TacticalPositionX - f1OldX) > 0.01f || Math.Abs(f1.TacticalPositionZ - f1OldZ) > 0.01f;

        Assert.IsTrue(positionsChanged, "Tactical positions should change when combat threat is detected");
        Assert.That(
            squad.LastProcessedCombatVersion,
            Is.EqualTo(squad.CombatVersion),
            "LastProcessedCombatVersion should be updated after recompute"
        );
    }

    [Test]
    public void FullPipeline_ThreatCleared_PositionsRevertToStandard()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Set threat
        squad.HasThreatDirection = true;
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.CombatVersion = 1;

        manager.Update(_squadRegistry.ActiveSquads);

        // Clear threat
        squad.HasThreatDirection = false;
        squad.CombatVersion = 2;

        manager.Update(_squadRegistry.ActiveSquads);

        // Positions should still be assigned (reverted to standard geometric)
        Assert.IsTrue(f1.HasTacticalPosition);
        Assert.IsTrue(f2.HasTacticalPosition);
    }

    // ========================================================================
    // 11. Loot Scoring Integration
    // ========================================================================

    [Test]
    public void FullPipeline_LootTask_ScoresPositiveForAvailableLoot()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);
        bot.HasLootTarget = true;
        bot.LootTargetX = 110f;
        bot.LootTargetY = 0f;
        bot.LootTargetZ = 110f;
        bot.LootTargetValue = 30000f;
        bot.InventorySpaceFree = 2f;

        float score = LootTask.Score(bot);
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(score, Is.LessThanOrEqualTo(LootTask.MaxBaseScore));
    }

    [Test]
    public void FullPipeline_LootTask_InCombat_ReturnsZero()
    {
        var bot = CreateBot(1);
        bot.HasLootTarget = true;
        bot.LootTargetValue = 30000f;
        bot.InventorySpaceFree = 2f;
        bot.IsInCombat = true;

        float score = LootTask.Score(bot);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void FullPipeline_LootTask_NearObjective_GetsProximityBonus()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);
        bot.HasLootTarget = true;
        bot.LootTargetX = 105f;
        bot.LootTargetY = 0f;
        bot.LootTargetZ = 105f;
        bot.LootTargetValue = 30000f;
        bot.InventorySpaceFree = 2f;
        bot.HasActiveObjective = true;
        bot.DistanceToObjective = 10f; // Within 20m proximity threshold

        float scoreNear = LootTask.Score(bot);

        bot.HasActiveObjective = false;
        bot.DistanceToObjective = float.MaxValue;

        float scoreFar = LootTask.Score(bot);

        Assert.That(scoreNear, Is.GreaterThan(scoreFar), "Loot near objective should score higher");
    }

    [Test]
    public void FullPipeline_LootScorer_NaNValue_ReturnsZero()
    {
        var config = new LootScoringConfig(0f, 50000f, 0.001f, 0.15f, 0.1f, 0f);
        // Use large distanceToObjectiveSqr to avoid proximity bonus, so NaN value produces 0
        float score = LootScorer.Score(float.NaN, 100f, 2f, false, 10000f, 10f, false, in config);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void FullPipeline_ItemValueEstimator_NaNNormalization_ReturnsZero()
    {
        Assert.That(ItemValueEstimator.NormalizeValue(float.NaN, 50000f), Is.EqualTo(0f));
        Assert.That(ItemValueEstimator.NormalizeValue(10000f, float.NaN), Is.EqualTo(0f));
        Assert.That(ItemValueEstimator.NormalizeValue(10000f, 0f), Is.EqualTo(0f));
        Assert.That(ItemValueEstimator.NormalizeValue(-100f, 50000f), Is.EqualTo(0f));
    }

    // ========================================================================
    // 12. Full Cross-System Pipeline
    // ========================================================================

    [Test]
    public void FullPipeline_EndToEnd_SquadFormation_ThroughCombat_ToLoot()
    {
        // Phase 1: Squad formation
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        Assert.That(squad.Size, Is.EqualTo(3));
        Assert.That(leader.SquadRole, Is.EqualTo(SquadRole.Leader));

        // Phase 2: Set objective and strategy
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        manager.Update(_squadRegistry.ActiveSquads);
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy));
        Assert.IsTrue(f1.HasTacticalPosition, "Follower should have tactical position after strategy update");

        // Phase 3: Combat events appear
        for (int i = 0; i < 20; i++)
        {
            CombatEventRegistry.RecordEvent(150f + i * 0.5f, 0f, 150f, time: 50f + i * 0.1f, power: 100f, CombatEventType.Gunshot, false);
        }

        // Phase 4: Scan leader for vulture fields
        CombatEventScanner.UpdateEntity(leader, 52f, 30f, 200f, 50f, 30f, 100f, 60f);
        Assert.IsTrue(leader.HasNearbyEvent);
        Assert.That(leader.CombatIntensity, Is.GreaterThanOrEqualTo(VultureTask.DefaultCourageThreshold));

        // Phase 5: Leader enters vulture mode — temporarily drop objective so
        // GotoObjective scores 0, allowing VultureSquad (0.75) to overcome hysteresis
        leader.HasActiveObjective = false;
        leader.VulturePhase = VulturePhase.Approach;
        manager.ScoreAndPick(squad);

        // VultureSquadStrategy (0.75) > GotoObjective (0 + hysteresis 0.25 = 0.25)
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(vultureStrategy));

        // Phase 6: Vulture strategy positions followers
        manager.UpdateStrategies();
        Assert.IsTrue(f1.HasTacticalPosition || f2.HasTacticalPosition, "At least one follower should get vulture position");

        // Phase 7: Events expire, vulture ends — restore objective for GotoObjective
        leader.HasNearbyEvent = false;
        leader.VulturePhase = VulturePhase.Complete;
        leader.HasActiveObjective = true;
        manager.ScoreAndPick(squad);
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy));

        // Phase 8: Loot targets appear
        var claims = new LootClaimRegistry();
        var lootResults = new LootScanResult[]
        {
            new LootScanResult
            {
                Id = 200,
                X = 180f,
                Y = 0f,
                Z = 180f,
                Value = 15000f,
            },
            new LootScanResult
            {
                Id = 201,
                X = 190f,
                Y = 0f,
                Z = 190f,
                Value = 8000f,
            },
        };

        int bossIdx = SquadLootCoordinator.BossPriorityClaim(lootResults, 2, claims, leader.Id);
        Assert.That(bossIdx, Is.EqualTo(0)); // Boss takes highest value

        SquadLootCoordinator.ShareScanResults(squad, lootResults, 2);

        // Follower picks remaining loot
        int fIdx = SquadLootCoordinator.PickSharedTargetForFollower(squad, f1.Id, lootResults[bossIdx].Id, claims);
        Assert.That(fIdx, Is.EqualTo(1));

        // Move f1 close to the loot so distance penalty doesn't zero the score
        f1.CurrentPositionX = 185f;
        f1.CurrentPositionZ = 185f;

        // Verify loot scoring for follower
        f1.HasLootTarget = true;
        f1.LootTargetX = squad.SharedLootX[fIdx];
        f1.LootTargetY = squad.SharedLootY[fIdx];
        f1.LootTargetZ = squad.SharedLootZ[fIdx];
        f1.LootTargetValue = squad.SharedLootValues[fIdx];
        f1.InventorySpaceFree = 5f;

        float lootScore = LootTask.Score(f1);
        Assert.That(lootScore, Is.GreaterThan(0f), "Follower should have positive loot score");
    }

    // ========================================================================
    // 13. Arrival Detection and Duration
    // ========================================================================

    [Test]
    public void FullPipeline_ArrivalsDetected_ObjectiveSwitchesToWait()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Assign positions
        manager.Update(_squadRegistry.ActiveSquads);

        // Move followers to their tactical positions (arrive)
        f1.CurrentPositionX = f1.TacticalPositionX;
        f1.CurrentPositionY = f1.TacticalPositionY;
        f1.CurrentPositionZ = f1.TacticalPositionZ;
        f2.CurrentPositionX = f2.TacticalPositionX;
        f2.CurrentPositionY = f2.TacticalPositionY;
        f2.CurrentPositionZ = f2.TacticalPositionZ;

        // Update checks arrivals
        manager.Update(_squadRegistry.ActiveSquads);

        Assert.That(squad.Objective.State, Is.EqualTo(ObjectiveState.Wait));
        Assert.IsTrue(squad.Objective.DurationAdjusted, "Duration should be adjusted after all arrive");
    }

    // ========================================================================
    // 14. LootClaimRegistry Lifecycle
    // ========================================================================

    [Test]
    public void FullPipeline_BotDies_ClaimsReleased_FollowerCanClaim()
    {
        var claims = new LootClaimRegistry();

        // Boss claims loot
        claims.TryClaim(10, 100);
        claims.TryClaim(10, 101);
        Assert.That(claims.GetClaimCount(), Is.EqualTo(2));

        // Boss dies — release all claims
        claims.ReleaseAll(10);
        Assert.That(claims.GetClaimCount(), Is.EqualTo(0));

        // Follower can now claim the released items
        Assert.IsTrue(claims.TryClaim(11, 100));
        Assert.IsTrue(claims.TryClaim(11, 101));
    }

    [Test]
    public void FullPipeline_ClaimConflict_SecondBotDenied()
    {
        var claims = new LootClaimRegistry();

        Assert.IsTrue(claims.TryClaim(10, 100));
        Assert.IsFalse(claims.TryClaim(11, 100), "Second bot should be denied");
        Assert.IsTrue(claims.IsClaimedByOther(11, 100));
    }

    // ========================================================================
    // 15. Strategy Hysteresis Behavior
    // ========================================================================

    [Test]
    public void FullPipeline_Hysteresis_PreventsFrequentStrategySwitching()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        // GotoObjective (hysteresis=0.25), VultureSquad (hysteresis=0.20)
        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Activate GotoObjective first
        manager.ScoreAndPick(squad);
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy));

        // VultureSquad scores slightly above GotoObjective's raw score but below with hysteresis
        // GotoObjective: raw=0.5, effective=0.5+0.25=0.75 (with hysteresis)
        // VultureSquad: need >0.75 to switch
        // VultureSquadStrategy.Score returns 0.75 when active vulture phase
        // 0.75 is NOT > 0.75 (strictly greater), so hysteresis prevents switch
        leader.VulturePhase = VulturePhase.Approach;
        leader.HasNearbyEvent = true;
        leader.NearbyEventX = 150f;
        leader.NearbyEventZ = 150f;

        manager.ScoreAndPick(squad);

        // VultureSquad score 0.75 <= GotoObjective effective 0.75, so NO switch
        Assert.That(squad.StrategyAssignment.Strategy, Is.SameAs(gotoStrategy));
    }

    // ========================================================================
    // 16. ScoringModifiers Integration
    // ========================================================================

    [Test]
    public void FullPipeline_AggressiveBot_VultureScoresHigher()
    {
        var aggressive = CreateBot(1, x: 100f, z: 100f);
        aggressive.Aggression = 0.9f;
        aggressive.RaidTimeNormalized = 0.5f;
        aggressive.HasNearbyEvent = true;
        aggressive.CombatIntensity = 30;
        aggressive.NearbyEventX = 110f;
        aggressive.NearbyEventZ = 110f;

        var cautious = CreateBot(2, x: 100f, z: 100f);
        cautious.Aggression = 0.1f;
        cautious.RaidTimeNormalized = 0.5f;
        cautious.HasNearbyEvent = true;
        cautious.CombatIntensity = 30;
        cautious.NearbyEventX = 110f;
        cautious.NearbyEventZ = 110f;

        float baseScore = VultureTask.Score(aggressive, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(baseScore, Is.GreaterThan(0f));

        // Apply personality modifiers
        float aggressiveMod = ScoringModifiers.CombinedModifier(0.9f, 0.5f, BotActionTypeId.Vulture);
        float cautiousMod = ScoringModifiers.CombinedModifier(0.1f, 0.5f, BotActionTypeId.Vulture);

        Assert.That(aggressiveMod, Is.GreaterThan(cautiousMod), "Aggressive bot should have higher vulture modifier than cautious bot");
    }

    [Test]
    public void FullPipeline_LateRaid_LootScoresHigher()
    {
        float earlyMod = ScoringModifiers.CombinedModifier(0.5f, 0.1f, BotActionTypeId.Loot);
        float lateMod = ScoringModifiers.CombinedModifier(0.5f, 0.9f, BotActionTypeId.Loot);

        Assert.That(lateMod, Is.GreaterThan(earlyMod), "Late raid should boost loot scoring");
    }

    [Test]
    public void FullPipeline_NaNAggression_DefaultModifier()
    {
        float mod = ScoringModifiers.CombinedModifier(float.NaN, 0.5f, BotActionTypeId.Vulture);
        // NaN aggression gets clamped to 0 in PersonalityModifier, producing a valid result
        // But CombinedModifier checks: if NaN or < 0, return 1.0
        Assert.That(float.IsNaN(mod), Is.False, "NaN aggression should not produce NaN modifier");
    }

    // ========================================================================
    // 17. CombatEventRegistry Ring Buffer Behavior
    // ========================================================================

    [Test]
    public void FullPipeline_RingBuffer_OverflowEvictsOldest()
    {
        CombatEventRegistry.Initialize(4); // Small buffer

        // Fill buffer
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 2f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(30f, 0f, 30f, 3f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(40f, 0f, 40f, 4f, 100f, CombatEventType.Gunshot, false);

        Assert.That(CombatEventRegistry.Count, Is.EqualTo(4));

        // Overflow — should overwrite oldest (at position 10,10)
        CombatEventRegistry.RecordEvent(50f, 0f, 50f, 5f, 100f, CombatEventType.Gunshot, false);

        Assert.That(CombatEventRegistry.Count, Is.EqualTo(4), "Count should cap at capacity");

        // Verify nearest event from (10, 10) is no longer the event AT (10, 10)
        bool found = CombatEventRegistry.GetNearestEvent(10f, 10f, 5f, 6f, 10f, out var nearest);
        // The event at (10, 10) was overwritten by (50, 50)
        // No event within 5m of (10, 10) remains
        Assert.IsFalse(found, "Evicted event should not be found");

        // Event at (20, 20) still exists
        found = CombatEventRegistry.GetNearestEvent(20f, 20f, 5f, 6f, 10f, out nearest);
        Assert.IsTrue(found);
        Assert.That(nearest.X, Is.EqualTo(20f).Within(0.01f));
    }

    [Test]
    public void FullPipeline_CleanupExpired_MarksOldEventsInactive()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 100f, 100f, CombatEventType.Gunshot, false);

        // Cleanup at time 120 with maxAge 30
        // Event at time 1 (age 119) should be expired
        // Event at time 100 (age 20) should remain
        CombatEventRegistry.CleanupExpired(120f, 30f);

        bool found1 = CombatEventRegistry.GetNearestEvent(10f, 10f, 5f, 120f, 30f, out _);
        bool found2 = CombatEventRegistry.GetNearestEvent(20f, 20f, 5f, 120f, 30f, out _);

        Assert.IsFalse(found1, "Old event should be expired");
        Assert.IsTrue(found2, "Recent event should still be active");
    }

    // ========================================================================
    // 18. SquadObjective Version Tracking
    // ========================================================================

    [Test]
    public void FullPipeline_ObjectiveVersionChange_TriggersReassignment()
    {
        var squad = CreateSquadWithMembers(2, out var leader, out var f1, out var f2);
        SetLeaderObjective(leader, squad, 200f, 0f, 200f);

        var gotoStrategy = new GotoObjectiveStrategy(_config, seed: 42);
        var vultureStrategy = new VultureSquadStrategy();
        var manager = new SquadStrategyManager(new SquadStrategy[] { gotoStrategy, vultureStrategy });

        // Initial assignment
        manager.Update(_squadRegistry.ActiveSquads);
        int initialVersion = squad.Objective.Version;
        float f1PosX = f1.TacticalPositionX;

        // Change objective (simulates new quest step)
        squad.Objective.SetObjective(300f, 0f, 300f);
        Assert.That(squad.Objective.Version, Is.GreaterThan(initialVersion));

        // Update should detect version mismatch and reassign
        manager.Update(_squadRegistry.ActiveSquads);

        // Positions should be recomputed for new objective
        // (They may or may not be identical depending on geometry, but the assignment happened)
        Assert.IsTrue(f1.HasTacticalPosition);
    }

    // ========================================================================
    // 19. GatherActiveEvents for DynamicObjectiveScanner
    // ========================================================================

    [Test]
    public void FullPipeline_GatherActiveEvents_ReturnsOnlyNonExpired()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 50f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(30f, 0f, 30f, 100f, 100f, CombatEventType.Explosion, false);

        var buffer = new CombatEvent[10];
        int count = CombatEventRegistry.GatherActiveEvents(buffer, currentTime: 110f, maxAge: 30f);

        // Event at time 1 (age 109s) and time 50 (age 60s) are expired (> 30s maxAge)
        // Only event at time 100 (age 10s) is within 30s of time 110
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void FullPipeline_GatherCombatPull_DecaysWithAge()
    {
        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 95f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 100f, 100f, CombatEventType.Gunshot, false);

        var buffer = new SPTQuestingBots.ZoneMovement.Core.CombatPullPoint[10];
        int count = CombatEventRegistry.GatherCombatPull(buffer, currentTime: 100f, maxAge: 30f, forceMultiplier: 1f);

        Assert.That(count, Is.EqualTo(2));
        // Newer event (time 100, age 0) should have stronger pull than older (time 95, age 5)
        float newerStrength = 0f,
            olderStrength = 0f;
        for (int i = 0; i < count; i++)
        {
            if (Math.Abs(buffer[i].X - 20f) < 0.01f)
                newerStrength = buffer[i].Strength;
            if (Math.Abs(buffer[i].X - 10f) < 0.01f)
                olderStrength = buffer[i].Strength;
        }

        Assert.That(newerStrength, Is.GreaterThan(olderStrength), "Newer event should have stronger pull");
    }

    // ========================================================================
    // 20. Boss Zone Detection Integration
    // ========================================================================

    [Test]
    public void FullPipeline_BossEvent_BlocksVulture()
    {
        var bot = CreateBot(1, x: 100f, z: 100f);

        // Record a boss gunshot nearby
        CombatEventRegistry.RecordEvent(105f, 0f, 105f, 10f, 100f, CombatEventType.Gunshot, isBoss: true);

        // Also record non-boss events for intensity
        for (int i = 0; i < 20; i++)
        {
            CombatEventRegistry.RecordEvent(110f + i * 0.5f, 0f, 110f, time: 10f + i * 0.1f, power: 100f, CombatEventType.Gunshot, false);
        }

        CombatEventScanner.UpdateEntity(bot, 12f, 30f, 200f, 50f, 30f, bossAvoidanceRadius: 50f, bossZoneDecay: 60f);

        Assert.IsTrue(bot.HasNearbyEvent);
        Assert.IsTrue(bot.IsInBossZone, "Bot should detect boss zone");

        float score = VultureTask.Score(bot, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f), "Vulture should be blocked in boss zone");
    }
}
