using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Integration;

/// <summary>
/// E2E tests for dead bot lifecycle management:
///   - DeactivateBot sets IsActive=false and removes from squad
///   - Dead leader triggers leader reassignment
///   - Dead bots are cleaned up from boss/follower references
///   - HiveMindSystem.CleanupDeadEntities only processes inactive entities
///   - Loot claims are released on deactivation
///   - Squad strategies skip squads with dead leaders
/// </summary>
[TestFixture]
public class DeadBotLifecycleTests
{
    private SquadRegistry _squadRegistry;

    [SetUp]
    public void SetUp()
    {
        _squadRegistry = new SquadRegistry();
    }

    // ========================================================================
    // Helpers
    // ========================================================================

    private BotEntity CreateBot(int id, float x = 0f, float y = 0f, float z = 0f)
    {
        var bot = new BotEntity(id) { IsActive = true };
        bot.CurrentPositionX = x;
        bot.CurrentPositionY = y;
        bot.CurrentPositionZ = z;
        bot.TaskScores = new float[18];
        return bot;
    }

    private SquadEntity CreateSquadWithMembers(int squadId, out BotEntity leader, out BotEntity follower1, out BotEntity follower2)
    {
        var squad = _squadRegistry.Add(1, 4);
        leader = CreateBot(0, x: 100, z: 100);
        follower1 = CreateBot(1, x: 110, z: 100);
        follower2 = CreateBot(2, x: 90, z: 100);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, follower1);
        _squadRegistry.AddMember(squad, follower2);

        return squad;
    }

    // ========================================================================
    // 1. DeactivateBot Basics (via direct entity manipulation, simulating BotEntityBridge.DeactivateBot)
    // ========================================================================

    [Test]
    public void DeactivateBot_SetsIsActiveFalse()
    {
        var bot = CreateBot(0);
        Assert.IsTrue(bot.IsActive);

        bot.IsActive = false;

        Assert.IsFalse(bot.IsActive);
    }

    [Test]
    public void DeactivateBot_ReleasesLootClaims()
    {
        var bot = CreateBot(0);
        var lootClaims = new LootClaimRegistry();

        bot.HasLootTarget = true;
        bot.IsLooting = true;
        bot.IsApproachingLoot = true;
        lootClaims.TryClaim(bot.Id, 42);

        // Simulate DeactivateBot
        bot.IsActive = false;
        if (bot.HasLootTarget)
        {
            lootClaims.ReleaseAll(bot.Id);
            bot.HasLootTarget = false;
            bot.IsLooting = false;
            bot.IsApproachingLoot = false;
        }

        Assert.IsFalse(bot.IsActive);
        Assert.IsFalse(bot.HasLootTarget);
        Assert.IsFalse(bot.IsLooting);
        Assert.IsFalse(bot.IsApproachingLoot);
        // Loot target 42 should now be claimable by another bot
        Assert.IsTrue(lootClaims.TryClaim(1, 42));
    }

    [Test]
    public void DeactivateBot_IdempotentOnDoubleCall()
    {
        var bot = CreateBot(0);
        var squad = _squadRegistry.Add(1, 4);
        _squadRegistry.AddMember(squad, bot);

        // First deactivation
        bot.IsActive = false;
        _squadRegistry.RemoveMember(squad, bot);

        Assert.IsFalse(bot.IsActive);
        Assert.IsNull(bot.Squad);
        Assert.AreEqual(0, squad.Members.Count);

        // Second deactivation should be harmless
        bot.IsActive = false;
        if (bot.Squad != null)
            _squadRegistry.RemoveMember(bot.Squad, bot);

        Assert.IsFalse(bot.IsActive);
    }

    // ========================================================================
    // 2. Squad Removal on Deactivation
    // ========================================================================

    [Test]
    public void DeactivateBot_RemovesFromSquadMembers()
    {
        var squad = CreateSquadWithMembers(0, out var leader, out var follower1, out var follower2);

        Assert.AreEqual(3, squad.Members.Count);
        Assert.AreEqual(squad, follower1.Squad);

        // Simulate DeactivateBot for follower1
        follower1.IsActive = false;
        _squadRegistry.RemoveMember(squad, follower1);

        Assert.AreEqual(2, squad.Members.Count);
        Assert.IsNull(follower1.Squad);
        Assert.AreEqual(SquadRole.None, follower1.SquadRole);
    }

    [Test]
    public void DeactivateBot_LeaderDeath_ReassignsLeader()
    {
        var squad = CreateSquadWithMembers(0, out var leader, out var follower1, out var follower2);

        Assert.AreEqual(leader, squad.Leader);
        Assert.AreEqual(SquadRole.Leader, leader.SquadRole);

        // Kill leader
        leader.IsActive = false;
        _squadRegistry.RemoveMember(squad, leader);

        // Leader should be reassigned to an active member
        Assert.IsNotNull(squad.Leader);
        Assert.AreNotEqual(leader, squad.Leader);
        Assert.IsTrue(squad.Leader.IsActive);
        Assert.AreEqual(SquadRole.Leader, squad.Leader.SquadRole);
        Assert.AreEqual(2, squad.Members.Count);
    }

    [Test]
    public void DeactivateBot_AllMembersDie_SquadEmpty()
    {
        var squad = CreateSquadWithMembers(0, out var leader, out var follower1, out var follower2);

        // Kill all members in order
        leader.IsActive = false;
        _squadRegistry.RemoveMember(squad, leader);

        follower1.IsActive = false;
        _squadRegistry.RemoveMember(squad, follower1);

        follower2.IsActive = false;
        _squadRegistry.RemoveMember(squad, follower2);

        Assert.AreEqual(0, squad.Members.Count);
        Assert.IsNull(squad.Leader);
    }

    [Test]
    public void DeactivateBot_LeaderReassignment_PrefersActiveMembers()
    {
        var squad = _squadRegistry.Add(1, 4);
        var leader = CreateBot(0);
        var dead_follower = CreateBot(1);
        var alive_follower = CreateBot(2);

        _squadRegistry.AddMember(squad, leader);
        _squadRegistry.AddMember(squad, dead_follower);
        _squadRegistry.AddMember(squad, alive_follower);

        // Mark one follower as inactive (but don't remove from squad yet)
        dead_follower.IsActive = false;

        // Kill the leader — should prefer alive_follower over dead_follower
        leader.IsActive = false;
        _squadRegistry.RemoveMember(squad, leader);

        Assert.AreEqual(alive_follower, squad.Leader);
        Assert.AreEqual(SquadRole.Leader, alive_follower.SquadRole);
    }

    // ========================================================================
    // 3. HiveMindSystem.CleanupDeadEntities
    // ========================================================================

    [Test]
    public void CleanupDeadEntities_DetachesFollowersFromDeadBoss()
    {
        var boss = CreateBot(0);
        var follower1 = CreateBot(1);
        var follower2 = CreateBot(2);

        HiveMindSystem.AssignBoss(follower1, boss);
        HiveMindSystem.AssignBoss(follower2, boss);

        Assert.AreEqual(2, boss.Followers.Count);
        Assert.AreEqual(boss, follower1.Boss);
        Assert.AreEqual(boss, follower2.Boss);

        // Mark boss as dead
        boss.IsActive = false;

        HiveMindSystem.CleanupDeadEntities(new List<BotEntity> { boss, follower1, follower2 });

        // Boss's followers list should be cleared
        Assert.AreEqual(0, boss.Followers.Count);
        // Followers should have no boss
        Assert.IsNull(follower1.Boss);
        Assert.IsNull(follower2.Boss);
    }

    [Test]
    public void CleanupDeadEntities_DetachesDeadFollowerFromBoss()
    {
        var boss = CreateBot(0);
        var follower1 = CreateBot(1);
        var follower2 = CreateBot(2);

        HiveMindSystem.AssignBoss(follower1, boss);
        HiveMindSystem.AssignBoss(follower2, boss);

        // Mark follower1 as dead
        follower1.IsActive = false;

        HiveMindSystem.CleanupDeadEntities(new List<BotEntity> { boss, follower1, follower2 });

        // Boss should only have follower2
        Assert.AreEqual(1, boss.Followers.Count);
        Assert.AreEqual(follower2, boss.Followers[0]);
        Assert.IsNull(follower1.Boss);
    }

    [Test]
    public void CleanupDeadEntities_SkipsActiveEntities()
    {
        var boss = CreateBot(0);
        var follower = CreateBot(1);

        HiveMindSystem.AssignBoss(follower, boss);

        // Both active — cleanup should do nothing
        HiveMindSystem.CleanupDeadEntities(new List<BotEntity> { boss, follower });

        Assert.AreEqual(1, boss.Followers.Count);
        Assert.AreEqual(boss, follower.Boss);
    }

    // ========================================================================
    // 4. Sensor Reset for Inactive Entities
    // ========================================================================

    [Test]
    public void ResetInactiveEntitySensors_ResetsSensorsForDeadBot()
    {
        var bot = CreateBot(0);
        bot.IsInCombat = true;
        bot.IsSuspicious = true;
        bot.CanQuest = true;
        bot.CanSprintToObjective = false;
        bot.WantsToLoot = true;

        // Mark as dead
        bot.IsActive = false;

        HiveMindSystem.ResetInactiveEntitySensors(new List<BotEntity> { bot });

        Assert.IsFalse(bot.IsInCombat);
        Assert.IsFalse(bot.IsSuspicious);
        Assert.IsFalse(bot.CanQuest);
        Assert.IsTrue(bot.CanSprintToObjective); // default is true
        Assert.IsFalse(bot.WantsToLoot);
    }

    [Test]
    public void ResetInactiveEntitySensors_PreservesActiveBotSensors()
    {
        var bot = CreateBot(0);
        bot.IsInCombat = true;
        bot.IsSuspicious = true;
        bot.CanQuest = true;

        // Bot is still active
        HiveMindSystem.ResetInactiveEntitySensors(new List<BotEntity> { bot });

        Assert.IsTrue(bot.IsInCombat);
        Assert.IsTrue(bot.IsSuspicious);
        Assert.IsTrue(bot.CanQuest);
    }

    // ========================================================================
    // 5. Squad Strategy Skipping Dead Leaders
    // ========================================================================

    [Test]
    public void SquadWithDeadLeader_IsEffectivelyDisabled()
    {
        var squad = CreateSquadWithMembers(0, out var leader, out var follower1, out var follower2);
        squad.Objective.SetObjective(200f, 0f, 200f);

        // Without DeactivateBot removing from squad, a dead leader stays
        // Verify the guard pattern: squad.Leader == null || !squad.Leader.IsActive
        Assert.IsTrue(squad.Leader.IsActive);

        // Kill leader but DON'T remove from squad (the old bug)
        leader.IsActive = false;

        // The guard check should now skip this squad
        bool shouldSkip = squad.Leader == null || !squad.Leader.IsActive;
        Assert.IsTrue(shouldSkip, "Squad with dead leader should be skipped by strategy loops");
    }

    [Test]
    public void SquadWithDeadLeader_AfterRemoval_GetsNewLeader()
    {
        var squad = CreateSquadWithMembers(0, out var leader, out var follower1, out var follower2);
        squad.Objective.SetObjective(200f, 0f, 200f);

        // Kill and remove leader (the fix)
        leader.IsActive = false;
        _squadRegistry.RemoveMember(squad, leader);

        // Squad should have a new active leader
        Assert.IsNotNull(squad.Leader);
        Assert.IsTrue(squad.Leader.IsActive);

        // Guard check should NOT skip
        bool shouldSkip = squad.Leader == null || !squad.Leader.IsActive;
        Assert.IsFalse(shouldSkip, "Squad with reassigned leader should NOT be skipped");
    }

    // ========================================================================
    // 6. Movement State Reset for Dead Bots
    // ========================================================================

    [Test]
    public void ResetMovementForInactiveEntities_ResetsDeadBotMovement()
    {
        var bot = CreateBot(0);
        bot.Movement.Status = PathFollowStatus.Following;
        bot.Movement.IsSprinting = true;

        bot.IsActive = false;

        HiveMindSystem.ResetMovementForInactiveEntities(new List<BotEntity> { bot });

        Assert.AreEqual(PathFollowStatus.None, bot.Movement.Status);
        Assert.IsFalse(bot.Movement.IsSprinting);
    }

    [Test]
    public void ResetMovementForInactiveEntities_PreservesActiveBotMovement()
    {
        var bot = CreateBot(0);
        bot.Movement.Status = PathFollowStatus.Following;
        bot.Movement.IsSprinting = true;

        // Bot is still active
        HiveMindSystem.ResetMovementForInactiveEntities(new List<BotEntity> { bot });

        Assert.AreEqual(PathFollowStatus.Following, bot.Movement.Status);
        Assert.IsTrue(bot.Movement.IsSprinting);
    }

    // ========================================================================
    // 7. Active Bot Counting
    // ========================================================================

    [Test]
    public void CountActive_ExcludesDeadBots()
    {
        var alive1 = CreateBot(0);
        var alive2 = CreateBot(1);
        var dead = CreateBot(2);
        dead.IsActive = false;

        var entities = new List<BotEntity> { alive1, alive2, dead };

        Assert.AreEqual(2, HiveMindSystem.CountActive(entities));
    }

    [Test]
    public void CountActive_ExcludesSleepingBots()
    {
        var alive = CreateBot(0);
        var sleeping = CreateBot(1);
        sleeping.IsSleeping = true;

        var entities = new List<BotEntity> { alive, sleeping };

        Assert.AreEqual(1, HiveMindSystem.CountActive(entities));
    }

    // ========================================================================
    // 8. Squad Threat Direction with Dead Members
    // ========================================================================

    [Test]
    public void ThreatDirection_DeadMembersExcluded_AfterDeactivation()
    {
        var squad = CreateSquadWithMembers(0, out var leader, out var follower1, out var follower2);
        squad.Objective.SetObjective(100f, 0f, 100f);

        // All members in combat, but follower1 is dead (deactivated)
        leader.IsInCombat = true;
        follower1.IsInCombat = true;
        follower1.IsActive = false; // Properly deactivated

        // Simulate updateSquadThreatDirections logic
        float sumX = 0,
            sumZ = 0;
        int combatCount = 0;
        for (int i = 0; i < squad.Members.Count; i++)
        {
            var m = squad.Members[i];
            if (!m.IsActive || !m.IsInCombat)
                continue;
            // Simulate enemy position
            sumX += m.CurrentPositionX + 50f;
            sumZ += m.CurrentPositionZ + 50f;
            combatCount++;
        }

        // Only the active combat member should be counted
        // (follower1 is in Members but IsActive=false, so it's filtered)
        Assert.AreEqual(1, combatCount, "Only active combat members should be counted");
    }

    [Test]
    public void ThreatDirection_DeadMembersNotExcluded_WithoutDeactivation()
    {
        var squad = CreateSquadWithMembers(0, out var leader, out var follower1, out var follower2);
        squad.Objective.SetObjective(100f, 0f, 100f);

        // follower1 is "dead" but IsActive was never set to false (the old bug)
        leader.IsInCombat = true;
        follower1.IsInCombat = true;
        // follower1.IsActive is still true — the bug!

        float sumX = 0,
            sumZ = 0;
        int combatCount = 0;
        for (int i = 0; i < squad.Members.Count; i++)
        {
            var m = squad.Members[i];
            if (!m.IsActive || !m.IsInCombat)
                continue;
            sumX += m.CurrentPositionX + 50f;
            sumZ += m.CurrentPositionZ + 50f;
            combatCount++;
        }

        // Without the fix, dead members that are still IsActive=true are incorrectly counted
        Assert.AreEqual(2, combatCount, "Without deactivation fix, dead members are incorrectly included");
    }

    // ========================================================================
    // 9. E2E: Full Dead Bot Pipeline
    // ========================================================================

    [Test]
    public void E2E_FullDeathPipeline_DeactivateAndSquadCleanup()
    {
        // Setup: leader + 2 followers in a squad with boss/follower ECS relationship
        var registry = new BotRegistry();
        var squadRegistry = new SquadRegistry();
        var lootClaims = new LootClaimRegistry();

        var leaderEntity = registry.Add(100);
        var follower1Entity = registry.Add(101);
        var follower2Entity = registry.Add(102);

        leaderEntity.IsActive = true;
        follower1Entity.IsActive = true;
        follower2Entity.IsActive = true;

        leaderEntity.TaskScores = new float[18];
        follower1Entity.TaskScores = new float[18];
        follower2Entity.TaskScores = new float[18];

        // Create squad
        var squad = squadRegistry.Add(1, 3);
        squadRegistry.AddMember(squad, leaderEntity);
        squadRegistry.AddMember(squad, follower1Entity);
        squadRegistry.AddMember(squad, follower2Entity);

        // Setup boss/follower
        HiveMindSystem.AssignBoss(follower1Entity, leaderEntity);
        HiveMindSystem.AssignBoss(follower2Entity, leaderEntity);

        // Follower1 has loot
        follower1Entity.HasLootTarget = true;
        follower1Entity.IsLooting = true;
        lootClaims.TryClaim(follower1Entity.Id, 42);

        // === Follower1 dies ===
        // Simulate DeactivateBot
        follower1Entity.IsActive = false;
        squadRegistry.RemoveMember(squad, follower1Entity);
        lootClaims.ReleaseAll(follower1Entity.Id);
        follower1Entity.HasLootTarget = false;
        follower1Entity.IsLooting = false;

        // Run CleanupDeadEntities
        HiveMindSystem.CleanupDeadEntities(registry.Entities);

        // Verify follower1 cleanup
        Assert.IsFalse(follower1Entity.IsActive);
        Assert.IsNull(follower1Entity.Squad);
        Assert.IsNull(follower1Entity.Boss, "Dead follower should be detached from boss");
        Assert.AreEqual(1, leaderEntity.Followers.Count, "Boss should have 1 remaining follower");
        Assert.AreEqual(2, squad.Members.Count, "Squad should have 2 remaining members");
        Assert.IsTrue(lootClaims.TryClaim(1, 42), "Loot should be claimable after dead bot's claims released");

        // === Leader dies ===
        leaderEntity.IsActive = false;
        squadRegistry.RemoveMember(squad, leaderEntity);

        HiveMindSystem.CleanupDeadEntities(registry.Entities);

        // Verify leader cleanup
        Assert.IsFalse(leaderEntity.IsActive);
        Assert.IsNull(leaderEntity.Squad);
        Assert.AreEqual(0, leaderEntity.Followers.Count, "Dead boss's followers should be detached");
        Assert.IsNull(follower2Entity.Boss, "Remaining follower should have no boss after boss death");

        // Squad should have new leader (follower2)
        Assert.AreEqual(1, squad.Members.Count);
        Assert.AreEqual(follower2Entity, squad.Leader);
        Assert.AreEqual(SquadRole.Leader, follower2Entity.SquadRole);
    }

    [Test]
    public void E2E_DeadBotNotCountedInActiveMetrics()
    {
        var registry = new BotRegistry();
        var entities = registry.Entities;

        var bot1 = registry.Add(100);
        var bot2 = registry.Add(101);
        var bot3 = registry.Add(102);

        bot1.IsActive = true;
        bot2.IsActive = true;
        bot3.IsActive = true;
        bot1.BotType = BotType.PMC;
        bot2.BotType = BotType.PMC;
        bot3.BotType = BotType.Scav;

        Assert.AreEqual(3, HiveMindSystem.CountActive(entities));
        Assert.AreEqual(2, HiveMindSystem.CountActiveByType(entities, BotType.PMC));

        // Kill bot2 (simulate DeactivateBot)
        bot2.IsActive = false;

        Assert.AreEqual(2, HiveMindSystem.CountActive(entities));
        Assert.AreEqual(1, HiveMindSystem.CountActiveByType(entities, BotType.PMC));
    }

    [Test]
    public void E2E_DeadBotStuckCountNotInflated()
    {
        var registry = new BotRegistry();
        var entities = registry.Entities;

        var alive = registry.Add(100);
        var dead = registry.Add(101);

        alive.IsActive = true;
        dead.IsActive = true;

        alive.Movement.StuckStatus = StuckPhase.HardStuck;
        dead.Movement.StuckStatus = StuckPhase.HardStuck;

        Assert.AreEqual(2, HiveMindSystem.CountStuckBots(entities));

        // Deactivate dead bot
        dead.IsActive = false;

        Assert.AreEqual(1, HiveMindSystem.CountStuckBots(entities));
    }

    // ========================================================================
    // 10. Formation Movement Skips Dead Followers
    // ========================================================================

    [Test]
    public void Formation_DeadFollowersNotCounted()
    {
        var squad = CreateSquadWithMembers(0, out var leader, out var follower1, out var follower2);

        follower1.HasTacticalPosition = true;
        follower2.HasTacticalPosition = true;

        // Count active followers with tactical positions (mirrors BotHiveMindMonitor logic)
        int followerCount = 0;
        for (int i = 0; i < squad.Members.Count; i++)
        {
            var m = squad.Members[i];
            if (m != leader && m.IsActive && m.HasTacticalPosition)
                followerCount++;
        }

        Assert.AreEqual(2, followerCount);

        // Kill follower1
        follower1.IsActive = false;

        followerCount = 0;
        for (int i = 0; i < squad.Members.Count; i++)
        {
            var m = squad.Members[i];
            if (m != leader && m.IsActive && m.HasTacticalPosition)
                followerCount++;
        }

        Assert.AreEqual(1, followerCount);
    }

    // ========================================================================
    // 11. SquadRegistry.RemoveMember Edge Cases
    // ========================================================================

    [Test]
    public void RemoveMember_WrongSquad_NoOp()
    {
        var squad1 = _squadRegistry.Add(1, 4);
        var squad2 = _squadRegistry.Add(1, 4);
        var bot = CreateBot(0);

        _squadRegistry.AddMember(squad1, bot);

        // Try to remove from wrong squad
        _squadRegistry.RemoveMember(squad2, bot);

        // Should still be in squad1
        Assert.AreEqual(squad1, bot.Squad);
        Assert.AreEqual(1, squad1.Members.Count);
    }

    [Test]
    public void RemoveMember_NullParams_NoException()
    {
        _squadRegistry.RemoveMember(null, null);
        _squadRegistry.RemoveMember(_squadRegistry.Add(1, 4), null);
        _squadRegistry.RemoveMember(null, CreateBot(0));
    }

    [Test]
    public void AddMember_TransfersFromPreviousSquad()
    {
        var squad1 = _squadRegistry.Add(1, 4);
        var squad2 = _squadRegistry.Add(1, 4);
        var bot = CreateBot(0);

        _squadRegistry.AddMember(squad1, bot);
        Assert.AreEqual(squad1, bot.Squad);
        Assert.AreEqual(1, squad1.Members.Count);

        // Transfer to squad2
        _squadRegistry.AddMember(squad2, bot);

        Assert.AreEqual(squad2, bot.Squad);
        Assert.AreEqual(0, squad1.Members.Count);
        Assert.AreEqual(1, squad2.Members.Count);
    }
}
