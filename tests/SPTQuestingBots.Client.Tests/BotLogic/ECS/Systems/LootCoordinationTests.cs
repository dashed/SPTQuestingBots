using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

// ── LootTask ───────────────────────────────────────────────
[TestFixture]
public class LootTaskTests
{
    private static BotEntity MakeEntity(int id = 1)
    {
        var e = new BotEntity(id);
        e.TaskScores = new float[QuestTaskFactory.TaskCount];
        return e;
    }

    [Test]
    public void Score_NoLootTarget_ReturnsZero()
    {
        var e = MakeEntity();
        e.HasLootTarget = false;

        Assert.AreEqual(0f, LootTask.Score(e));
    }

    [Test]
    public void Score_InCombat_ReturnsZero()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 20000f;
        e.IsInCombat = true;

        Assert.AreEqual(0f, LootTask.Score(e));
    }

    [Test]
    public void Score_NoInventorySpace_NegativeValue_ReturnsZero()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.InventorySpaceFree = 0f;
        e.LootTargetValue = -1f;

        Assert.AreEqual(0f, LootTask.Score(e));
    }

    [Test]
    public void Score_PositiveInventorySpace_NegativeValue_ClampedAtZero()
    {
        // InventorySpaceFree > 0 bypasses the inventory gate,
        // but negative value produces negative valueScore → clamped at 0.
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.InventorySpaceFree = 5f;
        e.LootTargetValue = -100f;

        float result = LootTask.Score(e);
        Assert.AreEqual(0f, result);
    }

    [Test]
    public void Score_HighValueLoot_HighScore()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 50000f;
        e.InventorySpaceFree = 5f;
        // Bot at origin, loot at origin: distSqr = 0, distPenalty = 0
        // valueScore = Min(50000/50000, 1) * 0.5 = 0.5

        float result = LootTask.Score(e);
        Assert.AreEqual(0.5f, result, 0.001f);
    }

    [Test]
    public void Score_LowValueLoot_LowerScore()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 10000f;
        e.InventorySpaceFree = 5f;
        // valueScore = Min(10000/50000, 1) * 0.5 = 0.2 * 0.5 = 0.1

        float result = LootTask.Score(e);
        Assert.AreEqual(0.1f, result, 0.001f);
    }

    [Test]
    public void Score_ValueExceedsCap_ClampedToHalf()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 100000f;
        e.InventorySpaceFree = 5f;
        // valueScore = Min(100000/50000, 1) * 0.5 = Min(2, 1) * 0.5 = 0.5

        float result = LootTask.Score(e);
        Assert.AreEqual(0.5f, result, 0.001f);
    }

    [Test]
    public void Score_FartherLootTarget_LowerScore()
    {
        var eClose = MakeEntity();
        eClose.HasLootTarget = true;
        eClose.LootTargetValue = 25000f;
        eClose.InventorySpaceFree = 5f;
        eClose.LootTargetX = 5f; // distSqr = 25

        var eFar = MakeEntity();
        eFar.HasLootTarget = true;
        eFar.LootTargetValue = 25000f;
        eFar.InventorySpaceFree = 5f;
        eFar.LootTargetX = 20f; // distSqr = 400

        float closeScore = LootTask.Score(eClose);
        float farScore = LootTask.Score(eFar);

        Assert.Greater(closeScore, farScore);
    }

    [Test]
    public void Score_NearObjective_ProximityBonusApplied()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 20000f;
        e.InventorySpaceFree = 5f;
        e.HasActiveObjective = true;
        e.DistanceToObjective = 10f; // 10^2 = 100 < 400 threshold
        // valueScore = 0.2, proximityBonus = 0.15, score = 0.35

        float result = LootTask.Score(e);
        Assert.AreEqual(0.35f, result, 0.001f);
    }

    [Test]
    public void Score_FarFromObjective_NoProximityBonus()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 20000f;
        e.InventorySpaceFree = 5f;
        e.HasActiveObjective = true;
        e.DistanceToObjective = 25f; // 25^2 = 625 > 400 threshold
        // valueScore = 0.2, no bonus, score = 0.2

        float result = LootTask.Score(e);
        Assert.AreEqual(0.2f, result, 0.001f);
    }

    [Test]
    public void Score_HasActiveObjectiveFalse_NoProximityBonusEvenIfClose()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 20000f;
        e.InventorySpaceFree = 5f;
        e.HasActiveObjective = false;
        e.DistanceToObjective = 5f;

        float result = LootTask.Score(e);
        Assert.AreEqual(0.2f, result, 0.001f);
    }

    [Test]
    public void Score_ClampedAtMaxBaseScore()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 100000f;
        e.InventorySpaceFree = 5f;
        e.HasActiveObjective = true;
        e.DistanceToObjective = 5f;
        // valueScore=0.5 + proximityBonus=0.15 = 0.65 -> clamped to 0.55

        float result = LootTask.Score(e);
        Assert.AreEqual(LootTask.MaxBaseScore, result, 0.001f);
    }

    [Test]
    public void Score_ClampedAtZero_NotNegative()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 1f;
        e.InventorySpaceFree = 5f;
        // Put loot far away so penalty dominates
        e.LootTargetX = 100f;
        e.LootTargetY = 100f;
        e.LootTargetZ = 100f;
        // valueScore ~ 0.00001, penalty = 0.4, score < 0 -> clamped to 0

        float result = LootTask.Score(e);
        Assert.AreEqual(0f, result);
    }

    [Test]
    public void Score_DistancePenalty_CappedAt04()
    {
        var e1 = MakeEntity();
        e1.HasLootTarget = true;
        e1.LootTargetValue = 50000f;
        e1.InventorySpaceFree = 5f;
        e1.LootTargetX = 500f; // distSqr = 250000, penalty capped at 0.4

        var e2 = MakeEntity();
        e2.HasLootTarget = true;
        e2.LootTargetValue = 50000f;
        e2.InventorySpaceFree = 5f;
        e2.LootTargetX = 1000f; // distSqr = 1000000, penalty still capped at 0.4

        float score1 = LootTask.Score(e1);
        float score2 = LootTask.Score(e2);

        // Both have capped penalty: valueScore=0.5 - penalty=0.4 = 0.1
        Assert.AreEqual(score1, score2, 0.001f);
        Assert.AreEqual(0.1f, score1, 0.001f);
    }

    [Test]
    public void Score_ZeroValue_ReturnsZero()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 0f;
        e.InventorySpaceFree = 5f;

        float result = LootTask.Score(e);
        Assert.AreEqual(0f, result);
    }

    [Test]
    public void Score_BotAtLootPosition_ZeroDistancePenalty()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 25000f;
        e.InventorySpaceFree = 5f;
        e.CurrentPositionX = 10f;
        e.CurrentPositionY = 5f;
        e.CurrentPositionZ = 20f;
        e.LootTargetX = 10f;
        e.LootTargetY = 5f;
        e.LootTargetZ = 20f;
        // distSqr=0, penalty=0, valueScore = 0.25

        float result = LootTask.Score(e);
        Assert.AreEqual(0.25f, result, 0.001f);
    }

    [Test]
    public void Score_ProximityBonusAtExactThreshold_NoBonus()
    {
        // DistanceToObjective = 20 -> 20^2 = 400 which is NOT < 400
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 20000f;
        e.InventorySpaceFree = 5f;
        e.HasActiveObjective = true;
        e.DistanceToObjective = 20f; // exactly at threshold

        float result = LootTask.Score(e);
        // valueScore = 0.2, no bonus (400 is not < 400)
        Assert.AreEqual(0.2f, result, 0.001f);
    }

    [Test]
    public void Score_ProximityBonusJustInsideThreshold_HasBonus()
    {
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 20000f;
        e.InventorySpaceFree = 5f;
        e.HasActiveObjective = true;
        e.DistanceToObjective = 19.9f; // 19.9^2 = 396.01 < 400

        float result = LootTask.Score(e);
        Assert.AreEqual(0.35f, result, 0.001f);
    }

    [Test]
    public void ScoreEntity_SetsTaskScoresAtOrdinal()
    {
        var task = new LootTask();
        var e = MakeEntity();
        e.HasLootTarget = true;
        e.LootTargetValue = 50000f;
        e.InventorySpaceFree = 5f;

        int ordinal = 3;
        task.ScoreEntity(ordinal, e);

        float rawScore = LootTask.Score(e);
        float modifier = ScoringModifiers.CombinedModifier(e.Aggression, e.RaidTimeNormalized, BotActionTypeId.Loot);
        Assert.AreEqual(rawScore * modifier, e.TaskScores[ordinal], 0.001f);
    }

    [Test]
    public void ScoreEntity_NoLootTarget_SetsZero()
    {
        var task = new LootTask();
        var e = MakeEntity();
        e.HasLootTarget = false;
        e.TaskScores[2] = 99f; // pre-fill to verify overwrite

        task.ScoreEntity(2, e);

        Assert.AreEqual(0f, e.TaskScores[2]);
    }
}

// ── SquadLootCoordinator (coordination integration) ────────
[TestFixture]
public class SquadLootCoordinationTests
{
    private static SquadEntity MakeSquad(int id = 1)
    {
        return new SquadEntity(id, strategyCount: 1, targetMembers: 4);
    }

    private static BotEntity MakeBot(int id)
    {
        return new BotEntity(id);
    }

    private static LootScanResult MakeResult(int resultId, float value, byte type = LootTargetType.LooseItem)
    {
        return new LootScanResult
        {
            Id = resultId,
            X = resultId * 10f,
            Y = 0f,
            Z = resultId * 5f,
            Type = type,
            Value = value,
            DistanceSqr = 100f,
        };
    }

    // ── BossPriorityClaim ────────────────────────────────

    [Test]
    public void BossPriorityClaim_EmptyResults_ReturnsNegativeOne()
    {
        var claims = new LootClaimRegistry();
        var results = new LootScanResult[4];

        int idx = SquadLootCoordinator.BossPriorityClaim(results, 0, claims, bossEntityId: 1);

        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void BossPriorityClaim_SingleUnclaimed_ClaimsAndReturnsZero()
    {
        var claims = new LootClaimRegistry();
        var results = new[] { MakeResult(100, 5000f) };

        int idx = SquadLootCoordinator.BossPriorityClaim(results, 1, claims, bossEntityId: 1);

        Assert.AreEqual(0, idx);
        Assert.IsTrue(claims.IsClaimedByOther(99, 100));
    }

    [Test]
    public void BossPriorityClaim_MultipleResults_PicksHighestValue()
    {
        var claims = new LootClaimRegistry();
        var results = new[] { MakeResult(100, 1000f), MakeResult(101, 5000f), MakeResult(102, 3000f) };

        int idx = SquadLootCoordinator.BossPriorityClaim(results, 3, claims, bossEntityId: 1);

        Assert.AreEqual(1, idx);
    }

    [Test]
    public void BossPriorityClaim_SkipsClaimedByOthers()
    {
        var claims = new LootClaimRegistry();
        claims.TryClaim(99, 101);

        var results = new[] { MakeResult(100, 1000f), MakeResult(101, 5000f), MakeResult(102, 3000f) };

        int idx = SquadLootCoordinator.BossPriorityClaim(results, 3, claims, bossEntityId: 1);

        Assert.AreEqual(2, idx);
    }

    [Test]
    public void BossPriorityClaim_AllClaimedByOthers_ReturnsNegativeOne()
    {
        var claims = new LootClaimRegistry();
        claims.TryClaim(99, 100);
        claims.TryClaim(99, 101);

        var results = new[] { MakeResult(100, 1000f), MakeResult(101, 5000f) };

        int idx = SquadLootCoordinator.BossPriorityClaim(results, 2, claims, bossEntityId: 1);

        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void BossPriorityClaim_AlreadyClaimedBySameBot_StillSelected()
    {
        var claims = new LootClaimRegistry();
        claims.TryClaim(1, 100);

        var results = new[] { MakeResult(100, 5000f), MakeResult(101, 3000f) };

        int idx = SquadLootCoordinator.BossPriorityClaim(results, 2, claims, bossEntityId: 1);

        Assert.AreEqual(0, idx);
    }

    [Test]
    public void BossPriorityClaim_RegistersClaimOnSelected()
    {
        var claims = new LootClaimRegistry();
        var results = new[] { MakeResult(200, 8000f), MakeResult(201, 3000f) };

        SquadLootCoordinator.BossPriorityClaim(results, 2, claims, bossEntityId: 5);

        Assert.IsTrue(claims.IsClaimedByOther(99, 200));
        Assert.IsFalse(claims.IsClaimedByOther(5, 200));
    }

    // ── ShouldFollowerLoot ───────────────────────────────

    [Test]
    public void ShouldFollowerLoot_FollowerInCombat_ReturnsFalse()
    {
        var follower = MakeBot(1);
        follower.IsInCombat = true;
        var boss = MakeBot(2);
        boss.IsLooting = true;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 10000f));
    }

    [Test]
    public void ShouldFollowerLoot_BossInCombat_ReturnsFalse()
    {
        var follower = MakeBot(1);
        var boss = MakeBot(2);
        boss.IsInCombat = true;
        boss.IsLooting = true;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 10000f));
    }

    [Test]
    public void ShouldFollowerLoot_OutOfCommRange_ReturnsFalse()
    {
        var follower = MakeBot(1);
        follower.CurrentPositionX = 200f;
        var boss = MakeBot(2);
        boss.IsLooting = true;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 1225f));
    }

    [Test]
    public void ShouldFollowerLoot_BossIsLooting_ReturnsTrue()
    {
        var follower = MakeBot(1);
        var boss = MakeBot(2);
        boss.IsLooting = true;

        Assert.IsTrue(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 10000f));
    }

    [Test]
    public void ShouldFollowerLoot_BossAtObjective_ReturnsTrue()
    {
        var follower = MakeBot(1);
        var boss = MakeBot(2);
        boss.IsCloseToObjective = true;
        boss.HasActiveObjective = true;

        Assert.IsTrue(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 10000f));
    }

    [Test]
    public void ShouldFollowerLoot_BossCloseButNoActiveObjective_ReturnsFalse()
    {
        var follower = MakeBot(1);
        var boss = MakeBot(2);
        boss.IsCloseToObjective = true;
        boss.HasActiveObjective = false;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 10000f));
    }

    [Test]
    public void ShouldFollowerLoot_FollowerAtTacticalPosition_ReturnsTrue()
    {
        var follower = MakeBot(1);
        follower.HasTacticalPosition = true;
        follower.TacticalPositionX = 10f;
        follower.TacticalPositionZ = 20f;
        follower.CurrentPositionX = 12f; // dx=2, dz=2, distSqr=8 < 25
        follower.CurrentPositionZ = 22f;
        follower.IsApproachingLoot = false;

        var boss = MakeBot(2);

        Assert.IsTrue(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 10000f));
    }

    [Test]
    public void ShouldFollowerLoot_FollowerFarFromTactical_ReturnsFalse()
    {
        var follower = MakeBot(1);
        follower.HasTacticalPosition = true;
        follower.TacticalPositionX = 10f;
        follower.TacticalPositionZ = 20f;
        follower.CurrentPositionX = 20f; // dx=10, dz=10, distSqr=200 > 25
        follower.CurrentPositionZ = 30f;
        follower.IsApproachingLoot = false;

        var boss = MakeBot(2);

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 10000f));
    }

    [Test]
    public void ShouldFollowerLoot_ApproachingLoot_ReturnsFalseEvenAtTactical()
    {
        var follower = MakeBot(1);
        follower.HasTacticalPosition = true;
        follower.TacticalPositionX = 10f;
        follower.TacticalPositionZ = 20f;
        follower.CurrentPositionX = 10f;
        follower.CurrentPositionZ = 20f;
        follower.IsApproachingLoot = true;

        var boss = MakeBot(2);

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 10000f));
    }

    [Test]
    public void ShouldFollowerLoot_NoIdleConditionMet_ReturnsFalse()
    {
        var follower = MakeBot(1);
        follower.HasTacticalPosition = false;
        var boss = MakeBot(2);
        boss.IsLooting = false;
        boss.IsCloseToObjective = false;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 10000f));
    }

    [Test]
    public void ShouldFollowerLoot_ExactlyAtCommRange_ReturnsTrue()
    {
        var follower = MakeBot(1);
        follower.CurrentPositionX = 30f; // distSqr = 900
        var boss = MakeBot(2);
        boss.IsLooting = true;

        // commRangeSqr = 900 -> distSqr == commRangeSqr -> NOT greater -> passes
        Assert.IsTrue(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 900f));
    }

    [Test]
    public void ShouldFollowerLoot_JustOutsideCommRange_ReturnsFalse()
    {
        var follower = MakeBot(1);
        follower.CurrentPositionX = 30f; // distSqr = 900
        var boss = MakeBot(2);
        boss.IsLooting = true;

        Assert.IsFalse(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, commRangeSqr: 899f));
    }

    // ── ShareScanResults ─────────────────────────────────

    [Test]
    public void ShareScanResults_CopiesIntoSquadBuffers()
    {
        var squad = MakeSquad();
        var results = new[]
        {
            MakeResult(10, 1000f, LootTargetType.Container),
            MakeResult(20, 2000f, LootTargetType.Corpse),
            MakeResult(30, 3000f, LootTargetType.LooseItem),
        };

        SquadLootCoordinator.ShareScanResults(squad, results, 3);

        Assert.AreEqual(3, squad.SharedLootCount);
        Assert.AreEqual(10, squad.SharedLootIds[0]);
        Assert.AreEqual(20, squad.SharedLootIds[1]);
        Assert.AreEqual(30, squad.SharedLootIds[2]);
        Assert.AreEqual(1000f, squad.SharedLootValues[0]);
        Assert.AreEqual(2000f, squad.SharedLootValues[1]);
        Assert.AreEqual(3000f, squad.SharedLootValues[2]);
        Assert.AreEqual(LootTargetType.Container, squad.SharedLootTypes[0]);
        Assert.AreEqual(LootTargetType.Corpse, squad.SharedLootTypes[1]);
        Assert.AreEqual(LootTargetType.LooseItem, squad.SharedLootTypes[2]);
        // Verify X positions: MakeResult sets X = resultId * 10
        Assert.AreEqual(100f, squad.SharedLootX[0], 0.001f); // 10 * 10
        Assert.AreEqual(200f, squad.SharedLootX[1], 0.001f); // 20 * 10
    }

    [Test]
    public void ShareScanResults_CapsAtEightResults()
    {
        var squad = MakeSquad();
        var results = new LootScanResult[12];
        for (int i = 0; i < 12; i++)
        {
            results[i] = MakeResult(i + 1, (i + 1) * 100f);
        }

        SquadLootCoordinator.ShareScanResults(squad, results, 12);

        Assert.AreEqual(8, squad.SharedLootCount);
        Assert.AreEqual(1, squad.SharedLootIds[0]);
        Assert.AreEqual(8, squad.SharedLootIds[7]);
    }

    [Test]
    public void ShareScanResults_FewerThanEight_CopiesAll()
    {
        var squad = MakeSquad();
        var results = new[] { MakeResult(50, 500f), MakeResult(51, 600f) };

        SquadLootCoordinator.ShareScanResults(squad, results, 2);

        Assert.AreEqual(2, squad.SharedLootCount);
        Assert.AreEqual(50, squad.SharedLootIds[0]);
        Assert.AreEqual(51, squad.SharedLootIds[1]);
    }

    [Test]
    public void ShareScanResults_ZeroResults_SetsCountToZero()
    {
        var squad = MakeSquad();
        squad.SharedLootCount = 5;

        SquadLootCoordinator.ShareScanResults(squad, new LootScanResult[4], 0);

        Assert.AreEqual(0, squad.SharedLootCount);
    }

    // ── PickSharedTargetForFollower ───────────────────────

    [Test]
    public void PickSharedTarget_EmptySharedResults_ReturnsNegativeOne()
    {
        var squad = MakeSquad();
        squad.SharedLootCount = 0;
        var claims = new LootClaimRegistry();

        int idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 2, bossLootTargetId: 999, claims);

        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void PickSharedTarget_SkipsBossTarget()
    {
        var squad = MakeSquad();
        squad.SharedLootIds[0] = 100;
        squad.SharedLootValues[0] = 9999f;
        squad.SharedLootIds[1] = 101;
        squad.SharedLootValues[1] = 500f;
        squad.SharedLootCount = 2;
        var claims = new LootClaimRegistry();

        int idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 2, bossLootTargetId: 100, claims);

        Assert.AreEqual(1, idx);
    }

    [Test]
    public void PickSharedTarget_SkipsClaimedByOthers()
    {
        var squad = MakeSquad();
        squad.SharedLootIds[0] = 200;
        squad.SharedLootValues[0] = 8000f;
        squad.SharedLootIds[1] = 201;
        squad.SharedLootValues[1] = 3000f;
        squad.SharedLootCount = 2;

        var claims = new LootClaimRegistry();
        claims.TryClaim(99, 200);

        int idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 2, bossLootTargetId: -1, claims);

        Assert.AreEqual(1, idx);
    }

    [Test]
    public void PickSharedTarget_PicksHighestValueAmongAvailable()
    {
        var squad = MakeSquad();
        squad.SharedLootIds[0] = 300;
        squad.SharedLootValues[0] = 1000f;
        squad.SharedLootIds[1] = 301;
        squad.SharedLootValues[1] = 5000f;
        squad.SharedLootIds[2] = 302;
        squad.SharedLootValues[2] = 3000f;
        squad.SharedLootCount = 3;
        var claims = new LootClaimRegistry();

        int idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 2, bossLootTargetId: -1, claims);

        Assert.AreEqual(1, idx);
    }

    [Test]
    public void PickSharedTarget_AllTaken_ReturnsNegativeOne()
    {
        var squad = MakeSquad();
        squad.SharedLootIds[0] = 400;
        squad.SharedLootValues[0] = 5000f;
        squad.SharedLootIds[1] = 401;
        squad.SharedLootValues[1] = 3000f;
        squad.SharedLootCount = 2;

        var claims = new LootClaimRegistry();
        claims.TryClaim(99, 401);

        int idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 2, bossLootTargetId: 400, claims);

        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void PickSharedTarget_SingleItemMatchesBossTarget_ReturnsNegativeOne()
    {
        var squad = MakeSquad();
        squad.SharedLootIds[0] = 500;
        squad.SharedLootValues[0] = 9000f;
        squad.SharedLootCount = 1;
        var claims = new LootClaimRegistry();

        int idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 2, bossLootTargetId: 500, claims);

        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void PickSharedTarget_OwnClaimNotBlocked()
    {
        var squad = MakeSquad();
        squad.SharedLootIds[0] = 600;
        squad.SharedLootValues[0] = 2000f;
        squad.SharedLootCount = 1;

        var claims = new LootClaimRegistry();
        claims.TryClaim(2, 600);

        int idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, followerEntityId: 2, bossLootTargetId: -1, claims);

        Assert.AreEqual(0, idx);
    }

    // ── ClearSharedLoot ──────────────────────────────────

    [Test]
    public void ClearSharedLoot_SetsCountToZero()
    {
        var squad = MakeSquad();
        squad.SharedLootCount = 5;
        squad.SharedLootIds[0] = 100;
        squad.SharedLootValues[0] = 999f;

        SquadLootCoordinator.ClearSharedLoot(squad);

        Assert.AreEqual(0, squad.SharedLootCount);
    }

    [Test]
    public void ClearSharedLoot_AlreadyZero_NoError()
    {
        var squad = MakeSquad();
        squad.SharedLootCount = 0;

        Assert.DoesNotThrow(() => SquadLootCoordinator.ClearSharedLoot(squad));
        Assert.AreEqual(0, squad.SharedLootCount);
    }
}
