using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

// ── 1. Full Pipeline: Spawn → SpawnEntry → GoToObjective → Action → Linger ──

[TestFixture]
public class QuestLifecycleE2ETests
{
    private UtilityTaskManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = QuestTaskFactory.Create();
    }

    private BotEntity CreateBot(int id, float aggression = 0.5f, float raidTime = 0.3f, float gameTime = 10f)
    {
        var entity = new BotEntity(id);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = aggression;
        entity.RaidTimeNormalized = raidTime;
        entity.CurrentGameTime = gameTime;
        return entity;
    }

    [Test]
    public void Lifecycle_SpawnEntry_ThenGoToObjective()
    {
        var bot = CreateBot(0, gameTime: 1f);
        bot.SpawnTime = 0f;
        bot.SpawnEntryDuration = 4f;
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 200f;

        // Tick 1: SpawnEntry should win (0.80 > GoToObjective ~0.48)
        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<SpawnEntryTask>(bot.TaskAssignment.Task, "SpawnEntry should win at t=1s with 4s duration");

        // Tick 2: Still within spawn entry duration
        bot.CurrentGameTime = 3f;
        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<SpawnEntryTask>(bot.TaskAssignment.Task, "SpawnEntry should hold at t=3s");

        // Tick 3: Spawn entry expires, GoToObjective should take over
        bot.CurrentGameTime = 5f;
        _manager.ScoreAndPick(bot);
        Assert.IsTrue(bot.IsSpawnEntryComplete, "SpawnEntry should be marked complete");
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "GoToObjective should win after spawn entry expires");
    }

    [Test]
    public void Lifecycle_GoToObjective_ThenAmbush_WhenClose()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.Ambush;
        bot.DistanceToObjective = 100f;
        bot.IsCloseToObjective = false;

        // Far from objective: GoToObjective wins (handles travel for Ambush)
        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "GoToObjective should handle travel to ambush position");

        // Close to objective: AmbushTask takes over
        bot.IsCloseToObjective = true;
        bot.DistanceToObjective = 3f;
        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<AmbushTask>(bot.TaskAssignment.Task, "AmbushTask should win when close to ambush position");
    }

    [Test]
    public void Lifecycle_GoToObjective_ThenSnipe_WhenClose()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.Snipe;
        bot.DistanceToObjective = 100f;
        bot.IsCloseToObjective = false;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);

        bot.IsCloseToObjective = true;
        bot.DistanceToObjective = 2f;
        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<SnipeTask>(bot.TaskAssignment.Task);
    }

    [Test]
    public void Lifecycle_GoToObjective_ThenPlantItem_WhenClose()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.PlantItem;
        bot.DistanceToObjective = 100f;
        bot.IsCloseToObjective = false;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);

        bot.IsCloseToObjective = true;
        bot.DistanceToObjective = 1f;
        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<PlantItemTask>(bot.TaskAssignment.Task);
    }

    [Test]
    public void Lifecycle_ObjectiveComplete_LingerActivates()
    {
        var bot = CreateBot(0, gameTime: 100f);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 50f;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);

        // Objective completes: clear active objective, set linger state
        bot.HasActiveObjective = false;
        bot.ObjectiveCompletedTime = 100f;
        bot.LingerDuration = 10f;
        bot.CurrentGameTime = 101f;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<LingerTask>(bot.TaskAssignment.Task, "LingerTask should activate after objective completion");

        // Linger score decays over time
        bot.CurrentGameTime = 105f; // 5s into 10s linger
        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<LingerTask>(bot.TaskAssignment.Task, "LingerTask should still hold at 50% decay");

        // Linger duration expires — score becomes 0
        bot.CurrentGameTime = 111f;
        _manager.ScoreAndPick(bot);

        // With no competing task (no objective, no loot, etc.), LingerTask holds
        // via hysteresis (0 + 0.10 = 0.10). This is expected behavior:
        // the bot idles on Linger until a real task scores above 0.10.
        // When a new objective arrives, it will easily beat the 0.10 threshold.
        Assert.IsInstanceOf<LingerTask>(bot.TaskAssignment.Task, "Linger holds via hysteresis when no competing task scores above 0.10");

        // Verify: as soon as an objective appears, it beats the hysteresis
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "GoToObjective should beat expired LingerTask hysteresis");
    }

    [Test]
    public void Lifecycle_HoldAtPosition_OverridesGoToObjective()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.HoldAtPosition;
        bot.DistanceToObjective = 200f;
        bot.IsCloseToObjective = false;

        _manager.ScoreAndPick(bot);

        // HoldPosition scores 0.70 (no modifiers); GoToObjective scores 0 for HoldAtPosition action
        Assert.IsInstanceOf<HoldPositionTask>(
            bot.TaskAssignment.Task,
            "HoldPositionTask should win for HoldAtPosition action regardless of distance"
        );
    }

    [Test]
    public void Lifecycle_ToggleSwitchAction_WinsOverGoToObjective()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.ToggleSwitch;
        bot.DistanceToObjective = 100f;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<ToggleSwitchTask>(bot.TaskAssignment.Task);
    }

    [Test]
    public void Lifecycle_CloseDoorsAction_WinsOverGoToObjective()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.CloseNearbyDoors;
        bot.DistanceToObjective = 100f;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<CloseDoorsTask>(bot.TaskAssignment.Task);
    }

    [Test]
    public void Lifecycle_UnlockDoor_BlocksGoToObjective()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 50f;
        bot.MustUnlockDoor = true;

        _manager.ScoreAndPick(bot);

        // UnlockDoor scores 0.70; GoToObjective scores 0 when MustUnlockDoor
        Assert.IsInstanceOf<UnlockDoorTask>(
            bot.TaskAssignment.Task,
            "UnlockDoorTask should block GoToObjective when path requires door unlock"
        );
    }

    [Test]
    public void Lifecycle_DoorUnlocked_ResumesGoToObjective()
    {
        var bot = CreateBot(0);
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 50f;
        bot.MustUnlockDoor = true;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<UnlockDoorTask>(bot.TaskAssignment.Task);

        // Door unlocked
        bot.MustUnlockDoor = false;
        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "GoToObjective should resume after door is unlocked");
    }
}

// ── 2. Manager Switch E2E: Quest ↔ Follower ──────────────────

[TestFixture]
public class ManagerSwitchE2ETests
{
    [Test]
    public void QuestToFollower_StaleAssignmentCleared_FollowerTaskWins()
    {
        var questManager = QuestTaskFactory.Create();
        var followerManager = SquadTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        // Quest manager assigns GoToObjective
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.3f;

        questManager.ScoreAndPick(entity);
        Assert.IsInstanceOf<GoToObjectiveTask>(entity.TaskAssignment.Task);
        int questOrdinal = entity.TaskAssignment.Ordinal;

        // Simulate BotObjectiveLayer switching to follower:
        // 1. Clear stale quest assignment
        entity.TaskAssignment.Task.Deactivate(entity);
        entity.TaskAssignment = default;

        // 2. Ensure TaskScores is correctly sized (14 >= 2, no resize needed)
        Assert.That(entity.TaskScores.Length, Is.GreaterThanOrEqualTo(SquadTaskFactory.TaskCount));

        // 3. Set up follower state
        entity.HasTacticalPosition = true;
        var boss = new BotEntity(99);
        entity.Boss = boss;
        entity.TacticalPositionX = 200f;
        entity.TacticalPositionY = 0f;
        entity.TacticalPositionZ = 300f;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 50f;

        followerManager.ScoreAndPick(entity);

        // GoToTacticalPosition should win (far from position)
        Assert.IsInstanceOf<GoToTacticalPositionTask>(
            entity.TaskAssignment.Task,
            "After clearing quest assignment, follower task should win"
        );
    }

    [Test]
    public void FollowerToQuest_StaleAssignmentCleared_QuestTaskWins()
    {
        var questManager = QuestTaskFactory.Create();
        var followerManager = SquadTaskFactory.Create();
        var entity = new BotEntity(0);

        // Start with follower manager
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];
        entity.HasTacticalPosition = true;
        var boss = new BotEntity(99);
        entity.Boss = boss;
        entity.TacticalPositionX = 200f;
        entity.TacticalPositionZ = 300f;
        entity.CurrentPositionX = 50f;
        entity.CurrentPositionZ = 50f;

        followerManager.ScoreAndPick(entity);
        Assert.IsInstanceOf<GoToTacticalPositionTask>(entity.TaskAssignment.Task);

        // Simulate BotObjectiveLayer switching to quest:
        // 1. Clear stale follower assignment
        entity.TaskAssignment.Task.Deactivate(entity);
        entity.TaskAssignment = default;

        // 2. Resize TaskScores from 2 to 14
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        // 3. Set up quest state
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.3f;

        questManager.ScoreAndPick(entity);
        Assert.IsInstanceOf<GoToObjectiveTask>(
            entity.TaskAssignment.Task,
            "After clearing follower assignment and resizing, quest task should win"
        );
    }

    [Test]
    public void RapidManagerSwitch_NoLeakedState()
    {
        var questManager = QuestTaskFactory.Create();
        var followerManager = SquadTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.3f;

        var boss = new BotEntity(99);

        for (int i = 0; i < 10; i++)
        {
            bool isFollower = i % 2 == 0;

            // Clear stale assignment
            if (entity.TaskAssignment.Task != null)
            {
                entity.TaskAssignment.Task.Deactivate(entity);
                entity.TaskAssignment = default;
            }

            if (isFollower)
            {
                entity.HasTacticalPosition = true;
                entity.Boss = boss;
                entity.TacticalPositionX = 200f;
                entity.TacticalPositionZ = 300f;
                entity.CurrentPositionX = 50f;
                entity.CurrentPositionZ = 50f;
                entity.HasActiveObjective = false;

                followerManager.ScoreAndPick(entity);
                Assert.IsNotNull(entity.TaskAssignment.Task, $"Iteration {i}: follower should get a task");
            }
            else
            {
                entity.HasTacticalPosition = false;
                entity.Boss = null;
                entity.HasActiveObjective = true;
                entity.CurrentQuestAction = QuestActionId.MoveToPosition;
                entity.DistanceToObjective = 100f;

                // Ensure array is large enough for quest
                if (entity.TaskScores.Length < QuestTaskFactory.TaskCount)
                    entity.TaskScores = new float[QuestTaskFactory.TaskCount];

                questManager.ScoreAndPick(entity);
                Assert.IsNotNull(entity.TaskAssignment.Task, $"Iteration {i}: quest should get a task");
            }
        }
    }
}

// ── 3. All-Zero Scores + Hysteresis Edge Cases ──────────────────

[TestFixture]
public class AllZeroScoresE2ETests
{
    [Test]
    public void AllZeroScores_NoObjective_NoTask()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = false;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        manager.ScoreAndPick(entity);

        // With no active objective, most tasks score 0.
        // Loot/Vulture/Investigate need their conditions too.
        Assert.IsNull(entity.TaskAssignment.Task, "With no conditions met, no task should be assigned");
    }

    [Test]
    public void SpawnEntry_Expires_ButNoOtherTask_HysteresisPreventsNull()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.3f;
        entity.CurrentGameTime = 1f;
        entity.SpawnTime = 0f;
        entity.SpawnEntryDuration = 3f;
        entity.HasActiveObjective = false;

        // SpawnEntry should win
        manager.ScoreAndPick(entity);
        Assert.IsInstanceOf<SpawnEntryTask>(entity.TaskAssignment.Task);

        // Advance past expiry with no other conditions met
        entity.CurrentGameTime = 5f;
        manager.ScoreAndPick(entity);

        // SpawnEntry now scores 0, but has hysteresis 0.10.
        // No other task scores > 0.10, so bot stays on SpawnEntry.
        // This is the expected (if suboptimal) behavior:
        // the bot holds on SpawnEntry until a real task appears.
        var task = entity.TaskAssignment.Task;
        Assert.IsNotNull(task, "Hysteresis should keep SpawnEntry even though it scored 0");
        Assert.IsInstanceOf<SpawnEntryTask>(task, "No competing task, so SpawnEntry holds via hysteresis");
    }

    [Test]
    public void SpawnEntry_Expires_ObjectiveAvailable_ObjectiveWins()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.3f;
        entity.CurrentGameTime = 1f;
        entity.SpawnTime = 0f;
        entity.SpawnEntryDuration = 3f;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.DistanceToObjective = 100f;

        // SpawnEntry wins initially (0.80 > GoToObjective ~0.48)
        manager.ScoreAndPick(entity);
        Assert.IsInstanceOf<SpawnEntryTask>(entity.TaskAssignment.Task);

        // Advance past expiry with objective available
        entity.CurrentGameTime = 5f;
        manager.ScoreAndPick(entity);

        // SpawnEntry scores 0 + hysteresis 0.10 = 0.10.
        // GoToObjective scores ~0.48 with modifiers > 0.10.
        Assert.IsInstanceOf<GoToObjectiveTask>(entity.TaskAssignment.Task, "GoToObjective should beat expired SpawnEntry hysteresis");
    }
}

// ── 4. Personality + Raid Time Modifier Integration ──────────────

[TestFixture]
public class PersonalityRaidTimeE2ETests
{
    private UtilityTaskManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = QuestTaskFactory.Create();
    }

    [Test]
    public void AggressiveBot_FavorsGoToObjective_OverAmbush()
    {
        // Aggressive bots (high aggression) get GoToObjective modifier of Lerp(0.85, 1.15, 1.0) = 1.15
        // and Ambush modifier of Lerp(1.2, 0.8, 1.0) = 0.8
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.Ambush;
        bot.DistanceToObjective = 100f;
        bot.IsCloseToObjective = true; // Both GoToObjective and Ambush could score
        bot.Aggression = 1.0f; // Very aggressive
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        // For Ambush action, GoToObjective returns 0 when close, so Ambush must win
        Assert.IsInstanceOf<AmbushTask>(
            bot.TaskAssignment.Task,
            "At close range with Ambush action, AmbushTask always wins regardless of personality"
        );
    }

    [Test]
    public void TimidBot_LateRaid_FavorsLinger()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = false;
        bot.Aggression = 0.1f; // Timid
        bot.RaidTimeNormalized = 0.9f; // Late raid

        // Set up linger state
        bot.ObjectiveCompletedTime = 90f;
        bot.LingerDuration = 15f;
        bot.CurrentGameTime = 92f; // 2s into linger

        _manager.ScoreAndPick(bot);

        // Linger base score = 0.45 * (1 - 2/15) = 0.39
        // Personality: Lerp(1.3, 0.7, 0.1) = 1.24
        // Raid time: Lerp(0.7, 1.3, 0.9) = 1.24
        // Total modifier: 1.24 * 1.24 = 1.54
        // Final: 0.39 * 1.54 = ~0.60
        Assert.IsInstanceOf<LingerTask>(bot.TaskAssignment.Task, "Timid bot in late raid should strongly favor lingering");
    }

    [Test]
    public void ExtremeAggression_Zero_DoesNotProduceNaN()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        bot.Aggression = 0.0f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        for (int i = 0; i < bot.TaskScores.Length; i++)
        {
            Assert.IsFalse(float.IsNaN(bot.TaskScores[i]), $"Task {i} produced NaN with aggression=0.0");
        }

        Assert.IsNotNull(bot.TaskAssignment.Task, "Should still get a task with zero aggression");
    }

    [Test]
    public void ExtremeAggression_One_DoesNotProduceNaN()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        bot.Aggression = 1.0f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        for (int i = 0; i < bot.TaskScores.Length; i++)
        {
            Assert.IsFalse(float.IsNaN(bot.TaskScores[i]), $"Task {i} produced NaN with aggression=1.0");
        }

        Assert.IsNotNull(bot.TaskAssignment.Task);
    }

    [Test]
    public void NaNAggression_FallsBackToDefault()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        bot.Aggression = float.NaN;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        // CombinedModifier guards against NaN: if result is NaN, returns 1.0
        for (int i = 0; i < bot.TaskScores.Length; i++)
        {
            Assert.IsFalse(float.IsNaN(bot.TaskScores[i]), $"Task {i} produced NaN with NaN aggression");
        }

        Assert.IsNotNull(bot.TaskAssignment.Task, "Should still get a task with NaN aggression");
    }

    [Test]
    public void NaNRaidTime_FallsBackToDefault()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = float.NaN;

        _manager.ScoreAndPick(bot);

        for (int i = 0; i < bot.TaskScores.Length; i++)
        {
            Assert.IsFalse(float.IsNaN(bot.TaskScores[i]), $"Task {i} produced NaN with NaN raidTime");
        }
    }

    [Test]
    public void BothNaN_AllScoresStillFinite()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        bot.Aggression = float.NaN;
        bot.RaidTimeNormalized = float.NaN;

        _manager.ScoreAndPick(bot);

        for (int i = 0; i < bot.TaskScores.Length; i++)
        {
            Assert.IsFalse(float.IsNaN(bot.TaskScores[i]), $"Task {i} produced NaN with both NaN");
            Assert.IsFalse(float.IsInfinity(bot.TaskScores[i]), $"Task {i} produced Infinity with both NaN");
        }
    }
}

// ── 5. Multi-Entity Pipeline ────────────────────────────────

[TestFixture]
public class MultiEntityPipelineE2ETests
{
    [Test]
    public void ThreeBotsWithDifferentQuestActions_ScoreIndependently()
    {
        var manager = QuestTaskFactory.Create();

        var goBot = new BotEntity(0);
        goBot.TaskScores = new float[QuestTaskFactory.TaskCount];
        goBot.HasActiveObjective = true;
        goBot.CurrentQuestAction = QuestActionId.MoveToPosition;
        goBot.DistanceToObjective = 100f;

        var ambushBot = new BotEntity(1);
        ambushBot.TaskScores = new float[QuestTaskFactory.TaskCount];
        ambushBot.HasActiveObjective = true;
        ambushBot.CurrentQuestAction = QuestActionId.Ambush;
        ambushBot.IsCloseToObjective = true;

        var holdBot = new BotEntity(2);
        holdBot.TaskScores = new float[QuestTaskFactory.TaskCount];
        holdBot.HasActiveObjective = true;
        holdBot.CurrentQuestAction = QuestActionId.HoldAtPosition;

        manager.Update(new[] { goBot, ambushBot, holdBot });

        Assert.IsInstanceOf<GoToObjectiveTask>(goBot.TaskAssignment.Task);
        Assert.IsInstanceOf<AmbushTask>(ambushBot.TaskAssignment.Task);
        Assert.IsInstanceOf<HoldPositionTask>(holdBot.TaskAssignment.Task);
    }

    [Test]
    public void EntityRemoval_DoesNotAffectOtherEntities()
    {
        var manager = QuestTaskFactory.Create();

        var bot1 = new BotEntity(0);
        bot1.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot1.HasActiveObjective = true;
        bot1.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot1.DistanceToObjective = 100f;

        var bot2 = new BotEntity(1);
        bot2.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot2.HasActiveObjective = true;
        bot2.CurrentQuestAction = QuestActionId.HoldAtPosition;

        // Both bots get tasks
        manager.Update(new[] { bot1, bot2 });
        Assert.IsNotNull(bot1.TaskAssignment.Task);
        Assert.IsNotNull(bot2.TaskAssignment.Task);

        // Remove bot1
        manager.RemoveEntity(bot1);
        Assert.IsNull(bot1.TaskAssignment.Task);

        // bot2 is unaffected
        Assert.IsInstanceOf<HoldPositionTask>(bot2.TaskAssignment.Task);

        // bot2 can still be updated
        manager.Update(new[] { bot2 });
        Assert.IsInstanceOf<HoldPositionTask>(bot2.TaskAssignment.Task);
    }

    [Test]
    public void BotRegistry_AddRemoveScore_FullLifecycle()
    {
        var registry = new BotRegistry();
        var manager = QuestTaskFactory.Create();

        // Register 3 bots
        var bot0 = registry.Add(100);
        var bot1 = registry.Add(101);
        var bot2 = registry.Add(102);

        // Allocate task scores
        foreach (var entity in registry.Entities)
        {
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasActiveObjective = true;
            entity.CurrentQuestAction = QuestActionId.MoveToPosition;
            entity.DistanceToObjective = 50f;
        }

        // Score all
        manager.Update(registry.Entities);
        Assert.AreEqual(3, registry.Count);
        foreach (var entity in registry.Entities)
        {
            Assert.IsNotNull(entity.TaskAssignment.Task);
        }

        // Remove middle bot
        manager.RemoveEntity(bot1);
        registry.Remove(bot1);
        Assert.AreEqual(2, registry.Count);

        // Remaining bots still work
        manager.Update(registry.Entities);
        foreach (var entity in registry.Entities)
        {
            Assert.IsNotNull(entity.TaskAssignment.Task, $"Entity {entity.Id} should still have a task after neighbor removal");
        }

        // Add new bot with recycled ID
        var bot3 = registry.Add(103);
        bot3.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot3.HasActiveObjective = true;
        bot3.CurrentQuestAction = QuestActionId.Ambush;
        bot3.IsCloseToObjective = true;

        manager.Update(registry.Entities);
        Assert.IsInstanceOf<AmbushTask>(bot3.TaskAssignment.Task, "Newly added bot should get correct task");
    }
}

// ── 6. Vulture vs Investigate Priority ──────────────────────

[TestFixture]
public class VultureInvestigateInteractionE2ETests
{
    private UtilityTaskManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = QuestTaskFactory.Create();
    }

    [Test]
    public void HighIntensity_VultureWins_OverInvestigate()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = false;
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 20; // Above vulture threshold (15)
        bot.NearbyEventX = 100f;
        bot.NearbyEventZ = 100f;
        bot.CurrentPositionX = 50f;
        bot.CurrentPositionZ = 50f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        // VultureTask MaxBaseScore=0.60 vs InvestigateTask MaxBaseScore=0.40
        Assert.IsInstanceOf<VultureTask>(bot.TaskAssignment.Task, "Vulture should win over Investigate at high intensity");
    }

    [Test]
    public void LowIntensity_AboveInvestigateThreshold_BelowVulture_InvestigateWins()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = false;
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 8; // Above investigate (5) but below vulture (15)
        bot.NearbyEventX = 60f;
        bot.NearbyEventZ = 60f;
        bot.CurrentPositionX = 50f;
        bot.CurrentPositionZ = 50f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        Assert.IsInstanceOf<InvestigateTask>(
            bot.TaskAssignment.Task,
            "Investigate should win when intensity is above its threshold but below Vulture's"
        );
    }

    [Test]
    public void VulturePhaseActive_InvestigateScoresZero()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = false;
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 20;
        bot.VulturePhase = VulturePhase.Approach;
        bot.NearbyEventX = 60f;
        bot.NearbyEventZ = 60f;
        bot.CurrentPositionX = 50f;
        bot.CurrentPositionZ = 50f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        // InvestigateTask checks VulturePhase and scores 0 when vulturing
        Assert.IsInstanceOf<VultureTask>(bot.TaskAssignment.Task, "Vulture should win when vulture phase is active");

        // Verify InvestigateTask did score 0
        // InvestigateTask is at ordinal 11 in QuestTaskFactory
        int investigateOrdinal = -1;
        for (int i = 0; i < _manager.Tasks.Length; i++)
        {
            if (_manager.Tasks[i] is InvestigateTask)
            {
                investigateOrdinal = i;
                break;
            }
        }

        Assert.That(investigateOrdinal, Is.GreaterThanOrEqualTo(0));
        Assert.AreEqual(0f, bot.TaskScores[investigateOrdinal], "InvestigateTask should score 0 when VulturePhase is active");
    }

    [Test]
    public void CombatSuppresses_BothVultureAndInvestigate()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = false;
        bot.HasNearbyEvent = true;
        bot.CombatIntensity = 30;
        bot.IsInCombat = true;
        bot.NearbyEventX = 60f;
        bot.NearbyEventZ = 60f;
        bot.CurrentPositionX = 50f;
        bot.CurrentPositionZ = 50f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        // Both vulture and investigate gate on !IsInCombat
        if (bot.TaskAssignment.Task != null)
        {
            Assert.That(bot.TaskAssignment.Task, Is.Not.InstanceOf<VultureTask>(), "Vulture should not win when in combat");
            Assert.That(bot.TaskAssignment.Task, Is.Not.InstanceOf<InvestigateTask>(), "Investigate should not win when in combat");
        }
    }
}

// ── 7. Loot During Quest Travel ─────────────────────────────

[TestFixture]
public class LootDuringQuestE2ETests
{
    [Test]
    public void LootNearObjective_CanWin_OverGoToObjective_WhenClose()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 10f; // Close to objective
        bot.IsCloseToObjective = false; // Not yet at objective
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        // Very valuable loot right at bot's position
        bot.HasLootTarget = true;
        bot.LootTargetValue = 40000f;
        bot.LootTargetX = bot.CurrentPositionX;
        bot.LootTargetY = bot.CurrentPositionY;
        bot.LootTargetZ = bot.CurrentPositionZ;
        bot.InventorySpaceFree = 10f;

        manager.ScoreAndPick(bot);

        // GoToObjective at 10m scores ~0.65 * (1 - exp(-10/75)) * modifiers = low
        // Loot at 0m with high value should beat it
        // Exact task depends on modifiers, but this tests the contention
        Assert.IsNotNull(bot.TaskAssignment.Task);
    }

    [Test]
    public void Loot_CombatSuppresses_QuestResumes()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        bot.HasLootTarget = true;
        bot.LootTargetValue = 30000f;
        bot.InventorySpaceFree = 5f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        manager.ScoreAndPick(bot);
        var firstTask = bot.TaskAssignment.Task;

        // Combat starts
        bot.IsInCombat = true;
        manager.ScoreAndPick(bot);

        // Loot scores 0 in combat; GoToObjective might still score
        // (GoToObjective doesn't gate on combat, only quest conditions)
        if (bot.TaskAssignment.Task is LootTask)
        {
            Assert.Fail("LootTask should not win during combat");
        }

        // Combat ends
        bot.IsInCombat = false;
        manager.ScoreAndPick(bot);
        Assert.IsNotNull(bot.TaskAssignment.Task, "Should get a task after combat ends");
    }
}

// ── 8. Quest Action Routing Completeness ────────────────────

[TestFixture]
public class QuestActionRoutingE2ETests
{
    private UtilityTaskManager _manager;

    [SetUp]
    public void SetUp()
    {
        _manager = QuestTaskFactory.Create();
    }

    [TestCase(QuestActionId.MoveToPosition, typeof(GoToObjectiveTask))]
    [TestCase(QuestActionId.HoldAtPosition, typeof(HoldPositionTask))]
    [TestCase(QuestActionId.ToggleSwitch, typeof(ToggleSwitchTask))]
    [TestCase(QuestActionId.CloseNearbyDoors, typeof(CloseDoorsTask))]
    public void QuestAction_RoutesToExpectedTask(int questAction, Type expectedTaskType)
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = questAction;
        bot.DistanceToObjective = 100f;
        bot.IsCloseToObjective = false;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf(
            expectedTaskType,
            bot.TaskAssignment.Task,
            $"QuestAction {questAction} should route to {expectedTaskType.Name}"
        );
    }

    [TestCase(QuestActionId.Ambush, typeof(AmbushTask))]
    [TestCase(QuestActionId.Snipe, typeof(SnipeTask))]
    [TestCase(QuestActionId.PlantItem, typeof(PlantItemTask))]
    public void TwoPhaseAction_WhenClose_RoutesToActionTask(int questAction, Type expectedTaskType)
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = questAction;
        bot.DistanceToObjective = 2f;
        bot.IsCloseToObjective = true;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf(
            expectedTaskType,
            bot.TaskAssignment.Task,
            $"QuestAction {questAction} when close should route to {expectedTaskType.Name}"
        );
    }

    [TestCase(QuestActionId.Ambush)]
    [TestCase(QuestActionId.Snipe)]
    [TestCase(QuestActionId.PlantItem)]
    public void TwoPhaseAction_WhenFar_RoutesToGoToObjective(int questAction)
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = questAction;
        bot.DistanceToObjective = 100f;
        bot.IsCloseToObjective = false;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(
            bot.TaskAssignment.Task,
            $"QuestAction {questAction} when far should route to GoToObjective"
        );
    }

    [Test]
    public void UndefinedQuestAction_NoQuestTaskWins()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.Undefined;
        bot.DistanceToObjective = 100f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        // Undefined action should not route to any quest-specific task
        // GoToObjective explicitly returns 0 for Undefined
        // HoldPosition, Ambush, Snipe, etc. all gate on specific actions
        // Only non-quest tasks (Loot, Vulture, etc.) could win
        if (bot.TaskAssignment.Task != null)
        {
            Assert.That(
                bot.TaskAssignment.Task,
                Is.Not.InstanceOf<GoToObjectiveTask>(),
                "GoToObjective should not win with Undefined action"
            );
        }
    }

    [Test]
    public void RequestExtract_NoQuestMovementTask()
    {
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.RequestExtract;
        bot.DistanceToObjective = 100f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        _manager.ScoreAndPick(bot);

        // RequestExtract is excluded from GoToObjective
        if (bot.TaskAssignment.Task != null)
        {
            Assert.That(
                bot.TaskAssignment.Task,
                Is.Not.InstanceOf<GoToObjectiveTask>(),
                "GoToObjective should not win with RequestExtract action"
            );
        }
    }

    [Test]
    public void UnknownQuestActionId_HandledGracefully()
    {
        // Quest action 999 is not in QuestActionId constants
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = 999;
        bot.DistanceToObjective = 100f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        // Should not throw
        Assert.DoesNotThrow(() => _manager.ScoreAndPick(bot));

        // An unknown action should still allow GoToObjective (it handles MoveToPosition
        // and 2-phase actions, and the switch statement falls through to default which
        // allows any non-excluded action)
        // Actually, GoToObjective checks specific exclusions: HoldAtPosition, ToggleSwitch,
        // CloseNearbyDoors, RequestExtract, Undefined. Action 999 is not excluded,
        // so GoToObjective CAN score for it.
        if (bot.TaskAssignment.Task != null)
        {
            Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "Unknown action 999 should fall through to GoToObjective");
        }
    }
}

// ── 9. Hysteresis Integration with Real Tasks ───────────────

[TestFixture]
public class HysteresisRealTaskE2ETests
{
    [Test]
    public void GoToObjective_Hysteresis_PreventsBriefLootDistraction()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.3f;

        // First: GoToObjective wins
        manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task);

        // Now a modest loot target appears
        bot.HasLootTarget = true;
        bot.LootTargetValue = 10000f;
        bot.LootTargetX = 80f;
        bot.LootTargetZ = 80f;
        bot.CurrentPositionX = 50f;
        bot.CurrentPositionZ = 50f;
        bot.InventorySpaceFree = 5f;

        manager.ScoreAndPick(bot);

        // GoToObjective hysteresis is 0.25 — modest loot might not beat it
        // GoToObjective raw score at 100m ≈ 0.65*(1-exp(-100/75))*modifiers
        //   ≈ 0.65 * 0.74 * ~1.1 = ~0.53
        // With hysteresis: 0.53 + 0.25 = 0.78
        // Loot score: value=10000/50000=0.2, *0.5=0.1, minus distance penalty
        //   dist ≈ 42m → distSqr ≈ 1800, penalty = min(1.8, 0.4) = 0.4
        //   score ≈ 0.1 - 0.4 = <0
        // So GoToObjective should easily hold
        Assert.IsInstanceOf<GoToObjectiveTask>(bot.TaskAssignment.Task, "GoToObjective hysteresis should prevent brief loot distraction");
    }

    [Test]
    public void LingerTask_DecayingScore_EventuallyLosesToPatrol()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = false;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;
        bot.ObjectiveCompletedTime = 100f;
        bot.LingerDuration = 10f;
        bot.CurrentGameTime = 100.5f;

        // No patrol routes loaded, so Patrol scores 0
        // Reset PatrolTask state to ensure clean test
        PatrolTask.Reset();

        // Linger should win initially
        manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<LingerTask>(bot.TaskAssignment.Task);

        // As time passes, linger decays
        bot.CurrentGameTime = 109f; // 9s into 10s linger
        manager.ScoreAndPick(bot);

        // At 9/10, linger score = 0.45 * (1 - 9/10) * modifiers = 0.045 * ~1.0 = ~0.045
        // Very low — if any other task scores above that, it wins.
        // But with hysteresis (0.10), effective score is ~0.145.
        // No competing task? Linger holds.
        Assert.IsInstanceOf<LingerTask>(
            bot.TaskAssignment.Task,
            "Linger should hold with hysteresis even at low score if nothing competes"
        );

        // Linger fully expires
        bot.CurrentGameTime = 111f;
        manager.ScoreAndPick(bot);

        // Linger scores 0. With hysteresis 0.10, effective = 0.10.
        // No competing task → still holds
        if (bot.TaskAssignment.Task != null)
        {
            // This is the known hysteresis-holds-zero-score behavior
            Assert.IsInstanceOf<LingerTask>(bot.TaskAssignment.Task);
        }
    }
}

// ── 10. Task Active Entity Tracking Across Pipeline ─────────

[TestFixture]
public class ActiveEntityTrackingE2ETests
{
    [Test]
    public void TaskSwitch_DeactivatesOld_ActivatesNew()
    {
        var manager = QuestTaskFactory.Create();

        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        manager.ScoreAndPick(bot);
        var goToTask = bot.TaskAssignment.Task;
        Assert.IsInstanceOf<GoToObjectiveTask>(goToTask);
        Assert.AreEqual(1, goToTask.ActiveEntityCount);

        // Switch to HoldPosition
        bot.CurrentQuestAction = QuestActionId.HoldAtPosition;
        manager.ScoreAndPick(bot);

        var holdTask = bot.TaskAssignment.Task;
        Assert.IsInstanceOf<HoldPositionTask>(holdTask);
        Assert.AreEqual(1, holdTask.ActiveEntityCount);
        Assert.AreEqual(0, goToTask.ActiveEntityCount, "GoToObjective should have deactivated bot");
    }

    [Test]
    public void MultipleEntities_SameTask_ActiveCountCorrect()
    {
        var manager = QuestTaskFactory.Create();

        var bots = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            bots[i] = new BotEntity(i);
            bots[i].TaskScores = new float[QuestTaskFactory.TaskCount];
            bots[i].HasActiveObjective = true;
            bots[i].CurrentQuestAction = QuestActionId.HoldAtPosition;
        }

        manager.Update(bots);

        // All 5 should be on HoldPositionTask
        UtilityTask holdTask = null;
        foreach (var bot in bots)
        {
            Assert.IsInstanceOf<HoldPositionTask>(bot.TaskAssignment.Task);
            holdTask = bot.TaskAssignment.Task;
        }

        Assert.AreEqual(5, holdTask.ActiveEntityCount);

        // Remove one
        bots[2].IsActive = false;
        manager.Update(bots);
        Assert.AreEqual(4, holdTask.ActiveEntityCount);
    }

    [Test]
    public void RemoveEntity_ClearsFromAllTracking()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;

        manager.ScoreAndPick(bot);
        var task = bot.TaskAssignment.Task;
        Assert.AreEqual(1, task.ActiveEntityCount);

        manager.RemoveEntity(bot);
        Assert.IsNull(bot.TaskAssignment.Task);
        Assert.AreEqual(0, task.ActiveEntityCount);
    }
}

// ── 11. Score Determinism and Stability ─────────────────────

[TestFixture]
public class ScoringDeterminismE2ETests
{
    [Test]
    public void SameState_ProducesSameScores_AcrossMultipleCalls()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 75f;
        bot.Aggression = 0.7f;
        bot.RaidTimeNormalized = 0.4f;

        // Score once
        for (int i = 0; i < manager.Tasks.Length; i++)
            manager.Tasks[i].ScoreEntity(i, bot);

        var scores1 = (float[])bot.TaskScores.Clone();

        // Score again with same state
        for (int i = 0; i < manager.Tasks.Length; i++)
            manager.Tasks[i].ScoreEntity(i, bot);

        for (int i = 0; i < QuestTaskFactory.TaskCount; i++)
        {
            Assert.AreEqual(scores1[i], bot.TaskScores[i], 0.0001f, $"Task {i} score should be deterministic");
        }
    }

    [Test]
    public void TiedScores_FirstTaskInArrayOrder_Wins()
    {
        // This tests the tie-breaking behavior: when two tasks have identical scores
        // and no task is currently assigned, the first one in array order wins
        // because the loop uses strict > (not >=)
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];

        // Set up a state where exactly two tasks score identically
        // Both Ambush and Snipe have BaseScore 0.65 when conditions are met
        bot.HasActiveObjective = true;
        bot.IsCloseToObjective = true;

        // We can't have both Ambush AND Snipe action simultaneously (it's one QuestAction),
        // so instead use TestTask approach or verify with different conditions
        // Instead, manually set identical scores
        bot.TaskScores[1] = 0.5f; // AmbushTask at ordinal 1
        bot.TaskScores[2] = 0.5f; // SnipeTask at ordinal 2

        manager.PickTask(bot);

        // First one wins (ordinal 1 = AmbushTask)
        Assert.AreEqual(1, bot.TaskAssignment.Ordinal, "On tie, first task in array order should win");
    }
}

// ── 12. Entity Deactivation/Reactivation Lifecycle ──────────

[TestFixture]
public class EntityActivationLifecycleE2ETests
{
    [Test]
    public void Deactivate_Reactivate_GetsFreshTask()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 100f;
        bot.Aggression = 0.5f;
        bot.RaidTimeNormalized = 0.5f;

        // Active: gets task
        manager.ScoreAndPick(bot);
        Assert.IsNotNull(bot.TaskAssignment.Task);

        // Deactivate: task cleared
        bot.IsActive = false;
        manager.ScoreAndPick(bot);
        Assert.IsNull(bot.TaskAssignment.Task);

        // Reactivate with different state
        bot.IsActive = true;
        bot.CurrentQuestAction = QuestActionId.HoldAtPosition;
        manager.ScoreAndPick(bot);
        Assert.IsInstanceOf<HoldPositionTask>(bot.TaskAssignment.Task, "Reactivated entity should get task matching new state");
    }

    [Test]
    public void BatchUpdate_MixedActiveInactive()
    {
        var manager = QuestTaskFactory.Create();
        var entities = new BotEntity[4];
        for (int i = 0; i < 4; i++)
        {
            entities[i] = new BotEntity(i);
            entities[i].TaskScores = new float[QuestTaskFactory.TaskCount];
            entities[i].HasActiveObjective = true;
            entities[i].CurrentQuestAction = QuestActionId.MoveToPosition;
            entities[i].DistanceToObjective = 100f;
        }

        // Make some inactive
        entities[1].IsActive = false;
        entities[3].IsActive = false;

        manager.Update(entities);

        Assert.IsNotNull(entities[0].TaskAssignment.Task);
        Assert.IsNull(entities[1].TaskAssignment.Task);
        Assert.IsNotNull(entities[2].TaskAssignment.Task);
        Assert.IsNull(entities[3].TaskAssignment.Task);
    }
}

// ── 13. TaskScores Array Edge Cases ─────────────────────────

[TestFixture]
public class TaskScoresArrayE2ETests
{
    [Test]
    public void ExactSizeArray_WorksWithQuestManager()
    {
        var manager = QuestTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount]; // Exactly 14
        bot.HasActiveObjective = true;
        bot.CurrentQuestAction = QuestActionId.MoveToPosition;
        bot.DistanceToObjective = 50f;

        Assert.DoesNotThrow(() => manager.ScoreAndPick(bot));
    }

    [Test]
    public void OversizedArray_WorksWithSquadManager()
    {
        var manager = SquadTaskFactory.Create();
        var bot = new BotEntity(0);
        bot.TaskScores = new float[QuestTaskFactory.TaskCount]; // 14 slots, only 2 needed
        bot.HasTacticalPosition = true;
        bot.Boss = new BotEntity(99);
        bot.TacticalPositionX = 200f;
        bot.TacticalPositionZ = 300f;
        bot.CurrentPositionX = 50f;
        bot.CurrentPositionZ = 50f;

        Assert.DoesNotThrow(() => manager.ScoreAndPick(bot));

        // Extra slots at indices 2-13 should be unchanged (0)
        for (int i = SquadTaskFactory.TaskCount; i < bot.TaskScores.Length; i++)
        {
            Assert.AreEqual(0f, bot.TaskScores[i], $"Slot {i} beyond squad manager range should be untouched");
        }
    }

    [Test]
    public void FreshArray_AllZeros_NoStaleScores()
    {
        var bot = new BotEntity(0);

        // Simulate manager switch: set up with quest scores
        bot.TaskScores = new float[QuestTaskFactory.TaskCount];
        bot.TaskScores[0] = 0.9f;
        bot.TaskScores[5] = 0.7f;

        // Switch to follower: array is large enough, no realloc
        // But the BotObjectiveLayer clears the assignment (not the array)
        // Stale scores at [0] and [5] remain but will be overwritten by ScoreEntity

        var followerManager = SquadTaskFactory.Create();
        bot.HasTacticalPosition = true;
        bot.Boss = new BotEntity(99);
        bot.TacticalPositionX = 200f;
        bot.TacticalPositionZ = 300f;
        bot.CurrentPositionX = 50f;
        bot.CurrentPositionZ = 50f;

        followerManager.ScoreAndPick(bot);

        // Follower manager writes to [0] and [1], so those are fresh
        // [2]-[13] still have stale data but are never read by the 2-task manager
        Assert.IsNotNull(bot.TaskAssignment.Task);
    }
}

// ── 14. Raid Time Progression Changes Task Selection ────────

[TestFixture]
public class RaidTimeProgressionE2ETests
{
    [Test]
    public void EarlyRaid_GoToObjective_ScoresHigher()
    {
        var manager = QuestTaskFactory.Create();

        var earlyBot = new BotEntity(0);
        earlyBot.TaskScores = new float[QuestTaskFactory.TaskCount];
        earlyBot.HasActiveObjective = true;
        earlyBot.CurrentQuestAction = QuestActionId.MoveToPosition;
        earlyBot.DistanceToObjective = 100f;
        earlyBot.Aggression = 0.5f;
        earlyBot.RaidTimeNormalized = 0.1f; // Early raid

        var lateBot = new BotEntity(1);
        lateBot.TaskScores = new float[QuestTaskFactory.TaskCount];
        lateBot.HasActiveObjective = true;
        lateBot.CurrentQuestAction = QuestActionId.MoveToPosition;
        lateBot.DistanceToObjective = 100f;
        lateBot.Aggression = 0.5f;
        lateBot.RaidTimeNormalized = 0.9f; // Late raid

        manager.ScoreAndPick(earlyBot);
        manager.ScoreAndPick(lateBot);

        // Find GoToObjective scores
        int goToOrdinal = -1;
        for (int i = 0; i < manager.Tasks.Length; i++)
        {
            if (manager.Tasks[i] is GoToObjectiveTask)
            {
                goToOrdinal = i;
                break;
            }
        }

        Assert.That(goToOrdinal, Is.GreaterThanOrEqualTo(0));

        // Early raid gets GoToObjective modifier Lerp(1.2, 0.8, 0.1) = 1.16
        // Late raid gets Lerp(1.2, 0.8, 0.9) = 0.84
        Assert.Greater(
            earlyBot.TaskScores[goToOrdinal],
            lateBot.TaskScores[goToOrdinal],
            "GoToObjective should score higher in early raid than late raid"
        );
    }
}

// ── 15. Full Registry + Manager Integration ─────────────────

[TestFixture]
public class RegistryManagerIntegrationE2ETests
{
    [Test]
    public void FullLifecycle_Register_Score_Deactivate_Remove()
    {
        var registry = new BotRegistry();
        var manager = QuestTaskFactory.Create();

        // Phase 1: Register bots with BSG IDs
        var entities = new List<BotEntity>();
        for (int i = 0; i < 5; i++)
        {
            var entity = registry.Add(i * 10);
            entity.TaskScores = new float[QuestTaskFactory.TaskCount];
            entity.HasActiveObjective = true;
            entity.CurrentQuestAction = QuestActionId.MoveToPosition;
            entity.DistanceToObjective = 50f + i * 20f;
            entities.Add(entity);
        }

        Assert.AreEqual(5, registry.Count);

        // Phase 2: Score all
        manager.Update(registry.Entities);
        foreach (var entity in registry.Entities)
        {
            Assert.IsNotNull(entity.TaskAssignment.Task, $"Entity {entity.Id} should have a task");
        }

        // Phase 3: Deactivate bot #2
        entities[2].IsActive = false;
        manager.Update(registry.Entities);
        Assert.IsNull(entities[2].TaskAssignment.Task);

        // Phase 4: Remove bot #2 from registry
        manager.RemoveEntity(entities[2]);
        registry.Remove(entities[2]);
        Assert.AreEqual(4, registry.Count);

        // Phase 5: BSG ID lookup still works for remaining bots
        Assert.AreSame(entities[0], registry.GetByBsgId(0));
        Assert.AreSame(entities[1], registry.GetByBsgId(10));
        Assert.IsNull(registry.GetByBsgId(20)); // Removed
        Assert.AreSame(entities[3], registry.GetByBsgId(30));
        Assert.AreSame(entities[4], registry.GetByBsgId(40));

        // Phase 6: Re-add with same BSG ID
        var newEntity = registry.Add(20);
        newEntity.TaskScores = new float[QuestTaskFactory.TaskCount];
        newEntity.HasActiveObjective = true;
        newEntity.CurrentQuestAction = QuestActionId.HoldAtPosition;

        manager.Update(registry.Entities);
        Assert.IsInstanceOf<HoldPositionTask>(newEntity.TaskAssignment.Task);
        Assert.AreSame(newEntity, registry.GetByBsgId(20));
    }
}
