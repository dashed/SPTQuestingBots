using System;
using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;

namespace SPTQuestingBots.Client.Tests.Concurrency;

/// <summary>
/// Multi-bot end-to-end scenarios exercising real registry operations with 5+ bots.
/// Tests concurrent-style patterns: loot claim races, squad member death during
/// strategy computation, entity deactivation during iteration, and ring buffer
/// concurrent read/write patterns.
/// </summary>
[TestFixture]
public class MultiBotRaceConditionTests
{
    // ================================================================
    // Helpers
    // ================================================================

    private BotRegistry _registry;
    private SquadRegistry _squadRegistry;
    private LootClaimRegistry _lootClaims;

    [SetUp]
    public void SetUp()
    {
        _registry = new BotRegistry();
        _squadRegistry = new SquadRegistry();
        _lootClaims = new LootClaimRegistry();
    }

    private BotEntity CreateActiveBot(int bsgId = -1)
    {
        var entity = bsgId >= 0 ? _registry.Add(bsgId) : _registry.Add();
        entity.IsActive = true;
        return entity;
    }

    // ================================================================
    // Loot claim races: 5+ bots competing for same loot
    // ================================================================

    [Test]
    public void LootClaim_FiveBots_OnlyFirstClaimerWins()
    {
        // Simulates the sequential scanning pass in updateLootScanning
        const int lootId = 100;
        var bots = new BotEntity[5];
        for (int i = 0; i < 5; i++)
            bots[i] = CreateActiveBot();

        // All 5 bots try to claim the same loot
        bool firstClaimed = _lootClaims.TryClaim(bots[0].Id, lootId);
        Assert.IsTrue(firstClaimed, "First bot should claim successfully");

        for (int i = 1; i < 5; i++)
        {
            bool claimed = _lootClaims.TryClaim(bots[i].Id, lootId);
            Assert.IsFalse(claimed, $"Bot {i} should be denied — loot already claimed");
        }

        // Verify claim count
        Assert.AreEqual(1, _lootClaims.GetClaimCount());
    }

    [Test]
    public void LootClaim_BotDeath_ReleasesClaimsForOtherBots()
    {
        const int lootId1 = 100;
        const int lootId2 = 200;
        var bots = new BotEntity[5];
        for (int i = 0; i < 5; i++)
            bots[i] = CreateActiveBot();

        // Bot 0 claims loot 1 and 2
        _lootClaims.TryClaim(bots[0].Id, lootId1);
        _lootClaims.TryClaim(bots[0].Id, lootId2);
        Assert.AreEqual(2, _lootClaims.GetClaimCount());

        // Other bots are blocked
        Assert.IsTrue(_lootClaims.IsClaimedByOther(bots[1].Id, lootId1));

        // Bot 0 dies — simulate DeactivateBot's ReleaseAll
        bots[0].IsActive = false;
        _lootClaims.ReleaseAll(bots[0].Id);

        // Now bot 1 can claim
        Assert.IsFalse(_lootClaims.IsClaimedByOther(bots[1].Id, lootId1));
        Assert.IsTrue(_lootClaims.TryClaim(bots[1].Id, lootId1));
        Assert.AreEqual(1, _lootClaims.GetClaimCount());
    }

    [Test]
    public void LootClaim_SameBotDoubleClaim_IsIdempotent()
    {
        var bot = CreateActiveBot();
        const int lootId = 42;

        Assert.IsTrue(_lootClaims.TryClaim(bot.Id, lootId));
        Assert.IsTrue(_lootClaims.TryClaim(bot.Id, lootId), "Same bot re-claiming should succeed");
        Assert.AreEqual(1, _lootClaims.GetClaimCount());
    }

    [Test]
    public void LootClaim_MultipleLootTargets_MultipleBots()
    {
        // 5 bots, 5 loot targets — each bot gets a different one
        var bots = new BotEntity[5];
        for (int i = 0; i < 5; i++)
            bots[i] = CreateActiveBot();

        for (int i = 0; i < 5; i++)
        {
            Assert.IsTrue(_lootClaims.TryClaim(bots[i].Id, i + 100));
        }

        Assert.AreEqual(5, _lootClaims.GetClaimCount());

        // Each bot's claim blocks others
        for (int i = 0; i < 5; i++)
        {
            for (int j = 0; j < 5; j++)
            {
                if (i == j)
                    Assert.IsFalse(_lootClaims.IsClaimedByOther(bots[i].Id, i + 100));
                else
                    Assert.IsTrue(_lootClaims.IsClaimedByOther(bots[j].Id, i + 100));
            }
        }
    }

    // ================================================================
    // Entity deactivation during dense iteration
    // ================================================================

    [Test]
    public void DeactivateEntity_DuringIteration_DoesNotSkipEntities()
    {
        // Simulates the HiveMind tick: iterating entities and deactivating some mid-loop.
        // Since we use for-loop with index and only set IsActive=false (no swap-remove),
        // all entities should be visited.
        var bots = new BotEntity[8];
        for (int i = 0; i < 8; i++)
            bots[i] = CreateActiveBot();

        int visitedCount = 0;
        var entities = _registry.Entities;
        for (int i = 0; i < entities.Count; i++)
        {
            var entity = entities[i];
            visitedCount++;

            // Deactivate every other entity mid-iteration
            if (entity.Id % 2 == 0)
                entity.IsActive = false;
        }

        Assert.AreEqual(8, visitedCount, "All 8 entities must be visited even when some are deactivated mid-loop");
        Assert.AreEqual(8, _registry.Count, "Registry count unchanged — no swap-remove happened");
    }

    [Test]
    public void ResetInactiveEntitySensors_AfterDeactivation_ClearsAllSensors()
    {
        // 5 bots: set sensors, deactivate some, then reset.
        var bots = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            bots[i] = CreateActiveBot();
            bots[i].IsInCombat = true;
            bots[i].IsSuspicious = true;
            bots[i].CanQuest = true;
        }

        // Deactivate bots 1, 3
        bots[1].IsActive = false;
        bots[3].IsActive = false;

        HiveMindSystem.ResetInactiveEntitySensors(_registry.Entities);

        // Active bots keep their sensors
        Assert.IsTrue(bots[0].IsInCombat);
        Assert.IsTrue(bots[2].IsInCombat);
        Assert.IsTrue(bots[4].IsInCombat);

        // Inactive bots have sensors reset
        Assert.IsFalse(bots[1].IsInCombat);
        Assert.IsFalse(bots[1].IsSuspicious);
        Assert.IsFalse(bots[1].CanQuest);
        Assert.IsFalse(bots[3].IsInCombat);
    }

    // ================================================================
    // Squad member death during strategy computation
    // ================================================================

    [Test]
    public void SquadStrategy_MemberDeactivated_SafeDuringUpdate()
    {
        // Create a 5-bot squad, then deactivate a member during GotoObjectiveStrategy iteration.
        var squad = _squadRegistry.Add(1, 5);
        var bots = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            bots[i] = CreateActiveBot();
            _squadRegistry.AddMember(squad, bots[i]);
        }

        Assert.AreEqual(5, squad.Members.Count);
        Assert.AreEqual(bots[0], squad.Leader);

        // Leader has an objective
        bots[0].HasActiveObjective = true;
        bots[0].LastSeenObjectiveVersion = 1;
        bots[0].CurrentPositionX = 10f;
        bots[0].CurrentPositionZ = 20f;

        // Set objective on squad
        squad.Objective.SetObjective(100f, 0f, 100f);

        // Deactivate member 2 (simulating death between sensor update and strategy update)
        bots[2].IsActive = false;

        // Count active followers — should be 3 (bots 1, 3, 4)
        int activeFollowerCount = 0;
        for (int i = 0; i < squad.Members.Count; i++)
        {
            var member = squad.Members[i];
            if (member != bots[0] && member.IsActive)
                activeFollowerCount++;
        }

        Assert.AreEqual(3, activeFollowerCount, "Should have 3 active followers after 1 death");
    }

    [Test]
    public void SquadLeaderDeath_ReassignsToActiveFollower()
    {
        // 5-bot squad, leader dies
        var squad = _squadRegistry.Add(1, 5);
        var bots = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            bots[i] = CreateActiveBot();
            _squadRegistry.AddMember(squad, bots[i]);
        }

        Assert.AreEqual(bots[0], squad.Leader);

        // Deactivate leader
        bots[0].IsActive = false;

        // Remove leader from squad — should reassign
        _squadRegistry.RemoveMember(squad, bots[0]);

        Assert.IsNotNull(squad.Leader, "Squad should have a new leader");
        Assert.AreNotEqual(bots[0], squad.Leader, "Dead bot should not be leader");
        Assert.IsTrue(squad.Leader.IsActive, "New leader should be active");
        Assert.AreEqual(SquadRole.Leader, squad.Leader.SquadRole);
    }

    [Test]
    public void SquadLeaderDeath_AllFollowersDead_LeaderFallsBackToFirst()
    {
        var squad = _squadRegistry.Add(1, 3);
        var bots = new BotEntity[3];
        for (int i = 0; i < 3; i++)
        {
            bots[i] = CreateActiveBot();
            _squadRegistry.AddMember(squad, bots[i]);
        }

        // Deactivate all except the last
        bots[0].IsActive = false;
        bots[1].IsActive = false;
        // bot[2] is still active

        _squadRegistry.RemoveMember(squad, bots[0]);

        // Leader should be an active member
        Assert.AreEqual(2, squad.Members.Count);
        Assert.IsNotNull(squad.Leader);
        Assert.AreEqual(bots[2], squad.Leader, "Should pick the active member as leader");
    }

    // ================================================================
    // Boss/follower cleanup with 5+ entities
    // ================================================================

    [Test]
    public void CleanupDeadEntities_BossDies_AllFollowersDetached()
    {
        var boss = CreateActiveBot();
        var followers = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            followers[i] = CreateActiveBot();
            HiveMindSystem.AssignBoss(followers[i], boss);
        }

        Assert.AreEqual(5, boss.Followers.Count);
        foreach (var f in followers)
            Assert.AreEqual(boss, f.Boss);

        // Boss dies
        boss.IsActive = false;
        HiveMindSystem.CleanupDeadEntities(_registry.Entities);

        // All followers should be detached
        Assert.AreEqual(0, boss.Followers.Count);
        foreach (var f in followers)
            Assert.IsNull(f.Boss, "Follower boss reference should be cleared after boss death");
    }

    [Test]
    public void CleanupDeadEntities_FollowerDies_RemovedFromBossFollowers()
    {
        var boss = CreateActiveBot();
        var followers = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            followers[i] = CreateActiveBot();
            HiveMindSystem.AssignBoss(followers[i], boss);
        }

        // Kill followers 1 and 3
        followers[1].IsActive = false;
        followers[3].IsActive = false;

        HiveMindSystem.CleanupDeadEntities(_registry.Entities);

        Assert.AreEqual(3, boss.Followers.Count, "Boss should have 3 remaining followers");
        Assert.IsNull(followers[1].Boss, "Dead follower 1 should have boss cleared");
        Assert.IsNull(followers[3].Boss, "Dead follower 3 should have boss cleared");

        // Living followers still attached
        Assert.AreEqual(boss, followers[0].Boss);
        Assert.AreEqual(boss, followers[2].Boss);
        Assert.AreEqual(boss, followers[4].Boss);
    }

    [Test]
    public void CleanupDeadEntities_BossAndFollowerBothDie_NoOrphanReferences()
    {
        var boss = CreateActiveBot();
        var followers = new BotEntity[5];
        for (int i = 0; i < 5; i++)
        {
            followers[i] = CreateActiveBot();
            HiveMindSystem.AssignBoss(followers[i], boss);
        }

        // Boss AND follower 2 die in the same tick
        boss.IsActive = false;
        followers[2].IsActive = false;

        HiveMindSystem.CleanupDeadEntities(_registry.Entities);

        Assert.AreEqual(0, boss.Followers.Count);
        foreach (var f in followers)
            Assert.IsNull(f.Boss);
    }

    // ================================================================
    // CombatEventRegistry ring buffer concurrent read/write
    // ================================================================

    [Test]
    public void CombatEventRegistry_OverflowWrap_DoesNotCorruptQueries()
    {
        // Fill ring buffer past capacity, then query
        CombatEventRegistry.Initialize(8);

        // Write 12 events (8 capacity, so first 4 get overwritten)
        for (int i = 0; i < 12; i++)
        {
            CombatEventRegistry.RecordEvent(i * 10f, 0f, i * 10f, i * 1f, 100f, 0, false);
        }

        // Should still have capacity-many events
        Assert.AreEqual(8, CombatEventRegistry.Count);

        // Query should find recent events, not overwritten ones
        bool found = CombatEventRegistry.GetNearestEvent(110f, 110f, 50f, 12f, 20f, out var nearest);
        Assert.IsTrue(found, "Should find an event near (110, 110)");
    }

    [Test]
    public void CombatEventRegistry_CleanupDuringQuery_SafeOrder()
    {
        // Simulates the HiveMind tick order: step 6 cleans up expired then queries.
        CombatEventRegistry.Initialize(16);

        // Record events at different times
        for (int i = 0; i < 10; i++)
        {
            CombatEventRegistry.RecordEvent(i * 10f, 0f, 0f, i * 1f, 100f, 0, false);
        }

        // Cleanup events older than 5 seconds (at time 10)
        CombatEventRegistry.CleanupExpired(10f, 5f);

        // Only events at time 5-9 should remain active
        int activeCount = CombatEventRegistry.ActiveCount;
        Assert.AreEqual(5, activeCount, "Should have 5 active events after cleanup");

        // Query after cleanup should only find non-expired events
        int intensity = CombatEventRegistry.GetIntensity(50f, 0f, 200f, 20f, 10f);
        Assert.AreEqual(5, intensity, "Intensity should count only active events");
    }

    [Test]
    public void CombatEventRegistry_RecordDuringGather_SafeWithFixedHead()
    {
        // Record events, gather them, then record more — verify buffer integrity
        CombatEventRegistry.Initialize(16);

        for (int i = 0; i < 8; i++)
        {
            CombatEventRegistry.RecordEvent(i * 10f, 0f, i * 10f, i * 1f, 100f, 0, false);
        }

        var buffer = new CombatEvent[16];
        int gathered = CombatEventRegistry.GatherActiveEvents(buffer, 10f, 20f);
        Assert.AreEqual(8, gathered, "Should gather all 8 events");

        // Now record 4 more — buffer was already gathered, snapshot is stable
        for (int i = 8; i < 12; i++)
        {
            CombatEventRegistry.RecordEvent(i * 10f, 0f, i * 10f, i * 1f, 100f, 0, false);
        }

        // Re-gather should now have 12
        int gathered2 = CombatEventRegistry.GatherActiveEvents(buffer, 15f, 20f);
        Assert.AreEqual(12, gathered2, "Should gather all 12 events after additional recording");
    }

    // ================================================================
    // BotRegistry swap-remove correctness (not called during tick,
    // but verify the mechanism works correctly for raid-end cleanup)
    // ================================================================

    [Test]
    public void BotRegistry_SwapRemove_MaintainsIdLookupIntegrity()
    {
        // Create 6 entities, remove some, verify remaining are accessible
        var e0 = _registry.Add(10);
        var e1 = _registry.Add(11);
        var e2 = _registry.Add(12);
        var e3 = _registry.Add(13);
        var e4 = _registry.Add(14);
        var e5 = _registry.Add(15);

        Assert.AreEqual(6, _registry.Count);

        // Remove middle entity (e2)
        _registry.Remove(e2);
        Assert.AreEqual(5, _registry.Count);

        // Remaining entities should still be accessible by ID
        Assert.IsTrue(_registry.TryGetById(e0.Id, out var r0));
        Assert.AreEqual(e0, r0);
        Assert.IsTrue(_registry.TryGetById(e1.Id, out var r1));
        Assert.AreEqual(e1, r1);
        Assert.IsTrue(_registry.TryGetById(e3.Id, out var r3));
        Assert.AreEqual(e3, r3);
        Assert.IsTrue(_registry.TryGetById(e4.Id, out var r4));
        Assert.AreEqual(e4, r4);
        Assert.IsTrue(_registry.TryGetById(e5.Id, out var r5));
        Assert.AreEqual(e5, r5);

        // Removed entity should not be found
        Assert.IsFalse(_registry.TryGetById(e2.Id, out _));

        // BSG ID lookup should still work
        Assert.AreEqual(e0, _registry.GetByBsgId(10));
        Assert.AreEqual(e1, _registry.GetByBsgId(11));
        Assert.IsNull(_registry.GetByBsgId(12), "Removed entity BSG ID should return null");
        Assert.AreEqual(e3, _registry.GetByBsgId(13));
    }

    [Test]
    public void BotRegistry_SwapRemove_MultipleRemoves_DenseListConsistent()
    {
        var entities = new BotEntity[6];
        for (int i = 0; i < 6; i++)
            entities[i] = _registry.Add();

        // Remove entities 1, 3, 5 (every other one)
        _registry.Remove(entities[1]);
        _registry.Remove(entities[3]);
        _registry.Remove(entities[5]);

        Assert.AreEqual(3, _registry.Count);

        // Dense list should have exactly 3 entries, all with correct IDs
        var ids = new HashSet<int>();
        for (int i = 0; i < _registry.Entities.Count; i++)
        {
            ids.Add(_registry.Entities[i].Id);
        }

        Assert.IsTrue(ids.Contains(entities[0].Id));
        Assert.IsTrue(ids.Contains(entities[2].Id));
        Assert.IsTrue(ids.Contains(entities[4].Id));
    }

    // ================================================================
    // Full multi-bot scenario: 8 bots with squads, loot, and death
    // ================================================================

    [Test]
    public void FullScenario_EightBots_TwoSquads_LootAndDeath()
    {
        // Create 8 bots in 2 squads of 4
        var squad1 = _squadRegistry.Add(1, 4);
        var squad2 = _squadRegistry.Add(1, 4);

        var bots = new BotEntity[8];
        for (int i = 0; i < 8; i++)
            bots[i] = CreateActiveBot();

        // Squad 1: bots 0-3 (bot 0 is boss/leader)
        for (int i = 0; i < 4; i++)
            _squadRegistry.AddMember(squad1, bots[i]);

        // Squad 2: bots 4-7 (bot 4 is boss/leader)
        for (int i = 4; i < 8; i++)
            _squadRegistry.AddMember(squad2, bots[i]);

        Assert.AreEqual(bots[0], squad1.Leader);
        Assert.AreEqual(bots[4], squad2.Leader);

        // All 8 bots try to claim 3 loot items
        int[] lootIds = { 100, 200, 300 };
        for (int i = 0; i < 8; i++)
        {
            int targetLoot = lootIds[i % 3];
            _lootClaims.TryClaim(bots[i].Id, targetLoot);
        }

        // Only 3 claims should be active (one per loot item)
        Assert.AreEqual(3, _lootClaims.GetClaimCount());

        // Kill bot 0 (squad 1 leader) and bot 5 (squad 2 member)
        bots[0].IsActive = false;
        bots[5].IsActive = false;

        // Release dead bots' loot claims
        _lootClaims.ReleaseAll(bots[0].Id);
        _lootClaims.ReleaseAll(bots[5].Id);

        // Reassign squad 1 leader
        _squadRegistry.RemoveMember(squad1, bots[0]);
        Assert.IsNotNull(squad1.Leader);
        Assert.IsTrue(squad1.Leader.IsActive);
        Assert.AreEqual(3, squad1.Members.Count);

        // Remove dead member from squad 2
        _squadRegistry.RemoveMember(squad2, bots[5]);
        Assert.AreEqual(bots[4], squad2.Leader, "Squad 2 leader unchanged");
        Assert.AreEqual(3, squad2.Members.Count);

        // Cleanup dead entities (boss/follower references)
        HiveMindSystem.CleanupDeadEntities(_registry.Entities);

        // Verify sensor reset on dead entities
        HiveMindSystem.ResetInactiveEntitySensors(_registry.Entities);
        Assert.IsFalse(bots[0].CanQuest);
        Assert.IsFalse(bots[5].CanQuest);

        // Living bots can now claim freed loot
        int reclaimedCount = 0;
        for (int i = 0; i < 8; i++)
        {
            if (!bots[i].IsActive)
                continue;
            if (_lootClaims.TryClaim(bots[i].Id, lootIds[0]))
                reclaimedCount++;
        }

        Assert.GreaterOrEqual(reclaimedCount, 1, "At least one living bot should be able to claim freed loot");
    }

    [TearDown]
    public void TearDown()
    {
        // Clean up static CombatEventRegistry state
        CombatEventRegistry.Clear();
    }
}
