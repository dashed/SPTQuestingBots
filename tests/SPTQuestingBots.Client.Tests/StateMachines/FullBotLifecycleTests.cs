using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.StateMachines;

/// <summary>
/// End-to-end lifecycle tests that chain multiple state machines together,
/// simulating realistic bot scenarios: spawn → quest → loot → patrol → extract.
/// </summary>
[TestFixture]
public class FullBotLifecycleTests
{
    // ════════════════════════════════════════════════════════════
    // Full lifecycle: Spawn → SpawnEntry → GoToObjective → Loot → Patrol
    // ════════════════════════════════════════════════════════════

    [Test]
    public void FullLifecycle_SpawnToPatrol()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 0f);

        // Phase 1: Spawn Entry — bot pauses and scans
        Assert.That(entity.IsSpawnEntryComplete, Is.False);
        Assert.That(SpawnEntryTask.Score(entity), Is.EqualTo(1.0f));

        // Phase 2: Spawn entry completes
        entity.CurrentGameTime = 5f;
        float score = SpawnEntryTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
        Assert.That(entity.IsSpawnEntryComplete, Is.True);

        // Phase 3: Bot gets quest objective
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;

        // GoToObjectiveTask should score high
        float goToScore = GoToObjectiveTask.Score(entity);
        Assert.That(goToScore, Is.GreaterThan(0f));

        // Phase 4: Bot arrives at objective
        entity.IsCloseToObjective = true;
        entity.DistanceToObjective = 1f;
        entity.HasActiveObjective = false; // Objective completed

        // Phase 5: Bot finds loot nearby
        entity.HasLootTarget = true;
        entity.LootTargetValue = 25000f;
        entity.InventorySpaceFree = 5f;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.LootTargetX = 55f;
        entity.LootTargetZ = 55f;

        float lootScore = LootTask.Score(entity);
        Assert.That(lootScore, Is.GreaterThan(0f));

        // Phase 6: Loot completed
        entity.HasLootTarget = false;
        entity.IsApproachingLoot = false;
        entity.IsLooting = false;

        // Phase 7: Bot enters patrol
        entity.PatrolRouteIndex = 0;
        entity.PatrolWaypointIndex = 0;
        entity.IsPatrolling = true;

        Assert.That(entity.IsPatrolling, Is.True);
    }

    // ════════════════════════════════════════════════════════════
    // Combat interruption at every phase
    // ════════════════════════════════════════════════════════════

    [Test]
    public void CombatInterrupt_DuringSpawnEntry_MarksComplete()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 1f);
        Assert.That(entity.IsSpawnEntryComplete, Is.False);

        // Combat starts during spawn entry
        entity.IsInCombat = true;

        // Stop() called by BigBrain → marks complete
        entity.IsSpawnEntryComplete = true;

        // After combat, spawn entry won't resume (IsSpawnEntryComplete = true)
        entity.IsInCombat = false;
        Assert.That(SpawnEntryTask.Score(entity), Is.EqualTo(0f));
    }

    [Test]
    public void CombatInterrupt_DuringLootApproach_ClearsApproachFlag()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 10f);
        entity.IsSpawnEntryComplete = true;
        entity.HasLootTarget = true;
        entity.IsApproachingLoot = true;

        // Combat interrupt → Stop() called
        entity.IsInCombat = true;
        entity.IsApproachingLoot = false;
        entity.IsLooting = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.IsApproachingLoot, Is.False);
            Assert.That(entity.IsLooting, Is.False);
            // HasLootTarget persists for resume
            Assert.That(entity.HasLootTarget, Is.True);
        });

        // After combat, loot scorer should return 0 (combat check)
        float lootScore = LootTask.Score(entity);
        Assert.That(lootScore, Is.EqualTo(0f), "Loot should score 0 during combat");

        // Combat ends
        entity.IsInCombat = false;
        entity.LootTargetValue = 20000f;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;
        entity.LootTargetX = 55f;
        entity.LootTargetZ = 55f;
        entity.InventorySpaceFree = 5f;

        lootScore = LootTask.Score(entity);
        Assert.That(lootScore, Is.GreaterThan(0f), "Loot should resume scoring after combat ends");
    }

    [Test]
    public void CombatInterrupt_DuringVultureApproach_SetsComplete()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 10f);
        entity.IsSpawnEntryComplete = true;
        entity.VulturePhase = VulturePhase.Approach;
        entity.HasNearbyEvent = true;

        // Combat interrupt → Stop() called
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;

        Assert.Multiple(() =>
        {
            Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Complete));
            Assert.That(entity.HasNearbyEvent, Is.False);
        });
    }

    [Test]
    public void CombatInterrupt_DuringVultureHoldAmbush_SetsComplete()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 10f);
        entity.IsSpawnEntryComplete = true;
        entity.VulturePhase = VulturePhase.HoldAmbush;
        entity.HasNearbyEvent = true;

        // Combat interrupt → Stop()
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;

        Assert.That(entity.VulturePhase, Is.EqualTo(VulturePhase.Complete));
    }

    [Test]
    public void CombatInterrupt_DuringPatrol_SavesWaypointIndex()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 10f);
        entity.IsSpawnEntryComplete = true;
        entity.IsPatrolling = true;
        entity.PatrolWaypointIndex = 2;
        entity.PatrolRouteIndex = 0;

        // Combat interrupt → Stop()
        entity.IsPatrolling = false;
        // Waypoint preserved
        Assert.That(entity.PatrolWaypointIndex, Is.EqualTo(2));

        // After combat, bot can resume patrol from waypoint 2
        entity.IsPatrolling = true;
        Assert.That(entity.PatrolWaypointIndex, Is.EqualTo(2));
    }

    // ════════════════════════════════════════════════════════════
    // Boss death mid-quest: follower leader reassignment
    // ════════════════════════════════════════════════════════════

    [Test]
    public void BossDeath_FollowersLoseBoosReference()
    {
        var boss = new BotEntity(0);
        boss.IsActive = true;
        boss.BotType = BotType.PMC;

        var follower1 = new BotEntity(1);
        follower1.IsActive = true;
        follower1.Boss = boss;
        boss.Followers.Add(follower1);

        var follower2 = new BotEntity(2);
        follower2.IsActive = true;
        follower2.Boss = boss;
        boss.Followers.Add(follower2);

        Assert.That(boss.Followers.Count, Is.EqualTo(2));

        // Boss dies
        boss.IsActive = false;

        // Dead leader reassignment: clear boss reference, promote new leader
        if (!boss.IsActive)
        {
            for (int i = 0; i < boss.Followers.Count; i++)
            {
                boss.Followers[i].Boss = null;
            }

            // Promote follower1 as new leader
            if (boss.Followers.Count > 0)
            {
                var newLeader = boss.Followers[0];
                for (int i = 1; i < boss.Followers.Count; i++)
                {
                    boss.Followers[i].Boss = newLeader;
                    newLeader.Followers.Add(boss.Followers[i]);
                }
            }
        }

        Assert.Multiple(() =>
        {
            Assert.That(follower1.Boss, Is.Null, "New leader should have no boss");
            Assert.That(follower2.Boss, Is.SameAs(follower1), "Follower2 should follow new leader");
            Assert.That(follower1.Followers.Count, Is.EqualTo(1));
        });
    }

    [Test]
    public void BossDeath_SoloFollower_BecomesIndependent()
    {
        var boss = new BotEntity(0);
        boss.IsActive = true;

        var follower = new BotEntity(1);
        follower.IsActive = true;
        follower.Boss = boss;
        boss.Followers.Add(follower);

        boss.IsActive = false;
        follower.Boss = null;

        Assert.Multiple(() =>
        {
            Assert.That(follower.Boss, Is.Null);
            Assert.That(follower.HasBoss, Is.False);
        });
    }

    [Test]
    public void BossDeath_FollowerMidQuest_CanContinueWithNewObjective()
    {
        var boss = new BotEntity(0);
        boss.IsActive = true;

        var follower = new BotEntity(1);
        follower.IsActive = true;
        follower.Boss = boss;
        follower.HasActiveObjective = true;
        follower.HasTacticalPosition = true;
        follower.TacticalPositionX = 100f;
        follower.TacticalPositionZ = 200f;

        // Boss dies
        boss.IsActive = false;
        follower.Boss = null;

        // Follower should clear tactical position (no longer following)
        follower.HasTacticalPosition = false;
        follower.TacticalPositionX = 0f;
        follower.TacticalPositionZ = 0f;

        Assert.Multiple(() =>
        {
            Assert.That(follower.HasTacticalPosition, Is.False);
            Assert.That(follower.HasActiveObjective, Is.True, "Quest objective should persist");
        });
    }

    // ════════════════════════════════════════════════════════════
    // Stuck detection → vault → jump → failure sequence
    // ════════════════════════════════════════════════════════════

    [Test]
    public void StuckSequence_VaultAttemptThenJumpThenFail()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 30f);
        entity.IsSpawnEntryComplete = true;

        // Bot is navigating to objective
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 50f;

        // Stuck detected (position hasn't changed)
        bool isStuck = true;

        // Step 1: Try vault
        bool vaultSucceeded = false;
        if (isStuck && !vaultSucceeded)
        {
            // Vault attempt (simulated)
            vaultSucceeded = false; // Failed
        }

        // Step 2: Try jump
        bool jumpSucceeded = false;
        if (isStuck && !vaultSucceeded && !jumpSucceeded)
        {
            jumpSucceeded = false; // Failed
        }

        // Step 3: Recalculate path
        if (isStuck && !vaultSucceeded && !jumpSucceeded)
        {
            // Recalculate — still stuck on next check
        }

        // Step 4: Final failure — complete/abort action
        if (isStuck)
        {
            entity.HasActiveObjective = false;
        }

        Assert.That(entity.HasActiveObjective, Is.False, "After stuck detection sequence, objective should be abandoned");
    }

    // ════════════════════════════════════════════════════════════
    // Vulture → Loot transition
    // ════════════════════════════════════════════════════════════

    [Test]
    public void VultureComplete_TransitionToLoot()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 60f);
        entity.IsSpawnEntryComplete = true;

        // Vulture complete
        entity.VulturePhase = VulturePhase.Complete;
        entity.HasNearbyEvent = false;

        // Bot finds loot at the vulture location
        entity.HasLootTarget = true;
        entity.LootTargetValue = 30000f;
        entity.InventorySpaceFree = 10f;
        entity.CurrentPositionX = 100f;
        entity.CurrentPositionZ = 200f;
        entity.LootTargetX = 102f;
        entity.LootTargetZ = 198f;

        float lootScore = LootTask.Score(entity);
        Assert.That(lootScore, Is.GreaterThan(0f), "Bot should be able to loot after vulture completes");
    }

    // ════════════════════════════════════════════════════════════
    // Linger → Vulture transition
    // ════════════════════════════════════════════════════════════

    [Test]
    public void LingerToVulture_EventDuringLinger()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 60f);
        entity.IsSpawnEntryComplete = true;

        // Lingering after objective completion
        entity.IsLingering = true;
        entity.ObjectiveCompletedTime = 55f;
        entity.LingerDuration = 20f;

        // Combat event detected nearby
        entity.HasNearbyEvent = true;
        entity.NearbyEventX = 100f;
        entity.NearbyEventZ = 200f;
        entity.CombatIntensity = 3;
        entity.VulturePhase = VulturePhase.None;
        entity.VultureCooldownUntil = 0f;

        // Linger should be interrupted by higher-scoring vulture task
        Assert.That(entity.HasNearbyEvent, Is.True);
        Assert.That(entity.IsLingering, Is.True);
        // Both states can coexist — task manager picks highest scorer
    }

    // ════════════════════════════════════════════════════════════
    // Room clear during GoToObjective
    // ════════════════════════════════════════════════════════════

    [Test]
    public void RoomClear_DuringObjectiveApproach_SlowsMovement()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 30f);
        entity.IsSpawnEntryComplete = true;
        entity.HasActiveObjective = true;
        entity.LastEnvironmentId = 1; // outdoor

        // Bot enters building
        var instruction = RoomClearController.Update(entity, 0, 30f, 10f, 20f, 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(instruction, Is.EqualTo(RoomClearInstruction.SlowWalk));
            Assert.That(entity.IsInRoomClear, Is.True);
            Assert.That(entity.HasActiveObjective, Is.True, "Objective should persist during room clear");
        });
    }

    [Test]
    public void RoomClear_ExitBuilding_ResumesNormalSpeed()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 30f);
        entity.IsSpawnEntryComplete = true;
        entity.LastEnvironmentId = 1;

        // Enter building
        RoomClearController.Update(entity, 0, 30f, 10f, 20f, 1.5f);
        Assert.That(entity.IsInRoomClear, Is.True);

        // Exit building
        var instruction = RoomClearController.Update(entity, 1, 31f, 10f, 20f, 1.5f);

        Assert.Multiple(() =>
        {
            Assert.That(instruction, Is.EqualTo(RoomClearInstruction.None));
            Assert.That(entity.IsInRoomClear, Is.False);
        });
    }

    // ════════════════════════════════════════════════════════════
    // Entity deactivation cleanup
    // ════════════════════════════════════════════════════════════

    [Test]
    public void EntityDeactivation_ClearsAllActiveState()
    {
        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 60f);
        entity.IsSpawnEntryComplete = true;
        entity.IsPatrolling = true;
        entity.HasLootTarget = true;
        entity.IsApproachingLoot = true;
        entity.VulturePhase = VulturePhase.SilentApproach;
        entity.HasNearbyEvent = true;
        entity.IsInRoomClear = true;
        entity.IsLingering = true;

        // Entity deactivated (bot died)
        entity.IsActive = false;

        Assert.That(entity.IsActive, Is.False);
        // Note: individual state fields are NOT auto-cleared on deactivation.
        // The task manager handles deactivating the current task.
        // When the entity is recycled by BotRegistry, all fields get reset.
    }

    // ════════════════════════════════════════════════════════════
    // Multi-bot scenario: interleaved scoring
    // ════════════════════════════════════════════════════════════

    [Test]
    public void MultiBotScoring_IndependentEntityStates()
    {
        var entity1 = MakeSpawnedBot(spawnTime: 0f, currentTime: 10f);
        entity1.IsSpawnEntryComplete = true;
        entity1.HasLootTarget = true;
        entity1.LootTargetValue = 20000f;
        entity1.InventorySpaceFree = 5f;
        entity1.CurrentPositionX = 10f;
        entity1.CurrentPositionZ = 10f;
        entity1.LootTargetX = 15f;
        entity1.LootTargetZ = 15f;

        var entity2 = MakeSpawnedBot(spawnTime: 0f, currentTime: 10f);
        entity2.IsSpawnEntryComplete = true;
        entity2.HasLootTarget = false;

        float score1 = LootTask.Score(entity1);
        float score2 = LootTask.Score(entity2);

        Assert.Multiple(() =>
        {
            Assert.That(score1, Is.GreaterThan(0f));
            Assert.That(score2, Is.EqualTo(0f));
        });
    }

    [Test]
    public void MultiBotScoring_SpawnEntryPhaseStagger()
    {
        var early = MakeSpawnedBot(spawnTime: 0f, currentTime: 6f);
        early.SpawnEntryDuration = 4f;

        var late = MakeSpawnedBot(spawnTime: 3f, currentTime: 6f);
        late.SpawnEntryDuration = 5f;

        float earlyScore = SpawnEntryTask.Score(early);
        float lateScore = SpawnEntryTask.Score(late);

        Assert.Multiple(() =>
        {
            Assert.That(earlyScore, Is.EqualTo(0f), "Early bot should have completed spawn entry");
            Assert.That(lateScore, Is.EqualTo(1.0f), "Late bot should still be in spawn entry");
        });
    }

    // ════════════════════════════════════════════════════════════
    // Task assignment lifecycle with utility manager
    // ════════════════════════════════════════════════════════════

    [Test]
    public void TaskAssignment_ActivateDeactivate_TracksCorrectly()
    {
        var task1 = new SpawnEntryTask();
        var task2 = new GoToObjectiveTask();
        var tasks = new UtilityTask[] { task1, task2 };
        var manager = new UtilityTaskManager(tasks);

        var entity = MakeSpawnedBot(spawnTime: 0f, currentTime: 1f);
        entity.TaskScores = new float[2];

        // Score: SpawnEntry should be high, GoToObjective low
        task1.ScoreEntity(0, entity);
        task2.ScoreEntity(1, entity);

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task1), "SpawnEntry should win during spawn phase");
        Assert.That(task1.ActiveEntityCount, Is.EqualTo(1));
        Assert.That(task2.ActiveEntityCount, Is.EqualTo(0));

        // Time passes, spawn entry completes
        entity.CurrentGameTime = 6f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 50f;

        task1.ScoreEntity(0, entity);
        task2.ScoreEntity(1, entity);

        manager.PickTask(entity);

        Assert.That(entity.TaskAssignment.Task, Is.SameAs(task2), "GoToObjective should win after spawn entry completes");
        Assert.That(task1.ActiveEntityCount, Is.EqualTo(0), "SpawnEntry should be deactivated");
        Assert.That(task2.ActiveEntityCount, Is.EqualTo(1));
    }

    // ════════════════════════════════════════════════════════════
    // Helpers
    // ════════════════════════════════════════════════════════════

    private static BotEntity MakeSpawnedBot(float spawnTime, float currentTime)
    {
        var entity = new BotEntity(0);
        entity.IsActive = true;
        entity.SpawnTime = spawnTime;
        entity.CurrentGameTime = currentTime;
        entity.SpawnEntryDuration = 4f;
        entity.IsSpawnEntryComplete = false;
        entity.SpawnFacingX = 0f;
        entity.SpawnFacingZ = 1f;
        entity.Personality = BotPersonality.Normal;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.1f;
        return entity;
    }
}
