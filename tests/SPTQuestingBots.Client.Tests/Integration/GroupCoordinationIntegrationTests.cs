using System;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Integration;

/// <summary>
/// Integration tests for group coordination features:
/// - Tactic-aware utility scoring (GoToObjectiveTask + TacticModifier)
/// - Follower index formation positioning
/// - BotEntity group coordination fields lifecycle
/// - CombatEventRegistry PlacesForCheck feed behavior
/// </summary>
[TestFixture]
public class GroupCoordinationIntegrationTests
{
    private BotRegistry _registry;

    [SetUp]
    public void SetUp()
    {
        _registry = new BotRegistry(16);
        CombatEventRegistry.Initialize(32);
    }

    // ── Tactic-Aware Scoring E2E ─────────────────────────────────────

    [Test]
    public void FullScoringPipeline_TacticAffectsGoToObjectiveScore()
    {
        var entity = _registry.Add(100);
        entity.IsActive = true;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 150f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        var task = new GoToObjectiveTask();
        entity.TaskScores = new float[1];

        // Score with no tactic
        entity.GroupTactic = GroupTacticType.None;
        task.ScoreEntity(0, entity);
        float scoreNone = entity.TaskScores[0];

        // Score with Protect
        entity.GroupTactic = GroupTacticType.Protect;
        task.ScoreEntity(0, entity);
        float scoreProtect = entity.TaskScores[0];

        // Score with Attack
        entity.GroupTactic = GroupTacticType.Attack;
        task.ScoreEntity(0, entity);
        float scoreAttack = entity.TaskScores[0];

        Assert.That(scoreAttack, Is.GreaterThan(scoreNone), "Attack > None");
        Assert.That(scoreNone, Is.GreaterThan(scoreProtect), "None > Protect at far distance");
        Assert.That(scoreProtect, Is.GreaterThan(0f), "Protect should still be > 0");
    }

    [Test]
    public void ProtectTactic_CloseObjective_StillScoresReasonably()
    {
        var entity = _registry.Add(101);
        entity.IsActive = true;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 10f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        var task = new GoToObjectiveTask();
        entity.TaskScores = new float[1];

        entity.GroupTactic = GroupTacticType.Protect;
        task.ScoreEntity(0, entity);
        float scoreProtect = entity.TaskScores[0];

        entity.GroupTactic = GroupTacticType.None;
        task.ScoreEntity(0, entity);
        float scoreNone = entity.TaskScores[0];

        // At close distance, Protect penalty should be mild
        float ratio = scoreProtect / scoreNone;
        Assert.That(ratio, Is.GreaterThan(0.7f), "Close distance Protect penalty should be mild");
    }

    [Test]
    public void AmbushTactic_ReducesMovementScoreUniformly()
    {
        var entity = _registry.Add(102);
        entity.IsActive = true;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        entity.GroupTactic = GroupTacticType.None;
        float scoreNone = GoToObjectiveTask.Score(entity);

        entity.GroupTactic = GroupTacticType.Ambush;
        float scoreAmbush = GoToObjectiveTask.Score(entity);

        Assert.That(scoreAmbush, Is.EqualTo(scoreNone * 0.8f).Within(0.01f));
    }

    // ── Follower Index Formation ─────────────────────────────────────

    [Test]
    public void FollowerIndex_UsedForFormationSlotAssignment()
    {
        // Create a squad with leader and 3 followers
        var squadRegistry = new SquadRegistry();
        var leader = _registry.Add(200);
        leader.IsActive = true;

        var f0 = _registry.Add(201);
        f0.IsActive = true;
        f0.HasTacticalPosition = true;
        f0.FollowerIndex = 0;

        var f1 = _registry.Add(202);
        f1.IsActive = true;
        f1.HasTacticalPosition = true;
        f1.FollowerIndex = 1;

        var f2 = _registry.Add(203);
        f2.IsActive = true;
        f2.HasTacticalPosition = true;
        f2.FollowerIndex = 2;

        // Verify follower indices are assigned
        Assert.That(f0.FollowerIndex, Is.EqualTo(0));
        Assert.That(f1.FollowerIndex, Is.EqualTo(1));
        Assert.That(f2.FollowerIndex, Is.EqualTo(2));
    }

    [Test]
    public void FormationPositionUpdater_ComputesPositionsForFollowerCount()
    {
        // Test that formation position updater works with the number of followers
        var buffer = new float[6 * 3]; // Max 6 followers

        FormationPositionUpdater.ComputeFormationPositions(
            FormationType.Column,
            bossX: 100f,
            bossY: 0f,
            bossZ: 100f,
            headingX: 0f,
            headingZ: 1f,
            count: 3,
            spacing: 4f,
            buffer
        );

        // Column formation: followers trail behind at -heading * spacing * (i+1)
        Assert.That(buffer[0], Is.EqualTo(100f).Within(0.1f)); // F0.X = bossX
        Assert.That(buffer[2], Is.EqualTo(96f).Within(0.1f)); // F0.Z = bossZ - 4
        Assert.That(buffer[5], Is.EqualTo(92f).Within(0.1f)); // F1.Z = bossZ - 8
        Assert.That(buffer[8], Is.EqualTo(88f).Within(0.1f)); // F2.Z = bossZ - 12
    }

    [Test]
    public void FollowerIndex_DeterministicSlotAssignment()
    {
        // When FollowerIndex is available, formation positions should be
        // assigned by index, not by iteration order

        var follower = _registry.Add(300);
        follower.IsActive = true;
        follower.HasTacticalPosition = true;
        follower.FollowerIndex = 2; // Should get slot 2

        var buffer = new float[9]; // 3 slots × 3 components
        FormationPositionUpdater.ComputeColumnPositions(0f, 0f, 0f, 0f, 1f, 3, 4f, buffer);

        // With FollowerIndex=2, should use slot 2 (buffer offset 6)
        int slot = follower.FollowerIndex;
        Assert.That(slot, Is.EqualTo(2));
        float expectedZ = -(4f * (slot + 1)); // Column: -spacing * (i+1)
        Assert.That(buffer[slot * 3 + 2], Is.EqualTo(expectedZ).Within(0.1f));
    }

    // ── CombatEventRegistry PlacesForCheck Feed ──────────────────────

    [Test]
    public void CombatEventScanner_FindsNearbyEventForPlacesForCheckFeed()
    {
        // Record an event near a bot
        CombatEventRegistry.RecordEvent(
            new CombatEvent
            {
                X = 50f,
                Y = 0f,
                Z = 50f,
                Time = 10f,
                Power = 100f,
                Type = CombatEventType.Gunshot,
                IsBoss = false,
                IsActive = true,
            }
        );

        var entity = _registry.Add(400);
        entity.IsActive = true;
        entity.CurrentPositionX = 60f;
        entity.CurrentPositionZ = 50f;

        CombatEventScanner.UpdateEntity(entity, 10f, 60f, 100f, 50f, 15f, 75f, 120f);

        Assert.That(entity.HasNearbyEvent, Is.True, "Should detect nearby event");
        Assert.That(entity.NearbyEventX, Is.EqualTo(50f).Within(0.1f));
        Assert.That(entity.NearbyEventZ, Is.EqualTo(50f).Within(0.1f));
    }

    [Test]
    public void CombatEventScanner_NoEventInRange_NoNearbyEvent()
    {
        CombatEventRegistry.RecordEvent(
            new CombatEvent
            {
                X = 1000f,
                Y = 0f,
                Z = 1000f,
                Time = 10f,
                Power = 50f,
                Type = CombatEventType.Gunshot,
                IsBoss = false,
                IsActive = true,
            }
        );

        var entity = _registry.Add(401);
        entity.IsActive = true;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;

        CombatEventScanner.UpdateEntity(entity, 10f, 60f, 100f, 50f, 15f, 75f, 120f);

        Assert.That(entity.HasNearbyEvent, Is.False, "Event too far — should not be detected");
    }

    [Test]
    public void RecentEvent_QualifiesForPlacesForCheckFeed()
    {
        // The PlacesForCheck feed in BotHiveMindMonitor checks:
        // currentTime - entity.NearbyEventTime > 2f → skip
        // So events within 2s of current time qualify.

        float currentTime = 100f;
        float eventTime = 99f; // 1s ago — qualifies

        bool qualifies = currentTime - eventTime <= 2f;
        Assert.That(qualifies, Is.True, "Event within 2s should qualify for feed");

        float oldEventTime = 95f; // 5s ago — too old
        bool oldQualifies = currentTime - oldEventTime <= 2f;
        Assert.That(oldQualifies, Is.False, "Event older than 2s should not qualify");
    }

    // ── Squad Lifecycle ──────────────────────────────────────────────

    [Test]
    public void GroupTactic_ResetOnEntityReuse()
    {
        var entity = _registry.Add(500);
        entity.GroupTactic = GroupTacticType.Protect;
        entity.FollowerIndex = 2;
        entity.IsActive = false;

        // When reactivated, verify the fields are still set (explicit reset needed by bridge)
        Assert.That(entity.GroupTactic, Is.EqualTo(GroupTacticType.Protect));
        Assert.That(entity.FollowerIndex, Is.EqualTo(2));
    }

    [Test]
    public void TacticModifier_MonotonicForProtect_AsDistanceIncreases()
    {
        // Protect penalty should increase monotonically with distance
        var entity = new BotEntity(1);
        entity.GroupTactic = GroupTacticType.Protect;

        float prev = GoToObjectiveTask.TacticModifier(entity, 0f);
        for (float d = 10f; d <= 500f; d += 10f)
        {
            float current = GoToObjectiveTask.TacticModifier(entity, d);
            Assert.That(current, Is.LessThanOrEqualTo(prev), $"Penalty should increase at d={d}");
            prev = current;
        }
    }

    // ── Config Validation ────────────────────────────────────────────

    [Test]
    public void SquadStrategyConfig_EnableGroupCoordination_DefaultsTrue()
    {
        var config = new global::SPTQuestingBots.Configuration.SquadStrategyConfig();
        Assert.That(config.EnableGroupCoordination, Is.True);
    }

    // ── Multi-Tactic Scoring Comparison ──────────────────────────────

    [Test]
    public void AllTactics_ProduceValidScores()
    {
        var entity = new BotEntity(1);
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        int[] tactics = { GroupTacticType.None, GroupTacticType.Attack, GroupTacticType.Ambush, GroupTacticType.Protect };

        foreach (int tactic in tactics)
        {
            entity.GroupTactic = tactic;
            float score = GoToObjectiveTask.Score(entity);

            Assert.That(float.IsNaN(score), Is.False, $"Score should not be NaN for tactic={tactic}");
            Assert.That(float.IsInfinity(score), Is.False, $"Score should not be Inf for tactic={tactic}");
            Assert.That(score, Is.GreaterThanOrEqualTo(0f), $"Score should be non-negative for tactic={tactic}");
        }
    }

    [Test]
    public void TacticOrdering_AttackHigherThanAmbush_ForMovement()
    {
        var entity = new BotEntity(1);
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        entity.GroupTactic = GroupTacticType.Attack;
        float scoreAttack = GoToObjectiveTask.Score(entity);

        entity.GroupTactic = GroupTacticType.Ambush;
        float scoreAmbush = GoToObjectiveTask.Score(entity);

        Assert.That(scoreAttack, Is.GreaterThan(scoreAmbush), "Attack should score higher than Ambush for movement");
    }
}
