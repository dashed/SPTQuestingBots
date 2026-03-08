using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.Systems;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.Data;

/// <summary>
/// Tests verifying loot claims carry meaningful values and that the loot scoring
/// pipeline produces non-zero scores when items have value.
/// </summary>
[TestFixture]
public class LootClaimValueTests
{
    // ── LootScanResult value constants ──────────────────────────────

    [Test]
    public void DefaultContainerValue_IsPositive()
    {
        Assert.Greater(LootTargetType.DefaultContainerValue, 0f, "Container default value should be positive");
    }

    [Test]
    public void DefaultCorpseValue_IsPositive()
    {
        Assert.Greater(LootTargetType.DefaultCorpseValue, 0f, "Corpse default value should be positive");
    }

    [Test]
    public void DefaultContainerValue_IsAboveMinItemValue()
    {
        // The default MinItemValue from config is 5000.
        // Containers should always pass the minimum value filter.
        Assert.GreaterOrEqual(LootTargetType.DefaultContainerValue, 5000f, "Container value should meet minimum item value threshold");
    }

    [Test]
    public void DefaultCorpseValue_IsAboveMinItemValue()
    {
        Assert.GreaterOrEqual(LootTargetType.DefaultCorpseValue, 5000f, "Corpse value should meet minimum item value threshold");
    }

    [Test]
    public void DefaultCorpseValue_HigherThanContainer()
    {
        // Corpses generally have more loot than containers
        Assert.Greater(
            LootTargetType.DefaultCorpseValue,
            LootTargetType.DefaultContainerValue,
            "Corpses should be valued higher than containers"
        );
    }

    // ── LootScorer with non-zero values ─────────────────────────────

    [Test]
    public void LootScorer_NonZeroValue_ProducesNonZeroScore()
    {
        float score = LootScorer.Score(
            targetValue: 15000f,
            distanceSqr: 25f, // 5m
            inventorySpaceFree: 1f,
            isInCombat: false,
            distanceToObjectiveSqr: 10000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            new LootScoringConfig(5000f, 50000f, 0.001f, 0.15f, 0.3f, 15f)
        );
        Assert.Greater(score, 0f, "Non-zero value loot should produce non-zero score");
    }

    [Test]
    public void LootScorer_ZeroValue_ProducesZeroScore()
    {
        float score = LootScorer.Score(
            targetValue: 0f,
            distanceSqr: 25f,
            inventorySpaceFree: 1f,
            isInCombat: false,
            distanceToObjectiveSqr: 10000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            new LootScoringConfig(5000f, 50000f, 0.001f, 0.15f, 0.3f, 15f)
        );
        Assert.AreEqual(0f, score, "Zero value loot below min should score zero");
    }

    [Test]
    public void LootScorer_ContainerDefaultValue_ProducesNonZeroScore()
    {
        float score = LootScorer.Score(
            targetValue: LootTargetType.DefaultContainerValue,
            distanceSqr: 100f, // 10m
            inventorySpaceFree: 1f,
            isInCombat: false,
            distanceToObjectiveSqr: 10000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            new LootScoringConfig(5000f, 50000f, 0.001f, 0.15f, 0.3f, 15f)
        );
        Assert.Greater(score, 0f, "Container with default value should score non-zero");
    }

    [Test]
    public void LootScorer_CorpseDefaultValue_ProducesNonZeroScore()
    {
        float score = LootScorer.Score(
            targetValue: LootTargetType.DefaultCorpseValue,
            distanceSqr: 100f, // 10m
            inventorySpaceFree: 1f,
            isInCombat: false,
            distanceToObjectiveSqr: 10000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            new LootScoringConfig(5000f, 50000f, 0.001f, 0.15f, 0.3f, 15f)
        );
        Assert.Greater(score, 0f, "Corpse with default value should score non-zero");
    }

    [Test]
    public void LootScorer_HigherValue_ProducesHigherScore()
    {
        var config = new LootScoringConfig(5000f, 50000f, 0.001f, 0.15f, 0.3f, 15f);

        float lowScore = LootScorer.Score(
            targetValue: 10000f,
            distanceSqr: 100f,
            inventorySpaceFree: 1f,
            isInCombat: false,
            distanceToObjectiveSqr: 10000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            config
        );

        float highScore = LootScorer.Score(
            targetValue: 40000f,
            distanceSqr: 100f,
            inventorySpaceFree: 1f,
            isInCombat: false,
            distanceToObjectiveSqr: 10000f,
            timeSinceLastLoot: 30f,
            isGearUpgrade: false,
            config
        );

        Assert.Greater(highScore, lowScore, "Higher value loot should produce higher score");
    }

    // ── LootTargetSelector with values ──────────────────────────────

    [Test]
    public void LootTargetSelector_WithValues_SelectsHighestValue()
    {
        var results = new LootScanResult[]
        {
            new LootScanResult
            {
                Id = 1,
                X = 5f,
                Y = 0f,
                Z = 0f,
                Type = LootTargetType.LooseItem,
                Value = 10000f,
                DistanceSqr = 25f,
            },
            new LootScanResult
            {
                Id = 2,
                X = 5f,
                Y = 0f,
                Z = 0f,
                Type = LootTargetType.LooseItem,
                Value = 40000f,
                DistanceSqr = 25f,
            },
        };

        var config = new LootScoringConfig(5000f, 50000f, 0.001f, 0.15f, 0.3f, 15f);
        int best = LootTargetSelector.SelectBest(
            results,
            2,
            inventorySpaceFree: 1f,
            isInCombat: false,
            distanceToObjectiveSqr: 10000f,
            timeSinceLastLoot: 30f,
            claims: null,
            botId: 0,
            config
        );

        Assert.AreEqual(1, best, "Should select second item (index 1) with higher value");
    }

    [Test]
    public void LootTargetSelector_WithZeroValues_ReturnsNoTarget()
    {
        var results = new LootScanResult[]
        {
            new LootScanResult
            {
                Id = 1,
                X = 5f,
                Y = 0f,
                Z = 0f,
                Type = LootTargetType.LooseItem,
                Value = 0f,
                DistanceSqr = 25f,
            },
        };

        var config = new LootScoringConfig(5000f, 50000f, 0.001f, 0.15f, 0.3f, 15f);
        int best = LootTargetSelector.SelectBest(
            results,
            1,
            inventorySpaceFree: 1f,
            isInCombat: false,
            distanceToObjectiveSqr: 10000f,
            timeSinceLastLoot: 30f,
            claims: null,
            botId: 0,
            config
        );

        Assert.AreEqual(-1, best, "Zero-value loot below min should not be selected");
    }

    // ── LootTask scoring with non-zero values ───────────────────────

    [Test]
    public void LootTask_NonZeroValue_ProducesNonZeroScore()
    {
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasLootTarget = true;
        entity.LootTargetValue = LootTargetType.DefaultContainerValue;
        entity.LootTargetX = 5f;
        entity.LootTargetY = 0f;
        entity.LootTargetZ = 0f;
        entity.CurrentPositionX = 0f;
        entity.CurrentPositionY = 0f;
        entity.CurrentPositionZ = 0f;
        entity.InventorySpaceFree = 1f;
        entity.IsInCombat = false;

        float score = LootTask.Score(entity);
        Assert.Greater(score, 0f, "LootTask should score non-zero with container default value");
    }

    [Test]
    public void LootTask_CorpseDefaultValue_ProducesHigherScoreThanContainer()
    {
        var containerEntity = new BotEntity(0);
        containerEntity.TaskScores = new float[QuestTaskFactory.TaskCount];
        containerEntity.HasLootTarget = true;
        containerEntity.LootTargetValue = LootTargetType.DefaultContainerValue;
        containerEntity.LootTargetX = 5f;
        containerEntity.LootTargetY = 0f;
        containerEntity.LootTargetZ = 0f;
        containerEntity.InventorySpaceFree = 1f;

        var corpseEntity = new BotEntity(1);
        corpseEntity.TaskScores = new float[QuestTaskFactory.TaskCount];
        corpseEntity.HasLootTarget = true;
        corpseEntity.LootTargetValue = LootTargetType.DefaultCorpseValue;
        corpseEntity.LootTargetX = 5f;
        corpseEntity.LootTargetY = 0f;
        corpseEntity.LootTargetZ = 0f;
        corpseEntity.InventorySpaceFree = 1f;

        Assert.Greater(
            LootTask.Score(corpseEntity),
            LootTask.Score(containerEntity),
            "Corpse loot should score higher than container with default values"
        );
    }

    // ── End-to-end: scan → claim → score pipeline ────────────────────

    [Test]
    public void Pipeline_ContainerScanResult_FlowsThroughToLootScore()
    {
        // Simulate: scanner produces result with container default value
        var scanResult = new LootScanResult
        {
            Id = 42,
            X = 10f,
            Y = 0f,
            Z = 0f,
            Type = LootTargetType.Container,
            Value = LootTargetType.DefaultContainerValue,
            DistanceSqr = 100f,
        };

        // Simulate: target selector picks it, then entity gets the value
        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasLootTarget = true;
        entity.LootTargetId = scanResult.Id;
        entity.LootTargetX = scanResult.X;
        entity.LootTargetY = scanResult.Y;
        entity.LootTargetZ = scanResult.Z;
        entity.LootTargetType = scanResult.Type;
        entity.LootTargetValue = scanResult.Value;
        entity.InventorySpaceFree = 1f;

        // The claim should work
        var registry = new LootClaimRegistry();
        Assert.IsTrue(registry.TryClaim(entity.Id, scanResult.Id));

        // LootTask should produce a meaningful score
        float score = LootTask.Score(entity);
        Assert.Greater(score, 0f, "Full pipeline from scan → claim → score should produce non-zero result");
    }

    [Test]
    public void Pipeline_CorpseScanResult_FlowsThroughToLootScore()
    {
        var scanResult = new LootScanResult
        {
            Id = 99,
            X = 8f,
            Y = 0f,
            Z = 0f,
            Type = LootTargetType.Corpse,
            Value = LootTargetType.DefaultCorpseValue,
            DistanceSqr = 64f,
        };

        var entity = new BotEntity(0);
        entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        entity.HasLootTarget = true;
        entity.LootTargetId = scanResult.Id;
        entity.LootTargetX = scanResult.X;
        entity.LootTargetY = scanResult.Y;
        entity.LootTargetZ = scanResult.Z;
        entity.LootTargetType = scanResult.Type;
        entity.LootTargetValue = scanResult.Value;
        entity.InventorySpaceFree = 1f;

        var registry = new LootClaimRegistry();
        Assert.IsTrue(registry.TryClaim(entity.Id, scanResult.Id));

        float score = LootTask.Score(entity);
        Assert.Greater(score, 0f, "Corpse pipeline from scan → claim → score should produce non-zero result");
    }

    // ── ItemValueEstimator integration ────────────────────────────────

    [Test]
    public void ItemValueEstimator_NormalizeValue_WithContainerDefault()
    {
        float normalized = ItemValueEstimator.NormalizeValue(LootTargetType.DefaultContainerValue, 50000f);
        Assert.Greater(normalized, 0f, "Normalized container value should be positive");
        Assert.LessOrEqual(normalized, 1f, "Normalized value should not exceed 1.0");
    }

    [Test]
    public void ItemValueEstimator_NormalizeValue_WithCorpseDefault()
    {
        float normalized = ItemValueEstimator.NormalizeValue(LootTargetType.DefaultCorpseValue, 50000f);
        Assert.Greater(normalized, 0f, "Normalized corpse value should be positive");
        Assert.LessOrEqual(normalized, 1f, "Normalized value should not exceed 1.0");
    }
}
