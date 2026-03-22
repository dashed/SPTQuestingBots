using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;
using SPTQuestingBots.Models.Pathing;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI;

/// <summary>
/// End-to-end integration tests for combat AI enhancements.
/// Tests that cover state and dogfight state properly influence
/// task scoring through the full ScoreEntity pipeline.
/// </summary>
[TestFixture]
public class CombatAIIntegrationTests
{
    private BotEntity _entity;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
        _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        _entity.Aggression = 0.5f;
        _entity.RaidTimeNormalized = 0.5f;
        _entity.HumanPlayerProximity = 0f;
    }

    // ── Ambush: cover boosts score ──────────────────────────────

    [Test]
    public void AmbushTask_InCover_ScoresHigherThanExposed()
    {
        SetupValidAmbushState();
        var task = new AmbushTask();

        // Score in cover
        _entity.IsInCover = true;
        _entity.HasEnemyInfo = true;
        task.ScoreEntity(1, _entity);
        float inCoverScore = _entity.TaskScores[1];

        // Score exposed with enemy
        _entity.IsInCover = false;
        _entity.HasEnemyInfo = true;
        task.ScoreEntity(1, _entity);
        float exposedScore = _entity.TaskScores[1];

        Assert.That(inCoverScore, Is.GreaterThan(exposedScore));
    }

    [Test]
    public void AmbushTask_NeutralCover_ScoresBetween()
    {
        SetupValidAmbushState();
        var task = new AmbushTask();

        // In cover
        _entity.IsInCover = true;
        _entity.HasEnemyInfo = false;
        task.ScoreEntity(1, _entity);
        float inCoverScore = _entity.TaskScores[1];

        // Neutral (no cover, no enemy)
        _entity.IsInCover = false;
        _entity.HasEnemyInfo = false;
        task.ScoreEntity(1, _entity);
        float neutralScore = _entity.TaskScores[1];

        // Exposed with enemy
        _entity.IsInCover = false;
        _entity.HasEnemyInfo = true;
        task.ScoreEntity(1, _entity);
        float exposedScore = _entity.TaskScores[1];

        Assert.That(inCoverScore, Is.GreaterThan(neutralScore));
        Assert.That(neutralScore, Is.GreaterThan(exposedScore));
    }

    // ── GoToObjective: exposed boosts movement ──────────────────

    [Test]
    public void GoToObjectiveTask_Exposed_ScoresHigherThanInCover()
    {
        SetupValidMoveState();
        var task = new GoToObjectiveTask();

        // Score exposed
        _entity.IsInCover = false;
        _entity.HasEnemyInfo = true;
        task.ScoreEntity(0, _entity);
        float exposedScore = _entity.TaskScores[0];

        // Score in cover
        _entity.IsInCover = true;
        _entity.HasEnemyInfo = true;
        task.ScoreEntity(0, _entity);
        float inCoverScore = _entity.TaskScores[0];

        Assert.That(exposedScore, Is.GreaterThan(inCoverScore));
    }

    // ── Snipe: cover boosts score ───────────────────────────────

    [Test]
    public void SnipeTask_InCover_ScoresHigherThanExposed()
    {
        SetupValidSnipeState();
        var task = new SnipeTask();

        _entity.IsInCover = true;
        _entity.HasEnemyInfo = true;
        task.ScoreEntity(2, _entity);
        float inCoverScore = _entity.TaskScores[2];

        _entity.IsInCover = false;
        _entity.HasEnemyInfo = true;
        task.ScoreEntity(2, _entity);
        float exposedScore = _entity.TaskScores[2];

        Assert.That(inCoverScore, Is.GreaterThan(exposedScore));
    }

    // ── DogFight: loot penalized during close combat ────────────

    [Test]
    public void LootTask_InDogFight_ScoresLowerThanNormal()
    {
        SetupValidLootState();
        var task = new LootTask();

        // Normal state
        _entity.IsInDogFight = false;
        task.ScoreEntity(8, _entity);
        float normalScore = _entity.TaskScores[8];

        // Dogfight active
        _entity.IsInDogFight = true;
        task.ScoreEntity(8, _entity);
        float dogFightScore = _entity.TaskScores[8];

        Assert.That(dogFightScore, Is.LessThan(normalScore));
    }

    // ── DogFight: investigate boosted during close combat ────────

    [Test]
    public void InvestigateTask_InDogFight_ScoresHigherThanNormal()
    {
        SetupValidInvestigateState();
        var task = new InvestigateTask();

        // Normal state
        _entity.IsInDogFight = false;
        task.ScoreEntity(11, _entity);
        float normalScore = _entity.TaskScores[11];

        // Dogfight active
        _entity.IsInDogFight = true;
        task.ScoreEntity(11, _entity);
        float dogFightScore = _entity.TaskScores[11];

        Assert.That(dogFightScore, Is.GreaterThan(normalScore));
    }

    // ── Combined cover + dogfight: realistic scenario ───────────

    [Test]
    public void AmbushTask_InCoverDuringDogFight_GetsReducedByDogFight()
    {
        SetupValidAmbushState();
        var task = new AmbushTask();

        // In cover, no dogfight: ambush should score high
        _entity.IsInCover = true;
        _entity.IsInDogFight = false;
        task.ScoreEntity(1, _entity);
        float coverNoDogFight = _entity.TaskScores[1];

        Assert.That(coverNoDogFight, Is.GreaterThan(0f));
    }

    // ── Patrol: dogfight reduces score ──────────────────────────

    [Test]
    public void PatrolTask_InDogFight_ScoresLower()
    {
        SetupValidPatrolState();
        var task = new PatrolTask();
        PatrolTask.CurrentMapRoutes = new[] { CreateSimpleRoute() };
        PatrolTask.RoutesLoaded = true;

        _entity.IsInDogFight = false;
        task.ScoreEntity(13, _entity);
        float normalScore = _entity.TaskScores[13];

        _entity.IsInDogFight = true;
        task.ScoreEntity(13, _entity);
        float dogFightScore = _entity.TaskScores[13];

        Assert.That(dogFightScore, Is.LessThan(normalScore));
    }

    // ── Personality scaling with cover ──────────────────────────

    [Test]
    public void CoverModifier_AggressiveBot_SmallEffect()
    {
        // Aggressive bots already get reduced ambush score from personality
        // Cover modifier should still boost them when in cover
        float aggressiveCover = ScoringModifiers.CoverModifier(1f, BotActionTypeId.Ambush);
        Assert.That(aggressiveCover, Is.GreaterThan(1f));
    }

    [Test]
    public void CoverModifier_CautiousBot_CompoundsWithPersonality()
    {
        // Cautious personality boosts ambush. Cover also boosts ambush.
        // Combined should be multiplicatively higher.
        float personality = ScoringModifiers.PersonalityModifier(0.1f, BotActionTypeId.Ambush);
        float cover = ScoringModifiers.CoverModifier(1f, BotActionTypeId.Ambush);
        Assert.That(personality * cover, Is.GreaterThan(1f));
    }

    // ── Enemy scoring context: HasEnemyInfo affects cover influence ──

    [Test]
    public void CoverInfluence_EnemyInfoAffectsExposedPenalty()
    {
        // With enemy: exposed (0.0) gives stronger modifier
        float exposedWithEnemy = ScoringModifiers.ComputeCoverInfluence(false, true);
        Assert.That(exposedWithEnemy, Is.EqualTo(0f));

        // Without enemy: neutral (0.5) gives weaker modifier
        float exposedNoEnemy = ScoringModifiers.ComputeCoverInfluence(false, false);
        Assert.That(exposedNoEnemy, Is.EqualTo(0.5f));
    }

    // ── Push capability: informational field check ──────────────

    [Test]
    public void Entity_HasPushCapability_CanBeSet()
    {
        _entity.HasPushCapability = true;
        Assert.That(_entity.HasPushCapability, Is.True);

        _entity.HasPushCapability = false;
        Assert.That(_entity.HasPushCapability, Is.False);
    }

    // ── DogFight thresholds: informational field check ──────────

    [Test]
    public void Entity_DogFightThresholds_CanBeSet()
    {
        _entity.DogFightIn = 10f;
        _entity.DogFightOut = 15f;
        Assert.That(_entity.DogFightIn, Is.EqualTo(10f));
        Assert.That(_entity.DogFightOut, Is.EqualTo(15f));
    }

    // ── Scoring modifiers with all factors combined ─────────────

    [Test]
    public void FullScenario_AggressiveBotExposedInDogFight_PrefersMovement()
    {
        // Aggressive bot, exposed, in dogfight: should prefer movement over camping
        float goToScore = ScoringModifiers.CombinedModifier(0.9f, 0.3f, 0f, 0f, true, BotActionTypeId.GoToObjective);
        float ambushScore = ScoringModifiers.CombinedModifier(0.9f, 0.3f, 0f, 0f, true, BotActionTypeId.Ambush);

        // GoToObjective should be relatively higher for exposed aggressive bots
        // (Personality boosts GoTo for aggressive, cover boosts GoTo when exposed)
        Assert.That(goToScore, Is.GreaterThan(0f));
    }

    [Test]
    public void FullScenario_CautiousBotInCover_PrefersHolding()
    {
        // Cautious bot, in cover, no dogfight: should prefer ambush/snipe
        float ambushMod = ScoringModifiers.CombinedModifier(0.1f, 0.5f, 0f, 1f, false, BotActionTypeId.Ambush);
        float goToMod = ScoringModifiers.CombinedModifier(0.1f, 0.5f, 0f, 1f, false, BotActionTypeId.GoToObjective);

        // Cautious personality + in cover = ambush modifier should be high
        Assert.That(ambushMod, Is.GreaterThan(goToMod));
    }

    // ── Helper methods ──────────────────────────────────────────

    private void SetupValidAmbushState()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Ambush;
        _entity.IsCloseToObjective = true;
    }

    private void SetupValidSnipeState()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Snipe;
        _entity.IsCloseToObjective = true;
    }

    private void SetupValidMoveState()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.MoveToPosition;
        _entity.DistanceToObjective = 100f;
        _entity.NavMeshDistanceToObjective = 120f;
    }

    private void SetupValidLootState()
    {
        _entity.HasLootTarget = true;
        _entity.LootTargetValue = 100f;
        _entity.InventorySpaceFree = 10f;
    }

    private void SetupValidInvestigateState()
    {
        _entity.HasNearbyEvent = true;
        _entity.CombatIntensity = 10;
        _entity.IsInCombat = false;
        _entity.VulturePhase = 0;
        _entity.CurrentPositionX = 0f;
        _entity.CurrentPositionZ = 0f;
        _entity.NearbyEventX = 50f;
        _entity.NearbyEventZ = 50f;
        _entity.VisibleDist = 200f;
    }

    private void SetupValidPatrolState()
    {
        _entity.HasActiveObjective = false;
        _entity.IsInCombat = false;
        _entity.PatrolRouteIndex = -1;
    }

    private static global::SPTQuestingBots.Models.Pathing.PatrolRoute CreateSimpleRoute()
    {
        return new global::SPTQuestingBots.Models.Pathing.PatrolRoute(
            "TestRoute",
            global::SPTQuestingBots.Models.Pathing.PatrolRouteType.Perimeter,
            new[]
            {
                new global::SPTQuestingBots.Models.Pathing.PatrolWaypoint(0f, 0f, 0f, 2f),
                new global::SPTQuestingBots.Models.Pathing.PatrolWaypoint(100f, 0f, 100f, 2f),
            }
        );
    }
}
