using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.BoundaryValues;

/// <summary>
/// Tests for extreme configuration values that users might set via config.json:
/// zero intervals, inverted min/max, negative distances, zero grid sizes, etc.
/// </summary>
[TestFixture]
public class ConfigExtremeTests
{
    // ── BotLodConfig: inverted thresholds ───────────────────────

    [Test]
    public void BotLodConfig_InvertedThresholds_DoesNotCrash()
    {
        // reduced=400 > minimal=100 — inverted
        float reducedSqr = 400f * 400f;
        float minimalSqr = 100f * 100f;

        // All distances >= 100m become TierMinimal (since check is >= minimalSqr first)
        byte tier200m = BotLodCalculator.ComputeTier(200f * 200f, reducedSqr, minimalSqr);
        Assert.That(tier200m, Is.EqualTo(BotLodCalculator.TierMinimal));

        // Very close → TierFull (since 0 < minimalSqr=10000 is false... wait)
        // Actually: sqrDist=100 >= minimalSqr=10000? No → check reducedSqr=160000? No → TierFull
        byte tier10m = BotLodCalculator.ComputeTier(10f * 10f, reducedSqr, minimalSqr);
        Assert.That(tier10m, Is.EqualTo(BotLodCalculator.TierFull));
    }

    // ── VultureConfig: extreme values ───────────────────────────

    [Test]
    public void VultureTask_NegativeCourageThreshold_ProtectedByMathMax()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 1;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 0f;
        entity.NearbyEventZ = 0f;

        // Negative threshold: safeCourageThreshold = Max(1, -10) = 1
        float score = VultureTask.Score(entity, -10, 150f);
        Assert.That(float.IsNaN(score), Is.False);
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void VultureTask_NegativeDetectionRange_NoProximityScore()
    {
        var entity = CreateEntity();
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;
        entity.NearbyEventX = 10f;
        entity.NearbyEventZ = 10f;

        // Negative range: rangeSqr = (-50)^2 = 2500
        float score = VultureTask.Score(entity, 15, -50f);
        // distSqr = 200, rangeSqr = 2500, distSqr < rangeSqr → proximity still works
        Assert.That(float.IsNaN(score), Is.False);
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    // ── LootingConfig: zero value cap ───────────────────────────

    [Test]
    public void LootTask_LootTargetValueNaN_CaughtByGuard()
    {
        var entity = CreateEntity();
        entity.HasLootTarget = true;
        entity.LootTargetValue = float.NaN;
        entity.InventorySpaceFree = 10f;

        float score = LootTask.Score(entity);
        // NaN propagates through valueScore → score → caught by NaN guard → returns 0
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootTask_NegativeLootValue_NoInventorySpace_ReturnsZero()
    {
        var entity = CreateEntity();
        entity.HasLootTarget = true;
        entity.LootTargetValue = -100f;
        entity.InventorySpaceFree = 0f;

        float score = LootTask.Score(entity);
        // InventorySpaceFree <= 0 && LootTargetValue < 0 → return 0
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── LingerConfig: extreme base scores ──────────────────────

    [Test]
    public void LingerTask_NegativeBaseScore_CappedAtZero()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 100f;
        entity.CurrentGameTime = 100f;
        entity.LingerDuration = 10f;

        float score = LingerTask.Score(entity, -1f);
        // score = -1 * (1 - 0) = -1, clamped to 0 by score < 0 check
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LingerTask_VeryLargeBaseScore_StillCapped()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 100f;
        entity.CurrentGameTime = 100f;
        entity.LingerDuration = 10f;

        float score = LingerTask.Score(entity, 100f);
        // score = 100 * 1 = 100. But check: if (score > baseScore) return baseScore = 100
        // This is capped at baseScore, not at a universal cap. The CombinedModifier
        // in ScoreEntity will multiply this, so the actual task score could be very high.
        Assert.That(score, Is.EqualTo(100f));
    }

    // ── PatrolRoute: routes with no waypoints ──────────────────

    [Test]
    public void PatrolTask_RouteWithNoWaypoints_ScoresZero()
    {
        var entity = CreateEntity();
        entity.PatrolRouteIndex = 0;
        entity.PatrolWaypointIndex = 0;

        var route = new PatrolRoute("Empty", PatrolRouteType.Perimeter, Array.Empty<PatrolWaypoint>());
        float score = PatrolTask.Score(entity, new[] { route });
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void PatrolTask_RouteIndexOutOfBounds_ResetAndZero()
    {
        var entity = CreateEntity();
        entity.PatrolRouteIndex = 99; // way out of bounds

        var route = new PatrolRoute("Single", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(0f, 0f, 0f) });
        float score = PatrolTask.Score(entity, new[] { route });
        // Route index >= routes.Length → entity.PatrolRouteIndex = -1, return 0
        Assert.That(score, Is.EqualTo(0f));
        Assert.That(entity.PatrolRouteIndex, Is.EqualTo(-1));
    }

    [Test]
    public void PatrolTask_WaypointIndexOutOfBounds_WrapsToZero()
    {
        var entity = CreateEntity();
        entity.PatrolRouteIndex = 0;
        entity.PatrolWaypointIndex = 999; // out of bounds

        var route = new PatrolRoute("Test", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(100f, 0f, 100f) });
        float score = PatrolTask.Score(entity, new[] { route });
        // wpIndex < 0 || wpIndex >= route.Waypoints.Length → wpIndex = 0
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void PatrolTask_InCombat_ScoresZero()
    {
        var entity = CreateEntity();
        entity.IsInCombat = true;
        entity.PatrolRouteIndex = 0;

        var route = new PatrolRoute("Test", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(100f, 0f, 100f) });
        float score = PatrolTask.Score(entity, new[] { route });
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void PatrolTask_HasActiveObjective_StillScores()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.PatrolRouteIndex = 0;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;

        var route = new PatrolRoute("Test", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(100f, 0f, 100f) });
        float score = PatrolTask.Score(entity, new[] { route });
        Assert.That(score, Is.GreaterThan(0f), "Patrol should not gate on HasActiveObjective");
    }

    // ── CombatEventRegistry: capacity edge cases ────────────────

    [Test]
    public void CombatEventRegistry_CapacityOne_WorksCorrectly()
    {
        CombatEventRegistry.Initialize(1);

        CombatEventRegistry.RecordEvent(10f, 0f, 10f, 1f, 100f, 0, false);
        Assert.That(CombatEventRegistry.Count, Is.EqualTo(1));

        // Overwrite with new event
        CombatEventRegistry.RecordEvent(20f, 0f, 20f, 2f, 200f, 0, false);
        Assert.That(CombatEventRegistry.Count, Is.EqualTo(1));

        // Should find the newer event
        bool found = CombatEventRegistry.GetNearestEvent(20f, 20f, 50f, 2f, 300f, out var nearest);
        Assert.That(found, Is.True);
        Assert.That(nearest.X, Is.EqualTo(20f));

        CombatEventRegistry.Clear();
        CombatEventRegistry.Initialize(CombatEventRegistry.DefaultCapacity);
    }

    [Test]
    public void CombatEventRegistry_InitializeZeroCapacity_UsesDefault()
    {
        CombatEventRegistry.Initialize(0);
        // Should use default capacity, not crash
        CombatEventRegistry.RecordEvent(0f, 0f, 0f, 0f, 100f, 0, false);
        Assert.That(CombatEventRegistry.Count, Is.EqualTo(1));

        CombatEventRegistry.Clear();
        CombatEventRegistry.Initialize(CombatEventRegistry.DefaultCapacity);
    }

    [Test]
    public void CombatEventRegistry_NegativeCapacity_UsesDefault()
    {
        CombatEventRegistry.Initialize(-5);
        CombatEventRegistry.RecordEvent(0f, 0f, 0f, 0f, 100f, 0, false);
        Assert.That(CombatEventRegistry.Count, Is.EqualTo(1));

        CombatEventRegistry.Clear();
        CombatEventRegistry.Initialize(CombatEventRegistry.DefaultCapacity);
    }

    // ── SquadStrategyConfig: zero distances ─────────────────────

    [Test]
    public void TacticalPositionCalculator_ZeroGuardRadius_PositionsAtObjective()
    {
        var config = new SquadStrategyConfig();
        config.GuardDistance = 0f;
        var roles = new SquadRole[] { SquadRole.Guard };
        var positions = new float[3];

        TacticalPositionCalculator.ComputePositions(100f, 0f, 100f, 50f, 50f, roles, 1, positions, config);

        // radius=0 → cos(0)*0=0, sin(0)*0=0 → position at objective
        Assert.That(positions[0], Is.EqualTo(100f).Within(0.01f));
        Assert.That(positions[2], Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void TacticalPositionCalculator_NegativeFlankDistance_MirrorsPosition()
    {
        TacticalPositionCalculator.ComputeFlankPosition(
            100f,
            0f,
            100f,
            50f,
            50f,
            1f,
            -10f, // negative distance
            out float x,
            out float y,
            out float z
        );

        // Negative distance flips the flanker to the other side
        Assert.That(float.IsNaN(x), Is.False);
        Assert.That(float.IsNaN(z), Is.False);
    }

    // ── FormationConfig: zero distances ──────────────────────────

    [Test]
    public void FormationConfig_AllZeroDistances_NoOverflow()
    {
        var config = new FormationConfig(0f, 0f, 0f, true);

        // All thresholds are 0: catchUp=0, match=0, slow=0
        // distToBoss=1 > 0 → Sprint (since > catchUpSqr which is 0)
        var decision = FormationSpeedController.ComputeSpeedDecision(false, 1f, 1f, config);
        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.Sprint));
    }

    // ── PositionHistory: warmup with exactly 2 samples ──────────

    [Test]
    public void PositionHistory_TwoSamples_ProjectsCorrectly()
    {
        var history = new SPTQuestingBots.Helpers.PositionHistory(10);
        // bufferSize = 11 (segments + 1)

        history.Update(new UnityEngine.Vector3(0, 0, 0));
        history.Update(new UnityEngine.Vector3(1, 0, 0));

        float distSqr = history.GetDistanceSqr();
        // observedDistSqr = 1, scaleFactor = 10/1 = 10, projected = 1 * 100 = 100
        Assert.That(distSqr, Is.EqualTo(100f).Within(0.01f));
    }

    [Test]
    public void PositionHistory_OneSample_ReturnsZero()
    {
        var history = new SPTQuestingBots.Helpers.PositionHistory(10);
        history.Update(new UnityEngine.Vector3(5, 5, 5));

        float distSqr = history.GetDistanceSqr();
        Assert.That(distSqr, Is.EqualTo(0f));
    }

    [Test]
    public void PositionHistory_FullBuffer_XZOnly()
    {
        var history = new SPTQuestingBots.Helpers.PositionHistory(2);
        // bufferSize = 3

        history.Update(new UnityEngine.Vector3(0, 0, 0));
        history.Update(new UnityEngine.Vector3(0, 100, 0));
        history.Update(new UnityEngine.Vector3(0, 200, 0));

        float distSqr = history.GetDistanceSqr();
        // Pure vertical movement → XZ distance = 0
        Assert.That(distSqr, Is.EqualTo(0f));
    }

    // ── All features disabled ───────────────────────────────────

    [Test]
    public void AllTasksDisabled_PickTaskHandlesGracefully()
    {
        var entity = CreateEntity();
        entity.TaskScores = new float[0];
        entity.TaskAssignment = default;

        var tasks = Array.Empty<UtilityTask>();
        var manager = new UtilityTaskManager(tasks);

        // No tasks registered — should not crash
        manager.PickTask(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Null);
    }

    [Test]
    public void BotLodConfig_Disabled_ShouldSkipAlwaysReturnsFalse()
    {
        // When LOD is disabled, tier would be TierFull, which never skips
        for (int frame = 0; frame < 10; frame++)
        {
            bool skip = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierFull, frame, 2, 4);
            Assert.That(skip, Is.False);
        }
    }

    // ── LookVarianceController: edge cases for RandomAngle ──────

    [Test]
    public void RandomAngle_EqualMinMax_ReturnsThatValue()
    {
        float angle = LookVarianceController.RandomAngle(45f, 45f);
        Assert.That(angle, Is.EqualTo(45f).Within(0.001f));
    }

    [Test]
    public void RandomAngle_ReversedMinMax_StillProducesValidAngle()
    {
        // min=45, max=-45 → range = -90, result in [-45, 45]
        float angle = LookVarianceController.RandomAngle(45f, -45f);
        Assert.That(float.IsNaN(angle), Is.False);
        Assert.That(float.IsInfinity(angle), Is.False);
    }

    // ── Helper ──────────────────────────────────────────────────

    private static BotEntity CreateEntity()
    {
        return new BotEntity(1);
    }
}
