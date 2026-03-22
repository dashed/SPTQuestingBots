using NUnit.Framework;
using SPTQuestingBots.Helpers;

namespace SPTQuestingBots.Client.Tests.Helpers;

[TestFixture]
public class PlaceInfoHelperTests
{
    [Test]
    public void PlaceInfoData_DefaultIsInvalid()
    {
        var data = new PlaceInfoData();
        Assert.That(data.IsValid, Is.False);
        Assert.That(data.IsInside, Is.False);
        Assert.That(data.IsDark, Is.False);
        Assert.That(data.AreaId, Is.EqualTo(0));
    }

    [Test]
    public void PlaceInfoData_FieldsStoreCorrectly()
    {
        var data = new PlaceInfoData
        {
            IsInside = true,
            IsDark = true,
            AreaId = 42,
            IsValid = true,
        };

        Assert.That(data.IsInside, Is.True);
        Assert.That(data.IsDark, Is.True);
        Assert.That(data.AreaId, Is.EqualTo(42));
        Assert.That(data.IsValid, Is.True);
    }

    [Test]
    public void PlaceInfoData_OutdoorArea()
    {
        var data = new PlaceInfoData
        {
            IsInside = false,
            IsDark = false,
            AreaId = 7,
            IsValid = true,
        };

        Assert.That(data.IsInside, Is.False);
        Assert.That(data.IsDark, Is.False);
        Assert.That(data.IsValid, Is.True);
    }

    [Test]
    public void PlaceInfoData_DarkOutdoor()
    {
        // Some outdoor areas can be dark (e.g. under bridges, dense forest at night)
        var data = new PlaceInfoData
        {
            IsInside = false,
            IsDark = true,
            AreaId = 15,
            IsValid = true,
        };

        Assert.That(data.IsInside, Is.False);
        Assert.That(data.IsDark, Is.True);
    }
}
