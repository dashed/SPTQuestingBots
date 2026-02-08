using NUnit.Framework;
using SPTQuestingBots.ZoneMovement.Core;
using SPTQuestingBots.ZoneMovement.Diag;

namespace SPTQuestingBots.Client.Tests.ZoneMovement;

[TestFixture]
public class MinimapProjectionTests
{
    // ---------- WorldToMinimap tests ----------

    [Test]
    public void WorldToMinimap_Origin_MapsToBottomLeft()
    {
        // World min corner should map to bottom-left of display rect (high Y in screen space)
        var (x, y) = MinimapProjection.WorldToMinimap(
            worldX: 0f,
            worldZ: 0f,
            rectX: 100f,
            rectY: 100f,
            rectW: 400f,
            rectH: 400f,
            minX: 0f,
            minZ: 0f,
            maxX: 1000f,
            maxZ: 1000f
        );

        Assert.That(x, Is.EqualTo(100f).Within(0.01f)); // Left edge
        Assert.That(y, Is.EqualTo(500f).Within(0.01f)); // Bottom edge (100 + 400)
    }

    [Test]
    public void WorldToMinimap_MaxCorner_MapsToTopRight()
    {
        // World max corner should map to top-right of display rect (low Y in screen space)
        var (x, y) = MinimapProjection.WorldToMinimap(
            worldX: 1000f,
            worldZ: 1000f,
            rectX: 100f,
            rectY: 100f,
            rectW: 400f,
            rectH: 400f,
            minX: 0f,
            minZ: 0f,
            maxX: 1000f,
            maxZ: 1000f
        );

        Assert.That(x, Is.EqualTo(500f).Within(0.01f)); // Right edge
        Assert.That(y, Is.EqualTo(100f).Within(0.01f)); // Top edge
    }

    [Test]
    public void WorldToMinimap_Center_MapsToCenter()
    {
        var (x, y) = MinimapProjection.WorldToMinimap(
            worldX: 500f,
            worldZ: 500f,
            rectX: 100f,
            rectY: 100f,
            rectW: 400f,
            rectH: 400f,
            minX: 0f,
            minZ: 0f,
            maxX: 1000f,
            maxZ: 1000f
        );

        Assert.That(x, Is.EqualTo(300f).Within(0.01f)); // Center X
        Assert.That(y, Is.EqualTo(300f).Within(0.01f)); // Center Y
    }

    [Test]
    public void WorldToMinimap_ZInversion_HigherZGivesLowerScreenY()
    {
        // Higher world Z should map to lower screen Y (closer to top)
        var (_, yLow) = MinimapProjection.WorldToMinimap(
            worldX: 0f,
            worldZ: 200f,
            rectX: 0f,
            rectY: 0f,
            rectW: 400f,
            rectH: 400f,
            minX: 0f,
            minZ: 0f,
            maxX: 1000f,
            maxZ: 1000f
        );

        var (_, yHigh) = MinimapProjection.WorldToMinimap(
            worldX: 0f,
            worldZ: 800f,
            rectX: 0f,
            rectY: 0f,
            rectW: 400f,
            rectH: 400f,
            minX: 0f,
            minZ: 0f,
            maxX: 1000f,
            maxZ: 1000f
        );

        Assert.That(yHigh, Is.LessThan(yLow), "Higher world Z should have lower screen Y");
    }

    [Test]
    public void WorldToMinimap_NegativeWorldCoords_MapsCorrectly()
    {
        // Map spanning -500 to +500
        var (x, y) = MinimapProjection.WorldToMinimap(
            worldX: 0f,
            worldZ: 0f,
            rectX: 0f,
            rectY: 0f,
            rectW: 400f,
            rectH: 400f,
            minX: -500f,
            minZ: -500f,
            maxX: 500f,
            maxZ: 500f
        );

        Assert.That(x, Is.EqualTo(200f).Within(0.01f)); // Center
        Assert.That(y, Is.EqualTo(200f).Within(0.01f)); // Center
    }

    [Test]
    public void WorldToMinimap_DegenerateRange_ReturnsCenterOfRect()
    {
        // Zero-width world range should map to center
        var (x, y) = MinimapProjection.WorldToMinimap(
            worldX: 100f,
            worldZ: 100f,
            rectX: 50f,
            rectY: 50f,
            rectW: 200f,
            rectH: 200f,
            minX: 100f,
            minZ: 100f,
            maxX: 100f,
            maxZ: 100f
        );

        Assert.That(x, Is.EqualTo(150f).Within(0.01f)); // Center of rect
        Assert.That(y, Is.EqualTo(150f).Within(0.01f));
    }

    // ---------- GetCellColor tests ----------

    [Test]
    public void GetCellColor_NonNavigable_ReturnsBlack()
    {
        var (r, g, b, a) = MinimapProjection.GetCellColor(null, isNavigable: false);

        Assert.That(r, Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(g, Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(b, Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(a, Is.EqualTo(0.8f).Within(0.001f));
    }

    [Test]
    public void GetCellColor_NavigableNoDominant_ReturnsDarkGray()
    {
        var (r, g, b, a) = MinimapProjection.GetCellColor(null, isNavigable: true);

        Assert.That(r, Is.EqualTo(0.2f).Within(0.001f));
        Assert.That(g, Is.EqualTo(0.2f).Within(0.001f));
        Assert.That(b, Is.EqualTo(0.2f).Within(0.001f));
        Assert.That(a, Is.EqualTo(0.5f).Within(0.001f));
    }

    [TestCase(PoiCategory.Container, 0.9f, 0.75f, 0.2f, 0.7f)]
    [TestCase(PoiCategory.LooseLoot, 0.9f, 0.5f, 0.1f, 0.7f)]
    [TestCase(PoiCategory.Quest, 0.2f, 0.8f, 0.3f, 0.7f)]
    [TestCase(PoiCategory.Exfil, 0.8f, 0.2f, 0.2f, 0.7f)]
    [TestCase(PoiCategory.SpawnPoint, 0.3f, 0.4f, 0.8f, 0.7f)]
    [TestCase(PoiCategory.Synthetic, 0.3f, 0.3f, 0.3f, 0.7f)]
    public void GetCellColor_EachCategory_ReturnsCorrectColor(
        PoiCategory category,
        float expectedR,
        float expectedG,
        float expectedB,
        float expectedA
    )
    {
        var (r, g, b, a) = MinimapProjection.GetCellColor(category, isNavigable: true);

        Assert.That(r, Is.EqualTo(expectedR).Within(0.001f));
        Assert.That(g, Is.EqualTo(expectedG).Within(0.001f));
        Assert.That(b, Is.EqualTo(expectedB).Within(0.001f));
        Assert.That(a, Is.EqualTo(expectedA).Within(0.001f));
    }

    [Test]
    public void GetCellColor_NonNavigableWithCategory_StillReturnsBlack()
    {
        // Even if a category is passed, non-navigable should always be black
        var (r, g, b, a) = MinimapProjection.GetCellColor(PoiCategory.Container, isNavigable: false);

        Assert.That(r, Is.EqualTo(0.1f).Within(0.001f));
        Assert.That(a, Is.EqualTo(0.8f).Within(0.001f));
    }
}
