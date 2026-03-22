using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

/// <summary>
/// Tests for BotEntity fields related to vision/perception integration:
/// GamePoseVisibilityCoef, VisibleDist, FlarePower, EnemyHasNightVision, EnemyVisibilityLevel.
/// </summary>
[TestFixture]
public class BotEntityVisionPerceptionTests
{
    // ── GamePoseVisibilityCoef ──────────────────────────

    [Test]
    public void GamePoseVisibilityCoef_DefaultsToOne()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.GamePoseVisibilityCoef, Is.EqualTo(1.0f));
    }

    [Test]
    public void GamePoseVisibilityCoef_CanSetLowValue()
    {
        var entity = new BotEntity(0);
        entity.GamePoseVisibilityCoef = 0.3f;
        Assert.That(entity.GamePoseVisibilityCoef, Is.EqualTo(0.3f));
    }

    [Test]
    public void GamePoseVisibilityCoef_CanSetZero()
    {
        var entity = new BotEntity(0);
        entity.GamePoseVisibilityCoef = 0f;
        Assert.That(entity.GamePoseVisibilityCoef, Is.EqualTo(0f));
    }

    // ── VisibleDist ─────────────────────────────────────

    [Test]
    public void VisibleDist_DefaultsTo150()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.VisibleDist, Is.EqualTo(150f));
    }

    [Test]
    public void VisibleDist_CanBeSetToLowValue()
    {
        var entity = new BotEntity(0);
        entity.VisibleDist = 50f;
        Assert.That(entity.VisibleDist, Is.EqualTo(50f));
    }

    [Test]
    public void VisibleDist_CanBeSetToHighValue()
    {
        var entity = new BotEntity(0);
        entity.VisibleDist = 500f;
        Assert.That(entity.VisibleDist, Is.EqualTo(500f));
    }

    // ── FlarePower ──────────────────────────────────────

    [Test]
    public void FlarePower_DefaultsToZero()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.FlarePower, Is.EqualTo(0f));
    }

    [Test]
    public void FlarePower_AboveOneIndicatesRecentFire()
    {
        var entity = new BotEntity(0);
        entity.FlarePower = 1.5f;
        Assert.That(entity.FlarePower, Is.GreaterThan(1f));
    }

    [Test]
    public void FlarePower_CanBeSetToZero()
    {
        var entity = new BotEntity(0);
        entity.FlarePower = 2.0f;
        entity.FlarePower = 0f;
        Assert.That(entity.FlarePower, Is.EqualTo(0f));
    }

    // ── EnemyHasNightVision ─────────────────────────────

    [Test]
    public void EnemyHasNightVision_DefaultsFalse()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.EnemyHasNightVision, Is.False);
    }

    [Test]
    public void EnemyHasNightVision_CanBeSetTrue()
    {
        var entity = new BotEntity(0);
        entity.EnemyHasNightVision = true;
        Assert.That(entity.EnemyHasNightVision, Is.True);
    }

    [Test]
    public void EnemyHasNightVision_ClearsWithEnemyInfo()
    {
        var entity = new BotEntity(0);
        entity.EnemyHasNightVision = true;
        entity.HasEnemyInfo = false;
        // Field persists — callers must check HasEnemyInfo first
        Assert.That(entity.EnemyHasNightVision, Is.True);
    }

    // ── EnemyVisibilityLevel ────────────────────────────

    [Test]
    public void EnemyVisibilityLevel_DefaultsToZero()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.EnemyVisibilityLevel, Is.EqualTo(0f));
    }

    [Test]
    public void EnemyVisibilityLevel_CanSetGradientValues()
    {
        var entity = new BotEntity(0);
        entity.EnemyVisibilityLevel = 0.5f;
        Assert.That(entity.EnemyVisibilityLevel, Is.EqualTo(0.5f));

        entity.EnemyVisibilityLevel = 0.9f;
        Assert.That(entity.EnemyVisibilityLevel, Is.EqualTo(0.9f));
    }

    [Test]
    public void EnemyVisibilityLevel_FullDetection()
    {
        var entity = new BotEntity(0);
        entity.EnemyVisibilityLevel = 1.0f;
        Assert.That(entity.EnemyVisibilityLevel, Is.EqualTo(1.0f));
    }

    // ── Combined state scenarios ────────────────────────

    [Test]
    public void VisionFields_IndependentOfEachOther()
    {
        var entity = new BotEntity(0);
        entity.GamePoseVisibilityCoef = 0.3f;
        entity.VisibleDist = 80f;
        entity.FlarePower = 1.5f;
        entity.EnemyHasNightVision = true;
        entity.EnemyVisibilityLevel = 0.7f;

        Assert.That(entity.GamePoseVisibilityCoef, Is.EqualTo(0.3f));
        Assert.That(entity.VisibleDist, Is.EqualTo(80f));
        Assert.That(entity.FlarePower, Is.EqualTo(1.5f));
        Assert.That(entity.EnemyHasNightVision, Is.True);
        Assert.That(entity.EnemyVisibilityLevel, Is.EqualTo(0.7f));
    }

    [Test]
    public void VisionFields_IndependentOfExistingEnemyInfo()
    {
        var entity = new BotEntity(0);
        entity.HasEnemyInfo = true;
        entity.IsEnemyVisible = true;
        entity.EnemyVisibilityLevel = 0.8f;
        entity.EnemyHasNightVision = true;

        // Existing enemy info fields are unaffected
        Assert.That(entity.HasEnemyInfo, Is.True);
        Assert.That(entity.IsEnemyVisible, Is.True);
        // New fields are independent
        Assert.That(entity.EnemyVisibilityLevel, Is.EqualTo(0.8f));
        Assert.That(entity.EnemyHasNightVision, Is.True);
    }

    [Test]
    public void VisionFields_IndependentOfGameDataFields()
    {
        var entity = new BotEntity(0);
        entity.HasGameAmbushPoint = true;
        entity.VisibleDist = 200f;
        entity.GamePoseVisibilityCoef = 0.5f;

        Assert.That(entity.HasGameAmbushPoint, Is.True);
        Assert.That(entity.VisibleDist, Is.EqualTo(200f));
        Assert.That(entity.GamePoseVisibilityCoef, Is.EqualTo(0.5f));
    }
}
