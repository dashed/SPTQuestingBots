using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.CrossFeature;

/// <summary>
/// Tests for cross-feature interference between:
/// - CustomMover + RoomClear (Pair 2)
/// - ZoneMovement + Patrol (Pair 3)
/// - Vulture + Investigate (Pair 4)
/// - Squad Strategies + Loot (Pair 5)
/// - SpawnEntry + Combat Gates (Pair 6)
/// </summary>
[TestFixture]
public class FeaturePairInterferenceTests
{
    private static BotEntity CreateEntity(int id)
    {
        var e = new BotEntity(id);
        e.IsActive = true;
        e.TaskScores = new float[QuestTaskFactory.TaskCount];
        return e;
    }

    // ================================================================
    // Pair 2: CustomMover + RoomClear
    // ================================================================

    [Test]
    public void RoomClear_IndoorTransition_SetsRoomClearState()
    {
        var entity = CreateEntity(1);
        entity.LastEnvironmentId = 0; // outdoor

        var instruction = RoomClearController.Update(entity, true, 10f, 3f, 5f, 0.5f);

        Assert.That(entity.IsInRoomClear, Is.True);
        Assert.That(instruction, Is.EqualTo(RoomClearInstruction.SlowWalk));
    }

    [Test]
    public void RoomClear_CornerPauseNotTriggeredAutomatically()
    {
        // Corner pause requires explicit TriggerCornerPause call
        // which is never invoked from GoToObjectiveAction (dead code path)
        var entity = CreateEntity(1);
        entity.LastEnvironmentId = 0; // outdoor
        RoomClearController.Update(entity, true, 10f, 3f, 5f, 0.5f);

        // CornerPauseUntil should still be 0 (not set)
        Assert.That(entity.CornerPauseUntil, Is.EqualTo(0f), "Corner pause should not trigger without explicit TriggerCornerPause call");
    }

    [Test]
    public void RoomClear_CornerPauseDoesNotStopMovementOnItsOwn()
    {
        // Even if TriggerCornerPause is called, the custom mover has no mechanism
        // to stop moving — the PauseAtCorner instruction only affects pose, not movement
        var entity = CreateEntity(1);
        entity.LastEnvironmentId = 0; // outdoor

        // Start room clear
        RoomClearController.Update(entity, true, 10f, 3f, 5f, 0.5f);
        Assert.That(entity.IsInRoomClear, Is.True);

        // Trigger corner pause
        RoomClearController.TriggerCornerPause(entity, 10.5f, 1.0f);
        Assert.That(entity.CornerPauseUntil, Is.EqualTo(11.5f));

        // Update during corner pause
        var instruction = RoomClearController.Update(entity, true, 10.8f, 3f, 5f, 0.5f);
        Assert.That(instruction, Is.EqualTo(RoomClearInstruction.PauseAtCorner));

        // The instruction is PauseAtCorner, but this only affects pose
        // The custom mover TickCustomMover will still advance the path
        // This is a known limitation: corner pause is visual-only
    }

    [Test]
    public void RoomClear_IndoorCheckAlreadyBlocksSprintBeforeCustomMover()
    {
        // In GoToObjectiveAction.Update, Player.Environment check sets CanSprint=false
        // BEFORE TickCustomMover. Room clear is redundant for sprint.
        // This test verifies the indoor detection correctly catches indoor environments.
        var entity = CreateEntity(1);
        entity.LastEnvironmentId = -1; // uninitialized

        // Indoor environment
        var instruction = RoomClearController.Update(entity, true, 10f, 3f, 5f, 0.5f);

        // Since LastEnvironmentId was -1 (not 1=indoor), and now indoor,
        // wasOutdoor = true, isIndoor = true
        Assert.That(entity.IsInRoomClear, Is.True);
        Assert.That(instruction, Is.EqualTo(RoomClearInstruction.SlowWalk));
    }

    [Test]
    public void RoomClear_ExpiresAfterDuration()
    {
        var entity = CreateEntity(1);
        entity.LastEnvironmentId = 0; // outdoor
        RoomClearController.Update(entity, true, 10f, 3f, 5f, 0.5f);
        Assert.That(entity.IsInRoomClear, Is.True);

        // After max duration (5s), room clear should expire
        var instruction = RoomClearController.Update(entity, true, 20f, 3f, 5f, 0.5f);
        Assert.That(entity.IsInRoomClear, Is.False);
        Assert.That(instruction, Is.EqualTo(RoomClearInstruction.None));
    }

    [Test]
    public void RoomClear_CancelledOnReturnToOutdoor()
    {
        var entity = CreateEntity(1);
        entity.LastEnvironmentId = 0; // outdoor
        RoomClearController.Update(entity, true, 10f, 3f, 5f, 0.5f);
        Assert.That(entity.IsInRoomClear, Is.True);

        // Bot goes back outdoor
        var instruction = RoomClearController.Update(entity, false, 10.5f, 3f, 5f, 0.5f);
        Assert.That(entity.IsInRoomClear, Is.False);
        Assert.That(instruction, Is.EqualTo(RoomClearInstruction.None));
    }

    // ================================================================
    // Pair 3: ZoneMovement + Patrol
    // ================================================================

    [Test]
    public void PatrolScoresZero_WhenBotHasActiveObjective()
    {
        // Patrol gates on !HasActiveObjective — quest objectives prevent patrol
        var entity = CreateEntity(1);
        entity.HasActiveObjective = true;

        float score = PatrolTask.Score(entity, System.Array.Empty<PatrolRoute>());
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void PatrolAndZoneMovement_NoOverlapInUtilityPipeline()
    {
        // Patrol is task #14 in QuestTaskFactory (scores in utility AI)
        // Zone movement is a FALLBACK in BotObjectiveLayer.tryZoneMovementFallback()
        // Zone wander only runs when ALL utility tasks score 0
        // They cannot compete in the same scoring round
        var entity = CreateEntity(1);
        entity.HasActiveObjective = false; // no quest
        entity.PatrolRouteIndex = -1; // no route assigned

        // With no routes loaded, patrol scores 0
        float patrolScore = PatrolTask.Score(entity, System.Array.Empty<PatrolRoute>());
        Assert.That(patrolScore, Is.EqualTo(0f), "Patrol with no routes should score 0, allowing zone movement fallback");
    }

    [Test]
    public void PatrolScoresPositive_WhenRouteAssigned_PreventsZoneFallback()
    {
        var entity = CreateEntity(1);
        entity.HasActiveObjective = false;
        entity.CurrentGameTime = 100f;

        var waypoint = new PatrolWaypoint(100f, 0f, 200f, 5f);
        var route = new PatrolRoute("TestRoute", PatrolRouteType.Perimeter, new[] { waypoint });

        entity.PatrolRouteIndex = 0;
        entity.PatrolWaypointIndex = 0;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionZ = 0f;

        float score = PatrolTask.Score(entity, new[] { route });
        Assert.That(score, Is.GreaterThan(0f), "Patrol with assigned route should score > 0, preventing zone movement fallback");
    }

    [Test]
    public void PatrolInCombat_ScoresZero()
    {
        var entity = CreateEntity(1);
        entity.IsInCombat = true;
        entity.PatrolRouteIndex = 0;

        float score = PatrolTask.Score(
            entity,
            new[] { new PatrolRoute("R", PatrolRouteType.Perimeter, new PatrolWaypoint[] { new PatrolWaypoint(0, 0, 0, 1f) }) }
        );
        Assert.That(score, Is.EqualTo(0f));
    }

    // ================================================================
    // Pair 4: Vulture + Investigate
    // ================================================================

    [Test]
    public void Investigate_GatesOnActiveVulturePhase()
    {
        // If the bot is vulturing, investigate should score 0
        var entity = CreateEntity(1);
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 10;
        entity.VulturePhase = VulturePhase.Approach;

        float score = InvestigateTask.Score(entity, InvestigateTask.DefaultIntensityThreshold, InvestigateTask.DefaultDetectionRange);
        Assert.That(score, Is.EqualTo(0f), "Investigate should yield to active vulture phase");
    }

    [Test]
    public void Vulture_GatesOnActiveVulturePhase_MaintainsScore()
    {
        // Active vulture phase maintains MaxBaseScore regardless of event state
        var entity = CreateEntity(1);
        entity.VulturePhase = VulturePhase.HoldAmbush;
        entity.HasNearbyEvent = false; // event expired

        float score = VultureTask.Score(entity, VultureTask.DefaultCourageThreshold, VultureTask.DefaultDetectionRange);
        Assert.That(
            score,
            Is.EqualTo(VultureTask.MaxBaseScore),
            "Active vulture phase should maintain max score even without nearby event"
        );
    }

    [Test]
    public void LowIntensity_OnlyInvestigateQualifies()
    {
        // Intensity 10: investigate threshold=5 (qualifies), vulture threshold=15 (doesn't)
        var entity = CreateEntity(1);
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 10;
        entity.CurrentGameTime = 100f;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.NearbyEventX = 60f;
        entity.NearbyEventZ = 60f;

        float vultureScore = VultureTask.Score(entity, 15, VultureTask.DefaultDetectionRange);
        float investigateScore = InvestigateTask.Score(entity, 5, InvestigateTask.DefaultDetectionRange);

        Assert.That(vultureScore, Is.EqualTo(0f), "Vulture should not trigger at intensity 10 < 15");
        Assert.That(investigateScore, Is.GreaterThan(0f), "Investigate should trigger at intensity 10 >= 5");
    }

    [Test]
    public void HighIntensity_VultureBeatsInvestigate()
    {
        // Intensity 25: both qualify. Vulture MaxBaseScore=0.60 > Investigate MaxBaseScore=0.40
        var entity = CreateEntity(1);
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 25;
        entity.CurrentGameTime = 100f;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.NearbyEventX = 55f; // close event
        entity.NearbyEventZ = 55f;

        float vultureScore = VultureTask.Score(entity, 15, VultureTask.DefaultDetectionRange);
        float investigateScore = InvestigateTask.Score(entity, 5, InvestigateTask.DefaultDetectionRange);

        Assert.That(vultureScore, Is.GreaterThan(investigateScore), "Vulture should outscore investigate at high intensity");
    }

    [Test]
    public void InvestigatingBot_TransitionsToVulture_WhenIntensityRises()
    {
        // Bot is investigating (IsInvestigating=true, score=0.40)
        // Intensity rises above vulture threshold
        // Vulture task should win if score exceeds investigate + hysteresis
        var entity = CreateEntity(1);
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30; // way above vulture threshold
        entity.IsInvestigating = true;
        entity.CurrentGameTime = 100f;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.NearbyEventX = 51f; // very close
        entity.NearbyEventZ = 51f;
        entity.Aggression = 0.7f; // aggressive personality boosts vulture

        float investigateBase = InvestigateTask.Score(entity, 5, InvestigateTask.DefaultDetectionRange);
        float investigateModified =
            investigateBase * ScoringModifiers.CombinedModifier(entity.Aggression, entity.RaidTimeNormalized, BotActionTypeId.Investigate);
        float investigateWithHysteresis = investigateModified + 0.15f; // investigate hysteresis

        float vultureBase = VultureTask.Score(entity, 15, VultureTask.DefaultDetectionRange);
        float vultureModified =
            vultureBase * ScoringModifiers.CombinedModifier(entity.Aggression, entity.RaidTimeNormalized, BotActionTypeId.Vulture);

        Assert.That(
            vultureModified,
            Is.GreaterThan(investigateWithHysteresis),
            $"Vulture ({vultureModified:F3}) should beat investigate+hysteresis ({investigateWithHysteresis:F3}) at high intensity for aggressive bot"
        );
    }

    [Test]
    public void VultureComplete_AllowsNewInvestigation()
    {
        // After vulture phase completes, investigate should be able to trigger again
        var entity = CreateEntity(1);
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 8;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.NearbyEventX = 60f;
        entity.NearbyEventZ = 60f;

        float score = InvestigateTask.Score(entity, 5, InvestigateTask.DefaultDetectionRange);
        Assert.That(score, Is.GreaterThan(0f), "Investigate should be able to trigger after vulture Complete");
    }

    [Test]
    public void InCombat_BothVultureAndInvestigateScoreZero()
    {
        var entity = CreateEntity(1);
        entity.IsInCombat = true;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 30;

        Assert.That(VultureTask.Score(entity, 15, 150f), Is.EqualTo(0f));
        Assert.That(InvestigateTask.Score(entity, 5, 120f), Is.EqualTo(0f));
    }

    [Test]
    public void VultureCooldown_BlocksNewActivation()
    {
        var entity = CreateEntity(1);
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 25;
        entity.CurrentGameTime = 100f;
        entity.VultureCooldownUntil = 200f; // cooldown active

        float score = VultureTask.Score(entity, 15, 150f);
        Assert.That(score, Is.EqualTo(0f), "Vulture should not activate during cooldown");
    }

    // ================================================================
    // Pair 5: Squad Strategies + Loot
    // ================================================================

    [Test]
    public void FollowerTaskManager_Has6Tasks()
    {
        // SquadTaskFactory creates a manager with 6 tasks (2 tactical + 4 opportunistic)
        Assert.That(
            SquadTaskFactory.TaskCount,
            Is.EqualTo(6),
            "Follower task manager should have 6 tasks (GoTo + Hold tactical + Loot + Investigate + Linger + Patrol)"
        );
    }

    [Test]
    public void GoToTacticalPosition_OutscoresMaxLoot()
    {
        // GoToTacticalPositionTask.BaseScore=0.70 > LootTask.MaxBaseScore=0.55
        Assert.That(
            GoToTacticalPositionTask.BaseScore,
            Is.GreaterThan(LootTask.MaxBaseScore),
            "GoToTacticalPosition should always outscore loot"
        );
    }

    [Test]
    public void HoldTacticalPosition_OutscoresMaxLoot()
    {
        // HoldTacticalPositionTask.BaseScore=0.65 > LootTask.MaxBaseScore=0.55
        Assert.That(
            HoldTacticalPositionTask.BaseScore,
            Is.GreaterThan(LootTask.MaxBaseScore),
            "HoldTacticalPosition should always outscore loot"
        );
    }

    [Test]
    public void Follower_TacticalTask_BeatsLoot_WhenFarFromPosition()
    {
        // SquadTaskFactory now includes LootTask, but GoToTacticalPosition
        // should still win when the follower is far from tactical position
        // because GoToTactical base score (0.70) > LootTask max (0.55)
        var manager = SquadTaskFactory.Create();
        var entity = CreateEntity(1);
        entity.Boss = CreateEntity(0);
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 100f;
        entity.TacticalPositionZ = 200f;
        entity.HasLootTarget = true;
        entity.LootTargetValue = 50000f; // very valuable loot
        entity.InventorySpaceFree = 5f;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];

        manager.ScoreAndPick(entity);

        // GoToTacticalPosition score should still beat LootTask
        Assert.IsInstanceOf<GoToTacticalPositionTask>(
            entity.TaskAssignment.Task,
            "GoToTactical should outscore loot when follower is far from tactical position"
        );
    }

    [Test]
    public void Follower_AtTacticalPosition_Holds()
    {
        var entity = CreateEntity(1);
        entity.Boss = CreateEntity(0);
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 1f;
        entity.TacticalPositionY = 0f;
        entity.TacticalPositionZ = 1f;
        entity.CurrentPositionX = 1f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 1f;

        var goTo = new GoToTacticalPositionTask();
        var hold = new HoldTacticalPositionTask();
        var fManager = new UtilityTaskManager(new UtilityTask[] { goTo, hold });
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];

        fManager.ScoreAndPick(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(hold), "Follower at tactical position should hold");
    }

    // ================================================================
    // Pair 6: SpawnEntry + Combat/Healing Gates
    // ================================================================

    [Test]
    public void SpawnEntry_ScoresHigherThanAllOtherTasks()
    {
        // SpawnEntry MaxBaseScore=1.0 is higher than GoToObjective (0.65), Vulture (0.60), etc.
        Assert.That(SpawnEntryTask.MaxBaseScore, Is.GreaterThan(0.65f), "SpawnEntry > GoToObjective");
        Assert.That(SpawnEntryTask.MaxBaseScore, Is.GreaterThan(VultureTask.MaxBaseScore), "SpawnEntry > Vulture");
        Assert.That(SpawnEntryTask.MaxBaseScore, Is.GreaterThan(LootTask.MaxBaseScore), "SpawnEntry > Loot");
        Assert.That(SpawnEntryTask.MaxBaseScore, Is.GreaterThan(PatrolTask.MaxBaseScore), "SpawnEntry > Patrol");
    }

    [Test]
    public void SpawnEntry_NoPersonalityModifier()
    {
        // SpawnEntry does not apply personality/raid-time modifiers (it's a gating task)
        var entity = CreateEntity(1);
        entity.SpawnTime = 0f;
        entity.SpawnEntryDuration = 5f;
        entity.CurrentGameTime = 1f;
        entity.Aggression = 0f; // most timid
        entity.RaidTimeNormalized = 1f; // late raid

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore), "SpawnEntry should not be affected by personality/raid-time");
    }

    [Test]
    public void SpawnEntry_CompletesAfterDuration()
    {
        var entity = CreateEntity(1);
        entity.SpawnTime = 0f;
        entity.SpawnEntryDuration = 3f;
        entity.CurrentGameTime = 5f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
        Assert.That(entity.IsSpawnEntryComplete, Is.True);
    }

    [Test]
    public void SpawnEntry_OnceComplete_NeverScoresAgain()
    {
        var entity = CreateEntity(1);
        entity.IsSpawnEntryComplete = true;
        entity.SpawnEntryDuration = 5f;
        entity.CurrentGameTime = 0f; // even at spawn time

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "Completed spawn entry should never score again");
    }

    [Test]
    public void SpawnEntry_WithCombatGate_CombatTakesPriority()
    {
        // In the real system, combat is checked by BotQuestingDecisionMonitor
        // BEFORE the layer reaches utility AI scoring. If the bot is in combat,
        // the questing layer deactivates entirely and SpawnEntry never scores.
        // This is correct: combat should override spawn entry scanning.

        // Verify that SpawnEntry scores normally when NOT in combat
        var entity = CreateEntity(1);
        entity.SpawnTime = 0f;
        entity.SpawnEntryDuration = 5f;
        entity.CurrentGameTime = 1f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(SpawnEntryTask.MaxBaseScore));

        // SpawnEntry doesn't check IsInCombat itself (combat gate is at the layer level)
        entity.IsInCombat = true;
        float combatScore = SpawnEntryTask.Score(entity);
        Assert.That(
            combatScore,
            Is.EqualTo(SpawnEntryTask.MaxBaseScore),
            "SpawnEntry scorer doesn't gate on combat — that's the layer's job"
        );
    }

    [Test]
    public void SpawnEntry_DisabledWhenDurationZero()
    {
        var entity = CreateEntity(1);
        entity.SpawnTime = 0f;
        entity.SpawnEntryDuration = 0f;
        entity.CurrentGameTime = 0f;

        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ================================================================
    // Cross-manager switch: Quest <-> Follower
    // ================================================================

    [Test]
    public void QuestToFollowerSwitch_ClearsStaleAssignment()
    {
        // When switching from quest manager (14 tasks) to follower manager (2 tasks),
        // the stale TaskAssignment must be cleared to prevent ordinal out-of-range
        var entity = CreateEntity(1);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = 1; // MoveToPosition

        // Quest manager assigns GoToObjective
        var questManager = QuestTaskFactory.Create();
        questManager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null);
        int questOrdinal = entity.TaskAssignment.Ordinal;

        // Now bot becomes follower — we need to clear and resize
        entity.Boss = CreateEntity(0);
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 100f;
        entity.TacticalPositionZ = 100f;

        // Clear stale assignment (what BotObjectiveLayer does)
        entity.TaskAssignment.Task.Deactivate(entity);
        entity.TaskAssignment = default;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];

        // Follower manager scores and picks
        var followerManager = SquadTaskFactory.Create();
        followerManager.ScoreAndPick(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Not.Null);
        Assert.That(
            entity.TaskAssignment.Ordinal,
            Is.LessThan(SquadTaskFactory.TaskCount),
            "Follower task ordinal should be within follower task count"
        );
    }

    [Test]
    public void FollowerToQuestSwitch_ClearsStaleAssignment()
    {
        var entity = CreateEntity(1);
        entity.Boss = CreateEntity(0);
        entity.HasTacticalPosition = true;
        entity.TacticalPositionX = 100f;
        entity.TacticalPositionZ = 100f;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];

        // Follower manager assigns GoToTacticalPosition
        var followerManager = SquadTaskFactory.Create();
        followerManager.ScoreAndPick(entity);
        Assert.That(entity.TaskAssignment.Task, Is.Not.Null);

        // Bot's boss dies — becomes solo questor
        entity.Boss = null;
        entity.HasTacticalPosition = false;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = 1;

        // Clear stale assignment
        entity.TaskAssignment.Task.Deactivate(entity);
        entity.TaskAssignment = default;
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        // Quest manager scores and picks
        var questManager = QuestTaskFactory.Create();
        questManager.ScoreAndPick(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Not.Null);
    }
}
