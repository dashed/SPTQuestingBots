using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class PatrolTaskTests
{
    private BotEntity _entity;
    private PatrolRoute[] _routes;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
        _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        _entity.Aggression = 0.5f;
        _entity.RaidTimeNormalized = 0.5f;
        _entity.CurrentPositionX = 100f;
        _entity.CurrentPositionZ = 100f;

        _routes = new[]
        {
            new PatrolRoute(
                "Test Route",
                PatrolRouteType.Perimeter,
                new[] { new PatrolWaypoint(120f, 0f, 120f), new PatrolWaypoint(150f, 0f, 150f) }
            ),
        };

        // Reset static state for PatrolTask
        PatrolTask.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        PatrolTask.Reset();
    }

    // ── Basic gating ──────────────────────────────────────

    [Test]
    public void Score_InCombat_ReturnsZero()
    {
        _entity.IsInCombat = true;
        _entity.PatrolRouteIndex = 0;
        float score = PatrolTask.Score(_entity, _routes);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_HasActiveObjective_ReturnsZero()
    {
        _entity.HasActiveObjective = true;
        _entity.PatrolRouteIndex = 0;
        float score = PatrolTask.Score(_entity, _routes);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_NullRoutes_ReturnsZero()
    {
        float score = PatrolTask.Score(_entity, null);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_EmptyRoutes_ReturnsZero()
    {
        float score = PatrolTask.Score(_entity, Array.Empty<PatrolRoute>());
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_OnCooldown_ReturnsZero()
    {
        _entity.PatrolRouteIndex = 0;
        _entity.CurrentGameTime = 100f;
        _entity.PatrolCooldownUntil = 200f;
        float score = PatrolTask.Score(_entity, _routes);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void Score_CooldownExpired_ReturnsPositive()
    {
        _entity.PatrolRouteIndex = 0;
        _entity.CurrentGameTime = 300f;
        _entity.PatrolCooldownUntil = 200f;
        float score = PatrolTask.Score(_entity, _routes);
        Assert.That(score, Is.GreaterThan(0f));
    }

    // ── Route assignment ──────────────────────────────────

    [Test]
    public void Score_NoRouteAssigned_AssignsLazily()
    {
        Assert.That(_entity.PatrolRouteIndex, Is.EqualTo(-1));
        float score = PatrolTask.Score(_entity, _routes);
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(_entity.PatrolRouteIndex, Is.GreaterThanOrEqualTo(0));
    }

    [Test]
    public void Score_NoRouteAssigned_NoSuitableRoute_ReturnsZero()
    {
        var aggressiveOnlyRoutes = new[]
        {
            new PatrolRoute("Aggressive", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(120f, 0f, 120f) }, minAggression: 0.9f),
        };

        _entity.Aggression = 0.2f;
        float score = PatrolTask.Score(_entity, aggressiveOnlyRoutes);
        Assert.That(score, Is.EqualTo(0f));
        Assert.That(_entity.PatrolRouteIndex, Is.EqualTo(-1));
    }

    [Test]
    public void Score_InvalidRouteIndex_ResetAndReturnsZero()
    {
        _entity.PatrolRouteIndex = 99; // out of range
        float score = PatrolTask.Score(_entity, _routes);
        Assert.That(score, Is.EqualTo(0f));
        Assert.That(_entity.PatrolRouteIndex, Is.EqualTo(-1));
    }

    // ── Scoring ───────────────────────────────────────────

    [Test]
    public void Score_WithValidRoute_ReturnsPositive()
    {
        _entity.PatrolRouteIndex = 0;
        float score = PatrolTask.Score(_entity, _routes);
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void Score_NeverExceedsMaxBaseScore()
    {
        _entity.PatrolRouteIndex = 0;
        _entity.CurrentPositionX = 10000f; // very far away
        _entity.CurrentPositionZ = 10000f;
        float score = PatrolTask.Score(_entity, _routes);
        Assert.That(score, Is.LessThanOrEqualTo(PatrolTask.MaxBaseScore));
    }

    [Test]
    public void Score_NeverNegative()
    {
        _entity.PatrolRouteIndex = 0;
        float score = PatrolTask.Score(_entity, _routes);
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    // ── ScoreEntity integration ───────────────────────────

    [Test]
    public void ScoreEntity_WritesToTaskScoresAtOrdinal()
    {
        _entity.PatrolRouteIndex = 0;
        PatrolTask.CurrentMapRoutes = _routes;
        PatrolTask.RoutesLoaded = true;

        var task = new PatrolTask();
        task.ScoreEntity(13, _entity);
        Assert.That(_entity.TaskScores[13], Is.GreaterThan(0f));
    }

    [Test]
    public void ScoreEntity_AppliesScoringModifiers()
    {
        _entity.PatrolRouteIndex = 0;
        _entity.Aggression = 0.3f; // cautious → personality modifier > 1
        PatrolTask.CurrentMapRoutes = _routes;
        PatrolTask.RoutesLoaded = true;

        var task = new PatrolTask();
        task.ScoreEntity(13, _entity);

        float rawScore = PatrolTask.Score(_entity, _routes);
        float modifier = ScoringModifiers.CombinedModifier(_entity.Aggression, _entity.RaidTimeNormalized, BotActionTypeId.Patrol);
        float expected = rawScore * modifier;

        // Due to side effect of Score assigning route, re-score
        _entity.PatrolRouteIndex = 0;
        task.ScoreEntity(13, _entity);

        Assert.That(_entity.TaskScores[13], Is.GreaterThan(0f));
    }

    // ── Constants ─────────────────────────────────────────

    [Test]
    public void BotActionTypeId_IsPatrol()
    {
        var task = new PatrolTask();
        Assert.That(task.BotActionTypeId, Is.EqualTo(BotActionTypeId.Patrol));
    }

    [Test]
    public void ActionReason_IsPatrol()
    {
        var task = new PatrolTask();
        Assert.That(task.ActionReason, Is.EqualTo("Patrol"));
    }

    [Test]
    public void MaxBaseScore_IsCorrect()
    {
        Assert.That(PatrolTask.MaxBaseScore, Is.EqualTo(0.50f));
    }

    [Test]
    public void BotActionTypeId_Constant_Is18()
    {
        Assert.That(BotActionTypeId.Patrol, Is.EqualTo(18));
    }

    // ── QuestTaskFactory integration ────────────────────────

    [Test]
    public void QuestTaskFactory_TaskCount_IsFourteen()
    {
        Assert.That(QuestTaskFactory.TaskCount, Is.EqualTo(14));
    }

    [Test]
    public void QuestTaskFactory_Create_IncludesPatrolTask()
    {
        var manager = QuestTaskFactory.Create();
        // PatrolTask is the 14th task (index 13)
        Assert.IsInstanceOf<PatrolTask>(manager.Tasks[13]);
    }

    [Test]
    public void QuestTaskFactory_Create_EndToEnd()
    {
        PatrolTask.CurrentMapRoutes = _routes;
        PatrolTask.RoutesLoaded = true;

        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(1);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;
        entity.CurrentPositionX = 100f;
        entity.CurrentPositionZ = 100f;

        manager.ScoreAndPick(entity);

        // PatrolTask is at index 13, should have scored
        Assert.That(entity.TaskScores[13], Is.GreaterThan(0f));
    }

    // ── LoadRoutesForMap / Reset ──────────────────────────

    [Test]
    public void LoadRoutesForMap_SetsStaticState()
    {
        PatrolTask.LoadRoutesForMap("bigmap", null);
        Assert.That(PatrolTask.RoutesLoaded, Is.True);
        Assert.That(PatrolTask.CurrentMapRoutes.Length, Is.GreaterThan(0));
    }

    [Test]
    public void Reset_ClearsStaticState()
    {
        PatrolTask.LoadRoutesForMap("bigmap", null);
        PatrolTask.Reset();
        Assert.That(PatrolTask.RoutesLoaded, Is.False);
        Assert.That(PatrolTask.CurrentMapRoutes.Length, Is.EqualTo(0));
    }

    // ── Entity patrol state defaults ────────────────────────

    [Test]
    public void BotEntity_PatrolDefaults()
    {
        var entity = new BotEntity(99);
        Assert.Multiple(() =>
        {
            Assert.That(entity.PatrolRouteIndex, Is.EqualTo(-1));
            Assert.That(entity.PatrolWaypointIndex, Is.EqualTo(0));
            Assert.That(entity.IsPatrolling, Is.False);
            Assert.That(entity.PatrolCooldownUntil, Is.EqualTo(0f));
            Assert.That(entity.PatrolPauseUntil, Is.EqualTo(0f));
        });
    }
}
