using NUnit.Framework;
using SPTQuestingBots.BotLogic.ECS;

namespace SPTQuestingBots.Client.Tests.BotLogic.ECS;

[TestFixture]
public class SquadPersonalitySettingsTests
{
    [Test]
    public void ForType_Elite_ReturnsCorrectSettings()
    {
        var settings = SquadPersonalitySettings.ForType(SquadPersonalityType.Elite);

        Assert.AreEqual(5f, settings.CoordinationLevel);
        Assert.AreEqual(4f, settings.AggressionLevel);
    }

    [Test]
    public void ForType_GigaChads_ReturnsCorrectSettings()
    {
        var settings = SquadPersonalitySettings.ForType(SquadPersonalityType.GigaChads);

        Assert.AreEqual(4f, settings.CoordinationLevel);
        Assert.AreEqual(5f, settings.AggressionLevel);
    }

    [Test]
    public void ForType_Rats_ReturnsCorrectSettings()
    {
        var settings = SquadPersonalitySettings.ForType(SquadPersonalityType.Rats);

        Assert.AreEqual(2f, settings.CoordinationLevel);
        Assert.AreEqual(1f, settings.AggressionLevel);
    }

    [Test]
    public void ForType_TimmyTeam6_ReturnsCorrectSettings()
    {
        var settings = SquadPersonalitySettings.ForType(SquadPersonalityType.TimmyTeam6);

        Assert.AreEqual(1f, settings.CoordinationLevel);
        Assert.AreEqual(2f, settings.AggressionLevel);
    }

    [Test]
    public void ForType_None_ReturnsDefaults()
    {
        var settings = SquadPersonalitySettings.ForType(SquadPersonalityType.None);

        Assert.AreEqual(3f, settings.CoordinationLevel);
        Assert.AreEqual(3f, settings.AggressionLevel);
    }

    [Test]
    public void GetSharingChance_Elite_Returns100()
    {
        var settings = SquadPersonalitySettings.ForType(SquadPersonalityType.Elite);
        Assert.AreEqual(100f, settings.GetSharingChance());
    }

    [Test]
    public void GetSharingChance_GigaChads_Returns85()
    {
        var settings = SquadPersonalitySettings.ForType(SquadPersonalityType.GigaChads);
        Assert.AreEqual(85f, settings.GetSharingChance());
    }

    [Test]
    public void GetSharingChance_Rats_Returns55()
    {
        var settings = SquadPersonalitySettings.ForType(SquadPersonalityType.Rats);
        Assert.AreEqual(55f, settings.GetSharingChance());
    }

    [Test]
    public void GetSharingChance_TimmyTeam6_Returns40()
    {
        var settings = SquadPersonalitySettings.ForType(SquadPersonalityType.TimmyTeam6);
        Assert.AreEqual(40f, settings.GetSharingChance());
    }

    [Test]
    public void Constructor_PreservesValues()
    {
        var settings = new SquadPersonalitySettings(3.5f, 2.7f);

        Assert.AreEqual(3.5f, settings.CoordinationLevel);
        Assert.AreEqual(2.7f, settings.AggressionLevel);
    }
}
