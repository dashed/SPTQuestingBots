using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;
using SPTQuestingBots.BotLogic.ECS.UtilityAI;
using SPTQuestingBots.BotLogic.ECS.UtilityAI.Tasks;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.UtilityAI.Tasks;

[TestFixture]
public class SnipeTaskWeaponTests
{
    private BotEntity _entity;

    [SetUp]
    public void SetUp()
    {
        _entity = new BotEntity(0);
        _entity.TaskScores = new float[QuestTaskFactory.TaskCount];
        SetupValidSnipeState();
    }

    // ── Close weapon penalty ──────────────────────────

    [Test]
    public void Score_CloseWeapon_ReturnsPenalizedScore()
    {
        _entity.IsCloseWeapon = true;
        float score = SnipeTask.Score(_entity);
        float expected = SnipeTask.BaseScore * SnipeTask.CloseWeaponPenalty;
        Assert.That(score, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void Score_NonCloseWeapon_ReturnsBaseScore()
    {
        _entity.IsCloseWeapon = false;
        float score = SnipeTask.Score(_entity);
        Assert.That(score, Is.EqualTo(SnipeTask.BaseScore).Within(0.001f));
    }

    [Test]
    public void Score_CloseWeapon_SignificantlyLower()
    {
        _entity.IsCloseWeapon = false;
        float normalScore = SnipeTask.Score(_entity);

        _entity.IsCloseWeapon = true;
        float closeScore = SnipeTask.Score(_entity);

        Assert.That(closeScore, Is.LessThan(normalScore * 0.5f));
    }

    // ── Ammo ratio ──────────────────────────────────

    [Test]
    public void Score_FullAmmo_ReturnsBaseScore()
    {
        _entity.AmmoRatio = 1f;
        float score = SnipeTask.Score(_entity);
        Assert.That(score, Is.EqualTo(SnipeTask.BaseScore).Within(0.001f));
    }

    [Test]
    public void Score_HalfAmmo_ReturnsReducedScore()
    {
        _entity.AmmoRatio = 0.5f;
        float score = SnipeTask.Score(_entity);

        float expectedMultiplier = SnipeTask.MinAmmoMultiplier + (1f - SnipeTask.MinAmmoMultiplier) * 0.5f;
        float expected = SnipeTask.BaseScore * expectedMultiplier;
        Assert.That(score, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void Score_EmptyAmmo_ReturnsMinimumScore()
    {
        _entity.AmmoRatio = 0f;
        float score = SnipeTask.Score(_entity);

        float expected = SnipeTask.BaseScore * SnipeTask.MinAmmoMultiplier;
        Assert.That(score, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void Score_EmptyAmmo_StillPositive()
    {
        _entity.AmmoRatio = 0f;
        float score = SnipeTask.Score(_entity);
        Assert.That(score, Is.GreaterThan(0f));
    }

    // ── Combined close weapon + ammo ──────────────────

    [Test]
    public void Score_CloseWeaponAndLowAmmo_DoubleReduction()
    {
        _entity.IsCloseWeapon = true;
        _entity.AmmoRatio = 0.5f;

        float score = SnipeTask.Score(_entity);

        float ammoMult = SnipeTask.MinAmmoMultiplier + (1f - SnipeTask.MinAmmoMultiplier) * 0.5f;
        float expected = SnipeTask.BaseScore * SnipeTask.CloseWeaponPenalty * ammoMult;
        Assert.That(score, Is.EqualTo(expected).Within(0.001f));
    }

    [Test]
    public void Score_CloseWeaponAndEmptyAmmo_VeryLowButPositive()
    {
        _entity.IsCloseWeapon = true;
        _entity.AmmoRatio = 0f;

        float score = SnipeTask.Score(_entity);
        Assert.That(score, Is.GreaterThan(0f));
        Assert.That(score, Is.LessThan(SnipeTask.BaseScore * 0.15f));
    }

    // ── Gating ──────────────────────────────────────────

    [Test]
    public void Score_NotCloseToObjective_ReturnsZero()
    {
        _entity.IsCloseToObjective = false;
        _entity.IsCloseWeapon = true;
        float score = SnipeTask.Score(_entity);
        Assert.That(score, Is.EqualTo(0f));
    }

    // ── Constants ──────────────────────────────────────

    [Test]
    public void CloseWeaponPenalty_IsPositive()
    {
        Assert.That(SnipeTask.CloseWeaponPenalty, Is.GreaterThan(0f));
    }

    [Test]
    public void CloseWeaponPenalty_IsLessThanOne()
    {
        Assert.That(SnipeTask.CloseWeaponPenalty, Is.LessThan(1f));
    }

    [Test]
    public void MinAmmoMultiplier_IsPositive()
    {
        Assert.That(SnipeTask.MinAmmoMultiplier, Is.GreaterThan(0f));
    }

    [Test]
    public void MinAmmoMultiplier_IsLessThanOne()
    {
        Assert.That(SnipeTask.MinAmmoMultiplier, Is.LessThan(1f));
    }

    // ── Helper ──────────────────────────────────────────

    private void SetupValidSnipeState()
    {
        _entity.HasActiveObjective = true;
        _entity.CurrentQuestAction = QuestActionId.Snipe;
        _entity.IsCloseToObjective = true;
        _entity.IsCloseWeapon = false;
        _entity.AmmoRatio = 1f;
    }
}
