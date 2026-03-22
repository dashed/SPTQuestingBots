using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.ECS;

[TestFixture]
public class BossAwarenessTests
{
    private SquadRegistry _registry;

    // Mirror the ECoverPointSpecial constants for testing (from BossAwarenessHelper)
    private const int CoverSpecialNoSnipePatrol = 1;
    private const int CoverSpecialForFollowers = 2;
    private const int CoverSpecialForBoss = 4;
    private const int CoverSpecialBossOrFollower = CoverSpecialForFollowers | CoverSpecialForBoss;

    [SetUp]
    public void SetUp()
    {
        _registry = new SquadRegistry();
    }

    // ── ECoverPointSpecial Bitmask Tests ──────────────────

    [Test]
    public void BossReservedCover_ForBossFlag_Detected()
    {
        Assert.That((CoverSpecialForBoss & CoverSpecialForBoss) != 0, Is.True);
    }

    [Test]
    public void BossReservedCover_ForFollowerFlag_NotDetected()
    {
        Assert.That((CoverSpecialForFollowers & CoverSpecialForBoss) != 0, Is.False);
    }

    [Test]
    public void BossOrFollowerReserved_BothBits_Detected()
    {
        // forBoss(4) | forFollowers(2) = 6
        int combined = CoverSpecialForBoss | CoverSpecialForFollowers;
        Assert.That((combined & CoverSpecialBossOrFollower) != 0, Is.True);
    }

    [Test]
    public void BossOrFollowerReserved_NoSnipePatrolOnly_NotDetected()
    {
        Assert.That((CoverSpecialNoSnipePatrol & CoverSpecialBossOrFollower) != 0, Is.False);
    }

    [Test]
    public void BossOrFollowerReserved_Zero_NotDetected()
    {
        Assert.That((0 & CoverSpecialBossOrFollower) != 0, Is.False);
    }

    [Test]
    public void BossOrFollowerReserved_AllFlags_Detected()
    {
        // 1 | 2 | 4 = 7
        Assert.That((7 & CoverSpecialBossOrFollower) != 0, Is.True);
    }

    [Test]
    public void CoverSpecialConstants_MatchDocumented()
    {
        Assert.That(CoverSpecialNoSnipePatrol, Is.EqualTo(1));
        Assert.That(CoverSpecialForFollowers, Is.EqualTo(2));
        Assert.That(CoverSpecialForBoss, Is.EqualTo(4));
        Assert.That(CoverSpecialBossOrFollower, Is.EqualTo(6));
    }

    // ── BotEntity Boss Fields Tests ──────────────────────

    [Test]
    public void BotEntity_BossFields_DefaultsFalse()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.IsBossBot, Is.False);
        Assert.That(entity.BossNeedProtection, Is.False);
    }

    [Test]
    public void BotEntity_BossFields_CanBeSet()
    {
        var entity = new BotEntity(0);
        entity.IsBossBot = true;
        entity.BossNeedProtection = true;

        Assert.That(entity.IsBossBot, Is.True);
        Assert.That(entity.BossNeedProtection, Is.True);
    }

    [Test]
    public void BotEntity_BossFields_IndependentFromBotType()
    {
        var entity = new BotEntity(0);
        entity.BotType = BotType.Boss;
        entity.IsBossBot = false;

        // BotType.Boss and IsBossBot are separate concepts
        // BotType.Boss means registered as a boss via our classification
        // IsBossBot means BSG's Boss.IamBoss is true
        Assert.That(entity.BotType, Is.EqualTo(BotType.Boss));
        Assert.That(entity.IsBossBot, Is.False);
    }

    // ── Boss Death Succession Tests ──────────────────────

    [Test]
    public void HandleBossDeathSuccession_NullBoss_ReturnsNull()
    {
        var result = HiveMindSystem.HandleBossDeathSuccession(null, _registry);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HandleBossDeathSuccession_NullRegistry_ReturnsNull()
    {
        var boss = new BotEntity(0);
        var result = HiveMindSystem.HandleBossDeathSuccession(boss, null);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HandleBossDeathSuccession_NoSquad_ReturnsNull()
    {
        var boss = new BotEntity(0);
        var result = HiveMindSystem.HandleBossDeathSuccession(boss, _registry);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HandleBossDeathSuccession_NotLeader_ReturnsNull()
    {
        var boss = new BotEntity(0);
        var follower = new BotEntity(1);
        var squad = _registry.Add(1, 3);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, follower);

        // follower is not the leader, so succession from follower should fail
        var result = HiveMindSystem.HandleBossDeathSuccession(follower, _registry);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HandleBossDeathSuccession_LeaderDies_SuccessorBecomesLeader()
    {
        var boss = new BotEntity(0) { IsActive = true, IsBossBot = true };
        var follower1 = new BotEntity(1) { IsActive = true };
        var follower2 = new BotEntity(2) { IsActive = true };
        var squad = _registry.Add(1, 3);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, follower1);
        _registry.AddMember(squad, follower2);

        Assert.That(squad.Leader, Is.EqualTo(boss));

        // Boss dies
        boss.IsActive = false;
        var newLeader = HiveMindSystem.HandleBossDeathSuccession(boss, _registry);

        Assert.That(newLeader, Is.Not.Null);
        Assert.That(squad.Leader, Is.EqualTo(newLeader));
        Assert.That(newLeader.SquadRole, Is.EqualTo(SquadRole.Leader));
        Assert.That(boss.SquadRole, Is.EqualTo(SquadRole.None));
    }

    [Test]
    public void HandleBossDeathSuccession_PicksFirstActiveFollower()
    {
        var boss = new BotEntity(0) { IsActive = false, IsBossBot = true };
        var follower1 = new BotEntity(1) { IsActive = true };
        var follower2 = new BotEntity(2) { IsActive = true };
        var squad = _registry.Add(1, 3);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, follower1);
        _registry.AddMember(squad, follower2);

        var newLeader = HiveMindSystem.HandleBossDeathSuccession(boss, _registry);

        // Should pick follower1 (first active member that isn't the dead boss)
        Assert.That(newLeader, Is.EqualTo(follower1));
    }

    [Test]
    public void HandleBossDeathSuccession_SkipsInactiveFollowers()
    {
        var boss = new BotEntity(0) { IsActive = false, IsBossBot = true };
        var follower1 = new BotEntity(1) { IsActive = false };
        var follower2 = new BotEntity(2) { IsActive = true };
        var squad = _registry.Add(1, 3);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, follower1);
        _registry.AddMember(squad, follower2);

        var newLeader = HiveMindSystem.HandleBossDeathSuccession(boss, _registry);

        Assert.That(newLeader, Is.EqualTo(follower2));
        Assert.That(squad.Leader, Is.EqualTo(follower2));
    }

    [Test]
    public void HandleBossDeathSuccession_AllFollowersInactive_ReturnsNull()
    {
        var boss = new BotEntity(0) { IsActive = false, IsBossBot = true };
        var follower1 = new BotEntity(1) { IsActive = false };
        var follower2 = new BotEntity(2) { IsActive = false };
        var squad = _registry.Add(1, 3);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, follower1);
        _registry.AddMember(squad, follower2);

        var result = HiveMindSystem.HandleBossDeathSuccession(boss, _registry);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HandleBossDeathSuccession_SoloSquad_ReturnsNull()
    {
        var boss = new BotEntity(0) { IsActive = false, IsBossBot = true };
        var squad = _registry.Add(1, 1);
        _registry.AddMember(squad, boss);

        // No other members to succeed
        var result = HiveMindSystem.HandleBossDeathSuccession(boss, _registry);
        Assert.That(result, Is.Null);
    }

    [Test]
    public void HandleBossDeathSuccession_NewLeaderTacticalPositionCleared()
    {
        var boss = new BotEntity(0) { IsActive = false, IsBossBot = true };
        var follower = new BotEntity(1) { IsActive = true, HasTacticalPosition = true };
        var squad = _registry.Add(1, 2);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, follower);

        HiveMindSystem.HandleBossDeathSuccession(boss, _registry);

        // New leader's tactical position should be cleared so it gets re-computed
        Assert.That(follower.HasTacticalPosition, Is.False);
    }

    [Test]
    public void HandleBossDeathSuccession_SquadMembershipPreserved()
    {
        var boss = new BotEntity(0) { IsActive = false, IsBossBot = true };
        var f1 = new BotEntity(1) { IsActive = true };
        var f2 = new BotEntity(2) { IsActive = true };
        var f3 = new BotEntity(3) { IsActive = true };
        var squad = _registry.Add(1, 4);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, f1);
        _registry.AddMember(squad, f2);
        _registry.AddMember(squad, f3);

        Assert.That(squad.Members.Count, Is.EqualTo(4));

        HiveMindSystem.HandleBossDeathSuccession(boss, _registry);

        // All members remain in the squad (dead boss included until CleanupDeadEntities)
        Assert.That(squad.Members.Count, Is.EqualTo(4));
        Assert.That(squad.Leader, Is.Not.Null);
        Assert.That(squad.Leader.IsActive, Is.True);
    }

    // ── Succession + Cleanup Integration ──────────────────

    [Test]
    public void BossDeathSuccession_ThenCleanup_SquadSurvives()
    {
        var boss = new BotEntity(0) { IsActive = false, IsBossBot = true };
        var f1 = new BotEntity(1) { IsActive = true };
        var f2 = new BotEntity(2) { IsActive = true };

        // Set up boss-follower ECS relationships
        HiveMindSystem.AssignBoss(f1, boss);
        HiveMindSystem.AssignBoss(f2, boss);

        var squad = _registry.Add(1, 3);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, f1);
        _registry.AddMember(squad, f2);

        // Step 1: boss death succession
        var newLeader = HiveMindSystem.HandleBossDeathSuccession(boss, _registry);
        Assert.That(newLeader, Is.Not.Null);

        // Step 2: cleanup dead entities (mirrors HiveMind tick)
        var allEntities = new System.Collections.Generic.List<BotEntity> { boss, f1, f2 };
        HiveMindSystem.CleanupDeadEntities(allEntities);

        // Boss references should be cleared
        Assert.That(boss.Followers.Count, Is.EqualTo(0));
        Assert.That(f1.Boss, Is.Null);
        Assert.That(f2.Boss, Is.Null);

        // Squad should still have the new leader
        Assert.That(squad.Leader, Is.EqualTo(newLeader));
    }

    [Test]
    public void BossDeathSuccession_MultipleDeaths_ChainedLeadership()
    {
        var boss = new BotEntity(0) { IsActive = true, IsBossBot = true };
        var f1 = new BotEntity(1) { IsActive = true };
        var f2 = new BotEntity(2) { IsActive = true };
        var squad = _registry.Add(1, 3);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, f1);
        _registry.AddMember(squad, f2);

        // First death: boss dies
        boss.IsActive = false;
        var leader1 = HiveMindSystem.HandleBossDeathSuccession(boss, _registry);
        Assert.That(leader1, Is.Not.Null);
        Assert.That(squad.Leader, Is.EqualTo(leader1));

        // Second death: new leader dies
        leader1.IsActive = false;
        var leader2 = HiveMindSystem.HandleBossDeathSuccession(leader1, _registry);
        Assert.That(leader2, Is.Not.Null);
        Assert.That(squad.Leader, Is.EqualTo(leader2));

        // Last survivor is the leader
        Assert.That(leader2.IsActive, Is.True);
    }

    [Test]
    public void BossDeathSuccession_LastManStanding_NoSuccessor()
    {
        var boss = new BotEntity(0) { IsActive = true, IsBossBot = true };
        var f1 = new BotEntity(1) { IsActive = true };
        var squad = _registry.Add(1, 2);
        _registry.AddMember(squad, boss);
        _registry.AddMember(squad, f1);

        // Boss dies, f1 becomes leader
        boss.IsActive = false;
        var leader1 = HiveMindSystem.HandleBossDeathSuccession(boss, _registry);
        Assert.That(leader1, Is.EqualTo(f1));

        // f1 also dies, no successor
        f1.IsActive = false;
        var leader2 = HiveMindSystem.HandleBossDeathSuccession(f1, _registry);
        Assert.That(leader2, Is.Null);
    }
}
