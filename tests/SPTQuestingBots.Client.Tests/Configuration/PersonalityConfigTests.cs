using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class PersonalityConfigTests
{
    [Test]
    public void DefaultValues_AreCorrect()
    {
        var config = new PersonalityConfig();
        Assert.That(config.Enabled, Is.True);
        Assert.That(config.RaidTimeEnabled, Is.True);
    }

    [Test]
    public void Deserialize_AllFields()
    {
        string json =
            @"{
                ""enabled"": false,
                ""raid_time_enabled"": false
            }";
        var config = JsonConvert.DeserializeObject<PersonalityConfig>(json);
        Assert.That(config.Enabled, Is.False);
        Assert.That(config.RaidTimeEnabled, Is.False);
    }

    [Test]
    public void Serialize_RoundTrip()
    {
        var original = new PersonalityConfig { Enabled = true, RaidTimeEnabled = false };
        string json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<PersonalityConfig>(json);
        Assert.That(deserialized.Enabled, Is.EqualTo(original.Enabled));
        Assert.That(deserialized.RaidTimeEnabled, Is.EqualTo(original.RaidTimeEnabled));
    }

    [Test]
    public void Deserialize_MissingFields_UsesDefaults()
    {
        string json = @"{}";
        var config = JsonConvert.DeserializeObject<PersonalityConfig>(json);
        Assert.That(config.Enabled, Is.True);
        Assert.That(config.RaidTimeEnabled, Is.True);
    }

    [Test]
    public void Deserialize_PartialFields_UsesDefaults()
    {
        string json = @"{ ""enabled"": false }";
        var config = JsonConvert.DeserializeObject<PersonalityConfig>(json);
        Assert.That(config.Enabled, Is.False);
        Assert.That(config.RaidTimeEnabled, Is.True);
    }
}
