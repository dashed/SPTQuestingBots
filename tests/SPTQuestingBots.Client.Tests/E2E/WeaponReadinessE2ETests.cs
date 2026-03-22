using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.E2E;

/// <summary>
/// End-to-end tests verifying weapon state integration across the scoring pipeline.
/// Tests entity state flows, scoring behavior, and cross-task interactions.
/// </summary>
[TestFixture]
public class WeaponReadinessE2ETests
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

    // ── Cross-task ammo impact ──────────────────────────

    [Test]
    public void LowAmmo_AmbushScoreLowerThanLootScore()
    {
        // Set up ambush conditions
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Ambush;
        _entity.IsCloseToObjective = true;
        _entity.AmmoRatio = 0.1f;

        float ambushScore = AmbushTask.Score(_entity);

        // Set up loot conditions
        _entity.HasLootTarget = true;
        _entity.LootTargetValue = 30000f;
        _entity.LootTargetX = _entity.CurrentPositionX + 5f;
        _entity.LootTargetZ = _entity.CurrentPositionZ + 5f;
        _entity.LootTargetY = _entity.CurrentPositionY;
        _entity.InventorySpaceFree = 10f;

        float lootScore = LootTask.Score(_entity);

        // With 10% ammo, ambush should be less attractive than looting
        Assert.That(ambushScore, Is.LessThan(AmbushTask.BaseScore));
        Assert.That(lootScore, Is.GreaterThan(0f));
    }

    [Test]
    public void FullAmmo_AmbushScoreHigherThanPartialAmmo()
    {
        SetupAmbush();
        _entity.AmmoRatio = 1f;
        float fullScore = AmbushTask.Score(_entity);

        _entity.AmmoRatio = 0.3f;
        float partialScore = AmbushTask.Score(_entity);

        Assert.That(fullScore, Is.GreaterThan(partialScore));
    }

    [Test]
    public void FullAmmo_SnipeScoreHigherThanPartialAmmo()
    {
        SetupSnipe();
        _entity.AmmoRatio = 1f;
        float fullScore = SnipeTask.Score(_entity);

        _entity.AmmoRatio = 0.3f;
        float partialScore = SnipeTask.Score(_entity);

        Assert.That(fullScore, Is.GreaterThan(partialScore));
    }

    // ── Close weapon snipe penalty ──────────────────────

    [Test]
    public void CloseWeapon_SnipeHeavilyPenalized_AmbushNot()
    {
        SetupAmbush();
        _entity.IsCloseWeapon = true;
        float ambushWithClose = AmbushTask.Score(_entity);

        SetupSnipe();
        _entity.IsCloseWeapon = true;
        float snipeWithClose = SnipeTask.Score(_entity);

        // Ambush score should be significantly higher than penalized snipe
        Assert.That(ambushWithClose, Is.GreaterThan(snipeWithClose));
    }

    [Test]
    public void RifleBot_SnipeHigherThanShotgunBot()
    {
        SetupSnipe();
        _entity.IsCloseWeapon = false;
        float rifleSnipe = SnipeTask.Score(_entity);

        _entity.IsCloseWeapon = true;
        float shotgunSnipe = SnipeTask.Score(_entity);

        Assert.That(rifleSnipe, Is.GreaterThan(shotgunSnipe * 2f));
    }

    // ── Malfunction behavior ──────────────────────────

    [Test]
    public void MalfunctionFlag_DefaultsFalse()
    {
        Assert.That(_entity.HasWeaponMalfunction, Is.False);
    }

    [Test]
    public void MalfunctionFlag_CanBeToggled()
    {
        _entity.HasWeaponMalfunction = true;
        Assert.That(_entity.HasWeaponMalfunction, Is.True);
        _entity.HasWeaponMalfunction = false;
        Assert.That(_entity.HasWeaponMalfunction, Is.False);
    }

    // ── Weapon ready state ──────────────────────────

    [Test]
    public void WeaponReady_DefaultsTrue()
    {
        Assert.That(_entity.IsWeaponReady, Is.True);
    }

    [Test]
    public void WeaponReady_CanBeToggledFalse()
    {
        _entity.IsWeaponReady = false;
        Assert.That(_entity.IsWeaponReady, Is.False);
    }

    // ── ScoreEntity integration with ammo ──────────────

    [Test]
    public void AmbushScoreEntity_LowAmmo_ProducesLowerFinalScore()
    {
        SetupAmbush();
        var task = new AmbushTask();

        _entity.AmmoRatio = 1f;
        task.ScoreEntity(0, _entity);
        float fullScore = _entity.TaskScores[0];

        _entity.AmmoRatio = 0.2f;
        task.ScoreEntity(0, _entity);
        float lowScore = _entity.TaskScores[0];

        Assert.That(lowScore, Is.LessThan(fullScore));
    }

    [Test]
    public void SnipeScoreEntity_CloseWeapon_ProducesLowerFinalScore()
    {
        SetupSnipe();
        var task = new SnipeTask();

        _entity.IsCloseWeapon = false;
        task.ScoreEntity(0, _entity);
        float normalScore = _entity.TaskScores[0];

        _entity.IsCloseWeapon = true;
        task.ScoreEntity(0, _entity);
        float closeScore = _entity.TaskScores[0];

        Assert.That(closeScore, Is.LessThan(normalScore));
    }

    // ── Ammo ratio boundary values ──────────────────────

    [Test]
    public void AmmoRatio_AtZero_AllCombatTasksStillPositive()
    {
        _entity.AmmoRatio = 0f;

        SetupAmbush();
        float ambush = AmbushTask.Score(_entity);
        Assert.That(ambush, Is.GreaterThan(0f), "Ambush should be positive at 0 ammo");

        SetupSnipe();
        float snipe = SnipeTask.Score(_entity);
        Assert.That(snipe, Is.GreaterThan(0f), "Snipe should be positive at 0 ammo");
    }

    [Test]
    public void AmmoRatio_AtOne_FullScoreForAllCombatTasks()
    {
        _entity.AmmoRatio = 1f;

        SetupAmbush();
        float ambush = AmbushTask.Score(_entity);
        Assert.That(ambush, Is.EqualTo(AmbushTask.BaseScore).Within(0.001f));

        SetupSnipe();
        float snipe = SnipeTask.Score(_entity);
        Assert.That(snipe, Is.EqualTo(SnipeTask.BaseScore).Within(0.001f));
    }

    [Test]
    public void AmmoRatio_Monotonic_HigherAmmoHigherScore()
    {
        SetupAmbush();
        float prevScore = 0f;
        for (float ammo = 0f; ammo <= 1f; ammo += 0.1f)
        {
            _entity.AmmoRatio = ammo;
            float score = AmbushTask.Score(_entity);
            Assert.That(score, Is.GreaterThanOrEqualTo(prevScore), $"Score should increase with ammo={ammo:F1}");
            prevScore = score;
        }
    }

    // ── Config defaults ──────────────────────────────

    [Test]
    public void WeaponReadinessConfig_HasCorrectDefaults()
    {
        var config = new SPTQuestingBots.Configuration.WeaponReadinessConfig();
        Assert.That(config.Enabled, Is.True);
        Assert.That(config.SkipQuestingOnMalfunction, Is.True);
        Assert.That(config.SwitchToSingleFireForAmbush, Is.True);
    }

    // ── Helpers ──────────────────────────────────────────

    private void SetupAmbush()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Ambush;
        _entity.IsCloseToObjective = true;
        _entity.IsCloseWeapon = false;
    }

    private void SetupSnipe()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Snipe;
        _entity.IsCloseToObjective = true;
        _entity.IsCloseWeapon = false;
    }
}
