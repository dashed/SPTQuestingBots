using System.Collections.Generic;
using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

[TestFixture]
public class SquadRegistryTests
{
    private SquadRegistry _registry;

    [SetUp]
    public void SetUp()
    {
        _registry = new SquadRegistry();
    }

    // ── Add ──────────────────────────────────────────────────

    [Test]
    public void Add_ReturnsSquadWithIncrementingIds()
    {
        var s0 = _registry.Add(2, 4);
        var s1 = _registry.Add(2, 4);
        var s2 = _registry.Add(2, 4);

        Assert.AreEqual(0, s0.Id);
        Assert.AreEqual(1, s1.Id);
        Assert.AreEqual(2, s2.Id);
    }

    [Test]
    public void Add_SquadIsActiveByDefault()
    {
        var squad = _registry.Add(2, 4);
        Assert.IsTrue(squad.IsActive);
    }

    [Test]
    public void Add_IncrementsCount()
    {
        Assert.AreEqual(0, _registry.Count);

        _registry.Add(2, 4);
        Assert.AreEqual(1, _registry.Count);

        _registry.Add(2, 4);
        Assert.AreEqual(2, _registry.Count);
    }

    [Test]
    public void Add_SquadAppearsInActiveSquads()
    {
        var squad = _registry.Add(2, 4);

        Assert.AreEqual(1, _registry.ActiveSquads.Count);
        Assert.AreSame(squad, _registry.ActiveSquads[0]);
    }

    // ── TryGetById ──────────────────────────────────────────

    [Test]
    public void TryGetById_ValidId_ReturnsTrue()
    {
        var squad = _registry.Add(2, 4);

        Assert.IsTrue(_registry.TryGetById(squad.Id, out var found));
        Assert.AreSame(squad, found);
    }

    [Test]
    public void TryGetById_InvalidId_ReturnsFalse()
    {
        Assert.IsFalse(_registry.TryGetById(0, out var found));
        Assert.IsNull(found);

        Assert.IsFalse(_registry.TryGetById(-1, out found));
        Assert.IsNull(found);
    }

    [Test]
    public void TryGetById_RemovedId_ReturnsFalse()
    {
        var squad = _registry.Add(2, 4);
        _registry.Remove(squad);

        Assert.IsFalse(_registry.TryGetById(squad.Id, out var found));
        Assert.IsNull(found);
    }

    // ── Contains ────────────────────────────────────────────

    [Test]
    public void Contains_RegisteredId_ReturnsTrue()
    {
        var squad = _registry.Add(2, 4);
        Assert.IsTrue(_registry.Contains(squad.Id));
    }

    [Test]
    public void Contains_UnregisteredId_ReturnsFalse()
    {
        Assert.IsFalse(_registry.Contains(0));
        Assert.IsFalse(_registry.Contains(-1));
        Assert.IsFalse(_registry.Contains(999));
    }

    // ── Remove (basic) ──────────────────────────────────────

    [Test]
    public void Remove_ValidSquad_ReturnsTrue()
    {
        var squad = _registry.Add(2, 4);
        Assert.IsTrue(_registry.Remove(squad));
    }

    [Test]
    public void Remove_DecrementsCount()
    {
        var s0 = _registry.Add(2, 4);
        _registry.Add(2, 4);
        Assert.AreEqual(2, _registry.Count);

        _registry.Remove(s0);
        Assert.AreEqual(1, _registry.Count);
    }

    [Test]
    public void Remove_Null_ReturnsFalse()
    {
        Assert.IsFalse(_registry.Remove(null));
    }

    [Test]
    public void Remove_AlreadyRemoved_ReturnsFalse()
    {
        var squad = _registry.Add(2, 4);
        _registry.Remove(squad);

        Assert.IsFalse(_registry.Remove(squad));
    }

    [Test]
    public void Remove_ClearsMemberReferences()
    {
        var squad = _registry.Add(2, 4);
        var bot = new BotEntity(0);
        _registry.AddMember(squad, bot);

        _registry.Remove(squad);

        Assert.IsNull(bot.Squad);
        Assert.AreEqual(SquadRole.None, bot.SquadRole);
        Assert.IsFalse(bot.HasTacticalPosition);
    }

    // ── Swap-Remove ─────────────────────────────────────────

    [Test]
    public void Remove_MiddleElement_SwapsLastIntoGap()
    {
        var s0 = _registry.Add(2, 4);
        var s1 = _registry.Add(2, 4);
        var s2 = _registry.Add(2, 4);

        _registry.Remove(s0);

        Assert.AreEqual(2, _registry.Count);

        // s2 should be swapped into index 0
        Assert.AreSame(s2, _registry.ActiveSquads[0]);
        Assert.AreSame(s1, _registry.ActiveSquads[1]);

        // ID lookups still work
        Assert.IsTrue(_registry.Contains(1));
        Assert.IsTrue(_registry.Contains(2));
        Assert.IsFalse(_registry.Contains(0));
    }

    [Test]
    public void Remove_OnlySquad_EmptiesRegistry()
    {
        var squad = _registry.Add(2, 4);
        _registry.Remove(squad);

        Assert.AreEqual(0, _registry.Count);
        Assert.IsFalse(_registry.Contains(squad.Id));
    }

    // ── ID Recycling ────────────────────────────────────────

    [Test]
    public void IdRecycling_FreedIdIsReused()
    {
        var s0 = _registry.Add(2, 4);
        _registry.Add(2, 4);

        _registry.Remove(s0); // frees id=0

        var s2 = _registry.Add(2, 4); // should recycle id=0
        Assert.AreEqual(0, s2.Id);
    }

    // ── GetOrCreate ─────────────────────────────────────────

    [Test]
    public void GetOrCreate_NewGroupId_CreatesSquad()
    {
        var squad = _registry.GetOrCreate(100, 3, 4);

        Assert.IsNotNull(squad);
        Assert.AreEqual(1, _registry.Count);
        Assert.AreEqual(3, squad.StrategyScores.Length);
        Assert.AreEqual(4, squad.TargetMembersCount);
    }

    [Test]
    public void GetOrCreate_ExistingGroupId_ReturnsSameSquad()
    {
        var first = _registry.GetOrCreate(100, 3, 4);
        var second = _registry.GetOrCreate(100, 5, 6);

        Assert.AreSame(first, second);
        Assert.AreEqual(1, _registry.Count);
    }

    [Test]
    public void GetOrCreate_DifferentGroupIds_CreatesDifferentSquads()
    {
        var s1 = _registry.GetOrCreate(100, 3, 4);
        var s2 = _registry.GetOrCreate(200, 3, 4);

        Assert.AreNotSame(s1, s2);
        Assert.AreEqual(2, _registry.Count);
    }

    // ── GetByBsgGroupId ─────────────────────────────────────

    [Test]
    public void GetByBsgGroupId_Registered_ReturnsSquad()
    {
        var squad = _registry.GetOrCreate(100, 3, 4);
        var found = _registry.GetByBsgGroupId(100);

        Assert.AreSame(squad, found);
    }

    [Test]
    public void GetByBsgGroupId_NotRegistered_ReturnsNull()
    {
        Assert.IsNull(_registry.GetByBsgGroupId(999));
    }

    [Test]
    public void GetByBsgGroupId_AfterRemove_ReturnsNull()
    {
        var squad = _registry.GetOrCreate(100, 3, 4);
        _registry.Remove(squad);

        Assert.IsNull(_registry.GetByBsgGroupId(100));
    }

    // ── AddMember ───────────────────────────────────────────

    [Test]
    public void AddMember_FirstMember_BecomesLeader()
    {
        var squad = _registry.Add(2, 4);
        var bot = new BotEntity(0);

        _registry.AddMember(squad, bot);

        Assert.AreSame(bot, squad.Leader);
        Assert.AreEqual(SquadRole.Leader, bot.SquadRole);
        Assert.AreSame(squad, bot.Squad);
        Assert.AreEqual(1, squad.Size);
    }

    [Test]
    public void AddMember_SubsequentMembers_GetGuardRole()
    {
        var squad = _registry.Add(2, 4);
        var leader = new BotEntity(0);
        var guard = new BotEntity(1);

        _registry.AddMember(squad, leader);
        _registry.AddMember(squad, guard);

        Assert.AreEqual(SquadRole.Leader, leader.SquadRole);
        Assert.AreEqual(SquadRole.Guard, guard.SquadRole);
        Assert.AreEqual(2, squad.Size);
    }

    [Test]
    public void AddMember_AlreadyInSquad_NoOp()
    {
        var squad = _registry.Add(2, 4);
        var bot = new BotEntity(0);

        _registry.AddMember(squad, bot);
        _registry.AddMember(squad, bot); // duplicate

        Assert.AreEqual(1, squad.Size);
    }

    [Test]
    public void AddMember_InDifferentSquad_MovesToNewSquad()
    {
        var squad1 = _registry.Add(2, 4);
        var squad2 = _registry.Add(2, 4);
        var bot = new BotEntity(0);

        _registry.AddMember(squad1, bot);
        _registry.AddMember(squad2, bot);

        Assert.AreEqual(0, squad1.Size);
        Assert.AreEqual(1, squad2.Size);
        Assert.AreSame(squad2, bot.Squad);
    }

    [Test]
    public void AddMember_NullParams_NoOp()
    {
        var squad = _registry.Add(2, 4);

        _registry.AddMember(null, new BotEntity(0));
        _registry.AddMember(squad, null);

        Assert.AreEqual(0, squad.Size);
    }

    // ── RemoveMember ────────────────────────────────────────

    [Test]
    public void RemoveMember_ClearsReferences()
    {
        var squad = _registry.Add(2, 4);
        var bot = new BotEntity(0);

        _registry.AddMember(squad, bot);
        bot.HasTacticalPosition = true;

        _registry.RemoveMember(squad, bot);

        Assert.IsNull(bot.Squad);
        Assert.AreEqual(SquadRole.None, bot.SquadRole);
        Assert.IsFalse(bot.HasTacticalPosition);
        Assert.AreEqual(0, squad.Size);
    }

    [Test]
    public void RemoveMember_Leader_ReassignsToNextMember()
    {
        var squad = _registry.Add(2, 4);
        var leader = new BotEntity(0);
        var second = new BotEntity(1);

        _registry.AddMember(squad, leader);
        _registry.AddMember(squad, second);

        _registry.RemoveMember(squad, leader);

        Assert.AreSame(second, squad.Leader);
        Assert.AreEqual(SquadRole.Leader, second.SquadRole);
        Assert.AreEqual(1, squad.Size);
    }

    [Test]
    public void RemoveMember_LastMember_LeaderBecomesNull()
    {
        var squad = _registry.Add(2, 4);
        var bot = new BotEntity(0);

        _registry.AddMember(squad, bot);
        _registry.RemoveMember(squad, bot);

        Assert.IsNull(squad.Leader);
        Assert.AreEqual(0, squad.Size);
    }

    [Test]
    public void RemoveMember_NotInSquad_NoOp()
    {
        var squad = _registry.Add(2, 4);
        var bot = new BotEntity(0);

        _registry.RemoveMember(squad, bot); // not a member — no crash
        Assert.AreEqual(0, squad.Size);
    }

    [Test]
    public void RemoveMember_NullParams_NoOp()
    {
        var squad = _registry.Add(2, 4);

        _registry.RemoveMember(null, new BotEntity(0));
        _registry.RemoveMember(squad, null);
    }

    // ── Clear ───────────────────────────────────────────────

    [Test]
    public void Clear_ResetsEverything()
    {
        var squad = _registry.Add(2, 4);
        var bot = new BotEntity(0);
        _registry.AddMember(squad, bot);

        _registry.Clear();

        Assert.AreEqual(0, _registry.Count);
        Assert.IsNull(bot.Squad);
        Assert.AreEqual(SquadRole.None, bot.SquadRole);
    }

    [Test]
    public void Clear_ThenAdd_WorksCorrectly()
    {
        _registry.Add(2, 4);
        _registry.Clear();

        var fresh = _registry.Add(2, 4);
        Assert.AreEqual(0, fresh.Id);
        Assert.AreEqual(1, _registry.Count);
    }

    // ── Stress / Integration ────────────────────────────────

    [Test]
    public void StressTest_AddRemoveRecycleCycle()
    {
        var alive = new List<SquadEntity>();

        for (int i = 0; i < 20; i++)
        {
            alive.Add(_registry.Add(2, 4));
        }

        Assert.AreEqual(20, _registry.Count);

        for (int i = 0; i < 10; i++)
        {
            var toRemove = alive[0];
            alive.RemoveAt(0);
            _registry.Remove(toRemove);
        }

        Assert.AreEqual(10, _registry.Count);

        for (int i = 0; i < 5; i++)
        {
            alive.Add(_registry.Add(2, 4));
        }

        Assert.AreEqual(15, _registry.Count);

        foreach (var squad in alive)
        {
            Assert.IsTrue(_registry.Contains(squad.Id));
            Assert.IsTrue(_registry.TryGetById(squad.Id, out _));
        }
    }

    [Test]
    public void Constructor_Default_StartsEmpty()
    {
        var registry = new SquadRegistry();
        Assert.AreEqual(0, registry.Count);
    }

    [Test]
    public void Constructor_CustomCapacity_StartsEmpty()
    {
        var registry = new SquadRegistry(64);
        Assert.AreEqual(0, registry.Count);
    }
}
