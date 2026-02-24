using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

/// <summary>
/// End-to-end tests for the looting system: LootClaimRegistry, LootScorer,
/// LootTargetSelector, LootInventoryPlanner, SquadLootCoordinator, LootTask,
/// ItemValueEstimator, and GearComparer.
/// </summary>
[TestFixture]
public class LootSystemTests
{
    #region LootClaimRegistry Tests

    [Test]
    public void LootClaimRegistry_TryClaim_UnclaimedItem_ReturnsTrue()
    {
        var registry = new LootClaimRegistry();
        Assert.That(registry.TryClaim(1, 100), Is.True);
    }

    [Test]
    public void LootClaimRegistry_TryClaim_SameBot_ReturnsTrue()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        Assert.That(registry.TryClaim(1, 100), Is.True);
    }

    [Test]
    public void LootClaimRegistry_TryClaim_DifferentBot_ReturnsFalse()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        Assert.That(registry.TryClaim(2, 100), Is.False);
    }

    [Test]
    public void LootClaimRegistry_Release_ThenOtherBotCanClaim()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.Release(1, 100);
        Assert.That(registry.TryClaim(2, 100), Is.True);
    }

    [Test]
    public void LootClaimRegistry_Release_WrongBot_DoesNotRelease()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.Release(2, 100); // wrong bot
        Assert.That(registry.TryClaim(2, 100), Is.False, "Item should still be claimed by bot 1");
    }

    [Test]
    public void LootClaimRegistry_Release_NonExistentClaim_DoesNotThrow()
    {
        var registry = new LootClaimRegistry();
        Assert.DoesNotThrow(() => registry.Release(1, 999));
    }

    [Test]
    public void LootClaimRegistry_ReleaseAll_FreesAllBotClaims()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.TryClaim(1, 200);
        registry.TryClaim(1, 300);
        registry.ReleaseAll(1);

        Assert.Multiple(() =>
        {
            Assert.That(registry.TryClaim(2, 100), Is.True);
            Assert.That(registry.TryClaim(2, 200), Is.True);
            Assert.That(registry.TryClaim(2, 300), Is.True);
            Assert.That(registry.GetClaimCount(), Is.EqualTo(3));
        });
    }

    [Test]
    public void LootClaimRegistry_ReleaseAll_NonExistentBot_DoesNotThrow()
    {
        var registry = new LootClaimRegistry();
        Assert.DoesNotThrow(() => registry.ReleaseAll(999));
    }

    [Test]
    public void LootClaimRegistry_IsClaimedByOther_Unclaimed_ReturnsFalse()
    {
        var registry = new LootClaimRegistry();
        Assert.That(registry.IsClaimedByOther(1, 100), Is.False);
    }

    [Test]
    public void LootClaimRegistry_IsClaimedByOther_ClaimedBySelf_ReturnsFalse()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        Assert.That(registry.IsClaimedByOther(1, 100), Is.False);
    }

    [Test]
    public void LootClaimRegistry_IsClaimedByOther_ClaimedByOther_ReturnsTrue()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        Assert.That(registry.IsClaimedByOther(2, 100), Is.True);
    }

    [Test]
    public void LootClaimRegistry_Clear_ResetsEverything()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.TryClaim(2, 200);
        registry.Clear();

        Assert.Multiple(() =>
        {
            Assert.That(registry.GetClaimCount(), Is.EqualTo(0));
            Assert.That(registry.TryClaim(3, 100), Is.True);
            Assert.That(registry.TryClaim(3, 200), Is.True);
        });
    }

    [Test]
    public void LootClaimRegistry_GetClaimCount_TracksCorrectly()
    {
        var registry = new LootClaimRegistry();
        Assert.That(registry.GetClaimCount(), Is.EqualTo(0));

        registry.TryClaim(1, 100);
        Assert.That(registry.GetClaimCount(), Is.EqualTo(1));

        registry.TryClaim(1, 200);
        Assert.That(registry.GetClaimCount(), Is.EqualTo(2));

        registry.Release(1, 100);
        Assert.That(registry.GetClaimCount(), Is.EqualTo(1));
    }

    [Test]
    public void LootClaimRegistry_DoubleClaim_DoesNotDuplicateReverseMap()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.TryClaim(1, 100); // same bot, same item
        registry.ReleaseAll(1);
        // If reverse map had duplicates, this count would be wrong
        Assert.That(registry.GetClaimCount(), Is.EqualTo(0));
    }

    [Test]
    public void LootClaimRegistry_TryClaimAfterReleaseAll_Works()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(1, 100);
        registry.ReleaseAll(1);
        Assert.That(registry.TryClaim(1, 200), Is.True);
        Assert.That(registry.GetClaimCount(), Is.EqualTo(1));
    }

    #endregion

    #region ItemValueEstimator Tests

    [Test]
    public void NormalizeValue_NormalValue_ReturnsCorrectRatio()
    {
        float result = ItemValueEstimator.NormalizeValue(25000f, 50000f);
        Assert.That(result, Is.EqualTo(0.5f).Within(0.001f));
    }

    [Test]
    public void NormalizeValue_ExceedsCap_ClampedToOne()
    {
        float result = ItemValueEstimator.NormalizeValue(100000f, 50000f);
        Assert.That(result, Is.EqualTo(1f));
    }

    [Test]
    public void NormalizeValue_ZeroCap_ReturnsZero()
    {
        float result = ItemValueEstimator.NormalizeValue(10000f, 0f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void NormalizeValue_NegativeCap_ReturnsZero()
    {
        float result = ItemValueEstimator.NormalizeValue(10000f, -1f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void NormalizeValue_ZeroValue_ReturnsZero()
    {
        float result = ItemValueEstimator.NormalizeValue(0f, 50000f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void NormalizeValue_NegativeValue_ReturnsZero()
    {
        float result = ItemValueEstimator.NormalizeValue(-100f, 50000f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void NormalizeValue_NaNValue_ReturnsZero()
    {
        float result = ItemValueEstimator.NormalizeValue(float.NaN, 50000f);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void NormalizeValue_NaNCap_ReturnsZero()
    {
        float result = ItemValueEstimator.NormalizeValue(10000f, float.NaN);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void NormalizeValue_BothNaN_ReturnsZero()
    {
        float result = ItemValueEstimator.NormalizeValue(float.NaN, float.NaN);
        Assert.That(result, Is.EqualTo(0f));
    }

    [Test]
    public void GetValue_NullLookup_ReturnsZero()
    {
        int result = ItemValueEstimator.GetValue(42, null);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void GetValue_ValidLookup_ReturnsLookupValue()
    {
        int result = ItemValueEstimator.GetValue(42, _ => 5000);
        Assert.That(result, Is.EqualTo(5000));
    }

    [Test]
    public void GetWeaponValue_NullArray_ReturnsZero()
    {
        int result = ItemValueEstimator.GetWeaponValue(null, 3, _ => 100);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void GetWeaponValue_NullLookup_ReturnsZero()
    {
        int result = ItemValueEstimator.GetWeaponValue(new[] { 1, 2 }, 2, null);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void GetWeaponValue_ZeroCount_ReturnsZero()
    {
        int result = ItemValueEstimator.GetWeaponValue(new[] { 1, 2 }, 0, _ => 100);
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void GetWeaponValue_CountExceedsArray_ClampsToArrayLength()
    {
        // modCount=10 but array only has 2 elements
        int result = ItemValueEstimator.GetWeaponValue(new[] { 1, 2 }, 10, _ => 100);
        Assert.That(result, Is.EqualTo(200));
    }

    #endregion

    #region LootScorer Tests

    private static LootScoringConfig DefaultConfig()
    {
        return new LootScoringConfig(
            minItemValue: 5000f,
            valueScoreCap: 50000f,
            distancePenaltyFactor: 0.001f,
            questProximityBonus: 0.15f,
            gearUpgradeScoreBonus: 0.3f,
            lootCooldownSeconds: 15f
        );
    }

    [Test]
    public void LootScorer_InCombat_ReturnsZero()
    {
        float score = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: true,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            DefaultConfig()
        );
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootScorer_NoInventorySpace_NonGear_ReturnsZero()
    {
        float score = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: 100f,
            inventorySpaceFree: 0f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            DefaultConfig()
        );
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootScorer_NoInventorySpace_GearUpgrade_ReturnsPositive()
    {
        float score = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: 100f,
            inventorySpaceFree: 0f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: true,
            DefaultConfig()
        );
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void LootScorer_BelowMinValue_NonGear_ReturnsZero()
    {
        float score = LootScorer.Score(
            targetValue: 1000f, // below 5000 min
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            DefaultConfig()
        );
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootScorer_BelowMinValue_GearUpgrade_StillScores()
    {
        float score = LootScorer.Score(
            targetValue: 1000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: true,
            DefaultConfig()
        );
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void LootScorer_NearObjective_GetsProximityBonus()
    {
        float scoreNear = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 100f, // within 400 threshold
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            DefaultConfig()
        );
        float scoreFar = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 10000f, // outside 400 threshold
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            DefaultConfig()
        );
        Assert.That(scoreNear, Is.GreaterThan(scoreFar));
    }

    [Test]
    public void LootScorer_CooldownActive_ReducesScore()
    {
        float fullScore = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 30f, // past cooldown
            isGearUpgrade: false,
            DefaultConfig()
        );
        float cooldownScore = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 5f, // within 15s cooldown
            isGearUpgrade: false,
            DefaultConfig()
        );
        Assert.That(cooldownScore, Is.LessThan(fullScore));
    }

    [Test]
    public void LootScorer_ZeroCooldownConfig_FullScore()
    {
        var config = new LootScoringConfig(
            minItemValue: 5000f,
            valueScoreCap: 50000f,
            distancePenaltyFactor: 0.001f,
            questProximityBonus: 0.15f,
            gearUpgradeScoreBonus: 0.3f,
            lootCooldownSeconds: 0f
        );
        float score = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 0f,
            isGearUpgrade: false,
            config
        );
        Assert.That(score, Is.GreaterThan(0f));
    }

    [Test]
    public void LootScorer_NaNTargetValue_ReturnsZero()
    {
        float score = LootScorer.Score(
            targetValue: float.NaN,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            DefaultConfig()
        );
        Assert.That(score, Is.EqualTo(0f), "NaN target value must produce 0 score, not NaN");
    }

    [Test]
    public void LootScorer_NaNDistanceSqr_ReturnsZero()
    {
        float score = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: float.NaN,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            DefaultConfig()
        );
        Assert.That(score, Is.EqualTo(0f), "NaN distance must produce 0 score, not NaN");
    }

    [Test]
    public void LootScorer_NaNTimeSinceLastLoot_DoesNotPropagateNaN()
    {
        // NaN timeSinceLastLoot: NaN < cooldown is false, so cooldownFactor = 1f (full score).
        // The NaN does NOT propagate — this test verifies the result is finite.
        float score = LootScorer.Score(
            targetValue: 30000f,
            distanceSqr: 100f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: float.NaN,
            isGearUpgrade: false,
            DefaultConfig()
        );
        Assert.That(float.IsNaN(score), Is.False, "NaN timeSinceLastLoot must not propagate NaN");
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
        Assert.That(score, Is.LessThanOrEqualTo(1f));
    }

    [Test]
    public void LootScorer_ZeroDistance_MaxValueScore()
    {
        // Bot is on top of loot: distance=0, should get full value score
        float score = LootScorer.Score(
            targetValue: 50000f,
            distanceSqr: 0f,
            inventorySpaceFree: 10f,
            isInCombat: false,
            distanceToObjectiveSqr: 1000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            DefaultConfig()
        );
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(float.IsNaN(score), Is.False);
    }

    [Test]
    public void LootScorer_ScoreClampsToOneMax()
    {
        // Extreme values that could push score above 1.0
        float score = LootScorer.Score(
            targetValue: 500000f,
            distanceSqr: 0f,
            inventorySpaceFree: 100f,
            isInCombat: false,
            distanceToObjectiveSqr: 0f,
            timeSinceLastLoot: 1000f,
            isGearUpgrade: true,
            DefaultConfig()
        );
        Assert.That(score, Is.LessThanOrEqualTo(1f));
        Assert.That(score, Is.GreaterThanOrEqualTo(0f));
    }

    #endregion

    #region LootTargetSelector Tests

    [Test]
    public void LootTargetSelector_NullResults_ReturnsMinus1()
    {
        int result = LootTargetSelector.SelectBest(null, 0, 10f, false, 1000f, 30f, new LootClaimRegistry(), 1, DefaultConfig());
        Assert.That(result, Is.EqualTo(-1));
    }

    [Test]
    public void LootTargetSelector_ZeroCount_ReturnsMinus1()
    {
        var results = new LootScanResult[4];
        int result = LootTargetSelector.SelectBest(results, 0, 10f, false, 1000f, 30f, new LootClaimRegistry(), 1, DefaultConfig());
        Assert.That(result, Is.EqualTo(-1));
    }

    [Test]
    public void LootTargetSelector_SkipsClaimedItems()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(2, 100); // bot 2 claims item 100

        var results = new[]
        {
            new LootScanResult
            {
                Id = 100,
                Value = 50000f,
                DistanceSqr = 10f,
            },
            new LootScanResult
            {
                Id = 200,
                Value = 30000f,
                DistanceSqr = 10f,
            },
        };

        int result = LootTargetSelector.SelectBest(results, 2, 10f, false, 1000f, 30f, registry, 1, DefaultConfig());
        Assert.That(result, Is.EqualTo(1), "Should skip claimed item 100 and select item 200");
    }

    [Test]
    public void LootTargetSelector_SelectsHighestScoring()
    {
        var results = new[]
        {
            new LootScanResult
            {
                Id = 100,
                Value = 10000f,
                DistanceSqr = 100f,
            },
            new LootScanResult
            {
                Id = 200,
                Value = 50000f,
                DistanceSqr = 100f,
            },
            new LootScanResult
            {
                Id = 300,
                Value = 20000f,
                DistanceSqr = 100f,
            },
        };

        int result = LootTargetSelector.SelectBest(results, 3, 10f, false, 1000f, 30f, new LootClaimRegistry(), 1, DefaultConfig());
        Assert.That(result, Is.EqualTo(1), "Should select item 200 with highest value");
    }

    [Test]
    public void LootTargetSelector_NullClaims_DoesNotThrow()
    {
        var results = new[]
        {
            new LootScanResult
            {
                Id = 100,
                Value = 30000f,
                DistanceSqr = 100f,
            },
        };

        int result = LootTargetSelector.SelectBest(results, 1, 10f, false, 1000f, 30f, null, 1, DefaultConfig());
        Assert.That(result, Is.EqualTo(0));
    }

    [Test]
    public void LootTargetSelector_AllClaimedByOthers_ReturnsMinus1()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(2, 100);
        registry.TryClaim(3, 200);

        var results = new[]
        {
            new LootScanResult
            {
                Id = 100,
                Value = 50000f,
                DistanceSqr = 10f,
            },
            new LootScanResult
            {
                Id = 200,
                Value = 30000f,
                DistanceSqr = 10f,
            },
        };

        int result = LootTargetSelector.SelectBest(results, 2, 10f, false, 1000f, 30f, registry, 1, DefaultConfig());
        Assert.That(result, Is.EqualTo(-1));
    }

    #endregion

    #region LootInventoryPlanner Tests

    [Test]
    public void LootInventoryPlanner_ArmorUpgrade_WithGearSwap_ReturnsSwap()
    {
        var item = new LootItemInfo(
            value: 1000,
            gridSize: 4,
            isArmor: true,
            armorClass: 5,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
        var gear = new GearStats(
            armorClass: 3,
            weaponValue: 0,
            backpackGridSize: 0,
            rigGridSize: 0,
            rigIsArmored: false,
            inventorySpaceFree: 10f
        );

        var action = LootInventoryPlanner.PlanAction(item, gear, 5000, gearSwapEnabled: true);
        Assert.That(action, Is.EqualTo(LootActionType.Swap));
    }

    [Test]
    public void LootInventoryPlanner_ArmorUpgrade_WithoutGearSwap_ReturnsSkip()
    {
        var item = new LootItemInfo(
            value: 1000,
            gridSize: 4,
            isArmor: true,
            armorClass: 5,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
        var gear = new GearStats(
            armorClass: 3,
            weaponValue: 0,
            backpackGridSize: 0,
            rigGridSize: 0,
            rigIsArmored: false,
            inventorySpaceFree: 10f
        );

        var action = LootInventoryPlanner.PlanAction(item, gear, 5000, gearSwapEnabled: false);
        Assert.That(action, Is.EqualTo(LootActionType.Skip));
    }

    [Test]
    public void LootInventoryPlanner_BelowMinValue_ReturnsSkip()
    {
        var item = new LootItemInfo(
            value: 1000,
            gridSize: 1,
            isArmor: false,
            armorClass: 0,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
        var gear = new GearStats(
            armorClass: 3,
            weaponValue: 0,
            backpackGridSize: 0,
            rigGridSize: 0,
            rigIsArmored: false,
            inventorySpaceFree: 10f
        );

        var action = LootInventoryPlanner.PlanAction(item, gear, 5000, gearSwapEnabled: true);
        Assert.That(action, Is.EqualTo(LootActionType.Skip));
    }

    [Test]
    public void LootInventoryPlanner_HasSpace_ReturnsPickup()
    {
        var item = new LootItemInfo(
            value: 10000,
            gridSize: 2,
            isArmor: false,
            armorClass: 0,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
        var gear = new GearStats(
            armorClass: 3,
            weaponValue: 0,
            backpackGridSize: 0,
            rigGridSize: 0,
            rigIsArmored: false,
            inventorySpaceFree: 10f
        );

        var action = LootInventoryPlanner.PlanAction(item, gear, 5000, gearSwapEnabled: true);
        Assert.That(action, Is.EqualTo(LootActionType.Pickup));
    }

    [Test]
    public void LootInventoryPlanner_NoSpace_ReturnsSkip()
    {
        var item = new LootItemInfo(
            value: 10000,
            gridSize: 4,
            isArmor: false,
            armorClass: 0,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
        var gear = new GearStats(
            armorClass: 3,
            weaponValue: 0,
            backpackGridSize: 0,
            rigGridSize: 0,
            rigIsArmored: false,
            inventorySpaceFree: 2f
        );

        var action = LootInventoryPlanner.PlanAction(item, gear, 5000, gearSwapEnabled: true);
        Assert.That(action, Is.EqualTo(LootActionType.Skip));
    }

    [Test]
    public void LootInventoryPlanner_ExactSpace_ReturnsPickup()
    {
        var item = new LootItemInfo(
            value: 10000,
            gridSize: 4,
            isArmor: false,
            armorClass: 0,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
        var gear = new GearStats(
            armorClass: 3,
            weaponValue: 0,
            backpackGridSize: 0,
            rigGridSize: 0,
            rigIsArmored: false,
            inventorySpaceFree: 4f
        );

        var action = LootInventoryPlanner.PlanAction(item, gear, 5000, gearSwapEnabled: true);
        Assert.That(action, Is.EqualTo(LootActionType.Pickup));
    }

    [Test]
    public void LootInventoryPlanner_WeaponUpgrade_ReturnsSwap()
    {
        var item = new LootItemInfo(
            value: 10000,
            gridSize: 8,
            isArmor: false,
            armorClass: 0,
            isWeapon: true,
            weaponValue: 80000,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
        var gear = new GearStats(
            armorClass: 0,
            weaponValue: 30000,
            backpackGridSize: 0,
            rigGridSize: 0,
            rigIsArmored: false,
            inventorySpaceFree: 0f
        );

        var action = LootInventoryPlanner.PlanAction(item, gear, 5000, gearSwapEnabled: true);
        Assert.That(action, Is.EqualTo(LootActionType.Swap));
    }

    [Test]
    public void LootInventoryPlanner_BackpackUpgrade_ReturnsSwap()
    {
        var item = new LootItemInfo(
            value: 10000,
            gridSize: 0,
            isArmor: false,
            armorClass: 0,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: true,
            backpackGridSize: 64,
            isRig: false,
            rigGridSize: 0,
            rigIsArmored: false
        );
        var gear = new GearStats(
            armorClass: 0,
            weaponValue: 0,
            backpackGridSize: 25,
            rigGridSize: 0,
            rigIsArmored: false,
            inventorySpaceFree: 0f
        );

        var action = LootInventoryPlanner.PlanAction(item, gear, 5000, gearSwapEnabled: true);
        Assert.That(action, Is.EqualTo(LootActionType.Swap));
    }

    [Test]
    public void LootInventoryPlanner_RigUpgrade_ArmoredOverNonArmored()
    {
        var item = new LootItemInfo(
            value: 10000,
            gridSize: 0,
            isArmor: false,
            armorClass: 4,
            isWeapon: false,
            weaponValue: 0,
            isBackpack: false,
            backpackGridSize: 0,
            isRig: true,
            rigGridSize: 12,
            rigIsArmored: true
        );
        var gear = new GearStats(
            armorClass: 3,
            weaponValue: 0,
            backpackGridSize: 0,
            rigGridSize: 10,
            rigIsArmored: false,
            inventorySpaceFree: 0f
        );

        var action = LootInventoryPlanner.PlanAction(item, gear, 5000, gearSwapEnabled: true);
        Assert.That(action, Is.EqualTo(LootActionType.Swap));
    }

    #endregion

    #region GearComparer Tests

    [Test]
    public void GearComparer_IsArmorUpgrade_HigherClass_ReturnsTrue()
    {
        Assert.That(GearComparer.IsArmorUpgrade(3, 5), Is.True);
    }

    [Test]
    public void GearComparer_IsArmorUpgrade_SameClass_ReturnsFalse()
    {
        Assert.That(GearComparer.IsArmorUpgrade(3, 3), Is.False);
    }

    [Test]
    public void GearComparer_IsArmorUpgrade_LowerClass_ReturnsFalse()
    {
        Assert.That(GearComparer.IsArmorUpgrade(5, 3), Is.False);
    }

    [Test]
    public void GearComparer_IsRigUpgrade_ArmoredOverNonArmored_SufficientSize()
    {
        // Candidate: armored, class 4, 12 slots vs current: non-armored, class 3, 10 slots
        Assert.That(GearComparer.IsRigUpgrade(10, 3, false, 12, 4, true), Is.True);
    }

    [Test]
    public void GearComparer_IsRigUpgrade_ArmoredOverNonArmored_InsufficientSize()
    {
        // Candidate: armored, class 4, 8 slots vs current: non-armored, class 3, 10 slots
        Assert.That(GearComparer.IsRigUpgrade(10, 3, false, 8, 4, true), Is.False);
    }

    [Test]
    public void GearComparer_IsRigUpgrade_BothArmored_HigherArmorClass()
    {
        Assert.That(GearComparer.IsRigUpgrade(10, 3, true, 10, 5, true), Is.True);
    }

    [Test]
    public void GearComparer_IsRigUpgrade_BothArmored_SameClassLargerSize()
    {
        Assert.That(GearComparer.IsRigUpgrade(10, 3, true, 14, 3, true), Is.True);
    }

    [Test]
    public void GearComparer_IsRigUpgrade_BothArmored_SameClassSameSize()
    {
        Assert.That(GearComparer.IsRigUpgrade(10, 3, true, 10, 3, true), Is.False);
    }

    [Test]
    public void GearComparer_IsRigUpgrade_NonArmoredOverNonArmored_LargerSize()
    {
        Assert.That(GearComparer.IsRigUpgrade(10, 0, false, 14, 0, false), Is.True);
    }

    [Test]
    public void GearComparer_IsContainerUpgrade_Larger_ReturnsTrue()
    {
        Assert.That(GearComparer.IsContainerUpgrade(25, 64), Is.True);
    }

    [Test]
    public void GearComparer_IsContainerUpgrade_Same_ReturnsFalse()
    {
        Assert.That(GearComparer.IsContainerUpgrade(25, 25), Is.False);
    }

    #endregion

    #region SquadLootCoordinator Tests

    [Test]
    public void BossPriorityClaim_EmptyResults_ReturnsMinus1()
    {
        var results = new LootScanResult[4];
        int idx = SquadLootCoordinator.BossPriorityClaim(results, 0, new LootClaimRegistry(), 1);
        Assert.That(idx, Is.EqualTo(-1));
    }

    [Test]
    public void BossPriorityClaim_SelectsHighestValue()
    {
        var registry = new LootClaimRegistry();
        var results = new[]
        {
            new LootScanResult { Id = 100, Value = 10000f },
            new LootScanResult { Id = 200, Value = 50000f },
            new LootScanResult { Id = 300, Value = 30000f },
        };

        int idx = SquadLootCoordinator.BossPriorityClaim(results, 3, registry, 1);
        Assert.That(idx, Is.EqualTo(1));
        Assert.That(registry.IsClaimedByOther(2, 200), Is.True, "Boss should have claimed item 200");
    }

    [Test]
    public void BossPriorityClaim_SkipsClaimedByOthers()
    {
        var registry = new LootClaimRegistry();
        registry.TryClaim(2, 200); // other bot claims highest value

        var results = new[]
        {
            new LootScanResult { Id = 100, Value = 10000f },
            new LootScanResult { Id = 200, Value = 50000f },
            new LootScanResult { Id = 300, Value = 30000f },
        };

        int idx = SquadLootCoordinator.BossPriorityClaim(results, 3, registry, 1);
        Assert.That(idx, Is.EqualTo(2), "Boss should select item 300 since 200 is claimed");
    }

    [Test]
    public void ShouldFollowerLoot_FollowerInCombat_ReturnsFalse()
    {
        var follower = new BotEntity(1) { IsInCombat = true };
        var boss = new BotEntity(2) { IsLooting = true };
        Assert.That(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 2500f), Is.False);
    }

    [Test]
    public void ShouldFollowerLoot_BossInCombat_ReturnsFalse()
    {
        var follower = new BotEntity(1);
        var boss = new BotEntity(2) { IsInCombat = true };
        Assert.That(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 2500f), Is.False);
    }

    [Test]
    public void ShouldFollowerLoot_OutOfCommRange_ReturnsFalse()
    {
        var follower = new BotEntity(1) { CurrentPositionX = 100f, CurrentPositionZ = 100f };
        var boss = new BotEntity(2)
        {
            CurrentPositionX = 0f,
            CurrentPositionZ = 0f,
            IsLooting = true,
        };
        // Distance = ~141m, commRangeSqr = 2500 (50m)
        Assert.That(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 2500f), Is.False);
    }

    [Test]
    public void ShouldFollowerLoot_BossLooting_InRange_ReturnsTrue()
    {
        var follower = new BotEntity(1) { CurrentPositionX = 5f, CurrentPositionZ = 5f };
        var boss = new BotEntity(2)
        {
            CurrentPositionX = 0f,
            CurrentPositionZ = 0f,
            IsLooting = true,
        };
        Assert.That(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 2500f), Is.True);
    }

    [Test]
    public void ShouldFollowerLoot_BossAtObjective_ReturnsTrue()
    {
        var follower = new BotEntity(1) { CurrentPositionX = 5f, CurrentPositionZ = 5f };
        var boss = new BotEntity(2)
        {
            CurrentPositionX = 0f,
            CurrentPositionZ = 0f,
            IsCloseToObjective = true,
            HasActiveObjective = true,
        };
        Assert.That(SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 2500f), Is.True);
    }

    [Test]
    public void ShouldFollowerLoot_NaNPositions_ReturnsFalse()
    {
        var follower = new BotEntity(1) { CurrentPositionX = float.NaN, CurrentPositionZ = 0f };
        var boss = new BotEntity(2)
        {
            CurrentPositionX = 0f,
            CurrentPositionZ = 0f,
            IsLooting = true,
        };
        Assert.That(
            SquadLootCoordinator.ShouldFollowerLoot(follower, boss, 2500f),
            Is.False,
            "NaN positions should be treated as out of comm range"
        );
    }

    [Test]
    public void ShareScanResults_CopiesUpToMaxShared()
    {
        var squad = new SquadEntity(1, 0, 3);
        var results = new LootScanResult[16];
        for (int i = 0; i < 16; i++)
        {
            results[i] = new LootScanResult
            {
                Id = i + 1,
                X = i * 10f,
                Y = 0f,
                Z = i * 5f,
                Value = i * 1000f,
                Type = LootTargetType.LooseItem,
            };
        }

        SquadLootCoordinator.ShareScanResults(squad, results, 16);

        Assert.Multiple(() =>
        {
            Assert.That(squad.SharedLootCount, Is.EqualTo(8), "Max 8 shared results");
            Assert.That(squad.SharedLootIds[0], Is.EqualTo(1));
            Assert.That(squad.SharedLootIds[7], Is.EqualTo(8));
            Assert.That(squad.SharedLootValues[0], Is.EqualTo(0f));
            Assert.That(squad.SharedLootValues[7], Is.EqualTo(7000f));
        });
    }

    [Test]
    public void PickSharedTargetForFollower_SkipsBossTarget()
    {
        var squad = new SquadEntity(1, 0, 3);
        squad.SharedLootCount = 3;
        squad.SharedLootIds[0] = 100;
        squad.SharedLootIds[1] = 200;
        squad.SharedLootIds[2] = 300;
        squad.SharedLootValues[0] = 50000f;
        squad.SharedLootValues[1] = 30000f;
        squad.SharedLootValues[2] = 10000f;

        int bossTargetId = 100;
        int idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, 2, bossTargetId, new LootClaimRegistry());
        Assert.That(idx, Is.EqualTo(1), "Should pick item 200 since 100 is boss target");
    }

    [Test]
    public void PickSharedTargetForFollower_SkipsClaimedByOthers()
    {
        var squad = new SquadEntity(1, 0, 3);
        var registry = new LootClaimRegistry();
        registry.TryClaim(3, 200); // other bot claims 200

        squad.SharedLootCount = 3;
        squad.SharedLootIds[0] = 100;
        squad.SharedLootIds[1] = 200;
        squad.SharedLootIds[2] = 300;
        squad.SharedLootValues[0] = 50000f;
        squad.SharedLootValues[1] = 30000f;
        squad.SharedLootValues[2] = 10000f;

        int idx = SquadLootCoordinator.PickSharedTargetForFollower(squad, 2, 100, registry);
        Assert.That(idx, Is.EqualTo(2), "Should pick item 300 since 100 is boss target and 200 is claimed");
    }

    [Test]
    public void ClearSharedLoot_ResetsCount()
    {
        var squad = new SquadEntity(1, 0, 3);
        squad.SharedLootCount = 5;
        SquadLootCoordinator.ClearSharedLoot(squad);
        Assert.That(squad.SharedLootCount, Is.EqualTo(0));
    }

    #endregion

    #region LootTask Tests

    [Test]
    public void LootTask_Score_NoTarget_ReturnsZero()
    {
        var entity = new BotEntity(1) { HasLootTarget = false };
        float score = LootTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootTask_Score_InCombat_ReturnsZero()
    {
        var entity = new BotEntity(1) { HasLootTarget = true, IsInCombat = true };
        float score = LootTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    [Test]
    public void LootTask_Score_NaNTargetValue_ReturnsZero()
    {
        var entity = new BotEntity(1)
        {
            HasLootTarget = true,
            LootTargetValue = float.NaN,
            InventorySpaceFree = 10f,
            LootTargetX = 10f,
            LootTargetY = 0f,
            LootTargetZ = 10f,
        };
        float score = LootTask.Score(entity);
        Assert.That(score, Is.EqualTo(0f), "NaN target value should produce 0, not NaN");
        Assert.That(float.IsNaN(score), Is.False);
    }

    [Test]
    public void LootTask_Score_NaNPosition_ReturnsZero()
    {
        var entity = new BotEntity(1)
        {
            HasLootTarget = true,
            LootTargetValue = 30000f,
            InventorySpaceFree = 10f,
            CurrentPositionX = float.NaN,
            LootTargetX = 10f,
            LootTargetY = 0f,
            LootTargetZ = 10f,
        };
        float score = LootTask.Score(entity);
        Assert.That(float.IsNaN(score), Is.False, "NaN position should not propagate");
    }

    [Test]
    public void LootTask_Score_ValidTarget_ReturnsPositive()
    {
        var entity = new BotEntity(1)
        {
            HasLootTarget = true,
            LootTargetValue = 30000f,
            InventorySpaceFree = 10f,
            CurrentPositionX = 0f,
            CurrentPositionY = 0f,
            CurrentPositionZ = 0f,
            LootTargetX = 10f,
            LootTargetY = 0f,
            LootTargetZ = 10f,
        };
        float score = LootTask.Score(entity);
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(score, Is.LessThanOrEqualTo(LootTask.MaxBaseScore));
    }

    [Test]
    public void LootTask_Score_ClampedToMaxBaseScore()
    {
        // Give extreme values to push score above cap
        var entity = new BotEntity(1)
        {
            HasLootTarget = true,
            LootTargetValue = 500000f,
            InventorySpaceFree = 100f,
            CurrentPositionX = 0f,
            CurrentPositionY = 0f,
            CurrentPositionZ = 0f,
            LootTargetX = 0.1f,
            LootTargetY = 0f,
            LootTargetZ = 0.1f,
            HasActiveObjective = true,
            DistanceToObjective = 1f,
        };
        float score = LootTask.Score(entity);
        Assert.That(score, Is.LessThanOrEqualTo(LootTask.MaxBaseScore));
    }

    #endregion

    #region LootScanResult Tests

    [Test]
    public void ComputeDistanceSqr_SamePosition_ReturnsZero()
    {
        float distSqr = LootScanResult.ComputeDistanceSqr(1f, 2f, 3f, 1f, 2f, 3f);
        Assert.That(distSqr, Is.EqualTo(0f));
    }

    [Test]
    public void ComputeDistanceSqr_KnownDistance()
    {
        // (3,4,0) - (0,0,0) = sqrt(25) = 5m, sqr = 25
        float distSqr = LootScanResult.ComputeDistanceSqr(0f, 0f, 0f, 3f, 4f, 0f);
        Assert.That(distSqr, Is.EqualTo(25f).Within(0.001f));
    }

    #endregion

    #region InventorySpaceHelper Regression Tests (source scan)

    [Test]
    public void InventorySpaceHelper_NegativeFreeSlots_GuardExists()
    {
        // Source-scan test: verify the negative free slots guard was applied
        var source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(FindRepoRoot(), "src/SPTQuestingBots.Client/Helpers/InventorySpaceHelper.cs")
        );

        Assert.That(
            source,
            Does.Contain("gridFree > 0"),
            "InventorySpaceHelper must guard against negative grid free slots (items > total cells)"
        );
    }

    [Test]
    public void LootScorer_HasNaNGuard()
    {
        var source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(FindRepoRoot(), "src/SPTQuestingBots.Client/BotLogic/ECS/Systems/LootScorer.cs")
        );

        Assert.That(source, Does.Contain("float.IsNaN(score)"), "LootScorer.Score must guard against NaN propagation in final score");
    }

    [Test]
    public void LootTask_HasNaNGuard()
    {
        var source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(FindRepoRoot(), "src/SPTQuestingBots.Client/BotLogic/ECS/UtilityAI/Tasks/LootTask.cs")
        );

        Assert.That(source, Does.Contain("float.IsNaN(score)"), "LootTask.Score must guard against NaN propagation in final score");
    }

    [Test]
    public void ItemValueEstimator_NormalizeValue_HasNaNGuard()
    {
        var source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(FindRepoRoot(), "src/SPTQuestingBots.Client/BotLogic/ECS/Systems/ItemValueEstimator.cs")
        );

        Assert.That(source, Does.Contain("float.IsNaN(rawValue)"), "ItemValueEstimator.NormalizeValue must guard against NaN inputs");
    }

    [Test]
    public void SquadLootCoordinator_ShouldFollowerLoot_HasNaNDistanceGuard()
    {
        var source = System.IO.File.ReadAllText(
            System.IO.Path.Combine(FindRepoRoot(), "src/SPTQuestingBots.Client/BotLogic/ECS/Systems/SquadLootCoordinator.cs")
        );

        Assert.That(
            source,
            Does.Contain("float.IsNaN(distSqr)"),
            "SquadLootCoordinator.ShouldFollowerLoot must guard against NaN in comm range distance check"
        );
    }

    #endregion

    #region Helpers

    private static string FindRepoRoot()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null)
        {
            if (System.IO.Directory.Exists(System.IO.Path.Combine(dir, ".git")))
                return dir;
            dir = System.IO.Directory.GetParent(dir)?.FullName;
        }
        return TestContext.CurrentContext.TestDirectory;
    }

    #endregion
}
