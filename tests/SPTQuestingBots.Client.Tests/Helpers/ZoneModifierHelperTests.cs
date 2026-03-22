using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class ZoneModifierHelperTests
{
    [Test]
    public void ZoneModifierData_DefaultIsInvalid()
    {
        var data = new ZoneModifierData();
        Assert.That(data.IsValid, Is.False);
    }

    [Test]
    public void ZoneModifierData_FieldsStoreCorrectly()
    {
        var data = new ZoneModifierData
        {
            VisibleDistance = 150f,
            DistToSleep = 75f,
            DistToActivate = 50f,
            AccuracySpeed = 0.85f,
            GainSight = 0.9f,
            Scattering = 1.2f,
            FogVisibilityDistanceCoef = 0.7f,
            RainVisibilityDistanceCoef = 0.8f,
            IsValid = true,
        };

        Assert.That(data.VisibleDistance, Is.EqualTo(150f));
        Assert.That(data.DistToSleep, Is.EqualTo(75f));
        Assert.That(data.DistToActivate, Is.EqualTo(50f));
        Assert.That(data.AccuracySpeed, Is.EqualTo(0.85f));
        Assert.That(data.GainSight, Is.EqualTo(0.9f));
        Assert.That(data.Scattering, Is.EqualTo(1.2f));
        Assert.That(data.FogVisibilityDistanceCoef, Is.EqualTo(0.7f));
        Assert.That(data.RainVisibilityDistanceCoef, Is.EqualTo(0.8f));
        Assert.That(data.IsValid, Is.True);
    }

    [Test]
    public void ZoneModifierData_WeatherCoefs_AreNormalized()
    {
        // Weather coefficients should typically be 0-1
        var data = new ZoneModifierData { FogVisibilityDistanceCoef = 0.5f, RainVisibilityDistanceCoef = 0.3f };

        Assert.That(data.FogVisibilityDistanceCoef, Is.InRange(0f, 1f));
        Assert.That(data.RainVisibilityDistanceCoef, Is.InRange(0f, 1f));
    }
}
