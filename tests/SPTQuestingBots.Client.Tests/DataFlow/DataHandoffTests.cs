using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Configuration;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.DataFlow;

/// <summary>
/// Tests for cross-system boundary handoff correctness: scoring pipeline,
/// task manager boundaries, tactical position distribution, and edge case data.
/// </summary>
[TestFixture]
public class DataHandoffTests
{
    // ── Scoring Pipeline: ScoreEntity → PickTask → Action ────────

    [Test]
    public void ScoringPipeline_EntityWithNoObjective_AllQuestTasksScoreZero()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0)
        {
            IsActive = true,
            HasActiveObjective = false,
            IsInCombat = false,
            IsSpawnEntryComplete = true,
        };
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        // Score all tasks
        for (int i = 0; i < manager.Tasks.Length; i++)
        {
            manager.Tasks[i].ScoreEntity(i, entity);
        }

        // All quest-dependent tasks should score 0 when no active objective
        Assert.That(entity.TaskScores[0], Is.EqualTo(0f), "GoToObjective should be 0 with no objective");
        Assert.That(entity.TaskScores[1], Is.EqualTo(0f), "Ambush should be 0 with no objective");
        Assert.That(entity.TaskScores[2], Is.EqualTo(0f), "Snipe should be 0 with no objective");
    }

    [Test]
    public void ScoringPipeline_InactiveBotScored_TaskNotAssigned()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0)
        {
            IsActive = false, // Inactive
            HasActiveObjective = true,
            CurrentQuestAction = QuestActionId.MoveToPosition,
            DistanceToObjective = 100f,
        };
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        manager.ScoreAndPick(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Null, "Inactive entity should not get a task");
    }

    // ── Squad → Follower Data Flow ───────────────────────────────

    [Test]
    public void SquadObjective_SetObjective_VersionIncremented()
    {
        var obj = new SquadObjective();
        int initialVersion = obj.Version;

        obj.SetObjective(10f, 20f, 30f);

        Assert.That(obj.Version, Is.EqualTo(initialVersion + 1));
        Assert.That(obj.HasObjective, Is.True);
        Assert.That(obj.ObjectiveX, Is.EqualTo(10f));
    }

    [Test]
    public void SquadObjective_SetObjective_PreviousPositionSaved()
    {
        var obj = new SquadObjective();
        obj.SetObjective(10f, 20f, 30f);
        obj.SetObjective(40f, 50f, 60f);

        Assert.That(obj.HasPreviousObjective, Is.True);
        Assert.That(obj.PreviousX, Is.EqualTo(10f));
        Assert.That(obj.PreviousY, Is.EqualTo(20f));
        Assert.That(obj.PreviousZ, Is.EqualTo(30f));
    }

    [Test]
    public void SquadObjective_ClearObjective_VersionIncremented()
    {
        var obj = new SquadObjective();
        obj.SetObjective(10f, 20f, 30f);
        int versionAfterSet = obj.Version;

        obj.ClearObjective();

        Assert.That(obj.Version, Is.EqualTo(versionAfterSet + 1));
        Assert.That(obj.HasObjective, Is.False);
    }

    // ── TacticalPositionCalculator → BotEntity Handoff ───────────

    [Test]
    public void TacticalPositions_InvalidPosition_MarkedNaN_SkippedInDistribution()
    {
        // Simulate the position distribution logic from GotoObjectiveStrategy:
        // NaN positions should be skipped and member.HasTacticalPosition set to false
        var positionBuffer = new float[] { float.NaN, 0f, 0f, 10f, 20f, 30f };

        var member1 = new BotEntity(0) { IsActive = true };
        var member2 = new BotEntity(1) { IsActive = true };

        // Member 1 gets NaN position
        if (float.IsNaN(positionBuffer[0]))
        {
            member1.HasTacticalPosition = false;
        }
        else
        {
            member1.TacticalPositionX = positionBuffer[0];
            member1.TacticalPositionY = positionBuffer[1];
            member1.TacticalPositionZ = positionBuffer[2];
            member1.HasTacticalPosition = true;
        }

        // Member 2 gets valid position
        if (float.IsNaN(positionBuffer[3]))
        {
            member2.HasTacticalPosition = false;
        }
        else
        {
            member2.TacticalPositionX = positionBuffer[3];
            member2.TacticalPositionY = positionBuffer[4];
            member2.TacticalPositionZ = positionBuffer[5];
            member2.HasTacticalPosition = true;
        }

        Assert.That(member1.HasTacticalPosition, Is.False, "NaN position should not be assigned");
        Assert.That(member2.HasTacticalPosition, Is.True);
        Assert.That(member2.TacticalPositionX, Is.EqualTo(10f));
    }

    [Test]
    public void TacticalPositionCalculator_GuardPosition_ProducesFiniteCoordinates()
    {
        TacticalPositionCalculator.ComputeGuardPosition(100f, 50f, 200f, 45f, 10f, out float x, out float y, out float z);

        Assert.That(float.IsFinite(x), Is.True);
        Assert.That(float.IsFinite(y), Is.True);
        Assert.That(float.IsFinite(z), Is.True);
        Assert.That(y, Is.EqualTo(50f), "Guard Y should match objective Y");
    }

    [Test]
    public void TacticalPositionCalculator_FlankPosition_ZeroApproach_Fallback()
    {
        // When approach direction has zero length, should still produce valid position
        TacticalPositionCalculator.ComputeFlankPosition(100f, 50f, 200f, 100f, 200f, 1f, 10f, out float x, out float y, out float z);

        Assert.That(float.IsFinite(x), Is.True);
        Assert.That(float.IsFinite(y), Is.True);
        Assert.That(float.IsFinite(z), Is.True);
    }

    [Test]
    public void TacticalPositionCalculator_OverwatchPosition_ZeroApproach_Fallback()
    {
        TacticalPositionCalculator.ComputeOverwatchPosition(100f, 50f, 200f, 100f, 200f, 15f, out float x, out float y, out float z);

        Assert.That(float.IsFinite(x), Is.True);
        Assert.That(float.IsFinite(y), Is.True);
        Assert.That(float.IsFinite(z), Is.True);
    }

    // ── Follower Task Manager Boundary ───────────────────────────

    [Test]
    public void FollowerTaskManager_ScoresCorrectIndices()
    {
        var manager = SquadTaskFactory.Create();
        Assert.That(manager.Tasks.Length, Is.EqualTo(SquadTaskFactory.TaskCount));
        Assert.That(manager.Tasks.Length, Is.EqualTo(6));

        var entity = new BotEntity(0) { IsActive = true, HasTacticalPosition = true };
        // Need boss for tactical tasks (HasBoss is computed from Boss != null)
        var boss = new BotEntity(1) { IsActive = true };
        HiveMindSystem.AssignBoss(entity, boss);

        entity.TaskScores = new float[SquadTaskFactory.TaskCount];
        entity.TacticalPositionX = 100f;
        entity.TacticalPositionY = 0f;
        entity.TacticalPositionZ = 100f;
        entity.CurrentPositionX = 200f; // Far from tactical position
        entity.CurrentPositionZ = 200f;

        manager.ScoreAndPick(entity);

        // GoToTacticalPosition should score > 0 because distance > 3m
        Assert.That(entity.TaskScores[0], Is.GreaterThan(0f), "GoToTacticalPosition should score for distant bot");
    }

    [Test]
    public void FollowerTaskManager_CloseToPosition_HoldScoresHigher()
    {
        var manager = SquadTaskFactory.Create();
        var entity = new BotEntity(0) { IsActive = true };
        var boss = new BotEntity(1) { IsActive = true };
        HiveMindSystem.AssignBoss(entity, boss);

        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 100f;
        entity.TacticalPositionY = 0f;
        entity.TacticalPositionZ = 100f;
        entity.CurrentPositionX = 101f; // 1.4m away (< 3m threshold)
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 101f;

        entity.TaskScores = new float[SquadTaskFactory.TaskCount];
        manager.ScoreAndPick(entity);

        // Close to position: GoToTactical=0 (< 3m), HoldTactical > 0
        Assert.That(entity.TaskScores[0], Is.EqualTo(0f), "GoToTactical should be 0 when close");
        Assert.That(entity.TaskScores[1], Is.GreaterThan(0f), "HoldTactical should score when close");
    }

    // ── Vector3.zero / Default Position Edge Cases ───────────────

    [Test]
    public void GoToObjectiveTask_ZeroDistance_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 0f;
        entity.MustUnlockDoor = false;

        float score = GoToObjectiveTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "Zero distance should produce zero score (exp curve at 0)");
    }

    [Test]
    public void GoToObjectiveTask_MaxValueDistance_ScoresNearBase()
    {
        var entity = CreateEntity();
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = float.MaxValue;
        entity.MustUnlockDoor = false;
        entity.IsCloseToObjective = false;

        float score = GoToObjectiveTask.Score(entity);

        // At extreme distance, exp(-dist/75) → 0, so score → BaseScore * 1.0 = 0.65
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(score, Is.LessThanOrEqualTo(0.70f)); // 0.65 base + max 0.05 direction bias
    }

    // ── Loot Score with Edge Case Values ─────────────────────────

    [Test]
    public void LootTask_ZeroValue_ProducesLowScore()
    {
        var entity = CreateEntity();
        entity.HasLootTarget = true;
        entity.IsInCombat = false;
        entity.LootTargetValue = 0f;
        entity.InventorySpaceFree = 10f;
        entity.LootTargetX = entity.CurrentPositionX; // Same position → 0 distance
        entity.LootTargetY = entity.CurrentPositionY;
        entity.LootTargetZ = entity.CurrentPositionZ;

        float score = LootTask.Score(entity);

        // Value component = 0, proximity bonus depends on objective distance, distance penalty = 0
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    [Test]
    public void LootTask_NegativeValue_NoInventorySpace_ScoresZero()
    {
        var entity = CreateEntity();
        entity.HasLootTarget = true;
        entity.IsInCombat = false;
        entity.LootTargetValue = -1f; // Encoded as gear upgrade marker
        entity.InventorySpaceFree = 0f; // No space

        float score = LootTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "No space + negative value → zero score");
    }

    // ── Vulture Score Edge Cases ─────────────────────────────────

    [Test]
    public void VultureTask_ActivePhase_MaintainsMaxScore()
    {
        var entity = CreateEntity();
        entity.IsInCombat = false;
        entity.VulturePhase = VulturePhase.Approach;
        entity.HasNearbyEvent = false; // Expired event

        float score = VultureTask.Score(entity, 15, 150f);

        Assert.That(score, Is.EqualTo(VultureTask.MaxBaseScore), "Active vulture phase should maintain max score even after event expires");
    }

    [Test]
    public void VultureTask_InCombat_AlwaysZero()
    {
        var entity = CreateEntity();
        entity.IsInCombat = true;
        entity.VulturePhase = VulturePhase.Approach; // Even with active phase

        float score = VultureTask.Score(entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f), "Combat cancels vulture even during active phase");
    }

    [Test]
    public void VultureTask_OnCooldown_ScoresZero()
    {
        var entity = CreateEntity();
        entity.IsInCombat = false;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 100;
        entity.VultureCooldownUntil = 999f;
        entity.CurrentGameTime = 100f;

        float score = VultureTask.Score(entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f), "On cooldown → zero score");
    }

    // ── Investigate Score Edge Cases ─────────────────────────────

    [Test]
    public void InvestigateTask_VultureActive_ScoresZero()
    {
        var entity = CreateEntity();
        entity.IsInCombat = false;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 10;
        entity.VulturePhase = VulturePhase.Approach;

        float score = InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.EqualTo(0f), "Investigate should yield to active vulture");
    }

    [Test]
    public void InvestigateTask_AlreadyInvestigating_MaintainsMaxScore()
    {
        var entity = CreateEntity();
        entity.IsInCombat = false;
        entity.HasNearbyEvent = true;
        entity.IsInvestigating = true;

        float score = InvestigateTask.Score(entity, 5, 120f);
        Assert.That(score, Is.EqualTo(InvestigateTask.MaxBaseScore));
    }

    // ── Patrol Score Edge Cases ──────────────────────────────────

    [Test]
    public void PatrolTask_NoRoutes_ScoresZero()
    {
        var entity = CreateEntity();
        entity.IsInCombat = false;
        entity.HasActiveObjective = false;

        float score = PatrolTask.Score(entity, null);
        Assert.That(score, Is.EqualTo(0f));

        score = PatrolTask.Score(entity, Array.Empty<PatrolRoute>());
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void PatrolTask_RouteIndexOutOfBounds_ResetToMinusOne()
    {
        var entity = CreateEntity();
        entity.IsInCombat = false;
        entity.HasActiveObjective = false;
        entity.PatrolRouteIndex = 5; // Out of bounds

        var route = new PatrolRoute("test", PatrolRouteType.Perimeter, new[] { new PatrolWaypoint(0, 0, 0, 2f) });

        float score = PatrolTask.Score(entity, new[] { route });

        Assert.That(score, Is.EqualTo(0f), "Out of bounds route index → zero score");
        Assert.That(entity.PatrolRouteIndex, Is.EqualTo(-1), "Route index should be reset");
    }

    // ── Linger Score Decay ───────────────────────────────────────

    [Test]
    public void LingerTask_LinearDecay_ZeroAtDurationEnd()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 100f;
        entity.LingerDuration = 30f;
        entity.IsInCombat = false;

        // At duration end
        entity.CurrentGameTime = 130f;
        float score = LingerTask.Score(entity, 0.45f);
        Assert.That(score, Is.EqualTo(0f), "Score should be 0 at duration end");
    }

    [Test]
    public void LingerTask_LinearDecay_HalfAtMidpoint()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 100f;
        entity.LingerDuration = 30f;
        entity.IsInCombat = false;

        // At midpoint
        entity.CurrentGameTime = 115f;
        float score = LingerTask.Score(entity, 0.45f);
        Assert.That(score, Is.EqualTo(0.225f).Within(0.01f), "Score should be half at midpoint");
    }

    [Test]
    public void LingerTask_NegativeElapsed_ReturnsZero()
    {
        var entity = CreateEntity();
        entity.ObjectiveCompletedTime = 100f;
        entity.LingerDuration = 30f;
        entity.IsInCombat = false;
        entity.CurrentGameTime = 50f; // Before completion (clock issue)

        float score = LingerTask.Score(entity, 0.45f);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── SpawnEntry Gating ────────────────────────────────────────

    [Test]
    public void SpawnEntryTask_AlreadyComplete_AlwaysZero()
    {
        var entity = CreateEntity();
        entity.IsSpawnEntryComplete = true;
        entity.SpawnEntryDuration = 5f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void SpawnEntryTask_ZeroDuration_ScoresZero()
    {
        var entity = CreateEntity();
        entity.IsSpawnEntryComplete = false;
        entity.SpawnEntryDuration = 0f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void SpawnEntryTask_DurationExpired_MarksComplete()
    {
        var entity = CreateEntity();
        entity.IsSpawnEntryComplete = false;
        entity.SpawnEntryDuration = 3f;
        entity.SpawnTime = 0f;
        entity.CurrentGameTime = 5f; // Past duration

        float score = SpawnEntryTask.Score(entity);

        Assert.That(score, Is.EqualTo(0f));
        Assert.That(entity.IsSpawnEntryComplete, Is.True, "Should be marked complete after duration expires");
    }

    // ── Full Pipeline Integration: Score → Pick → Verify ─────────

    [Test]
    public void FullPipeline_SpawnEntryWins_OverAllOtherTasks()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0)
        {
            IsActive = true,
            IsSpawnEntryComplete = false,
            SpawnEntryDuration = 5f,
            SpawnTime = 0f,
            CurrentGameTime = 1f,
            HasActiveObjective = true,
            CurrentQuestAction = QuestActionId.MoveToPosition,
            DistanceToObjective = 500f,
            IsCloseToObjective = false,
            MustUnlockDoor = false,
            Aggression = 0.9f, // Max aggressive
            RaidTimeNormalized = 0.0f, // Early raid
        };
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        manager.ScoreAndPick(entity);

        var task = entity.TaskAssignment.Task as QuestUtilityTask;
        Assert.That(task, Is.Not.Null, "A task should be assigned");
        Assert.That(
            task.BotActionTypeId,
            Is.EqualTo(BotActionTypeId.SpawnEntry),
            "SpawnEntry should win over GoToObjective even with max aggression at early raid"
        );
    }

    [Test]
    public void FullPipeline_SpawnComplete_GoToObjectiveTakesOver()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0)
        {
            IsActive = true,
            IsSpawnEntryComplete = true,
            HasActiveObjective = true,
            CurrentQuestAction = QuestActionId.MoveToPosition,
            DistanceToObjective = 200f,
            IsCloseToObjective = false,
            MustUnlockDoor = false,
            Aggression = 0.5f,
            RaidTimeNormalized = 0.3f,
        };
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        manager.ScoreAndPick(entity);

        var task = entity.TaskAssignment.Task as QuestUtilityTask;
        Assert.That(task, Is.Not.Null);
        Assert.That(task.BotActionTypeId, Is.EqualTo(BotActionTypeId.GoToObjective));
    }

    // ── Helpers ──────────────────────────────────────────────────

    private static BotEntity CreateEntity()
    {
        return new BotEntity(0) { IsActive = true };
    }
}
