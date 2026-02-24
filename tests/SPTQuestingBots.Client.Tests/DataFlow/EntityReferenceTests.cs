using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.DataFlow;

/// <summary>
/// Tests that verify entity reference staleness detection, cleanup on deactivation,
/// and cross-reference integrity between BotEntity, SquadEntity, and registries.
/// </summary>
[TestFixture]
public class EntityReferenceTests
{
    // ── Boss Reference Cleanup on Deactivation ───────────────────

    [Test]
    public void CleanupDeadEntities_BossDies_FollowersLoseBossReference()
    {
        var registry = new BotRegistry(8);
        var boss = registry.Add(0);
        var follower1 = registry.Add(1);
        var follower2 = registry.Add(2);

        HiveMindSystem.AssignBoss(follower1, boss);
        HiveMindSystem.AssignBoss(follower2, boss);

        Assert.That(follower1.Boss, Is.SameAs(boss));
        Assert.That(follower2.Boss, Is.SameAs(boss));

        boss.IsActive = false;
        HiveMindSystem.CleanupDeadEntities(registry.Entities);

        Assert.That(follower1.Boss, Is.Null, "Follower1 should lose boss ref after boss dies");
        Assert.That(follower2.Boss, Is.Null, "Follower2 should lose boss ref after boss dies");
        Assert.That(boss.Followers.Count, Is.EqualTo(0), "Dead boss should have no followers");
    }

    [Test]
    public void CleanupDeadEntities_FollowerDies_RemovedFromBossFollowerList()
    {
        var registry = new BotRegistry(8);
        var boss = registry.Add(0);
        var follower1 = registry.Add(1);
        var follower2 = registry.Add(2);

        HiveMindSystem.AssignBoss(follower1, boss);
        HiveMindSystem.AssignBoss(follower2, boss);

        follower1.IsActive = false;
        HiveMindSystem.CleanupDeadEntities(registry.Entities);

        Assert.That(boss.Followers.Count, Is.EqualTo(1));
        Assert.That(boss.Followers[0], Is.SameAs(follower2));
        Assert.That(follower1.Boss, Is.Null, "Dead follower should lose boss ref");
    }

    [Test]
    public void CleanupDeadEntities_MultipleCallsIdempotent()
    {
        var registry = new BotRegistry(8);
        var boss = registry.Add(0);
        var follower = registry.Add(1);

        HiveMindSystem.AssignBoss(follower, boss);
        boss.IsActive = false;

        HiveMindSystem.CleanupDeadEntities(registry.Entities);
        HiveMindSystem.CleanupDeadEntities(registry.Entities);

        Assert.That(follower.Boss, Is.Null);
        Assert.That(boss.Followers.Count, Is.EqualTo(0));
    }

    // ── Squad Leader Reassignment ────────────────────────────────

    [Test]
    public void SquadRegistry_RemoveMember_LeaderDies_ReassignsToActiveFollower()
    {
        var squadRegistry = new SquadRegistry();
        var squad = squadRegistry.Add(2, 3);

        var leader = new BotEntity(0) { IsActive = true };
        var follower1 = new BotEntity(1) { IsActive = true };
        var follower2 = new BotEntity(2) { IsActive = false };

        squadRegistry.AddMember(squad, leader);
        squadRegistry.AddMember(squad, follower1);
        squadRegistry.AddMember(squad, follower2);

        Assert.That(squad.Leader, Is.SameAs(leader));

        // Leader deactivates, gets removed
        leader.IsActive = false;
        squadRegistry.RemoveMember(squad, leader);

        // Follower1 is active and should become leader
        Assert.That(squad.Leader, Is.SameAs(follower1), "Active follower should become leader");
        Assert.That(follower1.SquadRole, Is.EqualTo(SquadRole.Leader));
    }

    [Test]
    public void SquadRegistry_RemoveMember_LeaderDies_FallsBackToInactiveIfNoActive()
    {
        var squadRegistry = new SquadRegistry();
        var squad = squadRegistry.Add(2, 2);

        var leader = new BotEntity(0) { IsActive = true };
        var follower = new BotEntity(1) { IsActive = false };

        squadRegistry.AddMember(squad, leader);
        squadRegistry.AddMember(squad, follower);

        squadRegistry.RemoveMember(squad, leader);

        // Only inactive follower remains — should still become leader
        Assert.That(squad.Leader, Is.SameAs(follower));
    }

    [Test]
    public void SquadRegistry_RemoveMember_LastMember_LeaderBecomesNull()
    {
        var squadRegistry = new SquadRegistry();
        var squad = squadRegistry.Add(2, 1);
        var bot = new BotEntity(0) { IsActive = true };

        squadRegistry.AddMember(squad, bot);
        squadRegistry.RemoveMember(squad, bot);

        Assert.That(squad.Leader, Is.Null, "Empty squad should have null leader");
        Assert.That(squad.Members.Count, Is.EqualTo(0));
    }

    // ── TaskAssignment Staleness Detection ───────────────────────

    [Test]
    public void TaskAssignment_ManagerSwitch_ClearsStaleTask()
    {
        // Simulate quest manager → follower manager switch
        var questTasks = QuestTaskFactory.Create();
        var followerTasks = SquadTaskFactory.Create();

        var entity = new BotEntity(0) { IsActive = true };
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        // Simulate quest task assignment at ordinal 5
        var questTask = questTasks.Tasks[5];
        questTask.Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(questTask, 5);

        Assert.That(entity.TaskAssignment.Task, Is.Not.Null);
        Assert.That(entity.TaskAssignment.Ordinal, Is.EqualTo(5));

        // Simulate manager switch: deactivate and clear assignment
        entity.TaskAssignment.Task.Deactivate(entity);
        entity.TaskAssignment = default;

        Assert.That(entity.TaskAssignment.Task, Is.Null, "Task should be cleared on manager switch");
        Assert.That(entity.TaskAssignment.Ordinal, Is.EqualTo(0));
    }

    [Test]
    public void TaskAssignment_DeactivatedEntity_TaskCleared()
    {
        var tasks = QuestTaskFactory.Create();
        var manager = tasks;

        var entity = new BotEntity(0) { IsActive = true };
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        // Assign a task
        manager.Tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(manager.Tasks[0], 0);

        // Deactivate entity — PickTasks should clear assignment
        entity.IsActive = false;
        manager.PickTasks(new List<BotEntity> { entity });

        Assert.That(entity.TaskAssignment.Task, Is.Null, "Inactive entity should have task cleared");
    }

    [Test]
    public void RemoveEntity_ClearsTaskFromAllTracking()
    {
        var manager = QuestTaskFactory.Create();
        var entity = new BotEntity(0) { IsActive = true };
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];

        // Assign task
        manager.Tasks[0].Activate(entity);
        entity.TaskAssignment = new UtilityTaskAssignment(manager.Tasks[0], 0);

        Assert.That(manager.Tasks[0].ActiveEntityCount, Is.EqualTo(1));

        manager.RemoveEntity(entity);

        Assert.That(entity.TaskAssignment.Task, Is.Null);
        Assert.That(manager.Tasks[0].ActiveEntityCount, Is.EqualTo(0));
    }

    // ── BotRegistry Reference Integrity ──────────────────────────

    [Test]
    public void BotRegistry_Add_BsgIdMapping_Bidirectional()
    {
        var registry = new BotRegistry(8);
        var entity = registry.Add(42);

        Assert.That(entity.BsgId, Is.EqualTo(42));
        Assert.That(registry.GetByBsgId(42), Is.SameAs(entity));
    }

    [Test]
    public void BotRegistry_Remove_ClearsBsgIdMapping()
    {
        var registry = new BotRegistry(8);
        var entity = registry.Add(42);

        registry.Remove(entity);

        Assert.That(registry.GetByBsgId(42), Is.Null, "BsgId mapping should be cleared on remove");
    }

    [Test]
    public void BotRegistry_GetByBsgId_OutOfRange_ReturnsNull()
    {
        var registry = new BotRegistry(8);

        Assert.That(registry.GetByBsgId(-1), Is.Null);
        Assert.That(registry.GetByBsgId(1000), Is.Null);
    }

    // ── SquadRegistry BSG Group Cleanup ──────────────────────────

    [Test]
    public void SquadRegistry_Remove_CleansBsgGroupMapping()
    {
        var squadRegistry = new SquadRegistry();
        var squad = squadRegistry.GetOrCreate(100, 2, 4);

        Assert.That(squadRegistry.GetByBsgGroupId(100), Is.SameAs(squad));

        squadRegistry.Remove(squad);

        Assert.That(squadRegistry.GetByBsgGroupId(100), Is.Null, "BSG group mapping should be cleaned on removal");
    }

    [Test]
    public void SquadRegistry_GetByBsgGroupId_StaleMapping_CleansUp()
    {
        var squadRegistry = new SquadRegistry();
        var squad = squadRegistry.GetOrCreate(100, 2, 4);

        // Remove the squad
        squadRegistry.Remove(squad);

        // Second access with same group ID should return null (stale mapping was cleaned)
        Assert.That(squadRegistry.GetByBsgGroupId(100), Is.Null);
    }

    // ── Cross-Reference Integrity ────────────────────────────────

    [Test]
    public void BotEntity_Squad_BidirectionalReference()
    {
        var squadRegistry = new SquadRegistry();
        var squad = squadRegistry.Add(2, 4);
        var bot = new BotEntity(0) { IsActive = true };

        squadRegistry.AddMember(squad, bot);

        Assert.That(bot.Squad, Is.SameAs(squad));
        Assert.That(squad.Members, Contains.Item(bot));
    }

    [Test]
    public void SquadRegistry_RemoveMember_ClearsBidirectional()
    {
        var squadRegistry = new SquadRegistry();
        var squad = squadRegistry.Add(2, 4);
        var bot = new BotEntity(0) { IsActive = true };

        squadRegistry.AddMember(squad, bot);
        squadRegistry.RemoveMember(squad, bot);

        Assert.That(bot.Squad, Is.Null);
        Assert.That(squad.Members, Does.Not.Contain(bot));
    }

    [Test]
    public void SquadRegistry_Remove_ClearsAllMemberSquadRefs()
    {
        var squadRegistry = new SquadRegistry();
        var squad = squadRegistry.Add(2, 4);
        var bot1 = new BotEntity(0) { IsActive = true };
        var bot2 = new BotEntity(1) { IsActive = true };

        squadRegistry.AddMember(squad, bot1);
        squadRegistry.AddMember(squad, bot2);

        squadRegistry.Remove(squad);

        Assert.That(bot1.Squad, Is.Null, "Member1 Squad ref should be cleared");
        Assert.That(bot2.Squad, Is.Null, "Member2 Squad ref should be cleared");
        Assert.That(bot1.SquadRole, Is.EqualTo(SquadRole.None));
        Assert.That(bot2.SquadRole, Is.EqualTo(SquadRole.None));
    }

    [Test]
    public void SquadRegistry_Clear_ClearsAllReferences()
    {
        var squadRegistry = new SquadRegistry();
        var squad1 = squadRegistry.GetOrCreate(1, 2, 3);
        var squad2 = squadRegistry.GetOrCreate(2, 2, 3);
        var bot1 = new BotEntity(0) { IsActive = true };
        var bot2 = new BotEntity(1) { IsActive = true };

        squadRegistry.AddMember(squad1, bot1);
        squadRegistry.AddMember(squad2, bot2);

        squadRegistry.Clear();

        Assert.That(squadRegistry.Count, Is.EqualTo(0));
        Assert.That(bot1.Squad, Is.Null);
        Assert.That(bot2.Squad, Is.Null);
    }

    // ── Boss-Follower Reference Integrity During Reassignment ────

    [Test]
    public void AssignBoss_SwitchBosses_OldBossLosesFollower()
    {
        var boss1 = new BotEntity(0) { IsActive = true };
        var boss2 = new BotEntity(1) { IsActive = true };
        var follower = new BotEntity(2) { IsActive = true };

        HiveMindSystem.AssignBoss(follower, boss1);
        Assert.That(boss1.Followers.Count, Is.EqualTo(1));

        HiveMindSystem.AssignBoss(follower, boss2);
        Assert.That(boss1.Followers.Count, Is.EqualTo(0), "Old boss should lose follower");
        Assert.That(boss2.Followers.Count, Is.EqualTo(1));
        Assert.That(follower.Boss, Is.SameAs(boss2));
    }

    [Test]
    public void SeparateFromGroup_BossWithFollowers_AllDetached()
    {
        var boss = new BotEntity(0) { IsActive = true };
        var f1 = new BotEntity(1) { IsActive = true };
        var f2 = new BotEntity(2) { IsActive = true };

        HiveMindSystem.AssignBoss(f1, boss);
        HiveMindSystem.AssignBoss(f2, boss);

        HiveMindSystem.SeparateFromGroup(boss);

        Assert.That(boss.Followers.Count, Is.EqualTo(0));
        Assert.That(f1.Boss, Is.Null);
        Assert.That(f2.Boss, Is.Null);
    }

    [Test]
    public void SeparateFromGroup_FollowerWithBoss_Detached()
    {
        var boss = new BotEntity(0) { IsActive = true };
        var follower = new BotEntity(1) { IsActive = true };

        HiveMindSystem.AssignBoss(follower, boss);

        HiveMindSystem.SeparateFromGroup(follower);

        Assert.That(follower.Boss, Is.Null);
        Assert.That(boss.Followers.Count, Is.EqualTo(0));
    }

    // ── Entity Sensor Reset on Deactivation ──────────────────────

    [Test]
    public void ResetInactiveEntitySensors_ClearsAllSensors()
    {
        var registry = new BotRegistry(4);
        var entity = registry.Add(0);
        entity.IsInCombat = true;
        entity.IsSuspicious = true;
        entity.CanQuest = true;
        entity.CanSprintToObjective = false;
        entity.WantsToLoot = true;

        entity.IsActive = false;
        HiveMindSystem.ResetInactiveEntitySensors(registry.Entities);

        Assert.That(entity.IsInCombat, Is.False);
        Assert.That(entity.IsSuspicious, Is.False);
        Assert.That(entity.CanQuest, Is.False);
        Assert.That(entity.CanSprintToObjective, Is.True, "Default for CanSprintToObjective is true");
        Assert.That(entity.WantsToLoot, Is.False);
    }
}
