using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

[TestFixture]
public class BotEntityZoneModifierTests
{
    [Test]
    public void NewEntity_ZoneModifier_DefaultsToNotSet()
    {
        var entity = new BotEntity(1);

        Assert.That(entity.HasZoneModifier, Is.False);
        Assert.That(entity.ZoneVisibleDistance, Is.EqualTo(0f));
        Assert.That(entity.ZoneDistToSleep, Is.EqualTo(0f));
        Assert.That(entity.ZoneAccuracySpeed, Is.EqualTo(0f));
        Assert.That(entity.ZoneGainSight, Is.EqualTo(0f));
    }

    [Test]
    public void ZoneModifier_CanBeSet()
    {
        var entity = new BotEntity(1);

        entity.ZoneVisibleDistance = 150f;
        entity.ZoneDistToSleep = 75f;
        entity.ZoneAccuracySpeed = 0.85f;
        entity.ZoneGainSight = 0.9f;
        entity.HasZoneModifier = true;

        Assert.That(entity.HasZoneModifier, Is.True);
        Assert.That(entity.ZoneVisibleDistance, Is.EqualTo(150f));
        Assert.That(entity.ZoneDistToSleep, Is.EqualTo(75f));
        Assert.That(entity.ZoneAccuracySpeed, Is.EqualTo(0.85f));
        Assert.That(entity.ZoneGainSight, Is.EqualTo(0.9f));
    }

    [Test]
    public void ZoneModifier_CanBeUpdatedForZoneChange()
    {
        var entity = new BotEntity(1);

        // First zone
        entity.ZoneVisibleDistance = 100f;
        entity.HasZoneModifier = true;

        // Zone change — clear and re-set
        entity.HasZoneModifier = false;
        entity.ZoneVisibleDistance = 200f;
        entity.HasZoneModifier = true;

        Assert.That(entity.ZoneVisibleDistance, Is.EqualTo(200f));
    }

    [Test]
    public void ZoneModifier_IndependentFromOtherEntityState()
    {
        var entity = new BotEntity(1);
        entity.IsInCombat = true;
        entity.ZoneVisibleDistance = 120f;
        entity.HasZoneModifier = true;

        Assert.That(entity.IsInCombat, Is.True);
        Assert.That(entity.ZoneVisibleDistance, Is.EqualTo(120f));
        Assert.That(entity.HasZoneModifier, Is.True);
    }
}
