using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using UnityEngine;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class PointOfInterestTests
{
    [Test]
    public void Constructor_WithExplicitWeight_UsesProvidedWeight()
    {
        var poi = new PointOfInterest(new Vector3(1, 0, 2), PoiCategory.Container, 5.0f);

        Assert.That(poi.Position.x, Is.EqualTo(1f));
        Assert.That(poi.Position.z, Is.EqualTo(2f));
        Assert.That(poi.Category, Is.EqualTo(PoiCategory.Container));
        Assert.That(poi.Weight, Is.EqualTo(5.0f));
    }

    [Test]
    public void Constructor_WithoutWeight_UsesDefaultForCategory()
    {
        var poi = new PointOfInterest(new Vector3(0, 0, 0), PoiCategory.Quest);
        Assert.That(poi.Weight, Is.EqualTo(1.2f));
    }

    [TestCase(PoiCategory.Container, 1.0f)]
    [TestCase(PoiCategory.LooseLoot, 0.8f)]
    [TestCase(PoiCategory.Quest, 1.2f)]
    [TestCase(PoiCategory.Exfil, 0.5f)]
    [TestCase(PoiCategory.SpawnPoint, 0.3f)]
    [TestCase(PoiCategory.Synthetic, 0.2f)]
    public void DefaultWeight_ReturnsExpectedValue(PoiCategory category, float expected)
    {
        Assert.That(PointOfInterest.DefaultWeight(category), Is.EqualTo(expected));
    }
}
