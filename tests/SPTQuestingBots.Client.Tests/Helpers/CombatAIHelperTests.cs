using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

/// <summary>
/// Unit tests for the EnemyScoringContext struct and CombatAIHelper pure-logic elements.
/// Note: BotOwner-dependent methods can only be tested in integration tests with game assemblies.
/// These tests verify the struct defaults and pure-logic behavior.
/// </summary>
[TestFixture]
public class CombatAIHelperTests
{
    // ── EnemyScoringContext defaults ─────────────────────────────

    [Test]
    public void EnemyScoringContext_Defaults_NoEnemy()
    {
        var ctx = new EnemyScoringContext();

        Assert.Multiple(() =>
        {
            Assert.That(ctx.HasEnemy, Is.False);
            Assert.That(ctx.Distance, Is.EqualTo(0f));
            Assert.That(ctx.IsVisible, Is.False);
            Assert.That(ctx.TimeSinceLastSeen, Is.EqualTo(0f));
        });
    }

    [Test]
    public void EnemyScoringContext_CanSetFields()
    {
        var ctx = new EnemyScoringContext
        {
            HasEnemy = true,
            Distance = 50f,
            IsVisible = true,
            TimeSinceLastSeen = 2.5f,
        };

        Assert.Multiple(() =>
        {
            Assert.That(ctx.HasEnemy, Is.True);
            Assert.That(ctx.Distance, Is.EqualTo(50f));
            Assert.That(ctx.IsVisible, Is.True);
            Assert.That(ctx.TimeSinceLastSeen, Is.EqualTo(2.5f));
        });
    }

    [Test]
    public void EnemyScoringContext_IsValueType()
    {
        var ctx1 = new EnemyScoringContext { HasEnemy = true, Distance = 10f };
        var ctx2 = ctx1; // copy
        ctx2.Distance = 20f;

        // Mutating ctx2 should not affect ctx1 (value type semantics)
        Assert.That(ctx1.Distance, Is.EqualTo(10f));
        Assert.That(ctx2.Distance, Is.EqualTo(20f));
    }

    // ── Entity combat AI field integration ──────────────────────

    [Test]
    public void BotEntity_CombatAIFields_Settable()
    {
        var entity = new SPTQuestingBots.BotLogic.ECS.BotEntity(0);

        entity.IsInCover = true;
        entity.IsInDogFight = true;
        entity.HasPushCapability = true;
        entity.DogFightIn = 12f;
        entity.DogFightOut = 18f;

        Assert.Multiple(() =>
        {
            Assert.That(entity.IsInCover, Is.True);
            Assert.That(entity.IsInDogFight, Is.True);
            Assert.That(entity.HasPushCapability, Is.True);
            Assert.That(entity.DogFightIn, Is.EqualTo(12f));
            Assert.That(entity.DogFightOut, Is.EqualTo(18f));
        });
    }

    [Test]
    public void BotEntity_CombatAIFields_Defaults()
    {
        var entity = new SPTQuestingBots.BotLogic.ECS.BotEntity(0);

        Assert.Multiple(() =>
        {
            Assert.That(entity.IsInCover, Is.False);
            Assert.That(entity.IsInDogFight, Is.False);
            Assert.That(entity.HasPushCapability, Is.False);
            Assert.That(entity.DogFightIn, Is.EqualTo(0f));
            Assert.That(entity.DogFightOut, Is.EqualTo(0f));
        });
    }

    // ── Cover influence in scoring pipeline ──────────────────────

    [Test]
    public void CoverState_AffectsAmbushScoring()
    {
        var entity = new SPTQuestingBots.BotLogic.ECS.BotEntity(0);
        entity.TaskScores = new float[SPTQuestingBots.BotLogic.ECS.UtilityAI.QuestTaskFactory.TaskCount];
        entity.HasActiveObjective = true;
        entity.CurrentQuestAction = SPTQuestingBots.BotLogic.ECS.UtilityAI.QuestActionId.Ambush;
        entity.IsCloseToObjective = true;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        var task = new SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.AmbushTask();

        // In cover
        entity.IsInCover = true;
        entity.HasEnemyInfo = true;
        task.ScoreEntity(1, entity);
        float coverScore = entity.TaskScores[1];

        // Exposed
        entity.IsInCover = false;
        entity.HasEnemyInfo = true;
        task.ScoreEntity(1, entity);
        float exposedScore = entity.TaskScores[1];

        Assert.That(coverScore, Is.GreaterThan(exposedScore));
        Assert.That(coverScore, Is.GreaterThan(0f));
        Assert.That(exposedScore, Is.GreaterThan(0f));
    }

    [Test]
    public void DogFightState_AffectsLootScoring()
    {
        var entity = new SPTQuestingBots.BotLogic.ECS.BotEntity(0);
        entity.TaskScores = new float[SPTQuestingBots.BotLogic.ECS.UtilityAI.QuestTaskFactory.TaskCount];
        entity.HasLootTarget = true;
        entity.LootTargetValue = 100f;
        entity.InventorySpaceFree = 10f;
        entity.Aggression = 0.5f;
        entity.RaidTimeNormalized = 0.5f;

        var task = new SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks.LootTask();

        // No dogfight
        entity.IsInDogFight = false;
        task.ScoreEntity(8, entity);
        float normalScore = entity.TaskScores[8];

        // In dogfight
        entity.IsInDogFight = true;
        task.ScoreEntity(8, entity);
        float dogFightScore = entity.TaskScores[8];

        Assert.That(normalScore, Is.GreaterThan(dogFightScore));
    }
}
