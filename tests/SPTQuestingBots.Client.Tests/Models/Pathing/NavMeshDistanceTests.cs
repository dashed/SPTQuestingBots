using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.Models.Pathing;

[TestFixture]
public class NavMeshDistanceTests
{
    // --- BotEntity NavMesh distance fields ---

    [Test]
    public void BotEntity_NavMeshDistanceToObjective_DefaultsToMaxValue()
    {
        var entity = new BotEntity(0);
        Assert.AreEqual(float.MaxValue, entity.NavMeshDistanceToObjective);
    }

    [Test]
    public void BotEntity_LastNavMeshDistanceTime_DefaultsToZero()
    {
        var entity = new BotEntity(0);
        Assert.AreEqual(0f, entity.LastNavMeshDistanceTime);
    }

    // --- GoToObjectiveTask uses NavMesh distance when available ---

    [Test]
    public void GoToObjectiveTask_PrefersNavMeshDistance_WhenAvailable()
    {
        var entity = CreateEntityWithObjective();

        // Set NavMesh distance much larger than straight-line
        entity.DistanceToObjective = 50f; // straight-line
        entity.NavMeshDistanceToObjective = 200f; // NavMesh (longer, goes around obstacles)

        float scoreWithNavMesh = GoToObjectiveTask.Score(entity);

        // Reset NavMesh distance to unavailable
        entity.NavMeshDistanceToObjective = float.MaxValue;
        float scoreWithoutNavMesh = GoToObjectiveTask.Score(entity);

        // NavMesh distance is larger → score should be higher
        Assert.Greater(scoreWithNavMesh, scoreWithoutNavMesh, "NavMesh distance (200m) should yield higher score than straight-line (50m)");
    }

    [Test]
    public void GoToObjectiveTask_FallsBackToStraightLine_WhenNavMeshUnavailable()
    {
        var entity = CreateEntityWithObjective();
        entity.DistanceToObjective = 100f;
        entity.NavMeshDistanceToObjective = float.MaxValue; // unavailable

        float score = GoToObjectiveTask.Score(entity);
        Assert.Greater(score, 0f, "Should still score using straight-line distance");
    }

    [Test]
    public void GoToObjectiveTask_NavMeshDistance_CloseToZero_LowScore()
    {
        var entity = CreateEntityWithObjective();
        entity.DistanceToObjective = 2f;
        entity.NavMeshDistanceToObjective = 3f; // nearly there via NavMesh too

        float score = GoToObjectiveTask.Score(entity);
        Assert.Less(score, 0.1f, "Close NavMesh distance should yield low score");
    }

    [Test]
    public void GoToObjectiveTask_NavMeshDistance_VeryFar_HighScore()
    {
        var entity = CreateEntityWithObjective();
        entity.DistanceToObjective = 50f;
        entity.NavMeshDistanceToObjective = 500f; // very long path via NavMesh

        float score = GoToObjectiveTask.Score(entity);
        Assert.Greater(score, 0.5f, "Very long NavMesh distance should yield high score");
    }

    // --- Helper ---

    private static BotEntity CreateEntityWithObjective()
    {
        var entity = new BotEntity(0);
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = 1; // MoveToPosition
        return entity;
    }
}
