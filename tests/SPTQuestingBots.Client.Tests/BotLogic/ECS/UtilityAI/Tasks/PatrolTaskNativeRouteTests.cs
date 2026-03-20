using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

/// <summary>
/// Tests for PatrolTask integration with BSG's native PatrollingData.
/// Verifies that NativePatrolRoute on BotEntity serves as a fallback
/// when no custom JSON-defined patrol routes are available.
/// </summary>
[TestFixture]
public class PatrolTaskNativeRouteTests
{
    private BotEntity _entity;
    private PatrolRoute _nativeRoute;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
        _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        _entity.Aggression = 0.5f;
        _entity.RaidTimeNormalized = 0.5f;
        _entity.CurrentPositionX = 100f;
        _entity.CurrentPositionZ = 100f;

        _nativeRoute = new PatrolRoute(
            "Native_TestWay",
            PatrolRouteType.Perimeter,
            new[] { new PatrolWaypoint(110f, 0f, 110f), new PatrolWaypoint(130f, 0f, 130f), new PatrolWaypoint(150f, 0f, 110f) },
            isLoop: true
        );

        PatrolTask.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        PatrolTask.Reset();
    }

    // ── Native route fallback scoring ─────────────────────

    [Test]
    public void Score_NoCustomRoutes_NativeRouteAvailable_ReturnsPositive()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_NullCustomRoutes_NativeRouteAvailable_ReturnsPositive()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        float score = PatrolTask.Score(_entity, null);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_NoCustomRoutes_NoNativeRoute_ReturnsZero()
    {
        _entity.NativePatrolRoute = null;
        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_NativeRoute_ScoresLowerThanCustom()
    {
        // Same entity, same position, same waypoints — native should score lower
        var customRoutes = new[] { new PatrolRoute("Custom Route", PatrolRouteType.Perimeter, _nativeRoute.Waypoints) };

        _entity.PatrolRouteIndex = 0;
        float customScore = PatrolTask.Score(_entity, customRoutes);

        // Reset to use native
        _entity.PatrolRouteIndex = -1;
        _entity.NativePatrolRoute = _nativeRoute;
        float nativeScore = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());

        Assert.That(nativeScore, Is.LessThan(customScore), "Native route should score lower than equivalent custom route");
    }

    [Test]
    public void Score_CustomRoutePreferred_OverNative()
    {
        // Both custom and native available — custom should be used
        var customRoutes = new[]
        {
            new PatrolRoute(
                "Custom Route",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(120f, 0f, 120f), new PatrolWaypoint(140f, 0f, 140f) }
            ),
        };

        _entity.NativePatrolRoute = _nativeRoute;
        float score = PatrolTask.Score(_entity, customRoutes);

        Assert.That(score, Is.GreaterThan(0f));
        // Should have assigned a custom route, not native
        Assert.That(_entity.PatrolRouteIndex, Is.GreaterThanOrEqualTo(0), "Should assign custom route when available");
    }

    [Test]
    public void Score_NativeRoute_InCombat_ReturnsZero()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        _entity.IsInCombat = true;
        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_NativeRoute_OnCooldown_ReturnsZero()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        _entity.CurrentGameTime = 100f;
        _entity.PatrolCooldownUntil = 200f;
        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_NativeRoute_CooldownExpired_ReturnsPositive()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        _entity.CurrentGameTime = 300f;
        _entity.PatrolCooldownUntil = 200f;
        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_NativeRoute_NeverExceedsScaledMaxBaseScore()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        _entity.CurrentPositionX = 10000f; // very far away
        _entity.CurrentPositionZ = 10000f;
        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        // Native routes use 80% of MaxBaseScore
        float nativeMaxScore = PatrolTask.MaxBaseScore * 0.8f;
        Assert.That(score, Is.LessThanOrEqualTo(nativeMaxScore + 0.001f));
    }

    [Test]
    public void Score_NativeRoute_NeverNegative()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    // ── Proximity scoring with native route ────────────────

    [Test]
    public void Score_NativeRoute_FarFromWaypoint_HigherScore()
    {
        _entity.NativePatrolRoute = _nativeRoute;

        _entity.CurrentPositionX = 110f;
        _entity.CurrentPositionZ = 110f;
        float closeScore = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());

        _entity.CurrentPositionX = 500f;
        _entity.CurrentPositionZ = 500f;
        float farScore = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());

        Assert.That(farScore, Is.GreaterThan(closeScore), "Far distance should give higher urgency to start patrolling");
    }

    // ── Waypoint index handling ─────────────────────────────

    [Test]
    public void Score_NativeRoute_InvalidWaypointIndex_ClampsToZero()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        _entity.PatrolWaypointIndex = 999; // out of range
        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.GreaterThan(0f), "Should clamp to waypoint 0 and score");
    }

    [Test]
    public void Score_NativeRoute_NegativeWaypointIndex_ClampsToZero()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        _entity.PatrolWaypointIndex = -5;
        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.GreaterThan(0f));
    }

    // ── NativePatrolRoute default state ──────────────────────

    [Test]
    public void BotEntity_NativePatrolRoute_DefaultNull()
    {
        var entity = new BotEntity(99);
        Assert.That(entity.NativePatrolRoute, Is.Null);
    }

    // ── Custom route selection failure + native fallback ─────

    [Test]
    public void Score_CustomRouteSelectionFails_FallsBackToNative()
    {
        // Routes that don't match bot's personality
        var aggressiveOnlyRoutes = new[]
        {
            new PatrolRoute(
                "Aggressive",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(120f, 0f, 120f), new PatrolWaypoint(140f, 0f, 140f) },
                minAggression: 0.9f
            ),
        };

        _entity.Aggression = 0.2f; // too cautious for the custom route
        _entity.NativePatrolRoute = _nativeRoute;

        float score = PatrolTask.Score(_entity, aggressiveOnlyRoutes);

        Assert.That(score, Is.GreaterThan(0f), "Should fall back to native route when custom routes don't match");
        Assert.That(_entity.PatrolRouteIndex, Is.EqualTo(-1), "Should not assign invalid custom route");
    }

    // ── ScoreEntity with native route ────────────────────────

    [Test]
    public void ScoreEntity_NativeRoute_WritesToTaskScores()
    {
        _entity.NativePatrolRoute = _nativeRoute;
        PatrolTask.CurrentMapRoutes = System.Array.Empty<PatrolRoute>();
        PatrolTask.RoutesLoaded = true;

        var task = new PatrolTask();
        task.ScoreEntity(13, _entity);

        Assert.That(_entity.TaskScores[13], Is.GreaterThan(0f));
    }

    // ── Empty waypoints on native route ──────────────────────

    [Test]
    public void Score_NativeRoute_EmptyWaypoints_ReturnsZero()
    {
        _entity.NativePatrolRoute = new PatrolRoute("Empty", PatrolRouteType.Perimeter, System.Array.Empty<PatrolWaypoint>());

        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_NativeRoute_NullWaypoints_ReturnsZero()
    {
        _entity.NativePatrolRoute = new PatrolRoute("NullWP", PatrolRouteType.Perimeter, null);

        float score = PatrolTask.Score(_entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.EqualTo(0f));
    }
}
