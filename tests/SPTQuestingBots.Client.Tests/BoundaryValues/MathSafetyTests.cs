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
/// Tests for division by zero, NaN propagation, sqrt of negatives,
/// vector normalization at zero, and related math safety issues.
/// </summary>
[TestFixture]
public class MathSafetyTests
{
    // ── TacticalPositionCalculator: zero-distance positions ───────

    [Test]
    public void TacticalPositionCalculator_FlankPosition_SamePoint_UsesFallback()
    {
        TacticalPositionCalculator.ComputeFlankPosition(
            100f,
            0f,
            100f, // objective
            100f,
            100f, // approach = same as objective on XZ
            1f,
            10f, // side, distance
            out float x,
            out float y,
            out float z
        );

        // Fallback: x = objX + distance * side = 110
        Assert.That(x, Is.EqualTo(110f).Within(0.01f));
        Assert.That(y, Is.EqualTo(0f));
        Assert.That(z, Is.EqualTo(100f));
    }

    [Test]
    public void TacticalPositionCalculator_OverwatchPosition_SamePoint_UsesFallback()
    {
        TacticalPositionCalculator.ComputeOverwatchPosition(
            100f,
            0f,
            100f, // objective
            100f,
            100f, // approach = same on XZ
            20f, // distance
            out float x,
            out float y,
            out float z
        );

        // Fallback: z = objZ - distance = 80
        Assert.That(x, Is.EqualTo(100f));
        Assert.That(y, Is.EqualTo(0f));
        Assert.That(z, Is.EqualTo(80f));
    }

    [Test]
    public void TacticalPositionCalculator_EscortPosition_SamePoint_UsesFallback()
    {
        TacticalPositionCalculator.ComputeEscortPosition(
            100f,
            0f,
            100f, // boss position
            100f,
            100f, // objective = same on XZ
            5f,
            2f, // trail, lateral
            out float x,
            out float y,
            out float z
        );

        // Fallback: x = bossX + lateralOffset = 102
        Assert.That(x, Is.EqualTo(102f).Within(0.01f));
        Assert.That(y, Is.EqualTo(0f));
        Assert.That(z, Is.EqualTo(100f));
    }

    [Test]
    public void TacticalPositionCalculator_GuardPosition_AlwaysProducesValidResult()
    {
        // Guard uses angle + radius, no normalization needed
        TacticalPositionCalculator.ComputeGuardPosition(
            0f,
            0f,
            0f, // objective at origin
            0f, // angle
            10f, // radius
            out float x,
            out float y,
            out float z
        );

        Assert.That(float.IsNaN(x), Is.False);
        Assert.That(float.IsNaN(z), Is.False);
        Assert.That(x, Is.EqualTo(10f).Within(0.01f)); // cos(0) * 10 = 10
        Assert.That(z, Is.EqualTo(0f).Within(0.01f)); // sin(0) * 10 = 0
    }

    // ── CombatPositionAdjuster: degenerate threat ─────────────

    [Test]
    public void CombatPositionAdjuster_ZeroThreatDirection_PlacesAtObjective()
    {
        var roles = new SquadRole[] { SquadRole.Guard, SquadRole.Flanker };
        var positions = new float[6];
        var config = new SquadStrategyConfig();

        CombatPositionAdjuster.ComputeCombatPositions(
            100f,
            0f,
            100f, // objective
            0f,
            0f, // zero threat direction
            roles,
            2,
            config,
            positions
        );

        // All positions at objective
        Assert.That(positions[0], Is.EqualTo(100f));
        Assert.That(positions[1], Is.EqualTo(0f));
        Assert.That(positions[2], Is.EqualTo(100f));
        Assert.That(positions[3], Is.EqualTo(100f));
        Assert.That(positions[4], Is.EqualTo(0f));
        Assert.That(positions[5], Is.EqualTo(100f));
    }

    [Test]
    public void CombatPositionAdjuster_UnnormalizedThreat_NormalizesDefensively()
    {
        var roles = new SquadRole[] { SquadRole.Flanker };
        var positionsNormalized = new float[3];
        var positionsUnnormalized = new float[3];
        var config = new SquadStrategyConfig();

        // Normalized threat: (1, 0)
        CombatPositionAdjuster.ComputeCombatPositions(100f, 0f, 100f, 1f, 0f, roles, 1, config, positionsNormalized);

        // Unnormalized threat: (3, 0) — magnitude 3, same direction
        CombatPositionAdjuster.ComputeCombatPositions(100f, 0f, 100f, 3f, 0f, roles, 1, config, positionsUnnormalized);

        // After fix: should produce the same result because we normalize internally
        Assert.That(
            positionsUnnormalized[0],
            Is.EqualTo(positionsNormalized[0]).Within(0.01f),
            "Flanker X should be same regardless of threat vector magnitude"
        );
        Assert.That(
            positionsUnnormalized[2],
            Is.EqualTo(positionsNormalized[2]).Within(0.01f),
            "Flanker Z should be same regardless of threat vector magnitude"
        );
    }

    [Test]
    public void CombatPositionAdjuster_NormalizedThreat_NoExtraNormalization()
    {
        // Verify that already-normalized vectors pass through correctly
        var roles = new SquadRole[] { SquadRole.Overwatch };
        var positions = new float[3];
        var config = new SquadStrategyConfig();

        CombatPositionAdjuster.ComputeCombatPositions(
            0f,
            0f,
            0f,
            1f,
            0f, // unit vector pointing X
            roles,
            1,
            config,
            positions
        );

        // Overwatch: obj - threatDir * distance = 0 - 1 * dist = -dist
        Assert.That(positions[0], Is.EqualTo(-config.OverwatchDistance).Within(0.01f));
        Assert.That(positions[2], Is.EqualTo(0f).Within(0.01f));
    }

    // ── BotLodCalculator: threshold edge cases ───────────────

    [Test]
    public void BotLodCalculator_ExactlyAtReducedThreshold_ReturnsTierReduced()
    {
        float reduced = 150f * 150f;
        float minimal = 300f * 300f;
        byte tier = BotLodCalculator.ComputeTier(reduced, reduced, minimal);
        Assert.That(tier, Is.EqualTo(BotLodCalculator.TierReduced));
    }

    [Test]
    public void BotLodCalculator_ExactlyAtMinimalThreshold_ReturnsTierMinimal()
    {
        float reduced = 150f * 150f;
        float minimal = 300f * 300f;
        byte tier = BotLodCalculator.ComputeTier(minimal, reduced, minimal);
        Assert.That(tier, Is.EqualTo(BotLodCalculator.TierMinimal));
    }

    [Test]
    public void BotLodCalculator_ZeroDistance_ReturnsTierFull()
    {
        float reduced = 150f * 150f;
        float minimal = 300f * 300f;
        byte tier = BotLodCalculator.ComputeTier(0f, reduced, minimal);
        Assert.That(tier, Is.EqualTo(BotLodCalculator.TierFull));
    }

    [Test]
    public void BotLodCalculator_NaN_ReturnsTierFull()
    {
        // NaN >= X is always false in IEEE 754
        float reduced = 150f * 150f;
        float minimal = 300f * 300f;
        byte tier = BotLodCalculator.ComputeTier(float.NaN, reduced, minimal);
        // NaN >= minimalThresholdSqr = false, NaN >= reducedThresholdSqr = false → TierFull
        Assert.That(tier, Is.EqualTo(BotLodCalculator.TierFull));
    }

    // ── BotLodCalculator.ShouldSkipUpdate: skip=0 ──────────────

    [Test]
    public void ShouldSkipUpdate_ReducedSkipZero_NeverSkips()
    {
        // skip=0 → cycle=Max(1, 1)=1 → frame%1=0 → return false
        for (int frame = 0; frame < 10; frame++)
        {
            bool skip = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frame, 0, 4);
            Assert.That(skip, Is.False, $"Frame {frame} should not be skipped with skip=0");
        }
    }

    [Test]
    public void ShouldSkipUpdate_NegativeSkip_TreatedAsZero()
    {
        // skip=-5 → cycle=Max(1, -4)=1 → never skip
        for (int frame = 0; frame < 10; frame++)
        {
            bool skip = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierReduced, frame, -5, 4);
            Assert.That(skip, Is.False, $"Frame {frame} should not be skipped with skip=-5");
        }
    }

    [Test]
    public void ShouldSkipUpdate_TierFull_NeverSkips()
    {
        for (int frame = 0; frame < 10; frame++)
        {
            bool skip = BotLodCalculator.ShouldSkipUpdate(BotLodCalculator.TierFull, frame, 2, 4);
            Assert.That(skip, Is.False, $"TierFull should never skip frame {frame}");
        }
    }

    // ── GoToObjectiveTask.DirectionBias: distSqr < 1 guard ──────

    [Test]
    public void DirectionBias_SamePosition_ReturnsZero()
    {
        var entity = CreateEntity();
        entity.SpawnFacingBias = 1.0f;
        entity.SpawnFacingX = 1.0f;
        entity.SpawnFacingZ = 0.0f;
        entity.DistanceToObjective = 0.5f;

        var squad = new SquadEntity(0, 1, 1);
        squad.Objective.SetObjective(entity.CurrentPositionX, entity.CurrentPositionY, entity.CurrentPositionZ);
        entity.Squad = squad;

        // Bot is at same position as objective → distSqr < 1 → bias = 0
        float bias = GoToObjectiveTask.DirectionBias(entity);
        Assert.That(bias, Is.EqualTo(0f));
    }

    // ── LootScanResult.ComputeDistanceSqr: NaN propagation ─────

    [Test]
    public void ComputeDistanceSqr_NaN_PropagatesNaN()
    {
        float result = LootScanResult.ComputeDistanceSqr(float.NaN, 0f, 0f, 0f, 0f, 0f);
        Assert.That(float.IsNaN(result), Is.True);
    }

    [Test]
    public void ComputeDistanceSqr_SamePosition_ReturnsZero()
    {
        float result = LootScanResult.ComputeDistanceSqr(5f, 10f, 15f, 5f, 10f, 15f);
        Assert.That(result, Is.EqualTo(0f));
    }

    // ── LookVarianceController.SampleInterval: min floor ────────

    [Test]
    public void SampleInterval_ZeroMinMax_ReturnsMinimumFloor()
    {
        float result = LookVarianceController.SampleInterval(0f, 0f);
        Assert.That(
            result,
            Is.GreaterThanOrEqualTo(LookVarianceController.MinInterval),
            "Zero interval should be clamped to minimum floor"
        );
    }

    [Test]
    public void SampleInterval_NegativeValues_ReturnsMinimumFloor()
    {
        float result = LookVarianceController.SampleInterval(-5f, -1f);
        Assert.That(
            result,
            Is.GreaterThanOrEqualTo(LookVarianceController.MinInterval),
            "Negative interval should be clamped to minimum floor"
        );
    }

    [Test]
    public void SampleInterval_NormalValues_ReturnsWithinRange()
    {
        for (int i = 0; i < 100; i++)
        {
            float result = LookVarianceController.SampleInterval(5f, 15f);
            Assert.That(
                result,
                Is.GreaterThanOrEqualTo(5f).And.LessThanOrEqualTo(15f),
                $"Iteration {i}: result {result} should be in [5, 15]"
            );
        }
    }

    // ── CombatEventRegistry ring buffer index safety ────────────

    [Test]
    public void CombatEventRegistry_OverfillBuffer_NoArrayOverflow()
    {
        CombatEventRegistry.Initialize(4);

        // Fill more events than capacity
        for (int i = 0; i < 10; i++)
        {
            CombatEventRegistry.RecordEvent(i, 0f, i, i, 100f, 0, false);
        }

        // Should not throw, count should be capped at capacity
        Assert.That(CombatEventRegistry.Count, Is.EqualTo(4));

        // Clean up
        CombatEventRegistry.Clear();
        CombatEventRegistry.Initialize(CombatEventRegistry.DefaultCapacity);
    }

    [Test]
    public void CombatEventRegistry_GetNearestEvent_EmptyBuffer_ReturnsFalse()
    {
        CombatEventRegistry.Initialize(4);

        bool found = CombatEventRegistry.GetNearestEvent(0f, 0f, 1000f, 0f, 300f, out _);
        Assert.That(found, Is.False);

        CombatEventRegistry.Clear();
        CombatEventRegistry.Initialize(CombatEventRegistry.DefaultCapacity);
    }

    [Test]
    public void CombatEventRegistry_GetIntensity_ZeroRadius_NoEvents()
    {
        CombatEventRegistry.Initialize(4);
        CombatEventRegistry.RecordEvent(0f, 0f, 0f, 0f, 100f, 0, false);

        // radius=0 → radiusSqr=0. distSqr=0 < 0 is false → no events counted
        int intensity = CombatEventRegistry.GetIntensity(0f, 0f, 0f, 300f, 0f);
        Assert.That(intensity, Is.EqualTo(0));

        CombatEventRegistry.Clear();
        CombatEventRegistry.Initialize(CombatEventRegistry.DefaultCapacity);
    }

    // ── PatrolRouteSelector: fitScore with equal min/max aggression ──

    [Test]
    public void PatrolRouteSelector_EqualMinMaxAggression_FitScoreIsOne()
    {
        // aggressionRange = 0 → fitScore = 1
        var route = new PatrolRoute(
            "Test",
            PatrolRouteType.Perimeter,
            new[] { new PatrolWaypoint(0f, 0f, 0f) },
            minAggression: 0.5f,
            maxAggression: 0.5f
        );

        // Only aggression=0.5 passes the filter, and fitScore = 1
        int selected = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, new[] { route }, 42);
        Assert.That(selected, Is.EqualTo(0));
    }

    [Test]
    public void PatrolRouteSelector_NoRoutes_ReturnsMinusOne()
    {
        int selected = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, Array.Empty<PatrolRoute>(), 42);
        Assert.That(selected, Is.EqualTo(-1));
    }

    [Test]
    public void PatrolRouteSelector_NullRoutes_ReturnsMinusOne()
    {
        int selected = PatrolRouteSelector.SelectRoute(0f, 0f, 0.5f, 0.5f, null, 42);
        Assert.That(selected, Is.EqualTo(-1));
    }

    // ── FormationSpeedController: NaN distance ──────────────────

    [Test]
    public void FormationSpeedController_NaNDistance_MatchesBoss()
    {
        var config = FormationConfig.Default;
        // NaN > catchUpDistanceSqr is false in IEEE 754
        // NaN > matchSpeedDistanceSqr is false
        // NaN < slowApproachDistanceSqr is false
        // Falls through to default: MatchBoss
        var decision = FormationSpeedController.ComputeSpeedDecision(false, float.NaN, float.NaN, config);
        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.MatchBoss));
    }

    [Test]
    public void FormationSpeedController_ZeroDistance_SlowApproach()
    {
        var config = FormationConfig.Default;
        // distToBoss=0 is within matchSpeedDistance, distToTactical=0 < slowApproachDistanceSqr
        var decision = FormationSpeedController.ComputeSpeedDecision(false, 0f, 0f, config);
        Assert.That(decision, Is.EqualTo(FormationSpeedDecision.SlowApproach));
    }

    // ── SquadLootCoordinator: same position comm range ──────────

    [Test]
    public void ShouldFollowerLoot_SamePosition_WithinCommRange()
    {
        var boss = CreateEntity();
        boss.IsCloseToObjective = true;
        boss.HasActiveObjective = true;

        var follower = new BotEntity(2);
        follower.CurrentPositionX = boss.CurrentPositionX;
        follower.CurrentPositionY = boss.CurrentPositionY;
        follower.CurrentPositionZ = boss.CurrentPositionZ;

        bool result = SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 100f);
        Assert.That(result, Is.True);
    }

    [Test]
    public void ShouldFollowerLoot_NaNPosition_DeniedLoot()
    {
        var boss = CreateEntity();
        boss.CurrentPositionX = float.NaN;

        var follower = new BotEntity(2);

        bool result = SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 100f);
        Assert.That(result, Is.False);
    }

    // ── Helper ──────────────────────────────────────────────────

    private static BotEntity CreateEntity()
    {
        return new BotEntity(1);
    }
}
