using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class SquadLootCoordinatorTests
{
    private LootClaimRegistry _claims;

    [SetUp]
    public void SetUp()
    {
        _claims = new LootClaimRegistry();
    }

    // ── BossPriorityClaim ─────────────────────────────────────────

    [Test]
    public void BossPriorityClaim_EmptyResults_ReturnsNegativeOne()
    {
        var results = new LootScanResult[4];
        int index = SquadLootCoordinator.BossPriorityClaim(results, 0, _claims, bossEntityId: 1);
        Assert.That(index, Is.EqualTo(-1));
    }

    [Test]
    public void BossPriorityClaim_SingleResult_ClaimsAndReturnsZero()
    {
        var results = new LootScanResult[]
        {
            new LootScanResult { Id = 100, Value = 5000f },
        };

        int index = SquadLootCoordinator.BossPriorityClaim(results, 1, _claims, bossEntityId: 1);

        Assert.That(index, Is.EqualTo(0));
        Assert.IsFalse(_claims.IsClaimedByOther(1, 100));
        Assert.IsTrue(_claims.IsClaimedByOther(2, 100));
    }

    [Test]
    public void BossPriorityClaim_MultipleResults_ClaimsHighestValue()
    {
        var results = new LootScanResult[]
        {
            new LootScanResult { Id = 100, Value = 3000f },
            new LootScanResult { Id = 101, Value = 8000f },
            new LootScanResult { Id = 102, Value = 5000f },
        };

        int index = SquadLootCoordinator.BossPriorityClaim(results, 3, _claims, bossEntityId: 1);

        Assert.That(index, Is.EqualTo(1));
        Assert.IsTrue(_claims.IsClaimedByOther(2, 101));
    }

    [Test]
    public void BossPriorityClaim_SkipsItemsClaimedByOthers()
    {
        _claims.TryClaim(2, 101); // Another bot claimed item 101

        var results = new LootScanResult[]
        {
            new LootScanResult { Id = 100, Value = 3000f },
            new LootScanResult { Id = 101, Value = 8000f },
            new LootScanResult { Id = 102, Value = 5000f },
        };

        int index = SquadLootCoordinator.BossPriorityClaim(results, 3, _claims, bossEntityId: 1);

        Assert.That(index, Is.EqualTo(2)); // Highest unclaimed
    }

    [Test]
    public void BossPriorityClaim_AllClaimedByOthers_ReturnsNegativeOne()
    {
        _claims.TryClaim(2, 100);
        _claims.TryClaim(3, 101);

        var results = new LootScanResult[]
        {
            new LootScanResult { Id = 100, Value = 3000f },
            new LootScanResult { Id = 101, Value = 8000f },
        };

        int index = SquadLootCoordinator.BossPriorityClaim(results, 2, _claims, bossEntityId: 1);

        Assert.That(index, Is.EqualTo(-1));
    }

    [Test]
    public void BossPriorityClaim_BossOwnClaim_NotSkipped()
    {
        _claims.TryClaim(1, 100); // Boss already claimed this

        var results = new LootScanResult[]
        {
            new LootScanResult { Id = 100, Value = 8000f },
            new LootScanResult { Id = 101, Value = 3000f },
        };

        int index = SquadLootCoordinator.BossPriorityClaim(results, 2, _claims, bossEntityId: 1);

        Assert.That(index, Is.EqualTo(0)); // Boss's own claim is not "claimed by other"
    }

    [Test]
    public void BossPriorityClaim_RespectsScanCount()
    {
        var results = new LootScanResult[]
        {
            new LootScanResult { Id = 100, Value = 3000f },
            new LootScanResult { Id = 101, Value = 8000f },
            new LootScanResult { Id = 102, Value = 12000f }, // Beyond count=2
        };

        int index = SquadLootCoordinator.BossPriorityClaim(results, 2, _claims, bossEntityId: 1);

        Assert.That(index, Is.EqualTo(1)); // Only checks first 2
    }

    [Test]
    public void BossPriorityClaim_EqualValues_ReturnsFirstEncountered()
    {
        var results = new LootScanResult[]
        {
            new LootScanResult { Id = 100, Value = 5000f },
            new LootScanResult { Id = 101, Value = 5000f },
        };

        int index = SquadLootCoordinator.BossPriorityClaim(results, 2, _claims, bossEntityId: 1);

        // With > comparison, first one wins when equal
        Assert.That(index, Is.EqualTo(0));
    }

    // ── ShouldFollowerLoot ────────────────────────────────────────

    private BotEntity CreateEntity(int id)
    {
        return new BotEntity(id);
    }

    private (BotEntity follower, BotEntity boss) CreateNearbyPair()
    {
        var follower = CreateEntity(1);
        var boss = CreateEntity(2);
        // Position them close together (within 35m comm range squared = 1225)
        follower.CurrentPositionX = 10f;
        follower.CurrentPositionZ = 10f;
        boss.CurrentPositionX = 15f;
        boss.CurrentPositionZ = 15f;
        return (follower, boss);
    }

    [Test]
    public void ShouldFollowerLoot_FollowerInCombat_ReturnsFalse()
    {
        var (follower, boss) = CreateNearbyPair();
        follower.IsInCombat = true;
        boss.IsLooting = true;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_BossInCombat_ReturnsFalse()
    {
        var (follower, boss) = CreateNearbyPair();
        boss.IsInCombat = true;
        boss.IsLooting = true;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_OutOfCommRange_ReturnsFalse()
    {
        var follower = CreateEntity(1);
        var boss = CreateEntity(2);
        follower.CurrentPositionX = 0f;
        follower.CurrentPositionZ = 0f;
        boss.CurrentPositionX = 100f;
        boss.CurrentPositionZ = 100f;
        boss.IsLooting = true;

        // commRangeSqr = 35*35 = 1225
        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_BossLooting_ReturnsTrue()
    {
        var (follower, boss) = CreateNearbyPair();
        boss.IsLooting = true;

        Assert.IsTrue(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_BossAtObjective_ReturnsTrue()
    {
        var (follower, boss) = CreateNearbyPair();
        boss.HasActiveObjective = true;
        boss.IsCloseToObjective = true;

        Assert.IsTrue(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_BossHasObjectiveButNotClose_ReturnsFalse()
    {
        var (follower, boss) = CreateNearbyPair();
        boss.HasActiveObjective = true;
        boss.IsCloseToObjective = false;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_FollowerAtTacticalPosition_ReturnsTrue()
    {
        var (follower, boss) = CreateNearbyPair();
        follower.HasTacticalPosition = true;
        follower.TacticalPositionX = 11f;
        follower.TacticalPositionZ = 11f;
        // Distance to tactical ~1.4m, well within 5m threshold

        Assert.IsTrue(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_FollowerFarFromTactical_ReturnsFalse()
    {
        var (follower, boss) = CreateNearbyPair();
        follower.HasTacticalPosition = true;
        follower.TacticalPositionX = 30f; // Far from follower at 10
        follower.TacticalPositionZ = 30f;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_FollowerApproachingLoot_ReturnsFalse()
    {
        var (follower, boss) = CreateNearbyPair();
        follower.HasTacticalPosition = true;
        follower.IsApproachingLoot = true;
        follower.TacticalPositionX = 11f;
        follower.TacticalPositionZ = 11f;

        // IsApproachingLoot blocks the tactical position check
        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_NoneOfTheConditions_ReturnsFalse()
    {
        var (follower, boss) = CreateNearbyPair();
        // No special states set

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 1225f));
    }

    // ── ShareScanResults ──────────────────────────────────────────

    [Test]
    public void ShareScanResults_CopiesUpToMaxShared()
    {
        var squad = new SquadEntity(1, 1, 4);
        var results = new LootScanResult[]
        {
            new LootScanResult
            {
                Id = 100,
                X = 1f,
                Y = 2f,
                Z = 3f,
                Value = 5000f,
                Type = 1,
            },
            new LootScanResult
            {
                Id = 101,
                X = 4f,
                Y = 5f,
                Z = 6f,
                Value = 8000f,
                Type = 2,
            },
        };

        SquadLootCoordinator.ShareScanResults(squad, results, 2);

        Assert.That(squad.SharedLootCount, Is.EqualTo(2));
        Assert.That(squad.SharedLootIds[0], Is.EqualTo(100));
        Assert.That(squad.SharedLootIds[1], Is.EqualTo(101));
        Assert.That(squad.SharedLootX[0], Is.EqualTo(1f));
        Assert.That(squad.SharedLootY[1], Is.EqualTo(5f));
        Assert.That(squad.SharedLootValues[0], Is.EqualTo(5000f));
        Assert.That(squad.SharedLootValues[1], Is.EqualTo(8000f));
        Assert.That(squad.SharedLootTypes[0], Is.EqualTo(1));
    }

    [Test]
    public void ShareScanResults_ZeroResults_SetsCountToZero()
    {
        var squad = new SquadEntity(1, 1, 4);
        squad.SharedLootCount = 5; // Previously had some
        var results = new LootScanResult[4];

        SquadLootCoordinator.ShareScanResults(squad, results, 0);

        Assert.That(squad.SharedLootCount, Is.EqualTo(0));
    }

    [Test]
    public void ShareScanResults_MoreThanMaxShared_TruncatesTo8()
    {
        var squad = new SquadEntity(1, 1, 4);
        var results = new LootScanResult[12];
        for (int i = 0; i < 12; i++)
        {
            results[i] = new LootScanResult { Id = 100 + i, Value = 1000f * (i + 1) };
        }

        SquadLootCoordinator.ShareScanResults(squad, results, 12);

        Assert.That(squad.SharedLootCount, Is.EqualTo(8)); // Capped at buffer size
    }

    [Test]
    public void ShareScanResults_RespectsScanCount()
    {
        var squad = new SquadEntity(1, 1, 4);
        var results = new LootScanResult[]
        {
            new LootScanResult { Id = 100, Value = 5000f },
            new LootScanResult { Id = 101, Value = 8000f },
            new LootScanResult { Id = 102, Value = 12000f },
        };

        SquadLootCoordinator.ShareScanResults(squad, results, 2); // Only first 2

        Assert.That(squad.SharedLootCount, Is.EqualTo(2));
    }

    // ── PickSharedTargetForFollower ────────────────────────────────

    [Test]
    public void PickSharedTarget_NoSharedLoot_ReturnsNegativeOne()
    {
        var squad = new SquadEntity(1, 1, 4);
        squad.SharedLootCount = 0;

        int index = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 10, bossLootTargetId: 100, _claims);

        Assert.That(index, Is.EqualTo(-1));
    }

    [Test]
    public void PickSharedTarget_SkipsBossTarget()
    {
        var squad = new SquadEntity(1, 1, 4);
        squad.SharedLootIds[0] = 100; // Boss's target
        squad.SharedLootValues[0] = 10000f;
        squad.SharedLootIds[1] = 101;
        squad.SharedLootValues[1] = 5000f;
        squad.SharedLootCount = 2;

        int index = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 10, bossLootTargetId: 100, _claims);

        Assert.That(index, Is.EqualTo(1)); // Skipped boss's target
    }

    [Test]
    public void PickSharedTarget_SkipsClaimedByOthers()
    {
        var squad = new SquadEntity(1, 1, 4);
        squad.SharedLootIds[0] = 100;
        squad.SharedLootValues[0] = 10000f;
        squad.SharedLootIds[1] = 101;
        squad.SharedLootValues[1] = 5000f;
        squad.SharedLootCount = 2;

        _claims.TryClaim(20, 100); // Claimed by a different follower

        int index = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 10, bossLootTargetId: 999, _claims);

        Assert.That(index, Is.EqualTo(1)); // Skipped claimed item
    }

    [Test]
    public void PickSharedTarget_AllExcluded_ReturnsNegativeOne()
    {
        var squad = new SquadEntity(1, 1, 4);
        squad.SharedLootIds[0] = 100;
        squad.SharedLootValues[0] = 10000f;
        squad.SharedLootCount = 1;

        int index = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 10, bossLootTargetId: 100, _claims);

        Assert.That(index, Is.EqualTo(-1)); // Only item is boss's target
    }

    [Test]
    public void PickSharedTarget_ChoosesHighestValue()
    {
        var squad = new SquadEntity(1, 1, 4);
        squad.SharedLootIds[0] = 100;
        squad.SharedLootValues[0] = 3000f;
        squad.SharedLootIds[1] = 101;
        squad.SharedLootValues[1] = 7000f;
        squad.SharedLootIds[2] = 102;
        squad.SharedLootValues[2] = 5000f;
        squad.SharedLootCount = 3;

        int index = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 10, bossLootTargetId: 999, _claims);

        Assert.That(index, Is.EqualTo(1)); // Highest value
    }

    // ── ClearSharedLoot ───────────────────────────────────────────

    [Test]
    public void ClearSharedLoot_SetsCountToZero()
    {
        var squad = new SquadEntity(1, 1, 4);
        squad.SharedLootCount = 5;

        SquadLootCoordinator.ClearSharedLoot(squad);

        Assert.That(squad.SharedLootCount, Is.EqualTo(0));
    }
}
