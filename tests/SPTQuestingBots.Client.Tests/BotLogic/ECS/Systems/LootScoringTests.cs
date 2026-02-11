using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

// ── ItemValueEstimator ───────────────────────────────────────

[TestFixture]
public class ItemValueEstimatorTests
{
    private static int SimpleLookup(int templateId)
    {
        return templateId * 100;
    }

    [Test]
    public void GetValue_ReturnsLookupResult()
    {
        Assert.AreEqual(500, ItemValueEstimator.GetValue(5, SimpleLookup));
    }

    [Test]
    public void GetValue_NullLookup_ReturnsZero()
    {
        Assert.AreEqual(0, ItemValueEstimator.GetValue(5, null));
    }

    [Test]
    public void GetValue_ZeroTemplateId_ReturnsLookupOfZero()
    {
        Assert.AreEqual(0, ItemValueEstimator.GetValue(0, SimpleLookup));
    }

    [Test]
    public void GetWeaponValue_SumsModValues()
    {
        var mods = new[] { 1, 2, 3 };
        Assert.AreEqual(600, ItemValueEstimator.GetWeaponValue(mods, 3, SimpleLookup));
    }

    [Test]
    public void GetWeaponValue_PartialCount_OnlySumsCount()
    {
        var mods = new[] { 1, 2, 3, 4, 5 };
        // Only first 2: 100 + 200 = 300
        Assert.AreEqual(300, ItemValueEstimator.GetWeaponValue(mods, 2, SimpleLookup));
    }

    [Test]
    public void GetWeaponValue_NullArray_ReturnsZero()
    {
        Assert.AreEqual(0, ItemValueEstimator.GetWeaponValue(null, 3, SimpleLookup));
    }

    [Test]
    public void GetWeaponValue_NullLookup_ReturnsZero()
    {
        Assert.AreEqual(0, ItemValueEstimator.GetWeaponValue(new[] { 1 }, 1, null));
    }

    [Test]
    public void GetWeaponValue_ZeroCount_ReturnsZero()
    {
        Assert.AreEqual(0, ItemValueEstimator.GetWeaponValue(new[] { 1 }, 0, SimpleLookup));
    }

    [Test]
    public void NormalizeValue_HalfCap_ReturnsPointFive()
    {
        Assert.AreEqual(0.5f, ItemValueEstimator.NormalizeValue(25000f, 50000f), 0.001f);
    }

    [Test]
    public void NormalizeValue_ExceedsCap_ClampedToOne()
    {
        Assert.AreEqual(1f, ItemValueEstimator.NormalizeValue(100000f, 50000f), 0.001f);
    }

    [Test]
    public void NormalizeValue_ZeroCap_ReturnsZero()
    {
        Assert.AreEqual(0f, ItemValueEstimator.NormalizeValue(5000f, 0f));
    }

    [Test]
    public void NormalizeValue_NegativeValue_ReturnsZero()
    {
        Assert.AreEqual(0f, ItemValueEstimator.NormalizeValue(-100f, 50000f));
    }

    [Test]
    public void NormalizeValue_ZeroValue_ReturnsZero()
    {
        Assert.AreEqual(0f, ItemValueEstimator.NormalizeValue(0f, 50000f));
    }
}

// ── LootScorer ───────────────────────────────────────────────

[TestFixture]
public class LootScorerTests
{
    private static readonly LootScoringConfig DefaultConfig = new LootScoringConfig(
        minItemValue: 5000f,
        valueScoreCap: 50000f,
        distancePenaltyFactor: 0.001f,
        questProximityBonus: 0.15f,
        gearUpgradeScoreBonus: 0.3f,
        lootCooldownSeconds: 15f
    );

    [Test]
    public void Score_InCombat_ReturnsZero()
    {
        float score = LootScorer.Score(10000f, 100f, 5f, isInCombat: true, 0f, 20f, false, DefaultConfig);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void Score_NoInventorySpace_NotGearUpgrade_ReturnsZero()
    {
        float score = LootScorer.Score(10000f, 100f, 0f, false, 0f, 20f, isGearUpgrade: false, DefaultConfig);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void Score_BelowMinValue_NotGearUpgrade_ReturnsZero()
    {
        float score = LootScorer.Score(1000f, 100f, 5f, false, 0f, 20f, isGearUpgrade: false, DefaultConfig);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void Score_GearUpgrade_BypassesSpaceCheck()
    {
        float score = LootScorer.Score(10000f, 100f, 0f, false, 0f, 20f, isGearUpgrade: true, DefaultConfig);
        Assert.Greater(score, 0f);
    }

    [Test]
    public void Score_GearUpgrade_BypassesMinValueCheck()
    {
        float score = LootScorer.Score(100f, 100f, 5f, false, 0f, 20f, isGearUpgrade: true, DefaultConfig);
        Assert.Greater(score, 0f);
    }

    [Test]
    public void Score_HigherValue_HigherScore()
    {
        float lowScore = LootScorer.Score(10000f, 100f, 5f, false, 500f, 20f, false, DefaultConfig);
        float highScore = LootScorer.Score(40000f, 100f, 5f, false, 500f, 20f, false, DefaultConfig);
        Assert.Greater(highScore, lowScore);
    }

    [Test]
    public void Score_FartherDistance_LowerScore()
    {
        float closeScore = LootScorer.Score(20000f, 10f, 5f, false, 500f, 20f, false, DefaultConfig);
        float farScore = LootScorer.Score(20000f, 300f, 5f, false, 500f, 20f, false, DefaultConfig);
        Assert.Greater(closeScore, farScore);
    }

    [Test]
    public void Score_NearObjective_GetsProximityBonus()
    {
        // distanceToObjectiveSqr < 400 (20m squared) gets bonus
        float nearScore = LootScorer.Score(20000f, 100f, 5f, false, 300f, 20f, false, DefaultConfig);
        float farScore = LootScorer.Score(20000f, 100f, 5f, false, 500f, 20f, false, DefaultConfig);
        Assert.Greater(nearScore, farScore);
    }

    [Test]
    public void Score_GearUpgrade_GetsBonus()
    {
        float withoutGear = LootScorer.Score(20000f, 100f, 5f, false, 500f, 20f, isGearUpgrade: false, DefaultConfig);
        float withGear = LootScorer.Score(20000f, 100f, 5f, false, 500f, 20f, isGearUpgrade: true, DefaultConfig);
        Assert.Greater(withGear, withoutGear);
    }

    [Test]
    public void Score_RecentLoot_ReducedByCooldown()
    {
        // timeSinceLastLoot < LootCooldownSeconds → reduced by factor
        float recentScore = LootScorer.Score(20000f, 50f, 5f, false, 500f, 5f, false, DefaultConfig);
        float readyScore = LootScorer.Score(20000f, 50f, 5f, false, 500f, 20f, false, DefaultConfig);
        Assert.Greater(readyScore, recentScore);
    }

    [Test]
    public void Score_ZeroCooldownTime_ReturnsZeroScore()
    {
        float score = LootScorer.Score(20000f, 50f, 5f, false, 500f, 0f, false, DefaultConfig);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void Score_ClampedToMaxOne()
    {
        // Very high value + gear bonus + proximity bonus, tiny distance
        var generousConfig = new LootScoringConfig(
            minItemValue: 0f,
            valueScoreCap: 100f,
            distancePenaltyFactor: 0f,
            questProximityBonus: 0.5f,
            gearUpgradeScoreBonus: 0.5f,
            lootCooldownSeconds: 0f
        );
        float score = LootScorer.Score(10000f, 0f, 5f, false, 0f, 20f, isGearUpgrade: true, generousConfig);
        Assert.AreEqual(1f, score);
    }

    [Test]
    public void Score_DistancePenalty_ClampedAt04()
    {
        // Very far distance: 1000000 * 0.001 = 1000 → clamped to 0.4
        var config = new LootScoringConfig(
            minItemValue: 0f,
            valueScoreCap: 50000f,
            distancePenaltyFactor: 0.001f,
            questProximityBonus: 0f,
            gearUpgradeScoreBonus: 0f,
            lootCooldownSeconds: 0f
        );
        float scoreNear = LootScorer.Score(50000f, 0f, 5f, false, 500f, 20f, false, config);
        float scoreFar = LootScorer.Score(50000f, 1000000f, 5f, false, 500f, 20f, false, config);
        // Near: 0.5 - 0.0 = 0.5
        // Far: 0.5 - 0.4 = 0.1 (distance penalty capped at 0.4)
        Assert.AreEqual(0.5f, scoreNear, 0.001f);
        Assert.AreEqual(0.1f, scoreFar, 0.001f);
    }

    [Test]
    public void Score_NegativeTimeSinceLastLoot_CooldownFactorIsRatio()
    {
        // Edge case: timeSinceLastLoot negative (shouldn't happen but guard)
        // timeSinceLastLoot < LootCooldownSeconds → ratio is negative → score * negative → clamped to 0
        float score = LootScorer.Score(20000f, 50f, 5f, false, 500f, -5f, false, DefaultConfig);
        Assert.AreEqual(0f, score);
    }

    [Test]
    public void LootScoringConfig_StoresAllFields()
    {
        var config = new LootScoringConfig(1f, 2f, 3f, 4f, 5f, 6f);
        Assert.AreEqual(1f, config.MinItemValue);
        Assert.AreEqual(2f, config.ValueScoreCap);
        Assert.AreEqual(3f, config.DistancePenaltyFactor);
        Assert.AreEqual(4f, config.QuestProximityBonus);
        Assert.AreEqual(5f, config.GearUpgradeScoreBonus);
        Assert.AreEqual(6f, config.LootCooldownSeconds);
    }
}

// ── LootClaimRegistry ────────────────────────────────────────

[TestFixture]
public class LootClaimRegistryTests
{
    [Test]
    public void TryClaim_Unclaimed_ReturnsTrue()
    {
        var registry = new LootClaimRegistry();
        Assert.IsTrue(registry.TryClaim(1, 100));
    }

    [Test]
    public void TryClaim_SameBot_ReturnsTrue()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        Assert.IsTrue(registry.TryClaim(1, 100));
    }

    [Test]
    public void TryClaim_DifferentBot_ReturnsFalse()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        Assert.IsFalse(registry.TryClaim(2, 100));
    }

    [Test]
    public void Release_RemovesClaim()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.Release(1, 100);
        Assert.IsTrue(registry.TryClaim(2, 100));
    }

    [Test]
    public void Release_WrongBot_DoesNotRemove()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.Release(2, 100);
        Assert.IsFalse(registry.TryClaim(2, 100));
    }

    [Test]
    public void ReleaseAll_RemovesAllForBot()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.TryClaim(1, 200);
        registry.TryClaim(1, 300);
        registry.ReleaseAll(1);
        Assert.AreEqual(0, registry.GetClaimCount());
        Assert.IsTrue(registry.TryClaim(2, 100));
    }

    [Test]
    public void ReleaseAll_DoesNotAffectOtherBots()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.TryClaim(2, 200);
        registry.ReleaseAll(1);
        Assert.AreEqual(1, registry.GetClaimCount());
        Assert.IsFalse(registry.TryClaim(1, 200));
    }

    [Test]
    public void IsClaimedByOther_UnclaimedLoot_ReturnsFalse()
    {
        var registry = new LootClaimRegistry();
        Assert.IsFalse(registry.IsClaimedByOther(1, 100));
    }

    [Test]
    public void IsClaimedByOther_SameBot_ReturnsFalse()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        Assert.IsFalse(registry.IsClaimedByOther(1, 100));
    }

    [Test]
    public void IsClaimedByOther_DifferentBot_ReturnsTrue()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        Assert.IsTrue(registry.IsClaimedByOther(2, 100));
    }

    [Test]
    public void GetClaimCount_TracksCorrectly()
    {
        var registry = new LootClaimRegistry();
        Assert.AreEqual(0, registry.GetClaimCount());
        registry.TryClaim(1, 100);
        registry.TryClaim(2, 200);
        Assert.AreEqual(2, registry.GetClaimCount());
    }

    [Test]
    public void Clear_ResetsAll()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.TryClaim(2, 200);
        registry.Clear();
        Assert.AreEqual(0, registry.GetClaimCount());
        Assert.IsTrue(registry.TryClaim(3, 100));
    }

    [Test]
    public void ReleaseAll_NonexistentBot_NoError()
    {
        var registry = new LootClaimRegistry();
        Assert.DoesNotThrow(() => registry.ReleaseAll(999));
    }
}

// ── LootTargetSelector ───────────────────────────────────────

[TestFixture]
public class LootTargetSelectorTests
{
    private static readonly LootScoringConfig DefaultConfig = new LootScoringConfig(
        minItemValue: 5000f,
        valueScoreCap: 50000f,
        distancePenaltyFactor: 0.001f,
        questProximityBonus: 0.15f,
        gearUpgradeScoreBonus: 0.3f,
        lootCooldownSeconds: 15f
    );

    private static LootScanResult MakeResult(int id, float value, float distanceSqr, bool isGearUpgrade = false)
    {
        return new LootScanResult
        {
            Id = id,
            X = 0f,
            Y = 0f,
            Z = 0f,
            Type = LootTargetType.LooseItem,
            Value = value,
            DistanceSqr = distanceSqr,
            IsGearUpgrade = isGearUpgrade,
        };
    }

    [Test]
    public void SelectBest_NullResults_ReturnsNegativeOne()
    {
        int idx = LootTargetSelector.SelectBest(null, 0, 5f, false, 500f, 20f, null, 1, DefaultConfig);
        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void SelectBest_ZeroCount_ReturnsNegativeOne()
    {
        var results = new LootScanResult[5];
        int idx = LootTargetSelector.SelectBest(results, 0, 5f, false, 500f, 20f, null, 1, DefaultConfig);
        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void SelectBest_SingleItem_ReturnsThatIndex()
    {
        var results = new[] { MakeResult(1, 20000f, 100f) };
        int idx = LootTargetSelector.SelectBest(results, 1, 5f, false, 500f, 20f, null, 1, DefaultConfig);
        Assert.AreEqual(0, idx);
    }

    [Test]
    public void SelectBest_MultipleItems_ReturnsHighestScoring()
    {
        var results = new[] { MakeResult(1, 10000f, 200f), MakeResult(2, 40000f, 100f), MakeResult(3, 15000f, 150f) };
        int idx = LootTargetSelector.SelectBest(results, 3, 5f, false, 500f, 20f, null, 1, DefaultConfig);
        // Item 2 has highest value and closest distance → highest score
        Assert.AreEqual(1, idx);
    }

    [Test]
    public void SelectBest_SkipsClaimedByOther()
    {
        var claims = new LootClaimRegistry();
        claims.TryClaim(99, 2); // Bot 99 claimed loot 2

        var results = new[]
        {
            MakeResult(1, 10000f, 200f),
            MakeResult(2, 50000f, 10f), // Claimed by other
            MakeResult(3, 15000f, 100f),
        };
        int idx = LootTargetSelector.SelectBest(results, 3, 5f, false, 500f, 20f, claims, 1, DefaultConfig);
        // Loot 2 is claimed by bot 99, so bot 1 should get loot 3 (second best)
        Assert.AreEqual(2, idx);
    }

    [Test]
    public void SelectBest_AllClaimedByOther_ReturnsNegativeOne()
    {
        var claims = new LootClaimRegistry();
        claims.TryClaim(99, 1);
        claims.TryClaim(99, 2);

        var results = new[] { MakeResult(1, 20000f, 100f), MakeResult(2, 30000f, 50f) };
        int idx = LootTargetSelector.SelectBest(results, 2, 5f, false, 500f, 20f, claims, 1, DefaultConfig);
        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void SelectBest_InCombat_ReturnsNegativeOne()
    {
        var results = new[] { MakeResult(1, 20000f, 100f) };
        int idx = LootTargetSelector.SelectBest(results, 1, 5f, isInCombat: true, 500f, 20f, null, 1, DefaultConfig);
        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void SelectBest_AllBelowMinValue_ReturnsNegativeOne()
    {
        var results = new[] { MakeResult(1, 100f, 100f), MakeResult(2, 200f, 50f) };
        int idx = LootTargetSelector.SelectBest(results, 2, 5f, false, 500f, 20f, null, 1, DefaultConfig);
        Assert.AreEqual(-1, idx);
    }

    [Test]
    public void SelectBest_OwnClaimNotBlocked()
    {
        var claims = new LootClaimRegistry();
        claims.TryClaim(1, 100); // Bot 1 already claimed loot 100

        var results = new[] { MakeResult(100, 20000f, 50f) };
        int idx = LootTargetSelector.SelectBest(results, 1, 5f, false, 500f, 20f, claims, 1, DefaultConfig);
        Assert.AreEqual(0, idx);
    }

    [Test]
    public void SelectBest_NullClaims_NoCrash()
    {
        var results = new[] { MakeResult(1, 20000f, 100f) };
        int idx = LootTargetSelector.SelectBest(results, 1, 5f, false, 500f, 20f, null, 1, DefaultConfig);
        Assert.AreEqual(0, idx);
    }

    [Test]
    public void SelectBest_GearUpgrade_SelectedDespiteLowValue()
    {
        var results = new[] { MakeResult(1, 100f, 100f, isGearUpgrade: false), MakeResult(2, 100f, 100f, isGearUpgrade: true) };
        int idx = LootTargetSelector.SelectBest(results, 2, 5f, false, 500f, 20f, null, 1, DefaultConfig);
        // Item 1 is below min value and not gear upgrade → 0 score
        // Item 2 is gear upgrade → nonzero score
        Assert.AreEqual(1, idx);
    }

    [Test]
    public void SelectBest_RespectsCount_IgnoresTail()
    {
        var results = new[]
        {
            MakeResult(1, 30000f, 10f), // Decent item, close
            MakeResult(2, 50000f, 10f), // Best, but at index 1 which is beyond count=1
        };
        int idx = LootTargetSelector.SelectBest(results, 1, 5f, false, 500f, 20f, null, 1, DefaultConfig);
        Assert.AreEqual(0, idx);
    }

    [Test]
    public void SelectBest_CloserItemPreferred_WhenSameValue()
    {
        var results = new[] { MakeResult(1, 20000f, 300f), MakeResult(2, 20000f, 50f) };
        int idx = LootTargetSelector.SelectBest(results, 2, 5f, false, 500f, 20f, null, 1, DefaultConfig);
        Assert.AreEqual(1, idx);
    }
}
