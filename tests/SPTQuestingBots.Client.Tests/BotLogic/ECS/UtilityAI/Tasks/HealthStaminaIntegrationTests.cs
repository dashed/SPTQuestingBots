using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class HealthStaminaIntegrationTests
{
    // ── HoldTacticalPositionTask: Squad Healing Overwatch ────────────────

    private static BotEntity CreateFollowerNearTactical(bool anySquadMemberHealing = false)
    {
        var boss = new BotEntity(0);
        var entity = new BotEntity(1);
        entity.Boss = boss;
        entity.HasTacticalPosition = true;
        entity.CurrentPositionX = 0;
        entity.CurrentPositionY = 0;
        entity.CurrentPositionZ = 0;
        entity.TacticalPositionX = 1;
        entity.TacticalPositionY = 0;
        entity.TacticalPositionZ = 0;
        entity.AnySquadMemberHealing = anySquadMemberHealing;
        entity.TaskScores = new float[SquadTaskFactory.TaskCount];
        return entity;
    }

    [Test]
    public void HoldTacticalPosition_NoSquadHealing_ReturnsBaseScore()
    {
        var entity = CreateFollowerNearTactical(anySquadMemberHealing: false);
        float score = HoldTacticalPositionTask.Score(entity);
        Assert.AreEqual(HoldTacticalPositionTask.BaseScore, score);
    }

    [Test]
    public void HoldTacticalPosition_SquadMemberHealing_ReturnsBaseScorePlusBonus()
    {
        var entity = CreateFollowerNearTactical(anySquadMemberHealing: true);
        float score = HoldTacticalPositionTask.Score(entity);
        float expected = HoldTacticalPositionTask.BaseScore + HoldTacticalPositionTask.SquadHealingBonus;
        Assert.AreEqual(expected, score, 0.001f);
    }

    [Test]
    public void HoldTacticalPosition_FarAway_SquadHealing_StillZero()
    {
        var entity = CreateFollowerNearTactical(anySquadMemberHealing: true);
        entity.TacticalPositionX = 100; // far away
        float score = HoldTacticalPositionTask.Score(entity);
        Assert.AreEqual(0f, score);
    }

    // ── LootTask: Overweight Gate ────────────────────────────────────────

    private static BotEntity CreateLootEntity(bool isOverweight = false)
    {
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasLootTarget = true;
        entity.LootTargetValue = 30000f;
        entity.LootTargetX = 5;
        entity.LootTargetY = 0;
        entity.LootTargetZ = 5;
        entity.CurrentPositionX = 0;
        entity.CurrentPositionY = 0;
        entity.CurrentPositionZ = 0;
        entity.InventorySpaceFree = 5f;
        entity.IsOverweight = isOverweight;
        return entity;
    }

    [Test]
    public void LootTask_NotOverweight_ScoresPositive()
    {
        var entity = CreateLootEntity(isOverweight: false);
        float score = LootTask.Score(entity);
        Assert.Greater(score, 0f);
    }

    [Test]
    public void LootTask_Overweight_ScoresZero()
    {
        var entity = CreateLootEntity(isOverweight: true);
        float score = LootTask.Score(entity);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void LootTask_Overweight_TakesPreecedenceOverValue()
    {
        var entity = CreateLootEntity(isOverweight: true);
        entity.LootTargetValue = 100000f; // extremely valuable
        float score = LootTask.Score(entity);
        Assert.AreEqual(0f, score);
    }

    // ── QuestUtilityTask: Healing Gate ────────────────────────────────────

    [Test]
    public void QuestUtilityTask_HealingEntity_ScoresZero()
    {
        var task = new GoToObjectiveTask();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.IsActive = true;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.IsHealing = true;

        task.UpdateScores(0, new[] { entity });

        Assert.AreEqual(0f, entity.TaskScores[0]);
    }

    [Test]
    public void QuestUtilityTask_NotHealing_ScoresPositive()
    {
        var task = new GoToObjectiveTask();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.IsActive = true;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.IsHealing = false;

        task.UpdateScores(0, new[] { entity });

        Assert.Greater(entity.TaskScores[0], 0f);
    }

    [Test]
    public void QuestUtilityTask_Disabled_SetsZeroEvenForHealthy()
    {
        var task = new GoToObjectiveTask();
        task.IsEnabled = false;
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.IsActive = true;
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.IsHealing = false;

        task.UpdateScores(0, new[] { entity });

        Assert.AreEqual(0f, entity.TaskScores[0]);
    }

    [Test]
    public void QuestUtilityTask_MixedHealingEntities_OnlyHealingZeroed()
    {
        var task = new GoToObjectiveTask();
        var healthy = new BotEntity(0);
        healthy.TaskScores = new float[QuestTaskFactory.TaskCount];
        healthy.IsActive = true;
        healthy.HasActiveObjective = true;
        healthy.DistanceToObjective = 100f;
        healthy.NavMeshDistanceToObjective = float.MaxValue;
        healthy.CurrentQuestAction = QuestActionId.MoveToPosition;
        healthy.IsHealing = false;

        var healing = new BotEntity(1);
        healing.TaskScores = new float[QuestTaskFactory.TaskCount];
        healing.IsActive = true;
        healing.HasActiveObjective = true;
        healing.DistanceToObjective = 100f;
        healing.NavMeshDistanceToObjective = float.MaxValue;
        healing.CurrentQuestAction = QuestActionId.MoveToPosition;
        healing.IsHealing = true;

        task.UpdateScores(0, new[] { healthy, healing });

        Assert.Greater(healthy.TaskScores[0], 0f, "Healthy entity should score positive");
        Assert.AreEqual(0f, healing.TaskScores[0], "Healing entity should be zeroed");
    }

    // ── BotEntity: Health Fields Default Values ──────────────────────────

    [Test]
    public void BotEntity_HealthFields_DefaultToFalse()
    {
        var entity = new BotEntity(0);
        Assert.IsFalse(entity.IsHealing);
        Assert.IsFalse(entity.IsOverweight);
        Assert.IsFalse(entity.HasLegDamage);
        Assert.IsFalse(entity.AnySquadMemberHealing);
    }

    // ── AmbushTask: Healing Gate Applies ─────────────────────────────────

    [Test]
    public void AmbushTask_HealingEntity_ScoresZero()
    {
        var task = new AmbushTask();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.IsActive = true;
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = QuestActionId.Ambush;
        entity.IsCloseToObjective = true;
        entity.IsHealing = true;

        task.UpdateScores(1, new[] { entity });

        Assert.AreEqual(0f, entity.TaskScores[1], "Healing should block ambush task scoring");
    }

    // ── VultureTask: Healing Gate Applies ─────────────────────────────────

    [Test]
    public void VultureTask_HealingEntity_ScoresZero()
    {
        var task = new VultureTask();
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.IsActive = true;
        entity.HasNearbyEvent = true;
        entity.CombatIntensity = 20;
        entity.CurrentGameTime = 100f;
        entity.VultureCooldownUntil = 0;
        entity.IsHealing = true;

        // Use ordinal 9 (VultureTask is at index 9 in QuestTaskFactory)
        task.UpdateScores(9, new[] { entity });

        Assert.AreEqual(0f, entity.TaskScores[9], "Healing should block vulture task scoring");
    }

    // ── LootTask: Healing Gate Applies ────────────────────────────────────

    [Test]
    public void LootTask_HealingEntity_ScoresZero()
    {
        var task = new LootTask();
        var entity = CreateLootEntity();
        entity.IsHealing = true;

        task.UpdateScores(8, new[] { entity });

        Assert.AreEqual(0f, entity.TaskScores[8], "Healing should block loot task scoring");
    }

    // ── SprintingLimitationsConfig: New Toggles ──────────────────────────

    [Test]
    public void SprintingLimitationsConfig_NewToggles_DefaultToTrue()
    {
        var config = new SPTQuestingBots.Configuration.SprintingLimitationsConfig();
        Assert.IsTrue(config.EnableStaminaExhaustionSprintBlock);
        Assert.IsTrue(config.EnablePhysicalConditionSprintBlock);
        Assert.IsTrue(config.EnableOverweightSprintBlock);
    }

    [Test]
    public void SprintingLimitationsConfig_NewToggles_Deserialize()
    {
        string json =
            @"{
            ""enable_stamina_exhaustion_sprint_block"": false,
            ""enable_physical_condition_sprint_block"": false,
            ""enable_overweight_sprint_block"": false
        }";
        var config = Newtonsoft.Json.JsonConvert.DeserializeObject<SPTQuestingBots.Configuration.SprintingLimitationsConfig>(json);
        Assert.IsFalse(config.EnableStaminaExhaustionSprintBlock);
        Assert.IsFalse(config.EnablePhysicalConditionSprintBlock);
        Assert.IsFalse(config.EnableOverweightSprintBlock);
    }

    // ── E2E: Squad Healing → HoldTacticalPosition Wins ───────────────────

    [Test]
    public void E2E_SquadMemberHealing_HoldTacticalPositionWins()
    {
        var manager = SquadTaskFactory.Create();

        var boss = new BotEntity(0);
        var follower = new BotEntity(1);
        follower.Boss = boss;
        follower.IsActive = true;
        follower.HasTacticalPosition = true;
        follower.TaskScores = new float[SquadTaskFactory.TaskCount];

        // Close to tactical position
        follower.CurrentPositionX = 0;
        follower.CurrentPositionY = 0;
        follower.CurrentPositionZ = 0;
        follower.TacticalPositionX = 1;
        follower.TacticalPositionY = 0;
        follower.TacticalPositionZ = 0;

        // Squad member is healing
        follower.AnySquadMemberHealing = true;

        manager.ScoreAndPick(follower);

        Assert.IsInstanceOf<HoldTacticalPositionTask>(follower.TaskAssignment.Task);
        // Score should include the healing bonus
        float expectedScore =
            (HoldTacticalPositionTask.BaseScore + HoldTacticalPositionTask.SquadHealingBonus)
            * ScoringModifiers.CombinedModifier(
                follower.Aggression,
                follower.RaidTimeNormalized,
                follower.HumanPlayerProximity,
                BotActionTypeId.HoldPosition
            );
        Assert.AreEqual(expectedScore, follower.TaskScores[1], 0.01f);
    }

    // ── E2E: Overweight Bot Ignores Loot ─────────────────────────────────

    [Test]
    public void E2E_OverweightBot_IgnoresLoot()
    {
        var manager = QuestTaskFactory.Create();

        var entity = new BotEntity(0);
        entity.IsActive = true;
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasLootTarget = true;
        entity.LootTargetValue = 50000f;
        entity.LootTargetX = 5;
        entity.LootTargetZ = 5;
        entity.InventorySpaceFree = 5f;
        entity.IsOverweight = true;

        // Also give it an objective so other tasks can score
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;

        manager.ScoreAndPick(entity);

        // LootTask (index 8) should be 0 due to overweight
        Assert.AreEqual(0f, entity.TaskScores[8], "Overweight bot should not score loot");
        // GoToObjective should win instead
        Assert.IsInstanceOf<GoToObjectiveTask>(entity.TaskAssignment.Task);
    }

    // ── E2E: Healing Bot Gets No Quest Tasks ─────────────────────────────

    [Test]
    public void E2E_HealingBot_AllQuestTasksZero()
    {
        var manager = QuestTaskFactory.Create();

        var entity = new BotEntity(0);
        entity.IsActive = true;
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = true;
        entity.DistanceToObjective = 100f;
        entity.NavMeshDistanceToObjective = float.MaxValue;
        entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        entity.HasLootTarget = true;
        entity.LootTargetValue = 50000f;
        entity.LootTargetX = 5;
        entity.LootTargetZ = 5;
        entity.InventorySpaceFree = 5f;
        entity.IsHealing = true;

        manager.ScoreAndPick(entity);

        // ALL task scores should be 0 because bot is healing
        for (int i = 0; i < QuestTaskFactory.TaskCount; i++)
        {
            Assert.AreEqual(0f, entity.TaskScores[i], "Task " + i + " should be 0 for healing bot");
        }

        // No task should be assigned
        Assert.IsNull(entity.TaskAssignment.Task, "Healing bot should have no task assigned");
    }
}
