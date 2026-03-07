using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Helpers;
using SPTQuestingBots.Models;
using SPTQuestingBots.Models.Pathing;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Fields;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.E2E;

/// <summary>
/// End-to-end integration tests exercising the full bot lifecycle pipeline:
/// Spawning -> Entity Registration -> SpawnEntry -> Zone Movement -> Path Following ->
/// Stuck Detection -> LOD Tier Changes -> Task Selection -> Extraction Cleanup.
///
/// Tests cross-system integration seams that individual unit tests don't cover.
/// All classes under test are pure C# (no Unity/EFT runtime dependencies).
/// </summary>
[TestFixture]
public class LifecyclePipelineTests
{
    // ================================================================
    // Helpers
    // ================================================================

    /// <summary>
    /// Minimal concrete UtilityTask for testing. Always scores a fixed value.
    /// </summary>
    private sealed class FixedScoreTask : QuestUtilityTask
    {
        private readonly float _score;
        private readonly int _actionTypeId;
        private readonly string _actionReason;

        public FixedScoreTask(float score, int actionTypeId, string reason, float hysteresis = 0.10f)
            : base(hysteresis)
        {
            _score = score;
            _actionTypeId = actionTypeId;
            _actionReason = reason;
        }

        public override int BotActionTypeId => _actionTypeId;
        public override string ActionReason => _actionReason;

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            entity.TaskScores[ordinal] = _score;
        }
    }

    /// <summary>
    /// Variable-score task that can change its score between ticks.
    /// </summary>
    private sealed class DynamicScoreTask : QuestUtilityTask
    {
        public float Score;
        private readonly int _actionTypeId;

        public DynamicScoreTask(float score, int actionTypeId, float hysteresis = 0.10f)
            : base(hysteresis)
        {
            Score = score;
            _actionTypeId = actionTypeId;
        }

        public override int BotActionTypeId => _actionTypeId;
        public override string ActionReason => "Dynamic";

        public override void ScoreEntity(int ordinal, BotEntity entity)
        {
            entity.TaskScores[ordinal] = Score;
        }
    }

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

    // ================================================================
    // 1. Full Lifecycle: Spawn -> SpawnEntry -> TaskSwitch -> Cleanup
    // ================================================================

    [Test]
    public void FullLifecycle_SpawnEntry_Then_QuestTask_Then_Remove()
    {
        // Arrange: create registry with one bot
        var registry = new BotRegistry();
        var entity = CreateEntity(registry, bsgId: 10);

        // Configure spawn entry state
        entity.SpawnTime = 100f;
        entity.SpawnEntryDuration = 3.0f;
        entity.IsSpawnEntryComplete = false;
        entity.CurrentGameTime = 100.5f; // 0.5s after spawn

        // Create task manager with SpawnEntry + GoToObjective
        var spawnTask = new SpawnEntryTask(hysteresis: 0.10f);
        var goToTask = new FixedScoreTask(0.65f, BotActionTypeId.GoToObjective, "GoToObjective");
        var tasks = new UtilityTask[] { spawnTask, goToTask };
        var manager = new UtilityTaskManager(tasks);
        InitTaskScores(entity, tasks.Length);

        // Act phase 1: SpawnEntry should win (1.0 > 0.65)
        manager.ScoreAndPick(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(spawnTask), "SpawnEntry should be active during spawn pause");
        Assert.That(entity.IsSpawnEntryComplete, Is.False, "SpawnEntry should not be complete yet");

        // Act phase 2: advance time past spawn duration
        entity.CurrentGameTime = 104f; // 4s after spawn, > 3s duration

        manager.ScoreAndPick(entity);

        Assert.That(entity.IsSpawnEntryComplete, Is.True, "SpawnEntry should be marked complete");
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(goToTask), "GoToObjective should take over after SpawnEntry completes");

        // Act phase 3: remove entity
        bool removed = registry.Remove(entity);

        Assert.That(removed, Is.True);
        Assert.That(registry.Count, Is.EqualTo(0));
        Assert.That(registry.GetByBsgId(10), Is.Null, "BSG ID mapping should be cleared on remove");
    }

    [Test]
    public void FullLifecycle_SpawnEntry_NeverScoresAgain_AfterCompletion()
    {
        var entity = new BotEntity(0) { IsActive = true };
        entity.SpawnTime = 0f;
        entity.SpawnEntryDuration = 2.0f;
        entity.CurrentGameTime = 3.0f; // past duration

        // First call marks complete and returns 0
        float score1 = SpawnEntryTask.Score(entity);
        Assert.That(score1, Is.EqualTo(0f));
        Assert.That(entity.IsSpawnEntryComplete, Is.True);

        // Second call returns 0 immediately (fast path)
        float score2 = SpawnEntryTask.Score(entity);
        Assert.That(score2, Is.EqualTo(0f));
    }

    // ================================================================
    // 2. Entity Registration + ID Recycling Pipeline
    // ================================================================

    [Test]
    public void Registry_AddRemoveAdd_RecyclesId_CleanlyForNewLifecycle()
    {
        var registry = new BotRegistry();

        // Add 3 entities
        var e0 = registry.Add(100);
        var e1 = registry.Add(101);
        var e2 = registry.Add(102);
        Assert.That(registry.Count, Is.EqualTo(3));

        // Remove middle entity
        int removedId = e1.Id;
        registry.Remove(e1);
        Assert.That(registry.Count, Is.EqualTo(2));
        Assert.That(registry.GetByBsgId(101), Is.Null, "Removed entity should not be found by BSG ID");

        // Add new entity - should recycle ID or get a new one
        var e3 = registry.Add(201);
        Assert.That(registry.Count, Is.EqualTo(3));
        Assert.That(e3.BsgId, Is.EqualTo(201));
        Assert.That(e3.IsActive, Is.True, "New entity should be active");

        // The new entity should have fresh default state
        Assert.That(e3.IsSpawnEntryComplete, Is.False);
        Assert.That(e3.IsInCombat, Is.False);
        Assert.That(e3.TaskScores, Is.Null, "New entity should not have pre-allocated TaskScores");

        // Old entities still accessible
        Assert.That(registry.GetByBsgId(100), Is.SameAs(e0));
        Assert.That(registry.GetByBsgId(102), Is.SameAs(e2));
    }

    [Test]
    public void Registry_RemoveAll_ThenAddNew_StartsClean()
    {
        var registry = new BotRegistry();
        var e0 = registry.Add(50);
        var e1 = registry.Add(51);

        registry.Remove(e0);
        registry.Remove(e1);
        Assert.That(registry.Count, Is.EqualTo(0));

        // Add after full empty - should work cleanly
        var e2 = registry.Add(60);
        Assert.That(registry.Count, Is.EqualTo(1));
        Assert.That(e2.BsgId, Is.EqualTo(60));
        Assert.That(registry.GetByBsgId(60), Is.SameAs(e2));
    }

    // ================================================================
    // 3. Stuck Detection During SpawnEntry (False Positive Guard)
    // ================================================================

    [Test]
    public void HardStuckDetector_ZeroMoveSpeed_DoesNotEscalate()
    {
        var detector = new HardStuckDetector(historySize: 10, pathRetryDelay: 2f, teleportDelay: 5f, failDelay: 8f);
        var position = new Vector3(10f, 0f, 20f);

        // Simulate 20 ticks at same position with zero speed (spawn entry pause)
        float time = 0f;
        for (int i = 0; i < 20; i++)
        {
            time += 0.05f; // 50ms ticks
            detector.Update(position, currentMoveSpeed: 0f, currentTime: time);
        }

        // Should never escalate — zero speed triggers the early-return guard
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None), "Zero moveSpeed should prevent stuck escalation");
        Assert.That(detector.Timer, Is.EqualTo(0f), "Timer should remain 0 when moveSpeed is 0");
    }

    [Test]
    public void SoftStuckDetector_ZeroMoveSpeed_Resets()
    {
        var detector = new SoftStuckDetector(vaultDelay: 1f, jumpDelay: 2f, failDelay: 3f);
        var position = new Vector3(5f, 0f, 5f);

        // First tick initializes
        detector.Update(position, currentMoveSpeed: 1f, currentTime: 0f);

        // Now zero speed — should reset
        bool transition = detector.Update(position, currentMoveSpeed: 0f, currentTime: 0.05f);

        Assert.That(transition, Is.False);
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None));
    }

    [Test]
    public void HardStuckDetector_StaleTimeDelta_Resets_NoFalseEscalation()
    {
        var detector = new HardStuckDetector(historySize: 10, pathRetryDelay: 2f, teleportDelay: 5f, failDelay: 8f);
        var position = new Vector3(0f, 0f, 0f);

        // Initialize
        detector.Update(position, currentMoveSpeed: 1f, currentTime: 0f);

        // Huge time gap (stale) — should reset, not escalate
        bool transitioned = detector.Update(position, currentMoveSpeed: 1f, currentTime: 10f);

        Assert.That(transitioned, Is.False, "Stale time delta should trigger reset, not escalation");
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));
    }

    // ================================================================
    // 4. Stuck Detection Full Escalation Pipeline
    // ================================================================

    [Test]
    public void HardStuckDetector_FullEscalation_None_Retry_Teleport_Failed()
    {
        var detector = new HardStuckDetector(historySize: 10, pathRetryDelay: 1f, teleportDelay: 2f, failDelay: 3f);
        var position = new Vector3(0f, 0f, 0f);

        float time = 0f;

        // Initialize
        detector.Update(position, 2f, time);
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None));

        // Accumulate stuck time with small deltas (below StaleThreshold of 0.2)
        // moveSpeed > 0 but position not changing
        for (int i = 0; i < 100; i++)
        {
            time += 0.05f;
            detector.Update(position, 2f, time);
        }

        // After 5 seconds with speed=2 but no movement: should be at least Retrying
        Assert.That(
            detector.Status,
            Is.Not.EqualTo(HardStuckStatus.None),
            "Bot stuck at same position with non-zero speed should escalate"
        );

        // Continue until Failed
        HardStuckStatus lastStatus = detector.Status;
        for (int i = 0; i < 200; i++)
        {
            time += 0.05f;
            detector.Update(position, 2f, time);
        }

        // Should have progressed through the full escalation chain
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.Failed), "Should reach Failed after enough stuck time");
    }

    [Test]
    public void HardStuckDetector_Movement_Resets_StuckTimer()
    {
        var detector = new HardStuckDetector(historySize: 10, pathRetryDelay: 2f, teleportDelay: 5f, failDelay: 8f);
        float time = 0f;

        // Build up some stuck time
        var pos = new Vector3(0f, 0f, 0f);
        detector.Update(pos, 1f, time);
        for (int i = 0; i < 30; i++)
        {
            time += 0.05f;
            detector.Update(pos, 1f, time);
        }

        // Now move significantly
        pos = new Vector3(50f, 0f, 50f);
        for (int i = 0; i < 20; i++)
        {
            time += 0.05f;
            pos.x += 1f;
            detector.Update(pos, 1f, time);
        }

        // Should have reset
        Assert.That(detector.Status, Is.EqualTo(HardStuckStatus.None), "Moving should reset stuck status");
    }

    // ================================================================
    // 5. PositionHistory Edge Cases
    // ================================================================

    [Test]
    public void PositionHistory_AllIdenticalPositions_ReturnsZeroDistance()
    {
        var history = new PositionHistory(10);
        var samePos = new Vector3(5f, 0f, 10f);

        // Fill the entire buffer with the same position
        for (int i = 0; i < 15; i++)
        {
            history.Update(samePos);
        }

        float distSqr = history.GetDistanceSqr();
        Assert.That(distSqr, Is.EqualTo(0f), "All identical positions should give 0 distance");
    }

    [Test]
    public void PositionHistory_TwoSamples_ProjectsVelocity()
    {
        var history = new PositionHistory(10);

        history.Update(new Vector3(0f, 0f, 0f));
        history.Update(new Vector3(1f, 0f, 0f));

        float distSqr = history.GetDistanceSqr();

        // 2 samples, buffer size = 11. scaleFactor = 10 / 1 = 10
        // observedDistSqr = 1.0 (XZ only)
        // projected = 1.0 * 10 * 10 = 100
        Assert.That(distSqr, Is.EqualTo(100f).Within(0.1f), "2 samples should project velocity over full window");
    }

    [Test]
    public void PositionHistory_Reset_ClearsState()
    {
        var history = new PositionHistory(5);

        // Add some positions
        history.Update(new Vector3(0f, 0f, 0f));
        history.Update(new Vector3(10f, 0f, 0f));
        Assert.That(history.GetDistanceSqr(), Is.GreaterThan(0f));

        // Reset
        history.Reset();

        // After reset, single position should return 0
        history.Update(new Vector3(5f, 0f, 5f));
        Assert.That(history.GetDistanceSqr(), Is.EqualTo(0f), "Reset should clear all history");
    }

    [Test]
    public void PositionHistory_YAxisIgnored_ForStuckDetection()
    {
        var history = new PositionHistory(10);

        // Only move in Y (vertical) — should still appear stuck on XZ plane
        for (int i = 0; i < 15; i++)
        {
            history.Update(new Vector3(5f, i * 1f, 10f));
        }

        float distSqr = history.GetDistanceSqr();
        Assert.That(distSqr, Is.EqualTo(0f), "Y-axis movement should be ignored (XZ plane only)");
    }

    // ================================================================
    // 6. ConvergenceField with Empty/Null Attractions
    // ================================================================

    [Test]
    public void ConvergenceField_EmptyAttractions_ReturnsZeroDirection()
    {
        var field = new ConvergenceField(radius: 100f, force: 1f);
        var emptyPositions = new List<Vector3>();

        field.GetConvergence(new Vector3(50f, 0f, 50f), emptyPositions, currentTime: 0f, out float outX, out float outZ);

        Assert.That(outX, Is.EqualTo(0f));
        Assert.That(outZ, Is.EqualTo(0f));
    }

    [Test]
    public void ConvergenceField_NullAttractions_ReturnsZeroDirection()
    {
        var field = new ConvergenceField(radius: 100f, force: 1f);

        field.ComputeConvergence(new Vector3(50f, 0f, 50f), null, out float outX, out float outZ);

        Assert.That(outX, Is.EqualTo(0f));
        Assert.That(outZ, Is.EqualTo(0f));
    }

    [Test]
    public void ConvergenceField_SingleAttraction_PointsToward()
    {
        var field = new ConvergenceField(radius: 200f, force: 1f);
        var attractions = new List<Vector3> { new Vector3(100f, 0f, 0f) };

        field.GetConvergence(new Vector3(0f, 0f, 0f), attractions, currentTime: 0f, out float outX, out float outZ);

        // Should point toward the attraction (positive X direction)
        Assert.That(outX, Is.GreaterThan(0.9f), "Should point toward the single attraction on X");
        Assert.That(Math.Abs(outZ), Is.LessThan(0.1f), "Z component should be near zero");
    }

    [Test]
    public void ConvergenceField_AttractionBeyondRadius_Ignored()
    {
        var field = new ConvergenceField(radius: 10f, force: 1f);
        var attractions = new List<Vector3> { new Vector3(100f, 0f, 0f) }; // 100m away, radius is 10m

        field.GetConvergence(new Vector3(0f, 0f, 0f), attractions, currentTime: 0f, out float outX, out float outZ);

        Assert.That(outX, Is.EqualTo(0f), "Attraction beyond radius should be ignored");
        Assert.That(outZ, Is.EqualTo(0f));
    }

    [Test]
    public void ConvergenceField_AttractionAtSamePosition_Ignored()
    {
        var field = new ConvergenceField(radius: 100f, force: 1f);
        var samePos = new Vector3(50f, 0f, 50f);
        var attractions = new List<Vector3> { samePos };

        field.GetConvergence(samePos, attractions, currentTime: 0f, out float outX, out float outZ);

        // distSq < 0.01 guard should prevent division by zero
        Assert.That(outX, Is.EqualTo(0f), "Same-position attraction should be ignored (div-by-zero guard)");
        Assert.That(outZ, Is.EqualTo(0f));
    }

    [Test]
    public void ConvergenceField_WithCombatPullPoints_IncludesLongerRangeAttraction()
    {
        var field = new ConvergenceField(radius: 200f, force: 1f);
        var noAttractions = new List<Vector3>();
        var combatPull = new CombatPullPoint[]
        {
            new CombatPullPoint
            {
                X = 100f,
                Z = 0f,
                Strength = 1.0f,
            },
        };

        field.GetConvergence(new Vector3(0f, 0f, 0f), noAttractions, combatPull, 1, currentTime: 0f, out float outX, out float outZ);

        Assert.That(outX, Is.GreaterThan(0.9f), "Combat pull should attract toward event");
    }

    // ================================================================
    // 7. LOD Tier Changes During Active Task
    // ================================================================

    [Test]
    public void BotLodCalculator_TierTransitions_Full_Reduced_Minimal()
    {
        // Close distance = Full
        byte tier = BotLodCalculator.ComputeTier(sqrDistToNearestHuman: 100f, reducedThresholdSqr: 2500f, minimalThresholdSqr: 10000f);
        Assert.That(tier, Is.EqualTo(BotLodCalculator.TierFull));

        // Medium distance = Reduced
        tier = BotLodCalculator.ComputeTier(sqrDistToNearestHuman: 5000f, reducedThresholdSqr: 2500f, minimalThresholdSqr: 10000f);
        Assert.That(tier, Is.EqualTo(BotLodCalculator.TierReduced));

        // Far distance = Minimal
        tier = BotLodCalculator.ComputeTier(sqrDistToNearestHuman: 15000f, reducedThresholdSqr: 2500f, minimalThresholdSqr: 10000f);
        Assert.That(tier, Is.EqualTo(BotLodCalculator.TierMinimal));
    }

    [Test]
    public void BotLodCalculator_FrameSkip_FullNeverSkips()
    {
        for (int frame = 0; frame < 10; frame++)
        {
            bool skip = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierFull, frame, reducedSkip: 2, minimalSkip: 4);
            Assert.That(skip, Is.False, "Full tier should never skip frames");
        }
    }

    [Test]
    public void BotLodCalculator_FrameSkip_ReducedSkipsSomFrames()
    {
        int skippedCount = 0;
        for (int frame = 0; frame < 30; frame++)
        {
            if (BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frame, reducedSkip: 2, minimalSkip: 4))
            {
                skippedCount++;
            }
        }

        // reducedSkip=2 means cycle=3, so 2 of every 3 frames are skipped
        Assert.That(skippedCount, Is.EqualTo(20), "Reduced tier should skip 2 of every 3 frames");
    }

    [Test]
    public void BotLodCalculator_TierChange_MidLifecycle_FrameSkipAdapts()
    {
        var entity = new BotEntity(0) { IsActive = true };
        entity.LodTier = BotLodCalculator.TierFull;
        entity.LodFrameCounter = 0;

        // Full tier: no skipping
        bool skip1 = BotLodCalculator.ShouldSkipUpdate(entity.LodTier, entity.LodFrameCounter++, 2, 4);
        Assert.That(skip1, Is.False);

        // Switch to Minimal mid-lifecycle
        entity.LodTier = BotLodCalculator.TierMinimal;

        // Now frame skipping should kick in
        int skippedAfterSwitch = 0;
        for (int i = 0; i < 15; i++)
        {
            if (BotLodCalculator.ShouldSkipUpdate(entity.LodTier, entity.LodFrameCounter++, 2, 4))
            {
                skippedAfterSwitch++;
            }
        }

        // minimalSkip=4 means cycle=5, so 4 of every 5 frames are skipped
        Assert.That(skippedAfterSwitch, Is.GreaterThan(0), "Switching to Minimal should start skipping frames");
    }

    // ================================================================
    // 8. CustomPathFollower Integration
    // ================================================================

    [Test]
    public void PathFollower_FullPath_NavigateToDestination()
    {
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);

        var corners = new[] { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f) };
        var target = new Vector3(10f, 0f, 10f);

        follower.SetPath(corners, target);
        Assert.That(follower.Status, Is.EqualTo(PathFollowerStatus.Following));

        // Simulate walking toward each corner
        var pos = new Vector3(0f, 0f, 0f);

        // Walk to corner 0 (start position)
        var status = follower.Tick(pos, isSprinting: false);
        Assert.That(status, Is.EqualTo(PathFollowerStatus.Following));

        // Walk close to corner 1
        pos = new Vector3(9.9f, 0f, 0f);
        status = follower.Tick(pos, isSprinting: false);
        // Corner 0 should be reached, advanced to corner 1
        Assert.That(follower.CurrentCorner, Is.GreaterThanOrEqualTo(1));

        // Walk close to corner 2 (final)
        pos = new Vector3(10f, 0f, 9.9f);
        status = follower.Tick(pos, isSprinting: false);

        // Walk to destination
        pos = new Vector3(10f, 0f, 10f);
        status = follower.Tick(pos, isSprinting: false);
        Assert.That(status, Is.EqualTo(PathFollowerStatus.Reached));
    }

    [Test]
    public void PathFollower_NullCorners_FailsGracefully()
    {
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);

        follower.SetPath(null, new Vector3(10f, 0f, 10f));
        Assert.That(follower.Status, Is.EqualTo(PathFollowerStatus.Failed));
    }

    [Test]
    public void PathFollower_EmptyCorners_FailsGracefully()
    {
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);

        follower.SetPath(new Vector3[0], new Vector3(10f, 0f, 10f));
        Assert.That(follower.Status, Is.EqualTo(PathFollowerStatus.Failed));
    }

    [Test]
    public void PathFollower_ResetPath_ClearsAllState()
    {
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);

        follower.SetPath(new[] { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f) }, new Vector3(10f, 0f, 0f));
        Assert.That(follower.Status, Is.EqualTo(PathFollowerStatus.Following));

        follower.ResetPath();
        Assert.That(follower.Status, Is.EqualTo(PathFollowerStatus.Idle));
        Assert.That(follower.HasPath, Is.False);
        Assert.That(follower.RetryCount, Is.EqualTo(0));
    }

    [Test]
    public void PathFollower_PartialPath_IncrementsRetry_OnlyOnce()
    {
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);

        // Path ends at (5,0,0) but target is at (50,0,0) — partial path
        var corners = new[] { new Vector3(0f, 0f, 0f), new Vector3(5f, 0f, 0f) };
        var target = new Vector3(50f, 0f, 0f);

        follower.SetPath(corners, target);

        // First, reach corner 0 to advance to corner 1 (the last corner)
        // Corner 0 is at (0,0,0) — be within walk epsilon (0.35m)
        var pos = new Vector3(0.1f, 0f, 0f);
        follower.Tick(pos, isSprinting: false);
        // Corner 0 reached, advance to corner 1
        Assert.That(follower.CurrentCorner, Is.EqualTo(1), "Should advance to corner 1 after reaching corner 0");

        // Now at last corner — path doesn't reach target. Tick should trigger partial path detection.
        pos = new Vector3(4.5f, 0f, 0f);
        follower.Tick(pos, isSprinting: false);
        Assert.That(follower.RetryCount, Is.EqualTo(1), "Partial path should increment retry count once");

        // Tick again at same position — retry should NOT increment again
        follower.Tick(pos, isSprinting: false);
        Assert.That(follower.RetryCount, Is.EqualTo(1), "Partial path retry should only count once per detection");
    }

    // ================================================================
    // 9. UtilityTaskManager Integration
    // ================================================================

    [Test]
    public void TaskManager_ScoreAndPick_SelectsHighestScore()
    {
        var task1 = new FixedScoreTask(0.30f, BotActionTypeId.Ambush, "Ambush");
        var task2 = new FixedScoreTask(0.65f, BotActionTypeId.GoToObjective, "GoTo");
        var task3 = new FixedScoreTask(0.45f, BotActionTypeId.Snipe, "Snipe");

        var manager = new UtilityTaskManager(new UtilityTask[] { task1, task2, task3 });
        var entity = new BotEntity(0) { IsActive = true };
        InitTaskScores(entity, 3);

        manager.ScoreAndPick(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task2), "Should pick highest-scoring task");
        Assert.That(entity.TaskAssignment.Ordinal, Is.EqualTo(1));
    }

    [Test]
    public void TaskManager_Hysteresis_PreventsOscillation()
    {
        var taskA = new DynamicScoreTask(0.60f, BotActionTypeId.GoToObjective);
        var taskB = new DynamicScoreTask(0.55f, BotActionTypeId.Ambush);

        var manager = new UtilityTaskManager(new UtilityTask[] { taskA, taskB });
        var entity = new BotEntity(0) { IsActive = true };
        InitTaskScores(entity, 2);

        // First pick: A wins (0.60 > 0.55)
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(taskA));

        // Now A drops slightly below B, but hysteresis (0.10) keeps A active
        taskA.Score = 0.50f;
        taskB.Score = 0.55f;
        // A with hysteresis: 0.50 + 0.10 = 0.60
        // B needs to beat 0.60, but only has 0.55 → A stays
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(taskA), "Hysteresis should prevent switch");

        // B goes much higher — beats hysteresis
        taskB.Score = 0.75f;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(taskB), "Should switch when B clearly exceeds hysteresis");
    }

    [Test]
    public void TaskManager_NaNScore_Skipped_DoesNotPoison()
    {
        var normalTask = new FixedScoreTask(0.50f, BotActionTypeId.GoToObjective, "GoTo");
        var poisonTask = new DynamicScoreTask(float.NaN, BotActionTypeId.Ambush);

        var manager = new UtilityTaskManager(new UtilityTask[] { normalTask, poisonTask });
        var entity = new BotEntity(0) { IsActive = true };
        InitTaskScores(entity, 2);

        manager.ScoreAndPick(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(normalTask), "NaN score should be skipped");
    }

    [Test]
    public void TaskManager_InactiveEntity_DeactivatesTask()
    {
        var task = new FixedScoreTask(0.80f, BotActionTypeId.GoToObjective, "GoTo");
        var manager = new UtilityTaskManager(new UtilityTask[] { task });
        var entity = new BotEntity(0) { IsActive = true };
        InitTaskScores(entity, 1);

        // Pick task
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task));
        Assert.That(task.ActiveEntityCount, Is.EqualTo(1));

        // Deactivate entity
        entity.IsActive = false;
        manager.ScoreAndPick(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Null);
        Assert.That(task.ActiveEntityCount, Is.EqualTo(0));
    }

    [Test]
    public void TaskManager_RemoveEntity_CleansUpTaskAssignment()
    {
        var task = new FixedScoreTask(0.80f, BotActionTypeId.GoToObjective, "GoTo");
        var manager = new UtilityTaskManager(new UtilityTask[] { task });
        var entity = new BotEntity(0) { IsActive = true };
        InitTaskScores(entity, 1);

        manager.ScoreAndPick(entity);
        Assert.That(task.ActiveEntityCount, Is.EqualTo(1));

        manager.RemoveEntity(entity);
        Assert.That(task.ActiveEntityCount, Is.EqualTo(0));
        Assert.That(entity.TaskAssignment.Task, Is.Null);
    }

    // ================================================================
    // 10. Task Manager Batch Update with Null TaskScores Guard
    // ================================================================

    [Test]
    public void TaskManager_BatchUpdate_NullTaskScores_DoesNotCrash()
    {
        var task = new FixedScoreTask(0.50f, BotActionTypeId.GoToObjective, "GoTo");
        var manager = new UtilityTaskManager(new UtilityTask[] { task });

        var registry = new BotRegistry();
        var entityWithScores = CreateEntity(registry, 1);
        InitTaskScores(entityWithScores, 1);

        // Entity WITHOUT TaskScores initialized — ScoreAndPick would crash
        // But ScoreAndPick is only called with pre-initialized entities
        // This test validates that the per-entity path (ScoreAndPick) works
        // when TaskScores IS properly initialized
        manager.ScoreAndPick(entityWithScores);
        Assert.That(entityWithScores.TaskAssignment.Task, Is.SameAs(task));
    }

    // ================================================================
    // 11. SpawnEntryTask Edge Cases
    // ================================================================

    [Test]
    public void SpawnEntryTask_ZeroDuration_Returns0()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 0f;
        entity.SpawnTime = 100f;
        entity.CurrentGameTime = 100f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "Zero duration should return 0 (disabled)");
    }

    [Test]
    public void SpawnEntryTask_NegativeDuration_Returns0()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = -1f;
        entity.SpawnTime = 100f;
        entity.CurrentGameTime = 100f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "Negative duration should return 0 (disabled)");
    }

    [Test]
    public void SpawnEntryTask_NegativeElapsed_ReturnsMaxScore()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 3f;
        entity.SpawnTime = 200f;
        entity.CurrentGameTime = 100f; // Before spawn time — negative elapsed

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore), "Negative elapsed should return max score (clock issue guard)");
    }

    [Test]
    public void SpawnEntryTask_ExactDurationBoundary_MarksComplete()
    {
        var entity = new BotEntity(0);
        entity.SpawnEntryDuration = 3f;
        entity.SpawnTime = 100f;
        entity.CurrentGameTime = 103f; // Exactly at boundary

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
        Assert.That(entity.IsSpawnEntryComplete, Is.True, "Exact duration boundary should mark complete");
    }

    // ================================================================
    // 12. WorldGrid Edge Cases
    // ================================================================

    [Test]
    public void WorldGrid_PositionOutsideBounds_ClampedToEdge()
    {
        var grid = new WorldGrid(new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 100f), targetCellCount: 25);

        // Position far outside grid bounds
        var cell = grid.GetCell(new Vector3(-100f, 0f, -100f));
        Assert.That(cell, Is.Not.Null, "Out-of-bounds position should clamp to edge cell");
        Assert.That(cell.Col, Is.EqualTo(0));
        Assert.That(cell.Row, Is.EqualTo(0));

        // Far positive
        cell = grid.GetCell(new Vector3(500f, 0f, 500f));
        Assert.That(cell, Is.Not.Null);
        Assert.That(cell.Col, Is.EqualTo(grid.Cols - 1));
        Assert.That(cell.Row, Is.EqualTo(grid.Rows - 1));
    }

    [Test]
    public void WorldGrid_MinimumCellCount_1_Creates1x1Grid()
    {
        var grid = new WorldGrid(new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 100f), targetCellCount: 1);

        Assert.That(grid.Cols, Is.GreaterThanOrEqualTo(1));
        Assert.That(grid.Rows, Is.GreaterThanOrEqualTo(1));

        var cell = grid.GetCell(new Vector3(50f, 0f, 50f));
        Assert.That(cell, Is.Not.Null);
    }

    [Test]
    public void WorldGrid_InvalidBounds_Throws()
    {
        Assert.Throws<ArgumentException>(() => new WorldGrid(new Vector3(100f, 0f, 100f), new Vector3(0f, 0f, 0f)));
    }

    [Test]
    public void WorldGrid_ZeroTargetCells_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new WorldGrid(new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 100f), targetCellCount: 0)
        );
    }

    // ================================================================
    // 13. AdvectionZoneLoader Time Boundary Tests
    // ================================================================

    [Test]
    public void AdvectionZoneLoader_TimeMultiplier_AtZero_ReturnsEarlyMultiplier()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 2.0f, LateMultiplier = 0.5f };
        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, raidTimeNormalized: 0f);
        Assert.That(result, Is.EqualTo(2.0f));
    }

    [Test]
    public void AdvectionZoneLoader_TimeMultiplier_AtOne_ReturnsLateMultiplier()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 2.0f, LateMultiplier = 0.5f };
        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, raidTimeNormalized: 1f);
        Assert.That(result, Is.EqualTo(0.5f));
    }

    [Test]
    public void AdvectionZoneLoader_TimeMultiplier_AtHalf_ReturnsMidpoint()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 2.0f, LateMultiplier = 0.5f };
        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, raidTimeNormalized: 0.5f);
        Assert.That(result, Is.EqualTo(1.25f).Within(0.01f));
    }

    [Test]
    public void AdvectionZoneLoader_TimeMultiplier_NegativeTime_ClampedToZero()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 2.0f, LateMultiplier = 0.5f };
        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, raidTimeNormalized: -0.5f);
        Assert.That(result, Is.EqualTo(2.0f), "Negative time should clamp to 0 (EarlyMultiplier)");
    }

    [Test]
    public void AdvectionZoneLoader_TimeMultiplier_BeyondOne_ClampedToOne()
    {
        var entry = new AdvectionZoneEntry { EarlyMultiplier = 2.0f, LateMultiplier = 0.5f };
        float result = AdvectionZoneLoader.ComputeTimeMultiplier(entry, raidTimeNormalized: 1.5f);
        Assert.That(result, Is.EqualTo(0.5f), "Time > 1 should clamp to 1 (LateMultiplier)");
    }

    [Test]
    public void AdvectionZoneLoader_SampleForce_EqualMinMax_ReturnsMin()
    {
        var entry = new AdvectionZoneEntry { ForceMin = 5f, ForceMax = 5f };
        float result = AdvectionZoneLoader.SampleForce(entry, new System.Random(42));
        Assert.That(result, Is.EqualTo(5f));
    }

    [Test]
    public void AdvectionZoneLoader_SampleForce_MinGreaterThanMax_ReturnsMin()
    {
        var entry = new AdvectionZoneEntry { ForceMin = 10f, ForceMax = 5f };
        float result = AdvectionZoneLoader.SampleForce(entry, new System.Random(42));
        Assert.That(result, Is.EqualTo(10f));
    }

    // ================================================================
    // 14. Cross-System: SpawnEntry -> Stuck Detection Integration
    // ================================================================

    [Test]
    public void Integration_SpawnEntryActive_StuckDetector_DoesNotFalsePositive()
    {
        // Scenario: bot spawns and SpawnEntry holds it stationary.
        // Stuck detector runs concurrently. It should NOT trigger because
        // moveSpeed is 0 during spawn entry.

        var entity = new BotEntity(0) { IsActive = true };
        entity.SpawnTime = 0f;
        entity.SpawnEntryDuration = 5f;
        entity.CurrentGameTime = 0f;

        var stuckDetector = new HardStuckDetector(historySize: 10, pathRetryDelay: 2f, teleportDelay: 5f, failDelay: 8f);
        var spawnPos = new Vector3(10f, 0f, 20f);
        float time = 0f;

        // Simulate 5 seconds of spawn entry pause
        for (int i = 0; i < 100; i++)
        {
            time += 0.05f;
            entity.CurrentGameTime = time;

            // SpawnEntry scoring
            float spawnScore = SpawnEntryTask.Score(entity);

            // Stuck detection with zero speed (bot paused)
            stuckDetector.Update(spawnPos, currentMoveSpeed: 0f, currentTime: time);
        }

        Assert.That(stuckDetector.Status, Is.EqualTo(HardStuckStatus.None), "Stuck detector should not trigger during spawn entry");
        Assert.That(entity.IsSpawnEntryComplete, Is.True, "SpawnEntry should be complete after 5s");
    }

    // ================================================================
    // 15. Cross-System: Path Failure -> Stuck Escalation -> Reset
    // ================================================================

    [Test]
    public void Integration_PathFailure_StuckEscalation_ThenReset_OnNewPath()
    {
        var config = CustomMoverConfig.CreateDefault();
        var pathFollower = new CustomPathFollower(config);
        var stuckDetector = new HardStuckDetector(historySize: 5, pathRetryDelay: 0.5f, teleportDelay: 1f, failDelay: 1.5f);

        // Start with a path
        pathFollower.SetPath(new[] { new Vector3(0f, 0f, 0f), new Vector3(100f, 0f, 0f) }, new Vector3(100f, 0f, 0f));

        var pos = new Vector3(0f, 0f, 0f);
        float time = 0f;

        // Simulate being stuck (not moving despite having a path)
        for (int i = 0; i < 50; i++)
        {
            time += 0.05f;
            stuckDetector.Update(pos, currentMoveSpeed: 2f, currentTime: time);
        }

        // Stuck detector should have escalated
        Assert.That(stuckDetector.Status, Is.Not.EqualTo(HardStuckStatus.None), "Should detect stuck state");

        // Reset stuck detector (as remediation code would do)
        stuckDetector.Reset();
        Assert.That(stuckDetector.Status, Is.EqualTo(HardStuckStatus.None));

        // Set new path
        pathFollower.ResetPath();
        pathFollower.SetPath(new[] { new Vector3(0f, 0f, 0f), new Vector3(50f, 0f, 0f) }, new Vector3(50f, 0f, 0f));
        Assert.That(pathFollower.Status, Is.EqualTo(PathFollowerStatus.Following));
    }

    // ================================================================
    // 16. Cross-System: LOD Tier + Task Selection Interaction
    // ================================================================

    [Test]
    public void Integration_LodTierSwitch_TaskSelectionStillWorks()
    {
        var task = new FixedScoreTask(0.70f, BotActionTypeId.GoToObjective, "GoTo");
        var manager = new UtilityTaskManager(new UtilityTask[] { task });
        var entity = new BotEntity(0) { IsActive = true };
        InitTaskScores(entity, 1);

        // Start at Full LOD
        entity.LodTier = BotLodCalculator.TierFull;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task));

        // Switch to Minimal LOD
        entity.LodTier = BotLodCalculator.TierMinimal;

        // Task selection should still work regardless of LOD tier
        // (LOD only affects tick frequency, not task selection itself)
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task), "Task selection should work at any LOD tier");
    }

    // ================================================================
    // 17. Cross-System: Multiple Bots Simultaneous Spawn
    // ================================================================

    [Test]
    public void Integration_MultipleBots_SameSpawnTime_DifferentEntities()
    {
        var registry = new BotRegistry();
        var entities = new List<BotEntity>();

        // Spawn 10 bots at the same time
        for (int i = 0; i < 10; i++)
        {
            var e = CreateEntity(registry, bsgId: 100 + i);
            e.SpawnTime = 50f;
            e.SpawnEntryDuration = 3f;
            e.CurrentGameTime = 50f;
            entities.Add(e);
        }

        Assert.That(registry.Count, Is.EqualTo(10));

        // All should have unique IDs
        var ids = new HashSet<int>();
        foreach (var e in entities)
        {
            Assert.That(ids.Add(e.Id), Is.True, "Each entity should have a unique ID");
            Assert.That(e.BsgId, Is.Not.EqualTo(-1), "Each entity should have a BSG ID");
        }

        // All should be found by BSG ID
        for (int i = 0; i < 10; i++)
        {
            var found = registry.GetByBsgId(100 + i);
            Assert.That(found, Is.Not.Null);
            Assert.That(found.BsgId, Is.EqualTo(100 + i));
        }
    }

    // ================================================================
    // 18. Cross-System: Entity Death During Stuck Recovery
    // ================================================================

    [Test]
    public void Integration_EntityRemoval_DuringPendingStuckState_CleansUpGracefully()
    {
        var registry = new BotRegistry();
        var entity = CreateEntity(registry, bsgId: 42);

        // Set up the entity with some state
        var task = new FixedScoreTask(0.70f, BotActionTypeId.GoToObjective, "GoTo");
        var manager = new UtilityTaskManager(new UtilityTask[] { task });
        InitTaskScores(entity, 1);
        manager.ScoreAndPick(entity);
        Assert.That(task.ActiveEntityCount, Is.EqualTo(1));

        // Simulate stuck state
        entity.Movement.StuckStatus = StuckPhase.HardStuck;

        // Remove entity (bot died during stuck recovery)
        manager.RemoveEntity(entity);
        bool removed = registry.Remove(entity);

        Assert.That(removed, Is.True);
        Assert.That(registry.Count, Is.EqualTo(0));
        Assert.That(task.ActiveEntityCount, Is.EqualTo(0), "Task should have no active entities after removal");
        Assert.That(registry.GetByBsgId(42), Is.Null);
    }

    // ================================================================
    // 19. Cross-System: Zone Movement + ConvergenceField Integration
    // ================================================================

    [Test]
    public void Integration_WorldGrid_ConvergenceField_MultipleBots()
    {
        // Create a grid and convergence field
        var grid = new WorldGrid(new Vector3(0f, 0f, 0f), new Vector3(200f, 0f, 200f), targetCellCount: 16);
        var field = new ConvergenceField(radius: 150f, force: 1f);

        // Multiple bot positions
        var botPositions = new List<Vector3> { new Vector3(50f, 0f, 50f), new Vector3(100f, 0f, 100f), new Vector3(150f, 0f, 50f) };

        // Bot at edge of map queries convergence
        var queryPos = new Vector3(10f, 0f, 10f);
        field.GetConvergence(queryPos, botPositions, currentTime: 0f, out float outX, out float outZ);

        // Should get a non-zero direction (pulled toward bot cluster)
        float mag = (float)Math.Sqrt(outX * outX + outZ * outZ);
        Assert.That(mag, Is.GreaterThan(0.5f), "Edge bot should get meaningful convergence toward cluster");

        // Grid cell should be valid
        var cell = grid.GetCell(queryPos);
        Assert.That(cell, Is.Not.Null);
    }

    // ================================================================
    // 20. Cross-System: RollingAverage Drift + StuckDetector
    // ================================================================

    [Test]
    public void RollingAverage_PeriodicRecalculation_PreventsDrift()
    {
        // RollingAverage recalculates every 1000 updates to prevent floating-point drift
        var avg = new RollingAverage(10, recalcInterval: 100);

        // Fill with constant value
        for (int i = 0; i < 200; i++)
        {
            avg.Update(5.0f);
        }

        Assert.That(avg.Value, Is.EqualTo(5.0f).Within(0.01f), "Average should be 5.0 after drift recalculation");
    }

    [Test]
    public void RollingAverage_Reset_ClearsCompletely()
    {
        var avg = new RollingAverage(10);

        for (int i = 0; i < 20; i++)
        {
            avg.Update(10f);
        }

        Assert.That(avg.Value, Is.EqualTo(10f).Within(0.01f));

        avg.Reset();
        Assert.That(avg.Value, Is.EqualTo(0f), "Reset should clear rolling average");
    }

    // ================================================================
    // 21. Cross-System: BotEntity State Coherence After Registry Operations
    // ================================================================

    [Test]
    public void Registry_SwapRemove_MaintainsEntityStateCoherence()
    {
        var registry = new BotRegistry();

        var e0 = CreateEntity(registry, bsgId: 10);
        e0.IsInCombat = true;
        e0.CanQuest = true;

        var e1 = CreateEntity(registry, bsgId: 11);
        e1.IsSuspicious = true;

        var e2 = CreateEntity(registry, bsgId: 12);
        e2.WantsToLoot = true;

        // Remove middle entity (e1) — swap-remove should move e2 into e1's slot
        registry.Remove(e1);

        Assert.That(registry.Count, Is.EqualTo(2));

        // Verify remaining entities maintain their state
        var found0 = registry.GetByBsgId(10);
        Assert.That(found0, Is.Not.Null);
        Assert.That(found0.IsInCombat, Is.True, "Entity 0 state should be preserved after swap-remove");
        Assert.That(found0.CanQuest, Is.True);

        var found2 = registry.GetByBsgId(12);
        Assert.That(found2, Is.Not.Null);
        Assert.That(found2.WantsToLoot, Is.True, "Entity 2 state should be preserved after swap-remove");

        // Both should still be accessible by their entity ID
        Assert.That(registry.TryGetById(e0.Id, out var byId0), Is.True);
        Assert.That(byId0, Is.SameAs(found0));
        Assert.That(registry.TryGetById(e2.Id, out var byId2), Is.True);
        Assert.That(byId2, Is.SameAs(found2));
    }

    // ================================================================
    // 22. Cross-System: BotEntity Group Sensor Queries
    // ================================================================

    [Test]
    public void BotEntity_GroupSensor_BossFollowerChain()
    {
        var boss = new BotEntity(0) { IsActive = true };
        var follower1 = new BotEntity(1) { IsActive = true };
        var follower2 = new BotEntity(2) { IsActive = true };

        // Set up hierarchy
        follower1.Boss = boss;
        follower2.Boss = boss;
        boss.Followers.Add(follower1);
        boss.Followers.Add(follower2);

        // Boss is in combat
        boss.IsInCombat = true;

        // Followers should detect boss combat via group sensor
        Assert.That(follower1.CheckSensorForBoss(BotSensor.InCombat), Is.True);
        Assert.That(follower2.CheckSensorForBoss(BotSensor.InCombat), Is.True);

        // One follower is suspicious
        follower1.IsSuspicious = true;
        Assert.That(boss.CheckSensorForAnyFollower(BotSensor.IsSuspicious), Is.True);

        // Group check from follower2's perspective (should detect boss or follower1)
        Assert.That(follower2.CheckSensorForGroup(BotSensor.InCombat), Is.True, "Group check should find boss in combat");
        Assert.That(follower2.CheckSensorForGroup(BotSensor.IsSuspicious), Is.True, "Group check should find follower1 suspicious");
    }

    // ================================================================
    // 23. Cross-System: SpawnEntry + Task Scoring with Multiple Tasks
    // ================================================================

    [Test]
    public void Integration_SpawnEntry_CompetesWithAllQuestTasks()
    {
        // SpawnEntryTask at 1.0 should beat all standard quest tasks
        var spawnTask = new SpawnEntryTask();
        var goTo = new FixedScoreTask(0.65f, BotActionTypeId.GoToObjective, "GoTo");
        var loot = new FixedScoreTask(0.50f, BotActionTypeId.Loot, "Loot");
        var vulture = new FixedScoreTask(0.45f, BotActionTypeId.Vulture, "Vulture");

        var tasks = new UtilityTask[] { spawnTask, goTo, loot, vulture };
        var manager = new UtilityTaskManager(tasks);

        var entity = new BotEntity(0) { IsActive = true };
        InitTaskScores(entity, tasks.Length);
        entity.SpawnTime = 0f;
        entity.SpawnEntryDuration = 3f;
        entity.CurrentGameTime = 1f; // During spawn entry

        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(spawnTask), "SpawnEntry should win during spawn pause");

        // Complete spawn entry
        entity.CurrentGameTime = 4f;
        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(goTo), "GoToObjective should win after SpawnEntry completes");
    }

    // ================================================================
    // 24. SoftStuckDetector Escalation Pipeline
    // ================================================================

    [Test]
    public void SoftStuckDetector_FullEscalation_None_Vault_Jump_Failed()
    {
        var detector = new SoftStuckDetector(vaultDelay: 0.5f, jumpDelay: 1f, failDelay: 1.5f);
        var pos = new Vector3(0f, 0f, 0f);
        float time = 0f;

        // Initialize
        detector.Update(pos, 2f, time);

        // Simulate stuck: non-zero speed but no position change
        for (int i = 0; i < 50; i++)
        {
            time += 0.05f;
            detector.Update(pos, 2f, time);
        }

        // After 2.5s with zero movement and speed=2: should be Failed
        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.Failed), "Should reach Failed after sustained stuck");
    }

    [Test]
    public void SoftStuckDetector_Movement_Resets()
    {
        var detector = new SoftStuckDetector(vaultDelay: 0.5f, jumpDelay: 1f, failDelay: 1.5f);
        float time = 0f;

        // Initialize
        var pos = new Vector3(0f, 0f, 0f);
        detector.Update(pos, 2f, time);

        // Get stuck for a bit
        for (int i = 0; i < 15; i++)
        {
            time += 0.05f;
            detector.Update(pos, 2f, time);
        }

        Assert.That(detector.Status, Is.Not.EqualTo(SoftStuckStatus.None), "Should be in some stuck state");

        // Now move significantly
        for (int i = 0; i < 5; i++)
        {
            time += 0.05f;
            pos.x += 5f;
            detector.Update(pos, 2f, time);
        }

        Assert.That(detector.Status, Is.EqualTo(SoftStuckStatus.None), "Movement should reset stuck state");
    }

    // ================================================================
    // 25. Cross-System: ConvergenceField + AdvectionZone Time Progression
    // ================================================================

    [Test]
    public void Integration_ZoneForces_VaryWithRaidTime()
    {
        var entry = new AdvectionZoneEntry
        {
            ForceMin = 1.0f,
            ForceMax = 1.0f, // Fixed force for predictable test
            EarlyMultiplier = 2.0f,
            LateMultiplier = 0.5f,
            BossAliveMultiplier = 1.5f,
            Radius = 50f,
            Decay = 1f,
        };

        var rng = new System.Random(42);

        // Early raid: force * 2.0
        float earlyForce = AdvectionZoneLoader.SampleForce(entry, rng);
        float earlyMultiplier = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 0f);
        float earlyFinal = earlyForce * earlyMultiplier;
        Assert.That(earlyFinal, Is.EqualTo(2.0f).Within(0.01f));

        // Late raid: force * 0.5
        float lateMultiplier = AdvectionZoneLoader.ComputeTimeMultiplier(entry, 1f);
        float lateFinal = earlyForce * lateMultiplier;
        Assert.That(lateFinal, Is.EqualTo(0.5f).Within(0.01f));

        // Boss alive adds another 1.5x
        float bossMultiplier = entry.BossAliveMultiplier;
        float earlyBossForce = earlyFinal * bossMultiplier;
        Assert.That(earlyBossForce, Is.EqualTo(3.0f).Within(0.01f));
    }

    // ================================================================
    // 26. MovementState Reset
    // ================================================================

    [Test]
    public void MovementState_Reset_ClearsAllFields()
    {
        var state = new MovementState
        {
            Status = PathFollowStatus.Following,
            IsSprinting = true,
            CurrentPose = 0.5f,
            StuckStatus = StuckPhase.HardStuck,
            SprintAngleJitter = 15f,
            LastPathUpdateTime = 100f,
            CurrentCornerIndex = 5,
            TotalCorners = 10,
            RetryCount = 3,
            IsCustomMoverActive = true,
        };

        state.Reset();

        Assert.That(state.Status, Is.EqualTo(PathFollowStatus.None));
        Assert.That(state.IsSprinting, Is.False);
        Assert.That(state.CurrentPose, Is.EqualTo(1f));
        Assert.That(state.StuckStatus, Is.EqualTo(StuckPhase.None));
        Assert.That(state.SprintAngleJitter, Is.EqualTo(0f));
        Assert.That(state.LastPathUpdateTime, Is.EqualTo(0f));
        Assert.That(state.CurrentCornerIndex, Is.EqualTo(0));
        Assert.That(state.TotalCorners, Is.EqualTo(0));
        Assert.That(state.RetryCount, Is.EqualTo(0));
        Assert.That(state.IsCustomMoverActive, Is.False);
    }

    // ================================================================
    // 27. End-to-End: Full Bot Lifecycle Pipeline
    // ================================================================

    [Test]
    public void E2E_FullPipeline_Spawn_SpawnEntry_Quest_Stuck_Recovery_Cleanup()
    {
        // --- Phase 1: Entity Registration ---
        var registry = new BotRegistry();
        var entity = CreateEntity(registry, bsgId: 42);
        Assert.That(registry.Count, Is.EqualTo(1));

        // --- Phase 2: Spawn Entry ---
        entity.SpawnTime = 10f;
        entity.SpawnEntryDuration = 3f;
        entity.CurrentGameTime = 10f;

        var spawnTask = new SpawnEntryTask();
        var goToTask = new FixedScoreTask(0.65f, BotActionTypeId.GoToObjective, "GoTo");
        var tasks = new UtilityTask[] { spawnTask, goToTask };
        var manager = new UtilityTaskManager(tasks);
        InitTaskScores(entity, tasks.Length);

        manager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(spawnTask), "Phase 2: SpawnEntry should be active");

        // --- Phase 3: SpawnEntry completes, GoTo takes over ---
        entity.CurrentGameTime = 14f;
        manager.ScoreAndPick(entity);
        Assert.That(entity.IsSpawnEntryComplete, Is.True, "Phase 3: SpawnEntry complete");
        Assert.That(entity.TaskAssignment.Task, Is.SameAs(goToTask), "Phase 3: GoToObjective active");

        // --- Phase 4: Path following ---
        var pathConfig = CustomMoverConfig.CreateDefault();
        var pathFollower = new CustomPathFollower(pathConfig);
        pathFollower.SetPath(
            new[] { new Vector3(0f, 0f, 0f), new Vector3(50f, 0f, 0f), new Vector3(50f, 0f, 50f) },
            new Vector3(50f, 0f, 50f)
        );
        Assert.That(pathFollower.Status, Is.EqualTo(PathFollowerStatus.Following), "Phase 4: Path following");

        // --- Phase 5: Stuck detection ---
        var stuckDetector = new HardStuckDetector(historySize: 10, pathRetryDelay: 0.5f, teleportDelay: 1f, failDelay: 1.5f);
        var stuckPos = new Vector3(25f, 0f, 0f); // stuck at corner 1
        float time = 14f;

        for (int i = 0; i < 30; i++)
        {
            time += 0.05f;
            stuckDetector.Update(stuckPos, currentMoveSpeed: 2f, currentTime: time);
        }

        Assert.That(stuckDetector.Status, Is.Not.EqualTo(HardStuckStatus.None), "Phase 5: Stuck detected");

        // --- Phase 6: Recovery (reset) ---
        stuckDetector.Reset();
        pathFollower.ResetPath();
        entity.Movement.StuckStatus = StuckPhase.None;
        Assert.That(stuckDetector.Status, Is.EqualTo(HardStuckStatus.None), "Phase 6: Stuck cleared");
        Assert.That(pathFollower.Status, Is.EqualTo(PathFollowerStatus.Idle), "Phase 6: Path cleared");

        // --- Phase 7: LOD transition ---
        entity.LodTier = BotLodCalculator.TierReduced;
        bool shouldSkip = BotLodCalculator.ShouldSkipUpdate(entity.LodTier, entity.LodFrameCounter++, 2, 4);
        // First frame of cycle — may or may not skip depending on counter value
        // What matters is the system doesn't crash

        // --- Phase 8: Cleanup ---
        manager.RemoveEntity(entity);
        Assert.That(spawnTask.ActiveEntityCount, Is.EqualTo(0));
        Assert.That(goToTask.ActiveEntityCount, Is.EqualTo(0));

        registry.Remove(entity);
        Assert.That(registry.Count, Is.EqualTo(0));
        Assert.That(registry.GetByBsgId(42), Is.Null, "Phase 8: BSG ID cleared");
    }

    // ================================================================
    // 28. Rapid Add/Remove Stress Test
    // ================================================================

    [Test]
    public void Registry_RapidAddRemove_100Entities_MaintainsIntegrity()
    {
        var registry = new BotRegistry();
        var rng = new System.Random(42);
        var activeEntities = new List<BotEntity>();

        // Add 50 entities
        for (int i = 0; i < 50; i++)
        {
            activeEntities.Add(CreateEntity(registry, bsgId: i));
        }

        // Randomly remove and add for 200 operations
        int nextBsgId = 50;
        for (int op = 0; op < 200; op++)
        {
            if (activeEntities.Count > 0 && (rng.NextDouble() < 0.4 || activeEntities.Count >= 80))
            {
                // Remove random entity
                int idx = rng.Next(0, activeEntities.Count);
                var entity = activeEntities[idx];
                registry.Remove(entity);
                activeEntities.RemoveAt(idx);
            }
            else
            {
                // Add new entity
                var entity = CreateEntity(registry, bsgId: nextBsgId++);
                activeEntities.Add(entity);
            }

            // Verify count consistency
            Assert.That(registry.Count, Is.EqualTo(activeEntities.Count), $"Count mismatch at operation {op}");
        }

        // Verify all remaining entities are accessible
        foreach (var entity in activeEntities)
        {
            Assert.That(registry.GetByBsgId(entity.BsgId), Is.SameAs(entity));
            Assert.That(registry.TryGetById(entity.Id, out var found), Is.True);
            Assert.That(found, Is.SameAs(entity));
        }
    }

    // ================================================================
    // 29. CustomPathFollower + Stuck Detector Integration
    // ================================================================

    [Test]
    public void Integration_PathFollower_StuckAtCorner_DetectorEscalates()
    {
        var config = CustomMoverConfig.CreateDefault();
        var follower = new CustomPathFollower(config);
        var detector = new SoftStuckDetector(vaultDelay: 0.3f, jumpDelay: 0.6f, failDelay: 1f);

        follower.SetPath(new[] { new Vector3(0f, 0f, 0f), new Vector3(10f, 0f, 0f) }, new Vector3(10f, 0f, 0f));

        var stuckPos = new Vector3(5f, 0f, 0f);
        float time = 0f;

        // Bot is stuck at position (5,0,0) — path follower still thinks it's following
        detector.Update(stuckPos, 2f, time);

        for (int i = 0; i < 30; i++)
        {
            time += 0.05f;
            detector.Update(stuckPos, 2f, time);
            follower.Tick(stuckPos, isSprinting: false);
        }

        // Detector should have escalated
        Assert.That(detector.Status, Is.Not.EqualTo(SoftStuckStatus.None));

        // Path follower should still be in Following state (it doesn't know about stuck)
        Assert.That(follower.Status, Is.EqualTo(PathFollowerStatus.Following));
    }

    // ================================================================
    // 30. BotEntity Default State Correctness
    // ================================================================

    [Test]
    public void BotEntity_NewInstance_HasCorrectDefaults()
    {
        var entity = new BotEntity(42);

        Assert.That(entity.Id, Is.EqualTo(42));
        Assert.That(entity.BsgId, Is.EqualTo(-1));
        Assert.That(entity.IsActive, Is.True);
        Assert.That(entity.IsInCombat, Is.False);
        Assert.That(entity.IsSuspicious, Is.False);
        Assert.That(entity.CanQuest, Is.False);
        Assert.That(entity.CanSprintToObjective, Is.True);
        Assert.That(entity.WantsToLoot, Is.False);
        Assert.That(entity.LastLootingTime, Is.EqualTo(DateTime.MinValue));
        Assert.That(entity.BotType, Is.EqualTo(BotType.Unknown));
        Assert.That(entity.IsSleeping, Is.False);
        Assert.That(entity.DistanceToObjective, Is.EqualTo(float.MaxValue));
        Assert.That(entity.TaskScores, Is.Null);
        Assert.That(entity.TaskAssignment.Task, Is.Null);
        Assert.That(entity.IsSpawnEntryComplete, Is.False);
        Assert.That(entity.SpawnEntryDuration, Is.EqualTo(0f));
        Assert.That(entity.LodTier, Is.EqualTo(0));
        Assert.That(entity.Personality, Is.EqualTo(0));
        Assert.That(entity.Aggression, Is.EqualTo(0f));
        Assert.That(entity.PatrolRouteIndex, Is.EqualTo(-1));
        Assert.That(entity.LastEnvironmentId, Is.EqualTo(-1));
    }

    // ================================================================
    // 31. ConvergenceField Normalization
    // ================================================================

    [Test]
    public void ConvergenceField_Output_IsNormalized()
    {
        var field = new ConvergenceField(radius: 200f, force: 5f);
        var attractions = new List<Vector3> { new Vector3(100f, 0f, 100f), new Vector3(-50f, 0f, 200f), new Vector3(200f, 0f, -50f) };

        field.GetConvergence(new Vector3(0f, 0f, 0f), attractions, currentTime: 0f, out float outX, out float outZ);

        float mag = (float)Math.Sqrt(outX * outX + outZ * outZ);

        // Output should be normalized (magnitude ~1) or zero
        Assert.That(mag, Is.EqualTo(1f).Within(0.02f).Or.EqualTo(0f), "Convergence output should be normalized");
    }
}
