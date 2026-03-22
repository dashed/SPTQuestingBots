using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.Helpers;

/// <summary>
/// Tests for weapon state fields on BotEntity (pure C#, no game DLL dependency).
/// The WeaponStateHelper itself wraps BotOwner calls and can't be unit-tested without game DLLs,
/// but the entity fields and their defaults are testable.
/// </summary>
[TestFixture]
public class WeaponStateHelperTests
{
    [Test]
    public void BotEntity_AmmoRatio_DefaultsToFull()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.AmmoRatio, Is.EqualTo(1f));
    }

    [Test]
    public void BotEntity_IsCloseWeapon_DefaultsFalse()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.IsCloseWeapon, Is.False);
    }

    [Test]
    public void BotEntity_HasWeaponMalfunction_DefaultsFalse()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.HasWeaponMalfunction, Is.False);
    }

    [Test]
    public void BotEntity_IsWeaponReady_DefaultsTrue()
    {
        var entity = new BotEntity(0);
        Assert.That(entity.IsWeaponReady, Is.True);
    }

    [Test]
    public void AmmoRatio_CanBeSetToZero()
    {
        var entity = new BotEntity(0);
        entity.AmmoRatio = 0f;
        Assert.That(entity.AmmoRatio, Is.EqualTo(0f));
    }

    [Test]
    public void AmmoRatio_CanBeSetToHalf()
    {
        var entity = new BotEntity(0);
        entity.AmmoRatio = 0.5f;
        Assert.That(entity.AmmoRatio, Is.EqualTo(0.5f));
    }
}
