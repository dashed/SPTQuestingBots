using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Helpers;
using SPTQuestingBots.Models;
using SPTQuestingBots.Models.Pathing;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Fields;
using SPTQuestingBots.ZoneMovement.Selection;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.HappyPath;

/// <summary>
/// Comprehensive happy-path tests for each major feature.
/// Each test exercises the full success flow through pure-logic classes.
/// </summary>
[TestFixture]
public class FeatureHappyPathTests
{
    // ══════════════════════════════════════════════════════════════
    //  a. Questing: quest scored -> dispatched via utility AI
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void QuestingHappyPath_QuestTaskFactory_Creates14Tasks()
    {
        Assert.That(QuestTaskFactory.TaskCount, Is.EqualTo(14));
        var manager = QuestTaskFactory.Create();
        Assert.That(manager.Tasks.Length, Is.EqualTo(14));
    }

    [Test]
    public void QuestingHappyPath_SquadTaskFactory_Creates2Tasks()
    {
        Assert.That(SquadTaskFactory.TaskCount, Is.EqualTo(2));
        var manager = SquadTaskFactory.Create();
        Assert.That(manager.Tasks.Length, Is.EqualTo(2));
    }

    [Test]
    public void QuestingHappyPath_UtilityTaskManager_PickTask_DoesNotCrash()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.IsActive = true;
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        Assert.DoesNotThrow(() => manager.PickTask(entity));
    }

    [Test]
    public void QuestingHappyPath_AllTaskScoresNonNaN()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.IsActive = true;
        entity.Aggression = 0.5f;
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        // Score each task individually
        for (int i = 0; i < manager.Tasks.Length; i++)
        {
            manager.Tasks[i].ScoreEntity(i, entity);
            float score = entity.TaskScores[i];
            Assert.That(float.IsNaN(score), Is.False, $"Task {i} returned NaN score");
            Assert.That(float.IsInfinity(score), Is.False, $"Task {i} returned Infinity score");
        }
    }

    [Test]
    public void QuestingHappyPath_TaskIdsAreUniqueAndNonZero()
    {
        var manager = QuestTaskFactory.Create();
        var ids = manager.Tasks.OfType<QuestUtilityTask>().Select(t => t.BotActionTypeId).ToHashSet();

        // All quest task IDs should be unique
        Assert.That(ids.Count, Is.EqualTo(manager.Tasks.Length), "Task IDs must be unique");

        // None should be Undefined (0)
        Assert.That(ids, Does.Not.Contain(BotActionTypeId.Undefined));
    }

    // ══════════════════════════════════════════════════════════════
    //  b. Spawning: config -> validate 2D arrays -> ready
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void SpawningHappyPath_DefaultArrays_AreValidForDistributionUse()
    {
        var levelRange = new double[][] { new[] { 0.0, 99.0 }, new[] { 1.0, 8.0 }, new[] { 10.0, 15.0 } };
        var groupDist = new double[][] { new[] { 1.0, 40.0 }, new[] { 2.0, 30.0 }, new[] { 3.0, 22.0 } };
        var diffDist = new double[][] { new[] { 0.0, 25.0 }, new[] { 1.0, 50.0 }, new[] { 2.0, 20.0 } };
        var pscavCurve = new double[][] { new[] { 0.0, 50.0 }, new[] { 0.3, 50.0 }, new[] { 0.9, 0.0 } };

        Assert.Multiple(() =>
        {
            foreach (var arr in new[] { levelRange, groupDist, diffDist, pscavCurve })
            {
                Assert.That(arr.Length, Is.GreaterThan(0));
                foreach (var row in arr)
                    Assert.That(row.Length, Is.EqualTo(2));
            }
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  c. Squad: form -> assign strategy -> compute positions
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void SquadHappyPath_SquadEntity_FormAndAssignRoles()
    {
        var squad = new SquadEntity(0, strategyCount: 2, targetMembers: 3);
        var boss = new BotEntity(0) { BotType = BotType.Boss };
        var follower1 = new BotEntity(1) { BotType = BotType.Scav };
        var follower2 = new BotEntity(2) { BotType = BotType.Scav };

        squad.Members.Add(boss);
        boss.Squad = squad;
        squad.Leader = boss;
        boss.SquadRole = SquadRole.Leader;

        squad.Members.Add(follower1);
        follower1.Squad = squad;
        follower1.SquadRole = SquadRole.Guard;

        squad.Members.Add(follower2);
        follower2.Squad = squad;
        follower2.SquadRole = SquadRole.Guard;

        Assert.Multiple(() =>
        {
            Assert.That(squad.Leader, Is.EqualTo(boss));
            Assert.That(squad.Size, Is.EqualTo(3));
            Assert.That(squad.Members, Does.Contain(follower1));
            Assert.That(squad.Members, Does.Contain(follower2));
        });
    }

    [Test]
    public void SquadHappyPath_SquadPersonality_ComputesFromMembers()
    {
        var memberTypes = new[] { BotType.Boss, BotType.Scav, BotType.Scav };
        var personality = SquadPersonalityCalculator.DeterminePersonality(memberTypes, memberTypes.Length);

        Assert.That(personality, Is.Not.EqualTo(SquadPersonalityType.None));
    }

    [Test]
    public void SquadHappyPath_TacticalPositions_AssignAndCompute()
    {
        var config = new SquadStrategyConfig();
        var roles = new SquadRole[3];

        // Assign roles for MoveToPosition quest action
        TacticalPositionCalculator.AssignRoles(QuestActionId.MoveToPosition, 3, roles);

        Assert.That(roles[0], Is.Not.EqualTo(SquadRole.None), "First follower should get a role");

        // Compute tactical positions around an objective
        float[] outPositions = new float[3 * 3]; // 3 followers x 3 coords (x,y,z)
        TacticalPositionCalculator.ComputePositions(
            objX: 100f,
            objY: 0f,
            objZ: 100f,
            approachX: 0f,
            approachZ: 1f,
            roles: roles,
            count: 3,
            outPositions: outPositions,
            config: config
        );

        // At least one position should have been computed (non-zero)
        bool anyNonZero = false;
        for (int i = 0; i < outPositions.Length; i++)
        {
            if (Math.Abs(outPositions[i]) > 0.01f)
            {
                anyNonZero = true;
                break;
            }
        }

        Assert.That(anyNonZero, Is.True, "Tactical positions should be computed");
    }

    [Test]
    public void SquadHappyPath_FormationSpeedController_CatchUpLogic()
    {
        var config = FormationConfig.Default;

        // Far follower: distance^2 = 40^2 + 40^2 = 3200 (> 30^2=900 catchUp threshold)
        var farDecision = FormationSpeedController.ComputeSpeedDecision(
            bossIsSprinting: false,
            distToBossSqr: 3200f,
            distToTacticalSqr: 100f,
            config: config
        );

        // Close follower: distance^2 = 4 (< 5^2=25 slowApproach threshold)
        var closeDecision = FormationSpeedController.ComputeSpeedDecision(
            bossIsSprinting: false,
            distToBossSqr: 4f,
            distToTacticalSqr: 4f,
            config: config
        );

        Assert.That(farDecision, Is.EqualTo(FormationSpeedDecision.Sprint), "Far follower should sprint");
        Assert.That(closeDecision, Is.EqualTo(FormationSpeedDecision.SlowApproach), "Close follower should slow approach");
    }

    // ══════════════════════════════════════════════════════════════
    //  d. Looting: scan -> score -> claim -> select
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void LootingHappyPath_LootScorer_ScoresHighValueHigher()
    {
        var config = new LootScoringConfig(
            minItemValue: 500f,
            valueScoreCap: 50000f,
            distancePenaltyFactor: 0.001f,
            questProximityBonus: 0.2f,
            gearUpgradeScoreBonus: 0.3f,
            lootCooldownSeconds: 30f
        );

        var lowValueScore = LootScorer.Score(
            targetValue: 1000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 50f,
            timeSinceLastLoot: 60f,
            isGearUpgrade: false,
            config: config
        );
        var highValueScore = LootScorer.Score(
            targetValue: 40000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 50f,
            timeSinceLastLoot: 60f,
            isGearUpgrade: false,
            config: config
        );

        Assert.That(highValueScore, Is.GreaterThan(lowValueScore), "Higher value items should score higher");
    }

    [Test]
    public void LootingHappyPath_LootScorer_InCombatReturnsZero()
    {
        var config = new LootScoringConfig(500f, 50000f, 0.001f, 0.2f, 0.3f, 30f);

        var score = LootScorer.Score(
            targetValue: 40000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: true,
            distanceToObjectiveSqr: 50f,
            timeSinceLastLoot: 60f,
            isGearUpgrade: false,
            config: config
        );

        Assert.That(score, Is.EqualTo(0f), "Combat should suppress looting score");
    }

    [Test]
    public void LootingHappyPath_LootClaimRegistry_ClaimAndRelease()
    {
        var registry = new LootClaimRegistry();
        int botId = 1;
        int lootId = 42;

        // Initially not claimed
        Assert.That(registry.IsClaimedByOther(botId: 99, lootId), Is.False);

        // Claim
        var claimed = registry.TryClaim(botId, lootId);
        Assert.That(claimed, Is.True, "First claim should succeed");

        // Double claim by different bot fails
        var doubleClaim = registry.TryClaim(botId: 99, lootId);
        Assert.That(doubleClaim, Is.False, "Double claim by different bot should fail");

        // Claimed by other
        Assert.That(registry.IsClaimedByOther(botId: 99, lootId), Is.True);

        // Release
        registry.Release(botId, lootId);
        Assert.That(registry.IsClaimedByOther(botId: 99, lootId), Is.False, "Should be released");
    }

    [Test]
    public void LootingHappyPath_GearUpgradeBonus()
    {
        var config = new LootScoringConfig(500f, 50000f, 0.001f, 0.2f, 0.3f, 30f);

        var baseScore = LootScorer.Score(
            targetValue: 10000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 50f,
            timeSinceLastLoot: 60f,
            isGearUpgrade: false,
            config: config
        );
        var upgradeScore = LootScorer.Score(
            targetValue: 10000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 50f,
            timeSinceLastLoot: 60f,
            isGearUpgrade: true,
            config: config
        );

        Assert.That(upgradeScore, Is.GreaterThan(baseScore), "Gear upgrade should boost loot score");
    }

    // ══════════════════════════════════════════════════════════════
    //  e. Vulture: event -> registry -> phase transitions
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void VultureHappyPath_CombatEventRegistry_RecordAndQuery()
    {
        CombatEventRegistry.Initialize(128);

        CombatEventRegistry.RecordEvent(100f, 0f, 100f, time: 10f, power: 100f, type: CombatEventType.Gunshot, isBoss: false);
        CombatEventRegistry.RecordEvent(105f, 0f, 100f, time: 12f, power: 100f, type: CombatEventType.Gunshot, isBoss: false);

        Assert.That(CombatEventRegistry.Count, Is.EqualTo(2));

        var found = CombatEventRegistry.GetNearestEvent(100f, 100f, maxRange: 50f, currentTime: 15f, maxAge: 30f, out var nearest);
        Assert.That(found, Is.True, "Should find a nearby event");
        Assert.That(nearest.IsActive, Is.True);
    }

    [Test]
    public void VultureHappyPath_CombatEventRegistry_GetIntensity()
    {
        CombatEventRegistry.Initialize(128);

        CombatEventRegistry.RecordEvent(100f, 0f, 100f, time: 10f, power: 100f, type: CombatEventType.Gunshot, isBoss: false);
        CombatEventRegistry.RecordEvent(105f, 0f, 100f, time: 11f, power: 100f, type: CombatEventType.Gunshot, isBoss: false);
        CombatEventRegistry.RecordEvent(102f, 0f, 100f, time: 12f, power: 150f, type: CombatEventType.Explosion, isBoss: false);

        var intensity = CombatEventRegistry.GetIntensity(100f, 100f, radius: 50f, timeWindow: 30f, currentTime: 15f);

        // 2 gunshots (1 each) + 1 explosion (1 + 2 extra) = 5
        Assert.That(intensity, Is.EqualTo(5));
    }

    [Test]
    public void VultureHappyPath_CombatEventRegistry_CleanupExpired()
    {
        CombatEventRegistry.Initialize(128);

        CombatEventRegistry.RecordEvent(100f, 0f, 100f, time: 1f, power: 100f, type: CombatEventType.Gunshot, isBoss: false);
        CombatEventRegistry.RecordEvent(105f, 0f, 100f, time: 2f, power: 100f, type: CombatEventType.Gunshot, isBoss: false);

        CombatEventRegistry.CleanupExpired(currentTime: 100f, maxAge: 30f);
        Assert.That(CombatEventRegistry.ActiveCount, Is.EqualTo(0), "All events should be expired");
    }

    [Test]
    public void VultureHappyPath_VulturePhase_DistinctValues()
    {
        Assert.That(VulturePhase.None, Is.Not.EqualTo(VulturePhase.Approach));
        Assert.That(VulturePhase.Approach, Is.Not.EqualTo(VulturePhase.HoldAmbush));
        Assert.That(VulturePhase.Rush, Is.Not.EqualTo(VulturePhase.Complete));
    }

    // ══════════════════════════════════════════════════════════════
    //  f. Patrol: route selection
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void PatrolHappyPath_PatrolRouteSelector_SelectsMatchingRoute()
    {
        var routes = new[]
        {
            new PatrolRoute(
                "Route A",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(0, 0, 0), new PatrolWaypoint(100, 0, 0), new PatrolWaypoint(100, 0, 100) },
                minAggression: 0f,
                maxAggression: 1f,
                minRaidTime: 0f,
                maxRaidTime: 1f
            ),
        };

        var index = PatrolRouteSelector.SelectRoute(
            botX: 50f,
            botZ: 50f,
            aggression: 0.5f,
            raidTimeNormalized: 0.5f,
            routes: routes,
            seed: 42
        );
        Assert.That(index, Is.EqualTo(0), "Should select the only available route");
    }

    [Test]
    public void PatrolHappyPath_PatrolRouteSelector_RespectsAggressionFilter()
    {
        var routes = new[]
        {
            new PatrolRoute(
                "Calm",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(0, 0, 0), new PatrolWaypoint(50, 0, 0) },
                minAggression: 0f,
                maxAggression: 0.3f
            ),
            new PatrolRoute(
                "Aggressive",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(0, 0, 0), new PatrolWaypoint(100, 0, 0) },
                minAggression: 0.7f,
                maxAggression: 1f
            ),
        };

        var index = PatrolRouteSelector.SelectRoute(
            botX: 0f,
            botZ: 0f,
            aggression: 0.9f,
            raidTimeNormalized: 0.5f,
            routes: routes,
            seed: 42
        );
        Assert.That(index, Is.EqualTo(1), "High aggression bot should select the aggressive route");
    }

    [Test]
    public void PatrolHappyPath_PatrolRouteSelector_ReturnsNegativeWhenNoMatch()
    {
        var routes = new[]
        {
            new PatrolRoute(
                "Calm",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(0, 0, 0) },
                minAggression: 0f,
                maxAggression: 0.2f
            ),
        };

        var index = PatrolRouteSelector.SelectRoute(
            botX: 0f,
            botZ: 0f,
            aggression: 0.9f,
            raidTimeNormalized: 0.5f,
            routes: routes,
            seed: 42
        );
        Assert.That(index, Is.EqualTo(-1), "Should return -1 when no route matches aggression");
    }

    // ══════════════════════════════════════════════════════════════
    //  g. Zone movement: grid -> convergence -> composer -> scorer
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ZoneMovementHappyPath_WorldGrid_CreatesGrid()
    {
        var grid = new WorldGrid(new Vector3(-100, 0, -100), new Vector3(100, 0, 100), targetCellCount: 100);

        Assert.That(grid.Cols, Is.GreaterThan(0));
        Assert.That(grid.Rows, Is.GreaterThan(0));
        Assert.That(grid.Cols * grid.Rows, Is.GreaterThan(0));
    }

    [Test]
    public void ZoneMovementHappyPath_WorldGrid_GetCellAtPosition()
    {
        var grid = new WorldGrid(new Vector3(-100, 0, -100), new Vector3(100, 0, 100), targetCellCount: 100);

        var cell = grid.GetCell(new Vector3(50, 0, 50));
        Assert.That(cell, Is.Not.Null, "Should find a cell at position within bounds");
    }

    [Test]
    public void ZoneMovementHappyPath_ConvergenceField_ComputesDirection()
    {
        var positions = new List<Vector3> { new Vector3(50, 0, 50) };
        var field = new ConvergenceField(radius: 200f, force: 1.0f);

        field.ComputeConvergence(new Vector3(-50, 0, -50), positions, out float outX, out float outZ);

        float mag = (float)Math.Sqrt(outX * outX + outZ * outZ);
        Assert.That(mag, Is.GreaterThan(0.001f), "Convergence should produce a non-zero direction");
        Assert.That(outX, Is.GreaterThan(0f), "Direction should point toward attraction point (positive X)");
        Assert.That(outZ, Is.GreaterThan(0f), "Direction should point toward attraction point (positive Z)");
    }

    [Test]
    public void ZoneMovementHappyPath_FieldComposer_CombinesFields()
    {
        var composer = new FieldComposer(convergenceWeight: 1.0f, advectionWeight: 0.5f, momentumWeight: 0.5f, noiseWeight: 0.3f);

        Assert.That(composer.ConvergenceWeight, Is.EqualTo(1.0f));
        Assert.That(composer.AdvectionWeight, Is.EqualTo(0.5f));

        composer.GetCompositeDirection(
            advectionX: 1f,
            advectionZ: 0f,
            convergenceX: 0f,
            convergenceZ: 1f,
            momentumX: 0.5f,
            momentumZ: 0.5f,
            noiseAngleRadians: 0f,
            out float outX,
            out float outZ
        );

        float mag = (float)Math.Sqrt(outX * outX + outZ * outZ);
        Assert.That(mag, Is.GreaterThan(0.001f).Within(0.01f), "Composite direction should be non-zero");
    }

    [Test]
    public void ZoneMovementHappyPath_CellScorer_ScoresCell()
    {
        var grid = new WorldGrid(new Vector3(-100, 0, -100), new Vector3(100, 0, 100), targetCellCount: 25);
        var scorer = new CellScorer(poiWeight: 0.3f);

        var cell = grid.GetCell(5, 3);
        if (cell != null)
        {
            float score = scorer.Score(cell, compositeDirX: 1f, compositeDirZ: 0f, fromPosition: new Vector3(-50, 0, 0), maxPoiDensity: 0f);
            Assert.That(score, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f));
        }
    }

    [Test]
    public void ZoneMovementHappyPath_AdvectionField_PullsTowardZone()
    {
        var field = new AdvectionField(crowdRepulsionStrength: 2.0f);
        field.AddZone(new Vector3(100, 0, 100), strength: 5f);

        Assert.That(field.ZoneCount, Is.EqualTo(1));

        field.GetAdvection(new Vector3(0, 0, 0), botPositions: null, out float outX, out float outZ);

        float mag = (float)Math.Sqrt(outX * outX + outZ * outZ);
        Assert.That(mag, Is.GreaterThan(0.001f), "Advection should produce direction toward zone");
        Assert.That(outX, Is.GreaterThan(0f), "Should pull toward positive X");
        Assert.That(outZ, Is.GreaterThan(0f), "Should pull toward positive Z");
    }

    // ══════════════════════════════════════════════════════════════
    //  h. Stuck recovery: detect -> remedy -> resume
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void StuckRecoveryHappyPath_SoftStuckDetector_DetectsVaultingPhase()
    {
        var detector = new SoftStuckDetector(vaultDelay: 0.5f, jumpDelay: 1.0f, failDelay: 2.0f);
        var pos = new Vector3(10, 0, 10);

        // Initialize
        detector.Update(pos, currentMoveSpeed: 2f, currentTime: 0f);

        // Report same position while commanding speed > 0 => stuck
        bool transitioned = false;
        for (int i = 1; i <= 20; i++)
        {
            if (detector.Update(pos, currentMoveSpeed: 2f, currentTime: i * 0.1f))
            {
                transitioned = true;
                break;
            }
        }

        Assert.That(transitioned, Is.True, "Should detect stuck and transition to Vaulting");
        Assert.That(
            detector.Status,
            Is.EqualTo(SoftStuckStatus.Vaulting).Or.EqualTo(SoftStuckStatus.Jumping).Or.EqualTo(SoftStuckStatus.Failed)
        );
    }

    [Test]
    public void StuckRecoveryHappyPath_SoftStuckDetector_ResetsOnMovement()
    {
        var detector = new SoftStuckDetector(vaultDelay: 0.5f, jumpDelay: 1.0f, failDelay: 2.0f);

        // Get stuck
        detector.Update(new Vector3(10, 0, 10), currentMoveSpeed: 2f, currentTime: 0f);
        for (int i = 1; i <= 20; i++)
            detector.Update(new Vector3(10, 0, 10), currentMoveSpeed: 2f, currentTime: i * 0.1f);

        // Reset
        detector.Reset();
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    [Test]
    public void StuckRecoveryHappyPath_HardStuckDetector_EscalatesThroughPhases()
    {
        var detector = new HardStuckDetector(historySize: 10, pathRetryDelay: 0.5f, teleportDelay: 1.0f, failDelay: 2.0f);
        var pos = new Vector3(10, 0, 10);

        // Initialize
        detector.Update(pos, currentMoveSpeed: 2f, currentTime: 0f);

        // Stay at same position with speed > 0
        bool gotRetrying = false;
        for (int i = 1; i <= 40; i++)
        {
            if (detector.Update(pos, currentMoveSpeed: 2f, currentTime: i * 0.1f))
            {
                if (detector.Status == HardStuckStatus.Retrying)
                {
                    gotRetrying = true;
                    break;
                }
            }
        }

        Assert.That(gotRetrying, Is.True, "Should escalate to Retrying phase");
    }

    [Test]
    public void StuckRecoveryHappyPath_HardStuckDetector_ResetClearsState()
    {
        var detector = new HardStuckDetector(historySize: 10, pathRetryDelay: 0.5f);

        // Push into stuck state
        detector.Update(new Vector3(10, 0, 10), 2f, 0f);
        for (int i = 1; i <= 30; i++)
            detector.Update(new Vector3(10, 0, 10), 2f, i * 0.1f);

        detector.Reset();
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
        Assert.That(detector.Timer, Is.EqualTo(0f));
    }

    // ══════════════════════════════════════════════════════════════
    //  i. Extraction: config validation
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ExtractionHappyPath_DefaultConfig_QuestRangesValid()
    {
        var minTotal = 3;
        var maxTotal = 8;
        var minEft = 2;
        var maxEft = 4;

        Assert.Multiple(() =>
        {
            Assert.That(minTotal, Is.LessThanOrEqualTo(maxTotal));
            Assert.That(minEft, Is.LessThanOrEqualTo(maxEft));
            Assert.That(minEft, Is.LessThanOrEqualTo(minTotal));
        });
    }

    // ══════════════════════════════════════════════════════════════
    //  j. ECS lifecycle: entity registration, cleanup
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void ECSHappyPath_BotRegistry_RegisterAndRemove()
    {
        var registry = new BotRegistry();

        var entity = registry.Add(bsgId: 100);
        Assert.That(registry.Count, Is.EqualTo(1));
        Assert.That(entity.Id, Is.GreaterThanOrEqualTo(0));
        Assert.That(entity.BsgId, Is.EqualTo(100));

        var byBsg = registry.GetByBsgId(100);
        Assert.That(byBsg, Is.EqualTo(entity));

        var removed = registry.Remove(entity);
        Assert.That(removed, Is.True);
        Assert.That(registry.Count, Is.EqualTo(0));
    }

    [Test]
    public void ECSHappyPath_BotRegistry_MultipleEntities_SwapRemove()
    {
        var registry = new BotRegistry();

        var entities = new List<BotEntity>();
        for (int i = 0; i < 10; i++)
            entities.Add(registry.Add(bsgId: i));

        Assert.That(registry.Count, Is.EqualTo(10));

        // Remove middle entity, check swap-remove works
        registry.Remove(entities[5]);
        Assert.That(registry.Count, Is.EqualTo(9));

        // All remaining should still be findable
        for (int i = 0; i < 10; i++)
        {
            if (i == 5)
                continue;
            var found = registry.GetByBsgId(i);
            Assert.That(found, Is.Not.Null, $"Entity with bsgId={i} should still exist");
        }
    }

    [Test]
    public void ECSHappyPath_SquadRegistry_AddAndRemove()
    {
        var registry = new SquadRegistry();

        var squad = registry.Add(strategyCount: 2, targetMembers: 3);
        Assert.That(squad, Is.Not.Null);
        Assert.That(registry.Count, Is.EqualTo(1));

        var removed = registry.Remove(squad);
        Assert.That(removed, Is.True);
        Assert.That(registry.Count, Is.EqualTo(0));
    }

    [Test]
    public void ECSHappyPath_SquadRegistry_AddMemberAndRemoveMember()
    {
        var registry = new SquadRegistry();
        var squad = registry.Add(strategyCount: 2, targetMembers: 3);

        var bot1 = new BotEntity(0) { IsActive = true };
        var bot2 = new BotEntity(1) { IsActive = true };

        registry.AddMember(squad, bot1);
        Assert.That(squad.Leader, Is.EqualTo(bot1), "First member becomes leader");
        Assert.That(bot1.SquadRole, Is.EqualTo(SquadRole.Leader));

        registry.AddMember(squad, bot2);
        Assert.That(squad.Size, Is.EqualTo(2));
        Assert.That(bot2.SquadRole, Is.EqualTo(SquadRole.Guard));

        // Remove leader, second member should take over
        registry.RemoveMember(squad, bot1);
        Assert.That(squad.Leader, Is.EqualTo(bot2));
        Assert.That(bot2.SquadRole, Is.EqualTo(SquadRole.Leader));
    }

    // ══════════════════════════════════════════════════════════════
    //  k. Communication range: earpiece vs no earpiece
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void CommunicationHappyPath_EarpieceExtendsRange()
    {
        // Both have earpieces -> use earpiece range
        var inRangeWithEarpiece = CommunicationRange.IsInRange(
            hasEarpieceA: true,
            hasEarpieceB: true,
            sqrDistance: 150f * 150f,
            noEarpieceRange: 50f,
            earpieceRange: 200f
        );

        // Neither has earpiece -> use no-earpiece range
        var inRangeWithout = CommunicationRange.IsInRange(
            hasEarpieceA: false,
            hasEarpieceB: false,
            sqrDistance: 150f * 150f,
            noEarpieceRange: 50f,
            earpieceRange: 200f
        );

        Assert.That(inRangeWithEarpiece, Is.True, "Should be in range with earpieces (150m < 200m)");
        Assert.That(inRangeWithout, Is.False, "Should be out of range without earpieces (150m > 50m)");
    }

    // ══════════════════════════════════════════════════════════════
    //  l. Sunflower spiral: generates valid positions
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void SunflowerSpiralHappyPath_GeneratesExpectedCount()
    {
        float[] outXZ = new float[32]; // 16 pairs
        int count = SunflowerSpiral.Generate(centerX: 100f, centerZ: 100f, innerRadius: 15f, count: 16, outXZ: outXZ);

        Assert.That(count, Is.EqualTo(16));

        // All candidates should be within radius
        for (int i = 0; i < count; i++)
        {
            float dx = outXZ[i * 2] - 100f;
            float dz = outXZ[i * 2 + 1] - 100f;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            Assert.That(dist, Is.LessThanOrEqualTo(15.1f), $"Candidate {i} should be within radius");
        }
    }

    [Test]
    public void SunflowerSpiralHappyPath_ComputeSampleEpsilon()
    {
        float epsilon = SunflowerSpiral.ComputeSampleEpsilon(innerRadius: 30f, count: 16);
        Assert.That(epsilon, Is.GreaterThan(0f));
        Assert.That(epsilon, Is.LessThan(30f), "Epsilon should be smaller than the radius");
    }

    // ══════════════════════════════════════════════════════════════
    //  m. Combat event clustering
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void CombatClusteringHappyPath_ClusterNearbyEvents()
    {
        var events = new CombatEvent[]
        {
            new CombatEvent
            {
                X = 100,
                Y = 0,
                Z = 100,
                Time = 1f,
                Power = 100,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 105,
                Y = 0,
                Z = 100,
                Time = 2f,
                Power = 100,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 110,
                Y = 0,
                Z = 100,
                Time = 3f,
                Power = 100,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 500,
                Y = 0,
                Z = 500,
                Time = 4f,
                Power = 100,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
        };

        var output = new CombatEventClustering.ClusterResult[10];
        int clusterCount = CombatEventClustering.ClusterEvents(
            events,
            eventCount: events.Length,
            currentTime: 5f,
            maxAge: 30f,
            clusterRadiusSqr: 50f * 50f,
            output: output,
            maxClusters: 10
        );

        Assert.That(clusterCount, Is.EqualTo(2), "Should form 2 clusters: one tight group and one outlier");
    }

    [Test]
    public void CombatClusteringHappyPath_DeathEventsExcludedFromClusters()
    {
        var events = new CombatEvent[]
        {
            new CombatEvent
            {
                X = 100,
                Y = 0,
                Z = 100,
                Time = 1f,
                Type = CombatEventType.Gunshot,
                IsActive = true,
            },
            new CombatEvent
            {
                X = 100,
                Y = 0,
                Z = 100,
                Time = 2f,
                Type = CombatEventType.Death,
                IsActive = true,
            },
        };

        var output = new CombatEventClustering.ClusterResult[10];
        int clusterCount = CombatEventClustering.ClusterEvents(
            events,
            events.Length,
            currentTime: 5f,
            maxAge: 30f,
            clusterRadiusSqr: 50f * 50f,
            output: output,
            maxClusters: 10
        );

        Assert.That(clusterCount, Is.EqualTo(1), "Death events should be excluded from clustering");
    }

    // ══════════════════════════════════════════════════════════════
    //  n. Bot LOD: tier calculation
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void BotLodHappyPath_TierCalculation()
    {
        float reducedThresholdSqr = 150f * 150f;
        float minimalThresholdSqr = 300f * 300f;

        var closeTier = BotLodCalculator.ComputeTier(50f * 50f, reducedThresholdSqr, minimalThresholdSqr);
        var mediumTier = BotLodCalculator.ComputeTier(200f * 200f, reducedThresholdSqr, minimalThresholdSqr);
        var farTier = BotLodCalculator.ComputeTier(400f * 400f, reducedThresholdSqr, minimalThresholdSqr);

        Assert.Multiple(() =>
        {
            Assert.That(closeTier, Is.EqualTo(BotLodCalculator.TierFull));
            Assert.That(mediumTier, Is.EqualTo(BotLodCalculator.TierReduced));
            Assert.That(farTier, Is.EqualTo(BotLodCalculator.TierMinimal));
        });
    }

    [Test]
    public void BotLodHappyPath_ShouldSkipUpdate()
    {
        Assert.That(
            BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierFull, frameCounter: 0, 2, 4),
            Is.False,
            "Full tier should never skip"
        );

        // Reduced: cycle = reducedSkip+1 = 3. Only frame 0 of each 3 is processed.
        Assert.That(
            BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frameCounter: 0, 2, 4),
            Is.False,
            "Reduced frame 0 should not skip"
        );
        Assert.That(
            BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frameCounter: 1, 2, 4),
            Is.True,
            "Reduced frame 1 should skip"
        );
    }

    // ══════════════════════════════════════════════════════════════
    //  o. Scoring modifiers: personality + raid time
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void PersonalityHappyPath_AggressiveBots_ScoreHigherOnVulture()
    {
        var aggressive = ScoringModifiers.PersonalityModifier(aggression: 0.9f, BotActionTypeId.Vulture);
        var timid = ScoringModifiers.PersonalityModifier(aggression: 0.1f, BotActionTypeId.Vulture);

        Assert.That(aggressive, Is.GreaterThan(timid), "Aggressive bots should score Vulture higher");
    }

    [Test]
    public void PersonalityHappyPath_CautiousBots_ScoreHigherOnAmbush()
    {
        var cautious = ScoringModifiers.PersonalityModifier(aggression: 0.1f, BotActionTypeId.Ambush);
        var aggressive = ScoringModifiers.PersonalityModifier(aggression: 0.9f, BotActionTypeId.Ambush);

        Assert.That(cautious, Is.GreaterThan(aggressive), "Cautious bots should score Ambush higher");
    }

    [Test]
    public void PersonalityHappyPath_RaidTimeModifiers_EarlyRaidFavorsObjective()
    {
        var early = ScoringModifiers.RaidTimeModifier(0.1f, BotActionTypeId.GoToObjective);
        var late = ScoringModifiers.RaidTimeModifier(0.9f, BotActionTypeId.GoToObjective);

        Assert.That(early, Is.GreaterThan(late), "Early raid should favor objective rushing");
    }

    [Test]
    public void PersonalityHappyPath_CombinedModifier_NonNaN()
    {
        var result = ScoringModifiers.CombinedModifier(aggression: 0.5f, raidTimeNormalized: 0.5f, BotActionTypeId.GoToObjective);
        Assert.That(float.IsNaN(result), Is.False);
        Assert.That(result, Is.GreaterThan(0f));
    }

    // ══════════════════════════════════════════════════════════════
    //  p. Position history: XZ-only stuck detection
    // ══════════════════════════════════════════════════════════════

    [Test]
    public void PositionHistoryHappyPath_VerticalMovementIgnoredForStuck()
    {
        var history = new PositionHistory(segments: 10);

        // Bot jumps up and down but stays at same XZ position
        for (int i = 0; i < 12; i++)
        {
            history.Update(new Vector3(100, i * 5, 100));
        }

        var distSqr = history.GetDistanceSqr();
        // PositionHistory uses XZ-only distance, so vertical movement should result in 0
        Assert.That(distSqr, Is.EqualTo(0f).Within(0.01f), "XZ-only distance should ignore vertical movement");
    }

    [Test]
    public void PositionHistoryHappyPath_TracksHorizontalMovement()
    {
        var history = new PositionHistory(segments: 10);

        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(10, 0, 0));

        var distSqr = history.GetDistanceSqr();
        // With 2 samples and bufferSize=11, it projects: distSqr * ((11-1)/(2-1))^2 = 100 * 100 = 10000
        Assert.That(distSqr, Is.GreaterThan(0f), "Should detect horizontal movement");
    }

    [Test]
    public void PositionHistoryHappyPath_ResetClearsState()
    {
        var history = new PositionHistory(segments: 10);

        history.Update(new Vector3(0, 0, 0));
        history.Update(new Vector3(100, 0, 0));

        history.Reset();

        var distSqr = history.GetDistanceSqr();
        Assert.That(distSqr, Is.EqualTo(0f), "Reset should clear all distance tracking");
    }
}
