using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS.Systems;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS.Systems;

[TestFixture]
public class FormationSelectorTests
{
    [Test]
    public void NarrowPath_ReturnsColumn_WithColumnSpacing()
    {
        var type = FormationSelector.SelectWithSpacing(5f, 8f, out float spacing, 3f, 5f);
        Assert.AreEqual(FormationType.Column, type);
        Assert.AreEqual(3f, spacing);
    }

    [Test]
    public void WidePath_ReturnsSpread_WithSpreadSpacing()
    {
        var type = FormationSelector.SelectWithSpacing(12f, 8f, out float spacing, 3f, 5f);
        Assert.AreEqual(FormationType.Spread, type);
        Assert.AreEqual(5f, spacing);
    }

    [Test]
    public void ExactSwitchWidth_ReturnsSpread()
    {
        // SelectFormation uses < so pathWidth == switchWidth → Spread
        var type = FormationSelector.SelectWithSpacing(8f, 8f, out float spacing, 3f, 5f);
        Assert.AreEqual(FormationType.Spread, type);
        Assert.AreEqual(5f, spacing);
    }

    [Test]
    public void ZeroWidth_ReturnsColumn()
    {
        var type = FormationSelector.SelectWithSpacing(0f, 8f, out float spacing, 2f, 4f);
        Assert.AreEqual(FormationType.Column, type);
        Assert.AreEqual(2f, spacing);
    }

    [Test]
    public void VeryLargeWidth_ReturnsSpread()
    {
        var type = FormationSelector.SelectWithSpacing(100f, 8f, out float spacing, 2f, 4f);
        Assert.AreEqual(FormationType.Spread, type);
        Assert.AreEqual(4f, spacing);
    }

    [Test]
    public void ColumnSpacing_IndependentOfPathWidth()
    {
        // Both narrow, different widths — same column spacing
        FormationSelector.SelectWithSpacing(3f, 10f, out float spacing1, 7f, 12f);
        FormationSelector.SelectWithSpacing(9f, 10f, out float spacing2, 7f, 12f);
        Assert.AreEqual(7f, spacing1);
        Assert.AreEqual(7f, spacing2);
    }

    [Test]
    public void SpreadSpacing_IndependentOfPathWidth()
    {
        // Both wide, different widths — same spread spacing
        FormationSelector.SelectWithSpacing(15f, 10f, out float spacing1, 3f, 6f);
        FormationSelector.SelectWithSpacing(25f, 10f, out float spacing2, 3f, 6f);
        Assert.AreEqual(6f, spacing1);
        Assert.AreEqual(6f, spacing2);
    }

    [Test]
    public void DifferentSwitchWidths_SelectsCorrectly()
    {
        // Same path width = 10, but different switch thresholds
        var type1 = FormationSelector.SelectWithSpacing(10f, 15f, out float spacing1, 2f, 4f);
        Assert.AreEqual(FormationType.Column, type1);
        Assert.AreEqual(2f, spacing1);

        var type2 = FormationSelector.SelectWithSpacing(10f, 5f, out float spacing2, 2f, 4f);
        Assert.AreEqual(FormationType.Spread, type2);
        Assert.AreEqual(4f, spacing2);
    }

    [Test]
    public void JustBelowSwitchWidth_ReturnsColumn()
    {
        // 7.99 < 8 → Column
        var type = FormationSelector.SelectWithSpacing(7.99f, 8f, out float spacing, 3f, 5f);
        Assert.AreEqual(FormationType.Column, type);
        Assert.AreEqual(3f, spacing);
    }

    [Test]
    public void JustAboveSwitchWidth_ReturnsSpread()
    {
        // 8.01 >= 8 → Spread
        var type = FormationSelector.SelectWithSpacing(8.01f, 8f, out float spacing, 3f, 5f);
        Assert.AreEqual(FormationType.Spread, type);
        Assert.AreEqual(5f, spacing);
    }
}
