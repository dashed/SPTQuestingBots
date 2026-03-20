using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

/// <summary>
/// Tests for BotEntity fields related to game data integration
/// (BotAmbushData, BotSearchData, BotMover).
/// </summary>
[TestFixture]
public class BotEntityGameDataTests
{
    // ── HasGameAmbushPoint ──────────────────────────────

    [Test]
    public void HasGameAmbushPoint_DefaultsFalse()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.HasGameAmbushPoint, Is.False);
    }

    [Test]
    public void HasGameAmbushPoint_CanBeSetTrue()
    {
        var entity = new BotEntity(0);
        entity.HasGameAmbushPoint = true;
        Assert.That(entity.HasGameAmbushPoint, Is.True);
    }

    // ── HasGameSearchTarget ─────────────────────────────

    [Test]
    public void HasGameSearchTarget_DefaultsFalse()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.HasGameSearchTarget, Is.False);
    }

    [Test]
    public void HasGameSearchTarget_CanBeSetTrue()
    {
        var entity = new BotEntity(0);
        entity.HasGameSearchTarget = true;
        Assert.That(entity.HasGameSearchTarget, Is.True);
    }

    // ── GameSearchTargetX/Y/Z ───────────────────────────

    [Test]
    public void GameSearchTargetX_DefaultsZero()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.GameSearchTargetX, Is.EqualTo(0f));
    }

    [Test]
    public void GameSearchTargetY_DefaultsZero()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.GameSearchTargetY, Is.EqualTo(0f));
    }

    [Test]
    public void GameSearchTargetZ_DefaultsZero()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.GameSearchTargetZ, Is.EqualTo(0f));
    }

    [Test]
    public void GameSearchTarget_PositionRoundTrips()
    {
        var entity = new BotEntity(0);
        entity.GameSearchTargetX = 100.5f;
        entity.GameSearchTargetY = 25.3f;
        entity.GameSearchTargetZ = -50.7f;

        Assert.That(entity.GameSearchTargetX, Is.EqualTo(100.5f));
        Assert.That(entity.GameSearchTargetY, Is.EqualTo(25.3f));
        Assert.That(entity.GameSearchTargetZ, Is.EqualTo(-50.7f));
    }

    // ── GameSearchTargetType ────────────────────────────

    [Test]
    public void GameSearchTargetType_DefaultsZero()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.GameSearchTargetType, Is.EqualTo(0));
    }

    [Test]
    public void GameSearchTargetType_CanStorePlayerPosition()
    {
        var entity = new BotEntity(0);
        entity.GameSearchTargetType = 0; // playerPosition
        Assert.That(entity.GameSearchTargetType, Is.EqualTo(0));
    }

    [Test]
    public void GameSearchTargetType_CanStoreMapPosition()
    {
        var entity = new BotEntity(0);
        entity.GameSearchTargetType = 1; // mapPosition
        Assert.That(entity.GameSearchTargetType, Is.EqualTo(1));
    }

    // ── Combined state scenarios ────────────────────────

    [Test]
    public void GameDataFields_IndependentOfEachOther()
    {
        var entity = new BotEntity(0);
        entity.HasGameAmbushPoint = true;
        entity.HasGameSearchTarget = false;

        Assert.That(entity.HasGameAmbushPoint, Is.True);
        Assert.That(entity.HasGameSearchTarget, Is.False);

        entity.HasGameAmbushPoint = false;
        entity.HasGameSearchTarget = true;

        Assert.That(entity.HasGameAmbushPoint, Is.False);
        Assert.That(entity.HasGameSearchTarget, Is.True);
    }

    [Test]
    public void GameSearchTarget_ClearingHasTarget_DoesNotClearPosition()
    {
        var entity = new BotEntity(0);
        entity.HasGameSearchTarget = true;
        entity.GameSearchTargetX = 42f;
        entity.GameSearchTargetY = 10f;
        entity.GameSearchTargetZ = 99f;

        entity.HasGameSearchTarget = false;

        // Position values persist — callers must check HasGameSearchTarget first
        Assert.That(entity.GameSearchTargetX, Is.EqualTo(42f));
        Assert.That(entity.GameSearchTargetY, Is.EqualTo(10f));
        Assert.That(entity.GameSearchTargetZ, Is.EqualTo(99f));
    }
}
