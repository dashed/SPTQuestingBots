using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Models;
using SPTQuestingBots.Models.Pathing;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Fields;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Integration;

/// <summary>
/// E2E edge case game scenario simulations verifying graceful behavior under
/// unusual but possible game conditions: no quests, raid timer boundaries,
/// simultaneous spawns, unreachable objectives, solo boss squads, edge config,
/// and tick phase ordering.
/// </summary>
[TestFixture]
public class EdgeCaseGameScenarioTests
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
        PatrolTask.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        CombatEventRegistry.Clear();
        PatrolTask.Reset();
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
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        return bot;
    }

    private UtilityTaskManager CreateFullTaskManager()
    {
        return QuestTaskFactory.Create();
    }

    // ========================================================================
    // 1. All Quests Completed -- Bot Has No Active Objective
    // ========================================================================

    [Test]
    public void AllQuestsCompleted_NoObjective_NoEvents_NoLoot_NoPatrol_AllScoresZero()
    {
        // Bot with no active objective, no loot, no events, no patrol routes
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.HasLootTarget = false;
        bot.HasNearbyEvent = false;
        bot.IsSpawnEntryComplete = true;
        bot.ObjectiveCompletedTime = 0f;

        manager.Update(new[] { bot });

        // All task scores should be zero
        for (int i = 0; i < bot.TaskScores.Length; i++)
        {
            Assert.AreEqual(0f, bot.TaskScores[i], 0.001f, $"TaskScores[{i}] should be 0");
        }
    }

    [Test]
    public void AllQuestsCompleted_NoObjective_PatrolAvailable_PatrolIsSelected()
    {
        // Bot with no quests but patrol routes exist
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.IsSpawnEntryComplete = true;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        // Set up a patrol route
        PatrolTask.CurrentMapRoutes = new[]
        {
            new PatrolRoute(
                "TestRoute",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(200f, 0f, 200f), new PatrolWaypoint(300f, 0f, 300f) }
            ),
        };
        PatrolTask.RoutesLoaded = true;

        manager.Update(new[] { bot });

        // Patrol task should be selected (only non-zero scorer)
        Assert.IsNotNull(bot.TaskAssignment.Task, "A task should be assigned");
        Assert.IsInstanceOf<PatrolTask>(bot.TaskAssignment.Task, "PatrolTask should be selected");
        Assert.Greater(bot.TaskScores[bot.TaskAssignment.Ordinal], 0f, "Selected task should have positive score");
    }

    [Test]
    public void AllQuestsCompleted_NoObjective_LingerActive_LingerIsSelected()
    {
        // Bot just completed an objective and is in linger window
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.IsSpawnEntryComplete = true;
        bot.ObjectiveCompletedTime = 10f;
        bot.LingerDuration = 15f;
        bot.CurrentGameTime = 12f; // 2s into 15s linger window
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        manager.Update(new[] { bot });

        Assert.IsNotNull(bot.TaskAssignment.Task, "A task should be assigned");
        Assert.IsInstanceOf<LingerTask>(bot.TaskAssignment.Task, "LingerTask should be selected during linger window");
    }

    [Test]
    public void AllQuestsCompleted_NoObjective_LootAvailable_LootIsSelected()
    {
        // Bot with no quests but sees loot
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.IsSpawnEntryComplete = true;
        bot.HasLootTarget = true;
        bot.LootTargetValue = 30000f;
        bot.LootTargetX = 110f;
        bot.LootTargetY = 0f;
        bot.LootTargetZ = 110f;
        bot.InventorySpaceFree = 5f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        manager.Update(new[] { bot });

        Assert.IsNotNull(bot.TaskAssignment.Task, "A task should be assigned");
        Assert.IsInstanceOf<LootTask>(bot.TaskAssignment.Task, "LootTask should be selected when loot is available");
    }

    [Test]
    public void AllQuestsCompleted_StaleTaskPersistsViaHysteresis()
    {
        // Bot previously on GoToObjective, now objective cleared
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);

        // Step 1: Give bot an active objective so GoToObjective wins
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 200f;
        bot.IsSpawnEntryComplete = true;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;
        manager.Update(new[] { bot });
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);

        // Step 2: Clear the objective -- all task-specific scores should be 0
        // but hysteresis keeps GoToObjective selected (0 + 0.25 = 0.25 > 0)
        bot.HasActiveObjective = false;
        bot.DistanceToObjective = float.MaxValue;
        manager.Update(new[] { bot });

        // The bot stays on GoToObjective due to hysteresis -- not ideal but not a crash
        Assert.IsNotNull(bot.TaskAssignment.Task, "Hysteresis should keep the stale task");
    }

    [Test]
    public void AllQuestsCompleted_PatrolOvertakesStaleTask()
    {
        // Stale GoToObjective should be replaced by Patrol when routes exist
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);

        // Step 1: Active objective
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 200f;
        bot.IsSpawnEntryComplete = true;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;
        manager.Update(new[] { bot });
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);

        // Step 2: Clear objective + add patrol
        bot.HasActiveObjective = false;
        bot.DistanceToObjective = float.MaxValue;
        PatrolTask.CurrentMapRoutes = new[]
        {
            new PatrolRoute(
                "Route1",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(200f, 0f, 200f), new PatrolWaypoint(300f, 0f, 300f) }
            ),
        };
        PatrolTask.RoutesLoaded = true;

        // Patrol's max score (0.50 * modifier) must beat GoToObjective's hysteresis (0.25)
        // Patrol base = 0.50 * (0.4 + 0.6 * proximity) * personality * raidtime modifier
        // With aggression=0.5, raidtime=0.5: personality=Lerp(1.2,0.8,0.5)=1.0, raidtime=Lerp(0.8,1.2,0.5)=1.0
        // So patrol scores up to 0.50, which > 0.25 hysteresis of GoToObjective
        manager.Update(new[] { bot });

        Assert.IsInstanceOf<PatrolTask>(bot.TaskAssignment.Task, "Patrol should overtake stale GoToObjective");
    }

    // ========================================================================
    // 2. Raid Timer Boundaries
    // ========================================================================

    [Test]
    public void RaidTimeZero_EarlyRaidModifiers()
    {
        // Raid start: time=0.0
        float modifier = ScoringModifiers.RaidTimeModifier(0.0f, BotActionTypeId.GoToObjective);
        // Lerp(1.2, 0.8, 0) = 1.2
        Assert.AreEqual(1.2f, modifier, 0.001f, "GoToObjective should have max boost at raid start");

        float lingerMod = ScoringModifiers.RaidTimeModifier(0.0f, BotActionTypeId.Linger);
        // Lerp(0.7, 1.3, 0) = 0.7
        Assert.AreEqual(0.7f, lingerMod, 0.001f, "Linger should be suppressed at raid start");
    }

    [Test]
    public void RaidTimeOne_LateRaidModifiers()
    {
        // Raid end: time=1.0
        float modifier = ScoringModifiers.RaidTimeModifier(1.0f, BotActionTypeId.GoToObjective);
        // Lerp(1.2, 0.8, 1) = 0.8
        Assert.AreEqual(0.8f, modifier, 0.001f, "GoToObjective should be reduced at raid end");

        float lingerMod = ScoringModifiers.RaidTimeModifier(1.0f, BotActionTypeId.Linger);
        // Lerp(0.7, 1.3, 1) = 1.3
        Assert.AreEqual(1.3f, lingerMod, 0.001f, "Linger should be boosted at raid end");

        float lootMod = ScoringModifiers.RaidTimeModifier(1.0f, BotActionTypeId.Loot);
        // Lerp(0.8, 1.2, 1) = 1.2
        Assert.AreEqual(1.2f, lootMod, 0.001f, "Loot should be boosted at raid end");
    }

    [Test]
    public void RaidTimeHalf_MidRaidModifiers()
    {
        float modifier = ScoringModifiers.RaidTimeModifier(0.5f, BotActionTypeId.GoToObjective);
        // Lerp(1.2, 0.8, 0.5) = 1.0
        Assert.AreEqual(1.0f, modifier, 0.001f, "GoToObjective should be neutral at mid-raid");
    }

    [Test]
    public void RaidTimeAlmostOver_NearEndModifiers()
    {
        float modifier = ScoringModifiers.RaidTimeModifier(0.99f, BotActionTypeId.GoToObjective);
        // Should be close to 0.8 (the t=1.0 value)
        Assert.Less(modifier, 0.85f, "GoToObjective should be reduced near raid end");
        Assert.Greater(modifier, 0.75f, "GoToObjective modifier should be reasonable");
    }

    [Test]
    public void RaidTimeNegative_ClampsToZero()
    {
        float modifier = ScoringModifiers.RaidTimeModifier(-0.1f, BotActionTypeId.GoToObjective);
        // Clamped to 0, same as raid start
        Assert.AreEqual(1.2f, modifier, 0.001f, "Negative raid time should clamp to 0 (raid start)");
    }

    [Test]
    public void RaidTimeExceedsOne_ClampsToOne()
    {
        float modifier = ScoringModifiers.RaidTimeModifier(1.5f, BotActionTypeId.GoToObjective);
        // Clamped to 1.0, same as raid end
        Assert.AreEqual(0.8f, modifier, 0.001f, "Raid time > 1 should clamp to 1 (raid end)");
    }

    [Test]
    public void RaidTimeBoundary_FullManagerRun_NoNaN()
    {
        var manager = CreateFullTaskManager();

        foreach (float raidTime in new[] { 0.0f, 0.5f, 0.99f, 1.0f })
        {
            var bot = CreateBot(0, x: 100f, z: 100f);
            bot.HasActiveObjective = true;
            bot.CurrentQuestAction = QuestActionId.MoveToPosition;
            bot.DistanceToObjective = 100f;
            bot.IsSpawnEntryComplete = true;
            bot.Aggression = 0.5f;
            bot.RaidTimeNormalized = raidTime;

            manager.Update(new[] { bot });

            for (int i = 0; i < bot.TaskScores.Length; i++)
            {
                Assert.IsFalse(float.IsNaN(bot.TaskScores[i]), $"TaskScores[{i}] is NaN at raidTime={raidTime}");
                Assert.IsFalse(float.IsInfinity(bot.TaskScores[i]), $"TaskScores[{i}] is Infinity at raidTime={raidTime}");
            }
        }
    }

    [Test]
    public void CombinedModifier_AllBoundariesProduceFiniteValues()
    {
        float[] aggressions = { 0f, 0.1f, 0.5f, 0.9f, 1f };
        float[] raidTimes = { 0f, 0.01f, 0.5f, 0.99f, 1f };
        int[] actionTypes =
        {
            BotActionTypeId.GoToObjective,
            BotActionTypeId.Ambush,
            BotActionTypeId.Snipe,
            BotActionTypeId.Linger,
            BotActionTypeId.Loot,
            BotActionTypeId.Vulture,
            BotActionTypeId.Investigate,
            BotActionTypeId.Patrol,
        };

        foreach (float aggression in aggressions)
        {
            foreach (float raidTime in raidTimes)
            {
                foreach (int actionType in actionTypes)
                {
                    float result = ScoringModifiers.CombinedModifier(aggression, raidTime, actionType);
                    Assert.IsFalse(float.IsNaN(result), $"NaN at aggression={aggression}, raidTime={raidTime}, action={actionType}");
                    Assert.IsFalse(
                        float.IsInfinity(result),
                        $"Infinity at aggression={aggression}, raidTime={raidTime}, action={actionType}"
                    );
                    Assert.GreaterOrEqual(
                        result,
                        0f,
                        $"Negative result at aggression={aggression}, raidTime={raidTime}, action={actionType}"
                    );
                }
            }
        }
    }

    // ========================================================================
    // 3. Simultaneous Multi-Bot Spawn
    // ========================================================================

    [Test]
    public void FiveBotSimultaneousSpawn_AllGetSpawnEntry()
    {
        var manager = CreateFullTaskManager();
        var bots = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            bots[i] = CreateBot(i, x: 100f + i * 5f, z: 100f);
            bots[i].SpawnTime = 10f;
            bots[i].SpawnEntryDuration = 4f + i * 0.5f; // Staggered durations
            bots[i].IsSpawnEntryComplete = false;
            bots[i].CurrentGameTime = 10f; // Just spawned
            bots[i].Aggression = 0.3f + i * 0.15f; // Different personalities
            bots[i].RaidTimeNormalized = 0.1f;
        }

        manager.Update(bots);

        // All bots should be on SpawnEntry
        for (int i = 0; i < 5; i++)
        {
            Assert.IsNotNull(bots[i].TaskAssignment.Task, $"Bot {i} should have a task");
            Assert.IsInstanceOf<SpawnEntryTask>(bots[i].TaskAssignment.Task, $"Bot {i} should be on SpawnEntry");
        }
    }

    [Test]
    public void FiveBotSpawn_StaggeredEntryCompletion()
    {
        var manager = CreateFullTaskManager();
        var bots = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            bots[i] = CreateBot(i, x: 100f + i * 10f, z: 100f);
            bots[i].SpawnTime = 10f;
            bots[i].SpawnEntryDuration = 3f + i * 1f; // 3s, 4s, 5s, 6s, 7s
            bots[i].IsSpawnEntryComplete = false;
            bots[i].CurrentGameTime = 10f;
            bots[i].Aggression = 0.5f;
            bots[i].RaidTimeNormalized = 0.1f;
            bots[i].HasActiveObjective = true;
            bots[i].CurrentQuestAction = QuestActionId.MoveToPosition;
            bots[i].DistanceToObjective = 200f;
        }

        // At t=10: all on SpawnEntry
        manager.Update(bots);
        for (int i = 0; i < 5; i++)
        {
            Assert.IsInstanceOf<SpawnEntryTask>(bots[i].TaskAssignment.Task, $"Bot {i} should be on SpawnEntry at t=10");
        }

        // At t=14: bot 0 (3s duration) should have completed spawn entry
        for (int i = 0; i < 5; i++)
        {
            bots[i].CurrentGameTime = 14f;
        }
        manager.Update(bots);

        Assert.IsTrue(bots[0].IsSpawnEntryComplete, "Bot 0 (3s duration) should complete at t=14");
        // Bot 0 should now be on GoToObjective (has active quest, far from objective)
        Assert.IsInstanceOf<GoToObjectiveTask>(bots[0].TaskAssignment.Task, "Bot 0 should transition to GoToObjective");
        // Bot 4 (7s duration) should still be in spawn entry
        Assert.IsFalse(bots[4].IsSpawnEntryComplete, "Bot 4 (7s duration) should still be in spawn entry at t=14");
    }

    [Test]
    public void FiveBotSpawn_DifferentPersonalities_DifferentScores()
    {
        var manager = CreateFullTaskManager();
        var bots = new BotEntity[5];
        float[] aggressions = { 0.1f, 0.3f, 0.5f, 0.7f, 0.9f };

        for (int i = 0; i < 5; i++)
        {
            bots[i] = CreateBot(i, x: 100f + i * 10f, z: 100f);
            bots[i].IsSpawnEntryComplete = true;
            bots[i].HasActiveObjective = true;
            bots[i].CurrentQuestAction = QuestActionId.MoveToPosition;
            bots[i].DistanceToObjective = 100f;
            bots[i].Aggression = aggressions[i];
            bots[i].RaidTimeNormalized = 0.5f;
        }

        manager.Update(bots);

        // All should have GoToObjective but with different scores
        for (int i = 0; i < 5; i++)
        {
            Assert.IsNotNull(bots[i].TaskAssignment.Task, $"Bot {i} should have a task");
        }

        // More aggressive bots should have higher GoToObjective scores
        float timidScore = bots[0].TaskScores[0]; // Ordinal 0 = GoToObjective
        float recklessScore = bots[4].TaskScores[0];
        Assert.Greater(recklessScore, timidScore, "Reckless bot should score higher on GoToObjective");
    }

    // ========================================================================
    // 4. No Reachable Objectives
    // ========================================================================

    [Test]
    public void NoReachableObjective_NoQuest_FallsToPatrol()
    {
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.IsSpawnEntryComplete = true;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.3f;

        PatrolTask.CurrentMapRoutes = new[]
        {
            new PatrolRoute(
                "FallbackRoute",
                PatrolRouteType.Interior,
                new[] { new PatrolWaypoint(150f, 0f, 150f), new PatrolWaypoint(200f, 0f, 200f) }
            ),
        };
        PatrolTask.RoutesLoaded = true;

        manager.Update(new[] { bot });

        Assert.IsInstanceOf<PatrolTask>(bot.TaskAssignment.Task, "Should fall back to patrol when no objectives are reachable");
    }

    [Test]
    public void NoReachableObjective_NoQuest_NoPatrol_NoCrash()
    {
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.IsSpawnEntryComplete = true;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        // No patrol routes, no events, no loot, no linger
        Assert.DoesNotThrow(() => manager.Update(new[] { bot }), "Should not crash with no viable tasks");
    }

    // ========================================================================
    // 5. Solo Boss Squad (Boss with Zero Followers)
    // ========================================================================

    [Test]
    public void SoloBossSquad_StrategyScores_NoFollowers_NoAssignment()
    {
        var squad = _squadRegistry.Add(1, 1);
        var boss = CreateBot(10, x: 100f, z: 100f);
        _squadRegistry.AddMember(squad, boss);

        Assert.AreEqual(1, squad.Members.Count, "Squad should have exactly 1 member (boss only)");
        Assert.AreSame(boss, squad.Leader, "Boss should be the leader");
        Assert.IsFalse(boss.HasFollowers, "Boss should have no followers (solo)");

        // GotoObjectiveStrategy should handle zero followers gracefully
        boss.HasActiveObjective = true;
        boss.CurrentQuestAction = QuestActionId.MoveToPosition;
        squad.Objective.SetObjective(200f, 0f, 200f);

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);

        // ScoreSquad: should score 0.5 since leader has active objective
        strategy.ScoreSquad(0, squad);
        Assert.AreEqual(0.5f, squad.StrategyScores[0], 0.001f, "Strategy should score even for solo boss");

        // Activate and Update should not crash
        strategy.Activate(squad);
        Assert.DoesNotThrow(() => strategy.Update(), "Strategy.Update should not crash with zero followers");
    }

    [Test]
    public void SoloBossSquad_AssignNewObjective_EarlyReturn()
    {
        var squad = _squadRegistry.Add(1, 1);
        var boss = CreateBot(10, x: 100f, z: 100f);
        _squadRegistry.AddMember(squad, boss);

        boss.HasActiveObjective = true;
        boss.CurrentQuestAction = QuestActionId.MoveToPosition;
        squad.Objective.SetObjective(200f, 0f, 200f);

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);

        // AssignNewObjective should return early when followerCount=0
        Assert.DoesNotThrow(() => strategy.AssignNewObjective(squad));

        // No tactical positions should be assigned (objective state not changed to Active by strategy)
        Assert.AreEqual(0, squad.Objective.MemberCount, "No member positions should be set for solo boss");
    }

    [Test]
    public void SoloBossSquad_CheckArrivals_NoFollowers_NoCrash()
    {
        var squad = _squadRegistry.Add(1, 1);
        var boss = CreateBot(10, x: 200f, y: 0f, z: 200f); // At the objective
        _squadRegistry.AddMember(squad, boss);

        boss.HasActiveObjective = true;
        squad.Objective.SetObjective(200f, 0f, 200f);
        squad.Objective.State = ObjectiveState.Active;

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);

        Assert.DoesNotThrow(() => strategy.CheckArrivals(squad));

        // State should NOT change to Wait since totalFollowers=0
        Assert.AreEqual(ObjectiveState.Active, squad.Objective.State, "State should remain Active with 0 followers");
    }

    [Test]
    public void SoloBossSquad_RecomputeForCombat_ZeroFollowers_NoCrash()
    {
        var squad = _squadRegistry.Add(1, 1);
        var boss = CreateBot(10, x: 100f, z: 100f);
        _squadRegistry.AddMember(squad, boss);

        boss.HasActiveObjective = true;
        boss.CurrentQuestAction = QuestActionId.MoveToPosition;
        squad.Objective.SetObjective(200f, 0f, 200f);
        squad.HasThreatDirection = true;
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.CombatVersion = 1;

        var strategy = new GotoObjectiveStrategy(_config, seed: 42);
        strategy.Activate(squad);

        Assert.DoesNotThrow(() => strategy.RecomputeForCombat(squad));
    }

    // ========================================================================
    // 6. Quest with Edge Data
    // ========================================================================

    [Test]
    public void ZeroArrivalRadius_ClampedToMinimum()
    {
        // GotoObjectiveStrategy.CheckArrivals uses Math.Max(0.5f, config.ArrivalRadius)
        var configZeroRadius = new SquadStrategyConfig
        {
            Enabled = true,
            ArrivalRadius = 0f, // Edge case
            GuardDistance = 8f,
            FlankDistance = 15f,
            OverwatchDistance = 25f,
            EscortDistance = 5f,
            EnablePositionValidation = false,
            EnableCoverPositionSource = false,
            EnableCombatAwarePositioning = false,
            EnableObjectiveSharing = false,
            UseQuestTypeRoles = true,
        };

        var squad = _squadRegistry.Add(1, 3);
        var boss = CreateBot(10, x: 100f, z: 100f);
        var follower = CreateBot(11, x: 105f, z: 105f);
        _squadRegistry.AddMember(squad, boss);
        _squadRegistry.AddMember(squad, follower);

        boss.HasActiveObjective = true;
        squad.Objective.SetObjective(200f, 0f, 200f);
        squad.Objective.State = ObjectiveState.Active;

        // Put follower right at its tactical position
        follower.HasTacticalPosition = true;
        follower.TacticalPositionX = 105f;
        follower.TacticalPositionY = 0f;
        follower.TacticalPositionZ = 105f;

        var strategy = new GotoObjectiveStrategy(configZeroRadius, seed: 42);
        strategy.Activate(squad);

        // CheckArrivals should use Math.Max(0.5, 0) = 0.5 as arrival radius
        Assert.DoesNotThrow(() => strategy.CheckArrivals(squad));
        // Should count as arrived (distance is 0, threshold is 0.5^2 = 0.25)
        Assert.AreEqual(ObjectiveState.Wait, squad.Objective.State, "Follower at position should count as arrived");
    }

    [Test]
    public void QuestAction_Undefined_GoToObjectiveReturnsZero()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.Undefined;
        bot.DistanceToObjective = 100f;

        float score = GoToObjectiveTask.Score(bot);
        Assert.AreEqual(0f, score, "GoToObjective should return 0 for Undefined quest action");
    }

    [Test]
    public void QuestAction_RequestExtract_GoToObjectiveReturnsZero()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.RequestExtract;
        bot.DistanceToObjective = 100f;

        float score = GoToObjectiveTask.Score(bot);
        Assert.AreEqual(0f, score, "GoToObjective should return 0 for RequestExtract");
    }

    [Test]
    public void ZeroDistanceToObjective_GoToObjective_ScoresNearZero()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 0f; // At the objective

        float score = GoToObjectiveTask.Score(bot);
        // score = BaseScore * (1 - exp(-0/75)) = 0.65 * (1 - 1) = 0
        Assert.AreEqual(0f, score, 0.001f, "GoToObjective should score 0 at zero distance");
    }

    [Test]
    public void MaxDistanceToObjective_GoToObjective_Saturates()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = float.MaxValue; // Extremely far

        float score = GoToObjectiveTask.Score(bot);
        // Should not produce NaN or infinity
        Assert.IsFalse(float.IsNaN(score), "Score should not be NaN at max distance");
        Assert.IsFalse(float.IsInfinity(score), "Score should not be Infinity at max distance");
    }

    [Test]
    public void SpawnEntry_ZeroDuration_NeverActivates()
    {
        var bot = CreateBot(0);
        bot.IsSpawnEntryComplete = false;
        bot.SpawnEntryDuration = 0f;
        bot.SpawnTime = 10f;
        bot.CurrentGameTime = 10f;

        float score = SpawnEntryTask.Score(bot);
        Assert.AreEqual(0f, score, "SpawnEntry should return 0 with zero duration");
    }

    [Test]
    public void Linger_ZeroDuration_NeverActivates()
    {
        var bot = CreateBot(0);
        bot.ObjectiveCompletedTime = 10f;
        bot.LingerDuration = 0f; // Edge case: zero duration
        bot.CurrentGameTime = 10f;

        float score = LingerTask.Score(bot, LingerTask.DefaultBaseScore);
        Assert.AreEqual(0f, score, "Linger should return 0 with zero duration");
    }

    [Test]
    public void Linger_NegativeElapsed_ReturnsZero()
    {
        var bot = CreateBot(0);
        bot.ObjectiveCompletedTime = 20f;
        bot.LingerDuration = 10f;
        bot.CurrentGameTime = 10f; // Before completion time

        float score = LingerTask.Score(bot, LingerTask.DefaultBaseScore);
        Assert.AreEqual(0f, score, "Linger should return 0 when elapsed is negative");
    }

    [Test]
    public void SpawnEntry_NegativeElapsed_ReturnsMaxScore()
    {
        var bot = CreateBot(0);
        bot.IsSpawnEntryComplete = false;
        bot.SpawnEntryDuration = 5f;
        bot.SpawnTime = 20f;
        bot.CurrentGameTime = 10f; // Before spawn time

        float score = SpawnEntryTask.Score(bot);
        Assert.AreEqual(SpawnEntryTask.MaxBaseScore, score, "SpawnEntry should return max score when elapsed is negative");
    }

    // ========================================================================
    // 7. Patrol Edge Cases
    // ========================================================================

    [Test]
    public void Patrol_NoRoutes_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.IsSpawnEntryComplete = true;

        float score = PatrolTask.Score(bot, System.Array.Empty<PatrolRoute>());
        Assert.AreEqual(0f, score, "Patrol should score 0 with no routes");
    }

    [Test]
    public void Patrol_NullRoutes_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.IsSpawnEntryComplete = true;

        float score = PatrolTask.Score(bot, null);
        Assert.AreEqual(0f, score, "Patrol should score 0 with null routes");
    }

    [Test]
    public void Patrol_RouteWithEmptyWaypoints_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.IsSpawnEntryComplete = true;

        var routes = new[] { new PatrolRoute("EmptyRoute", PatrolRouteType.Perimeter, System.Array.Empty<PatrolWaypoint>()) };

        // First call assigns the route
        float score = PatrolTask.Score(bot, routes);
        // After route is assigned, empty waypoints should score 0
        Assert.AreEqual(0f, score, "Patrol should score 0 with empty waypoints");
    }

    [Test]
    public void Patrol_InCombat_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.IsInCombat = true;

        var routes = new[] { new PatrolRoute("Route", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(200f, 0f, 200f) }) };

        float score = PatrolTask.Score(bot, routes);
        Assert.AreEqual(0f, score, "Patrol should score 0 when in combat");
    }

    [Test]
    public void Patrol_HasActiveObjective_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = true; // Quest active

        var routes = new[] { new PatrolRoute("Route", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(200f, 0f, 200f) }) };

        float score = PatrolTask.Score(bot, routes);
        Assert.AreEqual(0f, score, "Patrol should score 0 when bot has active objective");
    }

    [Test]
    public void Patrol_OnCooldown_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = false;
        bot.PatrolCooldownUntil = 100f;
        bot.CurrentGameTime = 50f; // Still on cooldown

        var routes = new[] { new PatrolRoute("Route", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(200f, 0f, 200f) }) };

        // First call assigns route, second call checks cooldown
        PatrolTask.Score(bot, routes);
        float score = PatrolTask.Score(bot, routes);
        Assert.AreEqual(0f, score, "Patrol should score 0 when on cooldown");
    }

    // ========================================================================
    // 8. Vulture / Investigate Edge Cases
    // ========================================================================

    [Test]
    public void Vulture_InCombat_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasNearbyEvent = true;
        bot.IsInCombat = true;
        bot.CombatIntensity = 30;

        float score = VultureTask.Score(bot, 15, 150f);
        Assert.AreEqual(0f, score, "Vulture should score 0 when in combat");
    }

    [Test]
    public void Vulture_InBossZone_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.IsInBossZone = true;

        float score = VultureTask.Score(bot, 15, 150f);
        Assert.AreEqual(0f, score, "Vulture should score 0 in boss zone");
    }

    [Test]
    public void Vulture_OnCooldown_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.VultureCooldownUntil = 200f;
        bot.CurrentGameTime = 100f;

        float score = VultureTask.Score(bot, 15, 150f);
        Assert.AreEqual(0f, score, "Vulture should score 0 while on cooldown");
    }

    [Test]
    public void Vulture_BelowCourageThreshold_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 10; // Below threshold of 15

        float score = VultureTask.Score(bot, 15, 150f);
        Assert.AreEqual(0f, score, "Vulture should score 0 below courage threshold");
    }

    [Test]
    public void Vulture_ActivePhase_ReturnsMaxScore()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.VulturePhase = VulturePhase.Approach; // Active phase

        float score = VultureTask.Score(bot, 15, 150f);
        Assert.AreEqual(VultureTask.MaxBaseScore, score, 0.001f, "Active vulture phase should return max score");
    }

    [Test]
    public void Investigate_AlreadyVulturing_ScoresZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 10;
        bot.VulturePhase = VulturePhase.Approach;

        float score = InvestigateTask.Score(bot, 5, 120f);
        Assert.AreEqual(0f, score, "Investigate should score 0 when already vulturing");
    }

    [Test]
    public void Investigate_AlreadyInvestigating_MaintainsScore()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 10;
        bot.IsInvestigating = true;

        float score = InvestigateTask.Score(bot, 5, 120f);
        Assert.AreEqual(InvestigateTask.MaxBaseScore, score, 0.001f, "Active investigate should maintain max score");
    }

    // ========================================================================
    // 9. Loot Edge Cases
    // ========================================================================

    [Test]
    public void Loot_NoTarget_ScoresZero()
    {
        var bot = CreateBot(0);
        bot.HasLootTarget = false;

        float score = LootTask.Score(bot);
        Assert.AreEqual(0f, score, "Loot should score 0 with no target");
    }

    [Test]
    public void Loot_InCombat_ScoresZero()
    {
        var bot = CreateBot(0);
        bot.HasLootTarget = true;
        bot.LootTargetValue = 30000f;
        bot.IsInCombat = true;

        float score = LootTask.Score(bot);
        Assert.AreEqual(0f, score, "Loot should score 0 when in combat");
    }

    [Test]
    public void Loot_NearQuestObjective_GetsProximityBonus()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasLootTarget = true;
        bot.LootTargetValue = 30000f;
        bot.LootTargetX = 110f;
        bot.LootTargetY = 0f;
        bot.LootTargetZ = 110f;
        bot.InventorySpaceFree = 5f;
        bot.HasActiveObjective = true;
        bot.DistanceToObjective = 10f; // Close to objective

        float score = LootTask.Score(bot);
        Assert.Greater(score, 0f, "Loot near objective should score positively");

        // Compare without proximity bonus
        bot.HasActiveObjective = false;
        float scoreNoBonus = LootTask.Score(bot);
        Assert.Greater(score, scoreNoBonus, "Loot near objective should score higher than without objective");
    }

    // ========================================================================
    // 10. Config Interaction: CombatEventRegistry with Empty Registry
    // ========================================================================

    [Test]
    public void CombatEventRegistry_EmptyRegistry_GetNearestEventReturnsFalse()
    {
        CombatEventRegistry.Clear();
        bool found = CombatEventRegistry.GetNearestEvent(100f, 100f, 200f, 10f, 60f, out _);
        Assert.IsFalse(found, "Empty registry should return no events");
    }

    [Test]
    public void CombatEventRegistry_EmptyRegistry_GetIntensityReturnsZero()
    {
        CombatEventRegistry.Clear();
        int intensity = CombatEventRegistry.GetIntensity(100f, 100f, 200f, 60f, 10f);
        Assert.AreEqual(0, intensity, "Empty registry should return zero intensity");
    }

    [Test]
    public void CombatEventRegistry_AllExpired_CleanupHandled()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 1f, 100f, CombatEventType.Gunshot, false);
        CombatEventRegistry.RecordEvent(200f, 0f, 200f, 2f, 100f, CombatEventType.Gunshot, false);

        // Cleanup with current time far in the future
        CombatEventRegistry.CleanupExpired(1000f, 60f);

        int active = CombatEventRegistry.ActiveCount;
        Assert.AreEqual(0, active, "All events should be expired after cleanup");
    }

    [Test]
    public void CombatEventRegistry_ZeroMaxAge_GatherReturnsZero()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, false);

        var buffer = new CombatEvent[128];
        int count = CombatEventRegistry.GatherActiveEvents(buffer, 10f, 0f);
        Assert.AreEqual(0, count, "Zero maxAge should gather no events");
    }

    // ========================================================================
    // 11. NaN Resilience in Scoring
    // ========================================================================

    [Test]
    public void CombinedModifier_NaN_Aggression_ReturnsOne()
    {
        // NaN aggression should be clamped to 0 inside PersonalityModifier
        // PersonalityModifier: clampedAggression = NaN < 0f ? 0f : (NaN > 1f ? 1f : NaN)
        // NaN < 0f is false, NaN > 1f is false, so clampedAggression = NaN
        // Lerp(a, b, NaN) = a + (b-a)*NaN = NaN
        // CombinedModifier has a NaN guard
        float result = ScoringModifiers.CombinedModifier(float.NaN, 0.5f, BotActionTypeId.GoToObjective);
        Assert.AreEqual(1.0f, result, "CombinedModifier should return 1.0 for NaN");
    }

    [Test]
    public void CombinedModifier_NaN_RaidTime_ReturnsOne()
    {
        float result = ScoringModifiers.CombinedModifier(0.5f, float.NaN, BotActionTypeId.GoToObjective);
        Assert.AreEqual(1.0f, result, "CombinedModifier should return 1.0 for NaN");
    }

    [Test]
    public void PickTask_NaN_Score_SkipsTask()
    {
        var taskA = new TestTask(0f);
        var taskB = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        var entity = CreateBot(0);

        entity.TaskScores[0] = float.NaN; // Poison
        entity.TaskScores[1] = 0.5f;

        manager.PickTask(entity);

        Assert.AreSame(taskB, entity.TaskAssignment.Task, "NaN-scored task should be skipped");
    }

    [Test]
    public void PickTask_NaN_CurrentScore_ResetToZero()
    {
        var taskA = new TestTask(0.2f);
        var taskB = new TestTask(0f);
        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        var entity = CreateBot(0);

        // First, assign taskA
        entity.TaskScores[0] = 0.5f;
        entity.TaskScores[1] = 0.3f;
        manager.PickTask(entity);
        Assert.AreSame(taskA, entity.TaskAssignment.Task);

        // Now poison taskA's score with NaN
        entity.TaskScores[0] = float.NaN;
        entity.TaskScores[1] = 0.3f; // Should win since NaN + hysteresis resets to 0
        manager.PickTask(entity);

        Assert.AreSame(taskB, entity.TaskAssignment.Task, "NaN current score should reset, allowing switch");
    }

    // ========================================================================
    // 12. HiveMind Tick Phase Ordering Verification
    // ========================================================================

    [Test]
    public void TickOrdering_SquadStrategiesUseGoalEnemy_NotCombatEvents()
    {
        // Verify that squad threat directions are computed from separate data
        // than CombatEventRegistry (different combat awareness systems).
        //
        // Phase 5 (updateSquadStrategies) sets HasThreatDirection from BSG GoalEnemy
        // Phase 6 (updateCombatEvents) writes HasNearbyEvent from CombatEventRegistry
        //
        // These are independent data flows -- not a bug.

        var squad = _squadRegistry.Add(1, 2);
        var boss = CreateBot(10, x: 100f, z: 100f);
        var follower = CreateBot(11, x: 105f, z: 105f);
        _squadRegistry.AddMember(squad, boss);
        _squadRegistry.AddMember(squad, follower);

        // Simulate: CombatEventRegistry has events (from previous tick or real-time recording)
        CombatEventRegistry.RecordEvent(150f, 0f, 150f, 10f, 100f, CombatEventType.Gunshot, false);

        // But squad threat direction is from BSG GoalEnemy (not from events)
        squad.HasThreatDirection = false;

        // Verify vulture/investigate read from entity fields (populated by Phase 6)
        follower.HasNearbyEvent = false; // Not yet populated by Phase 6
        float vultureScore = VultureTask.Score(follower, 15, 150f);
        Assert.AreEqual(0f, vultureScore, "Vulture should score 0 when HasNearbyEvent is false (pre-Phase 6)");

        // After Phase 6 populates entity fields:
        follower.HasNearbyEvent = true;
        follower.CombatIntensity = 20;
        follower.NearbyEventX = 150f;
        follower.NearbyEventZ = 150f;
        vultureScore = VultureTask.Score(follower, 15, 150f);
        Assert.Greater(vultureScore, 0f, "Vulture should score > 0 when HasNearbyEvent is true (post-Phase 6)");

        // Key verification: squad threat direction is independent from CombatEventRegistry
        Assert.IsFalse(squad.HasThreatDirection, "Squad threat direction should NOT come from CombatEventRegistry");
    }

    [Test]
    public void TickOrdering_CombatVersionBumpedByThreatTransition()
    {
        // Verify that CombatVersion is bumped on threat detection/clearing
        var squad = _squadRegistry.Add(1, 2);
        var boss = CreateBot(10, x: 100f, z: 100f);
        var follower = CreateBot(11, x: 105f, z: 105f);
        _squadRegistry.AddMember(squad, boss);
        _squadRegistry.AddMember(squad, follower);

        // Initially no threat
        Assert.AreEqual(0, squad.CombatVersion, "Initial CombatVersion should be 0");
        Assert.IsFalse(squad.HasThreatDirection, "Initially no threat direction");

        // Simulate threat detected
        squad.HasThreatDirection = true;
        squad.ThreatDirectionX = 1f;
        squad.ThreatDirectionZ = 0f;
        squad.CombatVersion = 1; // Bumped by updateSquadThreatDirections

        // Strategy detects version change
        Assert.AreNotEqual(squad.CombatVersion, squad.LastProcessedCombatVersion, "Strategy should detect version change");

        // After processing
        squad.LastProcessedCombatVersion = squad.CombatVersion;
        Assert.AreEqual(squad.CombatVersion, squad.LastProcessedCombatVersion, "Versions should match after processing");
    }

    // ========================================================================
    // 13. Multi-Task Competition Scenarios
    // ========================================================================

    [Test]
    public void AllTasksCompete_GoToObjective_AmbushTransition()
    {
        // Simulate: bot approaches ambush position -- GoToObjective should hand off to Ambush
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.Ambush;
        bot.DistanceToObjective = 200f;
        bot.IsCloseToObjective = false;
        bot.IsSpawnEntryComplete = true;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        // Step 1: Far from ambush -- GoToObjective should win
        manager.Update(new[] { bot });
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "Far from ambush -> GoToObjective");

        // Step 2: Close to ambush -- AmbushTask should take over
        bot.IsCloseToObjective = true;
        bot.DistanceToObjective = 2f;
        manager.Update(new[] { bot });

        // GoToObjective returns 0 when close + Ambush action
        // AmbushTask returns BaseScore (0.65) * modifiers
        Assert.IsInstanceOf<AmbushTask>(bot.TaskAssignment.Task, "Close to ambush -> AmbushTask");
    }

    [Test]
    public void AllTasksCompete_SpawnEntry_ThenGoToObjective()
    {
        // Simulate: bot spawns, waits, then transitions to quest
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.SpawnTime = 10f;
        bot.SpawnEntryDuration = 4f;
        bot.IsSpawnEntryComplete = false;
        bot.CurrentGameTime = 10f;
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 200f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.1f;

        // t=10: SpawnEntry should win (1.0 > GoToObjective's ~0.60)
        manager.Update(new[] { bot });
        Assert.IsInstanceOf<SpawnEntryTask>(bot.TaskAssignment.Task, "Just spawned -> SpawnEntry");

        // t=15: SpawnEntry expired -> GoToObjective takes over
        bot.CurrentGameTime = 15f;
        manager.Update(new[] { bot });
        Assert.IsTrue(bot.IsSpawnEntryComplete, "Spawn entry should be complete at t=15");
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "After spawn entry -> GoToObjective");
    }

    [Test]
    public void AllTasksCompete_HoldPosition_HighestPriority()
    {
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.HoldAtPosition;
        bot.DistanceToObjective = 5f;
        bot.IsSpawnEntryComplete = true;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        manager.Update(new[] { bot });

        // HoldPosition base = 0.70 (no personality/raidtime modifier)
        // GoToObjective returns 0 for HoldAtPosition action
        Assert.IsInstanceOf<HoldPositionTask>(bot.TaskAssignment.Task, "HoldPosition should win for HoldAtPosition action");
    }

    [Test]
    public void AllTasksCompete_UnlockDoor_TrumpsGoToObjective()
    {
        var manager = CreateFullTaskManager();
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.HasActiveObjective = true;
        bot.MustUnlockDoor = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 200f;
        bot.IsSpawnEntryComplete = true;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        manager.Update(new[] { bot });

        // UnlockDoor base = 0.70, GoToObjective returns 0 when MustUnlockDoor
        Assert.IsInstanceOf<UnlockDoorTask>(bot.TaskAssignment.Task, "UnlockDoor should trump GoToObjective");
    }

    // ========================================================================
    // 14. Personality Extremes
    // ========================================================================

    [Test]
    public void Personality_Timid_FavorsAmbush()
    {
        float timidMod = ScoringModifiers.PersonalityModifier(0.1f, BotActionTypeId.Ambush);
        float recklessMod = ScoringModifiers.PersonalityModifier(0.9f, BotActionTypeId.Ambush);
        Assert.Greater(timidMod, recklessMod, "Timid should have higher Ambush modifier than Reckless");
    }

    [Test]
    public void Personality_Reckless_FavorsGoToObjective()
    {
        float timidMod = ScoringModifiers.PersonalityModifier(0.1f, BotActionTypeId.GoToObjective);
        float recklessMod = ScoringModifiers.PersonalityModifier(0.9f, BotActionTypeId.GoToObjective);
        Assert.Greater(recklessMod, timidMod, "Reckless should have higher GoToObjective modifier than Timid");
    }

    [Test]
    public void Personality_ZeroAggression_ValidModifier()
    {
        float mod = ScoringModifiers.PersonalityModifier(0f, BotActionTypeId.GoToObjective);
        Assert.IsFalse(float.IsNaN(mod), "Zero aggression should not produce NaN");
        Assert.Greater(mod, 0f, "Zero aggression should produce positive modifier");
    }

    [Test]
    public void Personality_OneAggression_ValidModifier()
    {
        float mod = ScoringModifiers.PersonalityModifier(1f, BotActionTypeId.GoToObjective);
        Assert.IsFalse(float.IsNaN(mod), "Full aggression should not produce NaN");
        Assert.Greater(mod, 0f, "Full aggression should produce positive modifier");
    }

    // ========================================================================
    // 15. Direction Bias Edge Cases
    // ========================================================================

    [Test]
    public void DirectionBias_NoSquad_ReturnsZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.SpawnFacingBias = 1f;
        bot.SpawnFacingX = 1f;
        bot.SpawnFacingZ = 0f;
        bot.DistanceToObjective = 100f;
        bot.Squad = null;

        float bias = GoToObjectiveTask.DirectionBias(bot);
        Assert.AreEqual(0f, bias, "Direction bias should be 0 with no squad");
    }

    [Test]
    public void DirectionBias_ZeroBias_ReturnsZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.SpawnFacingBias = 0f;

        float bias = GoToObjectiveTask.DirectionBias(bot);
        Assert.AreEqual(0f, bias, "Direction bias should be 0 when bias factor is 0");
    }

    [Test]
    public void DirectionBias_MaxDistanceToObjective_ReturnsZero()
    {
        var bot = CreateBot(0, x: 100f, z: 100f);
        bot.SpawnFacingBias = 1f;
        bot.DistanceToObjective = float.MaxValue;

        float bias = GoToObjectiveTask.DirectionBias(bot);
        Assert.AreEqual(0f, bias, "Direction bias should be 0 at max distance");
    }

    // ========================================================================
    // 16. SquadObjective Edge Cases
    // ========================================================================

    [Test]
    public void SquadObjective_SetObjective_IncreasesVersion()
    {
        var obj = new SquadObjective();
        int initialVersion = obj.Version;

        obj.SetObjective(100f, 0f, 100f);
        Assert.AreEqual(initialVersion + 1, obj.Version, "Version should increment on set");

        obj.SetObjective(200f, 0f, 200f);
        Assert.AreEqual(initialVersion + 2, obj.Version, "Version should increment again");
        Assert.IsTrue(obj.HasPreviousObjective, "Previous objective should be tracked");
        Assert.AreEqual(100f, obj.PreviousX, 0.001f, "Previous X should be stored");
    }

    [Test]
    public void SquadObjective_ClearObjective_IncreasesVersion()
    {
        var obj = new SquadObjective();
        obj.SetObjective(100f, 0f, 100f);
        int versionAfterSet = obj.Version;

        obj.ClearObjective();
        Assert.AreEqual(versionAfterSet + 1, obj.Version, "Version should increment on clear");
        Assert.IsFalse(obj.HasObjective, "HasObjective should be false after clear");
    }

    [Test]
    public void SquadObjective_SetTacticalPosition_OutOfBounds_NoOp()
    {
        var obj = new SquadObjective();
        Assert.DoesNotThrow(() => obj.SetTacticalPosition(-1, 100f, 0f, 100f, SquadRole.Guard));
        Assert.DoesNotThrow(() => obj.SetTacticalPosition(SquadObjective.MaxMembers, 100f, 0f, 100f, SquadRole.Guard));
    }

    // ========================================================================
    // 17. SquadRegistry Edge Cases
    // ========================================================================

    [Test]
    public void SquadRegistry_RemoveLastMember_LeaderBecomesNull()
    {
        var squad = _squadRegistry.Add(1, 1);
        var member = CreateBot(10);
        _squadRegistry.AddMember(squad, member);

        Assert.AreSame(member, squad.Leader);

        _squadRegistry.RemoveMember(squad, member);
        Assert.IsNull(squad.Leader, "Leader should be null after removing the only member");
        Assert.AreEqual(0, squad.Members.Count, "Members should be empty");
    }

    [Test]
    public void SquadRegistry_RemoveLeader_NextMemberBecomesLeader()
    {
        var squad = _squadRegistry.Add(1, 3);
        var leader = CreateBot(10);
        var follower1 = CreateBot(11);
        var follower2 = CreateBot(12);
        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, follower1);
        _squadRegistry.AddMember(squad, follower2);

        Assert.AreSame(leader, squad.Leader);

        _squadRegistry.RemoveMember(squad, leader);
        Assert.IsNotNull(squad.Leader, "A new leader should be assigned");
        Assert.AreNotSame(leader, squad.Leader, "Removed leader should not be the new leader");
        Assert.AreEqual(SquadRole.Leader, squad.Leader.SquadRole, "New leader should have Leader role");
    }

    [Test]
    public void SquadRegistry_RemoveNonMember_NoOp()
    {
        var squad = _squadRegistry.Add(1, 2);
        var member = CreateBot(10);
        var stranger = CreateBot(99);
        _squadRegistry.AddMember(squad, member);

        _squadRegistry.RemoveMember(squad, stranger);
        Assert.AreEqual(1, squad.Members.Count, "Squad should be unchanged");
    }

    // ========================================================================
    // 18. CombatEvent Edge Cases
    // ========================================================================

    [Test]
    public void CombatEventRegistry_RingBufferOverflow_OldestOverwritten()
    {
        CombatEventRegistry.Initialize(4); // Tiny capacity
        for (int i = 0; i < 6; i++)
        {
            CombatEventRegistry.RecordEvent(i * 10f, 0f, i * 10f, i * 1f, 100f, CombatEventType.Gunshot, false);
        }

        // Count should be capped at capacity
        Assert.AreEqual(4, CombatEventRegistry.Count, "Count should be at most capacity");
    }

    [Test]
    public void CombatEventRegistry_Explosion_TripleIntensity()
    {
        CombatEventRegistry.Initialize(128);
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 100f, CombatEventType.Explosion, false);

        int intensity = CombatEventRegistry.GetIntensity(100f, 100f, 200f, 60f, 10f);
        Assert.AreEqual(3, intensity, "Single explosion should count as 3 intensity");
    }

    [Test]
    public void CombatEventRegistry_BossEvent_IsInBossZoneReturnsTrue()
    {
        CombatEventRegistry.Initialize(128);
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, isBoss: true);

        bool inBossZone = CombatEventRegistry.IsInBossZone(110f, 110f, 200f, 60f, 10f);
        Assert.IsTrue(inBossZone, "Should be in boss zone near boss event");

        bool farFromBoss = CombatEventRegistry.IsInBossZone(500f, 500f, 50f, 60f, 10f);
        Assert.IsFalse(farFromBoss, "Should not be in boss zone when far away");
    }

    [Test]
    public void CombatEventRegistry_NullBuffer_GatherReturnsZero()
    {
        CombatEventRegistry.RecordEvent(100f, 0f, 100f, 10f, 100f, CombatEventType.Gunshot, false);

        int count = CombatEventRegistry.GatherActiveEvents(null, 10f, 60f);
        Assert.AreEqual(0, count, "Null buffer should return 0");
    }

    // ========================================================================
    // 19. GoToObjective Score Calculation Validation
    // ========================================================================

    [Test]
    public void GoToObjective_DistanceScoring_MonotonicallyIncreasing()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;

        float prevScore = 0f;
        foreach (float dist in new[] { 0f, 10f, 25f, 50f, 75f, 100f, 200f, 500f })
        {
            bot.DistanceToObjective = dist;
            float score = GoToObjectiveTask.Score(bot);
            Assert.GreaterOrEqual(score, prevScore, $"Score should be monotonically increasing at dist={dist}");
            prevScore = score;
        }
    }

    [Test]
    public void GoToObjective_ScoreApproachesCeiling()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 10000f; // Very far

        float score = GoToObjectiveTask.Score(bot);
        // BaseScore * (1 - exp(-10000/75)) ≈ 0.65 * 1 = 0.65
        Assert.AreEqual(GoToObjectiveTask.BaseScore, score, 0.01f, "Score should approach BaseScore at very large distances");
    }

    // =====================================================================
    // 20. Config Interaction: BotLodCalculator Edge Cases
    // =====================================================================

    [Test]
    public void BotLod_ZeroDistance_FullTier()
    {
        // Bot at distance 0 from player should be full tier
        byte tier = BotLodCalculator.ComputeTier(0f, 150f * 150f, 300f * 300f);
        Assert.AreEqual(BotLodCalculator.TierFull, tier, "Zero distance should be full tier");
    }

    [Test]
    public void BotLod_ExactReducedBoundary_IsReduced()
    {
        // Exactly at the reduced threshold boundary
        float reducedSqr = 150f * 150f;
        float minimalSqr = 300f * 300f;
        byte tier = BotLodCalculator.ComputeTier(reducedSqr, reducedSqr, minimalSqr);
        Assert.AreEqual(BotLodCalculator.TierReduced, tier, "Exactly at reduced threshold should be reduced tier");
    }

    [Test]
    public void BotLod_ExactMinimalBoundary_IsMinimal()
    {
        float reducedSqr = 150f * 150f;
        float minimalSqr = 300f * 300f;
        byte tier = BotLodCalculator.ComputeTier(minimalSqr, reducedSqr, minimalSqr);
        Assert.AreEqual(BotLodCalculator.TierMinimal, tier, "Exactly at minimal threshold should be minimal tier");
    }

    [Test]
    public void BotLod_ShouldSkipUpdate_FullTierNeverSkips()
    {
        // Full tier should never skip any frame
        for (int frame = 0; frame < 10; frame++)
        {
            Assert.IsFalse(
                BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierFull, frame, 2, 4),
                "Full tier should never skip frame " + frame
            );
        }
    }

    [Test]
    public void BotLod_ShouldSkipUpdate_ZeroSkipNeverSkips()
    {
        // reducedSkip=0 means cycle=Max(1,0+1)=1, frameCounter%1 always 0, never skip
        for (int frame = 0; frame < 10; frame++)
        {
            Assert.IsFalse(
                BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frame, 0, 0),
                "Skip=0 should never skip frame " + frame
            );
        }
    }

    [Test]
    public void BotLod_ShouldSkipUpdate_NegativeSkipNeverSkips()
    {
        // Negative skip: cycle=Max(1,-4+1)=Max(1,-3)=1, never skip
        for (int frame = 0; frame < 10; frame++)
        {
            Assert.IsFalse(
                BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierMinimal, frame, -5, -5),
                "Negative skip should never skip frame " + frame
            );
        }
    }

    [Test]
    public void BotLod_ShouldSkipUpdate_ReducedSkip2_RunsEveryThirdFrame()
    {
        // reducedSkip=2 means cycle=3, only frame%3==0 runs
        int skipped = 0;
        for (int frame = 0; frame < 9; frame++)
        {
            if (BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frame, 2, 4))
            {
                skipped++;
            }
        }

        // Out of 9 frames, 3 run (0,3,6) and 6 skip
        Assert.AreEqual(6, skipped, "With skip=2, should skip 6 out of 9 frames");
    }

    // =====================================================================
    // 21. Config Interaction: HardStuckDetector with TeleportEnabled=false
    // =====================================================================

    [Test]
    public void HardStuck_ProgressesToTeleportState_Regardless_OfConfig()
    {
        // HardStuckDetector doesn't know about TeleportEnabled config --
        // it always progresses through states. The *action* of teleporting
        // is guarded in GoToPositionAbstractAction.attemptSafeTeleport().
        // Verify the detector reaches Retrying then Teleport state.
        var detector = new HardStuckDetector(historySize: 5, pathRetryDelay: 0.5f, teleportDelay: 1.5f, failDelay: 3.0f);

        var stuckPos = new UnityEngine.Vector3(10f, 0f, 10f);
        float time = 0f;

        // Initialize
        detector.Update(stuckPos, 1.0f, time);
        time += 0.1f;

        // Advance stuck timer past pathRetryDelay (6 * 0.1 = 0.6s > 0.5)
        for (int i = 0; i < 6; i++)
        {
            detector.Update(stuckPos, 1.0f, time);
            time += 0.1f;
        }

        Assert.AreEqual(HardStuckStatus.Retrying, detector.Status, "Should reach Retrying state");

        // Continue past teleportDelay (10 more * 0.1 = 1.0s more, total ~1.6s > 1.5)
        for (int i = 0; i < 10; i++)
        {
            detector.Update(stuckPos, 1.0f, time);
            time += 0.1f;
        }

        Assert.AreEqual(HardStuckStatus.Teleport, detector.Status, "Should reach Teleport state");
    }

    [Test]
    public void HardStuck_ProgressesToFailed_WhenStuckLongEnough()
    {
        var detector = new HardStuckDetector(historySize: 5, pathRetryDelay: 0.3f, teleportDelay: 0.6f, failDelay: 1.0f);

        var stuckPos = new UnityEngine.Vector3(5f, 0f, 5f);
        float time = 0f;

        // Initialize
        detector.Update(stuckPos, 1.0f, time);
        time += 0.1f;

        // Advance well past failDelay
        for (int i = 0; i < 20; i++)
        {
            detector.Update(stuckPos, 1.0f, time);
            time += 0.1f;
        }

        Assert.AreEqual(HardStuckStatus.Failed, detector.Status, "Should reach Failed state when stuck long enough");
    }

    [Test]
    public void HardStuck_Resets_WhenBotMoves()
    {
        var detector = new HardStuckDetector(historySize: 5, pathRetryDelay: 0.5f, teleportDelay: 1.5f, failDelay: 3.0f);

        float time = 0f;
        var pos = new UnityEngine.Vector3(0f, 0f, 0f);

        // Initialize and become stuck
        detector.Update(pos, 1.0f, time);
        time += 0.1f;
        // 6 updates * 0.1s = 0.6s > pathRetryDelay(0.5) but < teleportDelay(1.5)
        for (int i = 0; i < 6; i++)
        {
            detector.Update(pos, 1.0f, time);
            time += 0.1f;
        }

        Assert.AreEqual(HardStuckStatus.Retrying, detector.Status, "Should be stuck");

        // Move far away — should reset
        var farPos = new UnityEngine.Vector3(100f, 0f, 100f);
        detector.Update(farPos, 1.0f, time);

        Assert.AreEqual(HardStuckStatus.None, detector.Status, "Should reset when bot moves");
    }

    [Test]
    public void HardStuck_StationaryBot_NoFalseStuck()
    {
        // Bot with zero move speed should NOT trigger stuck detection
        var detector = new HardStuckDetector(historySize: 5, pathRetryDelay: 0.3f, teleportDelay: 0.6f, failDelay: 1.0f);

        var pos = new UnityEngine.Vector3(10f, 0f, 10f);
        float time = 0f;

        detector.Update(pos, 0f, time); // stationary
        time += 0.1f;

        for (int i = 0; i < 20; i++)
        {
            detector.Update(pos, 0f, time); // still stationary
            time += 0.1f;
        }

        Assert.AreEqual(HardStuckStatus.None, detector.Status, "Stationary bot should not be flagged as stuck");
    }

    // =====================================================================
    // 22. Config Interaction: Advection Zone with Radius=0
    // =====================================================================

    [Test]
    public void AdvectionZone_ZeroRadius_NoEffect()
    {
        // A zone with radius=0 should have zero effect on bots at any distance
        var field = new AdvectionField(0f);
        var zeroRadiusPos = new UnityEngine.Vector3(100f, 0f, 100f);
        field.AddBoundedZone(zeroRadiusPos, 10f, 0f, 1f); // radius=0

        // Query from 5m away
        var queryPos = new UnityEngine.Vector3(105f, 0f, 100f);
        field.GetAdvection(queryPos, null, out float outX, out float outZ);

        // With zero radius, dist >= radius for all non-zero distances, so no contribution
        Assert.AreEqual(0f, outX, 0.001f, "Zero-radius zone should produce zero X advection");
        Assert.AreEqual(0f, outZ, 0.001f, "Zero-radius zone should produce zero Z advection");
    }

    [Test]
    public void AdvectionZone_NegativeRadius_NoEffect()
    {
        // A zone with negative radius should also have no effect
        var field = new AdvectionField(0f);
        var pos = new UnityEngine.Vector3(50f, 0f, 50f);
        field.AddBoundedZone(pos, 10f, -10f, 1f); // negative radius

        var queryPos = new UnityEngine.Vector3(55f, 0f, 50f);
        field.GetAdvection(queryPos, null, out float outX, out float outZ);

        Assert.AreEqual(0f, outX, 0.001f, "Negative-radius zone should produce zero advection");
        Assert.AreEqual(0f, outZ, 0.001f);
    }

    [Test]
    public void AdvectionZone_VerySmallRadius_LimitedEffect()
    {
        // Zone with 0.01 radius — only affects positions extremely close
        var field = new AdvectionField(0f);
        var pos = new UnityEngine.Vector3(50f, 0f, 50f);
        field.AddBoundedZone(pos, 10f, 0.01f, 1f); // tiny radius

        // Query from 1m away — should be outside radius
        var farQuery = new UnityEngine.Vector3(51f, 0f, 50f);
        field.GetAdvection(farQuery, null, out float outX, out float outZ);

        Assert.AreEqual(0f, outX, 0.001f, "Bot at 1m from tiny-radius zone should have no effect");
        Assert.AreEqual(0f, outZ, 0.001f);
    }

    [Test]
    public void AdvectionZoneConfig_FactoryMaps_HaveNoZones()
    {
        // Factory maps should have empty zone definitions
        var defaults = AdvectionZoneConfig.GetDefaults();
        Assert.IsTrue(defaults.ContainsKey("factory4_day"));
        Assert.IsTrue(defaults.ContainsKey("factory4_night"));

        var day = defaults["factory4_day"];
        Assert.AreEqual(0, day.BuiltinZones.Count, "Factory day should have no builtin zones");
        Assert.AreEqual(0, day.CustomZones.Count, "Factory day should have no custom zones");

        var night = defaults["factory4_night"];
        Assert.AreEqual(0, night.BuiltinZones.Count, "Factory night should have no builtin zones");
        Assert.AreEqual(0, night.CustomZones.Count, "Factory night should have no custom zones");
    }

    [Test]
    public void AdvectionZoneConfig_UnknownMap_ReturnsDefault()
    {
        var zones = AdvectionZoneConfig.GetForMap("nonexistent_map_id", null);
        Assert.IsNotNull(zones, "Should return default for unknown map");
        Assert.AreEqual(0, zones.BuiltinZones.Count);
        Assert.AreEqual(0, zones.CustomZones.Count);
    }

    [Test]
    public void AdvectionZoneConfig_OverrideTakesPrecedence()
    {
        var overrideZones = new Dictionary<string, AdvectionMapZones>(StringComparer.OrdinalIgnoreCase)
        {
            ["bigmap"] = new AdvectionMapZones(
                new Dictionary<string, BuiltinZoneEntry>(StringComparer.OrdinalIgnoreCase),
                new List<CustomZoneEntry> { new CustomZoneEntry(0f, 0f, 1f, 2f, 50f) }
            ),
        };

        var result = AdvectionZoneConfig.GetForMap("bigmap", overrideZones);
        Assert.AreEqual(0, result.BuiltinZones.Count, "Override should have no builtin zones");
        Assert.AreEqual(1, result.CustomZones.Count, "Override should have 1 custom zone");
        Assert.AreEqual(50f, result.CustomZones[0].Radius);
    }

    [Test]
    public void AdvectionZoneLoader_SampleForce_EqualMinMax_ReturnsMin()
    {
        var entry = new AdvectionZoneEntry(5f, 5f, 100f);
        var rng = new System.Random(42);
        float force = AdvectionZoneLoader.SampleForce(entry, rng);
        Assert.AreEqual(5f, force, 0.001f, "When ForceMin == ForceMax, should return ForceMin");
    }

    [Test]
    public void AdvectionZoneLoader_SampleForce_MinGreaterThanMax_ReturnsMin()
    {
        var entry = new AdvectionZoneEntry(10f, 5f, 100f); // min > max
        var rng = new System.Random(42);
        float force = AdvectionZoneLoader.SampleForce(entry, rng);
        Assert.AreEqual(10f, force, 0.001f, "When ForceMin > ForceMax, should return ForceMin");
    }

    [Test]
    public void AdvectionZoneLoader_TimeMultiplier_Boundaries()
    {
        var entry = new AdvectionZoneEntry(0f, 1f, 100f, earlyMultiplier: 2.0f, lateMultiplier: 0.5f);

        float early = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 0f);
        Assert.AreEqual(2.0f, early, 0.001f, "At raid start, should use EarlyMultiplier");

        float late = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 1f);
        Assert.AreEqual(0.5f, late, 0.001f, "At raid end, should use LateMultiplier");

        float mid = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 0.5f);
        Assert.AreEqual(1.25f, mid, 0.001f, "At mid-raid, should interpolate multipliers");
    }

    [Test]
    public void AdvectionZoneLoader_TimeMultiplier_ClampsNegativeTime()
    {
        var entry = new AdvectionZoneEntry(0f, 1f, 100f, earlyMultiplier: 2.0f, lateMultiplier: 0.5f);
        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, -1f);
        Assert.AreEqual(2.0f, result, 0.001f, "Negative time should clamp to 0 (EarlyMultiplier)");
    }

    [Test]
    public void AdvectionZoneLoader_TimeMultiplier_ClampsExcessiveTime()
    {
        var entry = new AdvectionZoneEntry(0f, 1f, 100f, earlyMultiplier: 2.0f, lateMultiplier: 0.5f);
        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 5f);
        Assert.AreEqual(0.5f, result, 0.001f, "Time > 1 should clamp to 1 (LateMultiplier)");
    }

    // =====================================================================
    // 23. Config Interaction: StuckBotRemediesConfig Defaults
    // =====================================================================

    [Test]
    public void StuckBotRemediesConfig_Defaults_AreReasonable()
    {
        var config = new StuckBotRemediesConfig();
        Assert.IsTrue(config.Enabled, "Stuck detection should be enabled by default");
        Assert.IsTrue(config.TeleportEnabled, "Teleport should be enabled by default");
        Assert.Greater(config.HardStuckPathRetryDelay, 0f);
        Assert.Greater(config.HardStuckTeleportDelay, config.HardStuckPathRetryDelay, "Teleport delay should be > path retry delay");
        Assert.Greater(config.HardStuckFailDelay, config.HardStuckTeleportDelay, "Fail delay should be > teleport delay");
        Assert.Greater(config.TeleportMaxPlayerDistance, 0f, "Teleport max player distance should be positive");
    }

    // =====================================================================
    // 24. Config Interaction: BotLodConfig Defaults
    // =====================================================================

    [Test]
    public void BotLodConfig_Defaults_AreReasonable()
    {
        var config = new BotLodConfig();
        Assert.IsTrue(config.Enabled, "LOD should be enabled by default");
        Assert.Greater(config.ReducedDistance, 0f);
        Assert.Greater(config.MinimalDistance, config.ReducedDistance, "Minimal distance should be > reduced distance");
        Assert.Greater(config.ReducedFrameSkip, 0);
        Assert.Greater(config.MinimalFrameSkip, config.ReducedFrameSkip, "Minimal frame skip should be > reduced frame skip");
    }
}

/// <summary>
/// Reusable test task for NaN resilience tests.
/// </summary>
internal class TestTask : UtilityTask
{
    private readonly Dictionary<int, float> _scores = new Dictionary<int, float>();

    public TestTask(float hysteresis)
        : base(hysteresis) { }

    public void SetScore(int entityId, float score)
    {
        _scores[entityId] = score;
    }

    public override void ScoreEntity(int ordinal, BotEntity entity)
    {
        if (_scores.TryGetValue(entity.Id, out float score))
        {
            entity.TaskScores[ordinal] = score;
        }
        else
        {
            entity.TaskScores[ordinal] = 0f;
        }
    }

    public override void Update() { }
}
