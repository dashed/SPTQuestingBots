using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class RoomClearConfigTests
{
    [Test]
    public void DefaultConstructor_SetsCorrectDefaults()
    {
        var config = new RoomClearConfig();

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.DurationMin, Is.EqualTo(15.0f));
            Assert.That(config.DurationMax, Is.EqualTo(30.0f));
            Assert.That(config.CornerPauseDuration, Is.EqualTo(1.5f));
            Assert.That(config.CornerAngleThreshold, Is.EqualTo(60.0f));
            Assert.That(config.Pose, Is.EqualTo(0.7f));
            Assert.That(config.EnableForPmcs, Is.True);
            Assert.That(config.EnableForScavs, Is.True);
            Assert.That(config.EnableForPscavs, Is.True);
        });
    }

    [Test]
    public void Deserialize_EmptyJson_UsesDefaults()
    {
        var config = JsonConvert.DeserializeObject<RoomClearConfig>("{}");

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.DurationMin, Is.EqualTo(15.0f));
            Assert.That(config.DurationMax, Is.EqualTo(30.0f));
            Assert.That(config.CornerPauseDuration, Is.EqualTo(1.5f));
            Assert.That(config.CornerAngleThreshold, Is.EqualTo(60.0f));
            Assert.That(config.Pose, Is.EqualTo(0.7f));
            Assert.That(config.EnableForPmcs, Is.True);
            Assert.That(config.EnableForScavs, Is.True);
            Assert.That(config.EnableForPscavs, Is.True);
        });
    }

    [Test]
    public void Deserialize_AllProperties_FromJson()
    {
        var json =
            @"{
                    ""enabled"": false,
                    ""duration_min"": 10,
                    ""duration_max"": 20,
                    ""corner_pause_duration"": 2.0,
                    ""corner_angle_threshold"": 45.0,
                    ""pose"": 0.5,
                    ""enable_for_pmcs"": false,
                    ""enable_for_scavs"": false,
                    ""enable_for_pscavs"": false
                }";
        var config = JsonConvert.DeserializeObject<RoomClearConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.False);
            Assert.That(config.DurationMin, Is.EqualTo(10f));
            Assert.That(config.DurationMax, Is.EqualTo(20f));
            Assert.That(config.CornerPauseDuration, Is.EqualTo(2.0f).Within(0.01f));
            Assert.That(config.CornerAngleThreshold, Is.EqualTo(45.0f).Within(0.01f));
            Assert.That(config.Pose, Is.EqualTo(0.5f).Within(0.01f));
            Assert.That(config.EnableForPmcs, Is.False);
            Assert.That(config.EnableForScavs, Is.False);
            Assert.That(config.EnableForPscavs, Is.False);
        });
    }

    [Test]
    public void Deserialize_PartialOverride_OtherDefaultsIntact()
    {
        var json = @"{ ""enabled"": false, ""duration_min"": 5 }";
        var config = JsonConvert.DeserializeObject<RoomClearConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.False);
            Assert.That(config.DurationMin, Is.EqualTo(5f));
            Assert.That(config.DurationMax, Is.EqualTo(30.0f));
            Assert.That(config.CornerPauseDuration, Is.EqualTo(1.5f));
            Assert.That(config.CornerAngleThreshold, Is.EqualTo(60.0f));
            Assert.That(config.Pose, Is.EqualTo(0.7f));
            Assert.That(config.EnableForPmcs, Is.True);
            Assert.That(config.EnableForScavs, Is.True);
            Assert.That(config.EnableForPscavs, Is.True);
        });
    }

    [Test]
    public void RoundTrip_PreservesAllValues()
    {
        var original = new RoomClearConfig
        {
            Enabled = false,
            DurationMin = 8f,
            DurationMax = 20f,
            CornerPauseDuration = 2.5f,
            CornerAngleThreshold = 45f,
            Pose = 0.5f,
            EnableForPmcs = false,
            EnableForScavs = false,
            EnableForPscavs = false,
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<RoomClearConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.Enabled, Is.EqualTo(original.Enabled));
            Assert.That(deserialized.DurationMin, Is.EqualTo(original.DurationMin));
            Assert.That(deserialized.DurationMax, Is.EqualTo(original.DurationMax));
            Assert.That(deserialized.CornerPauseDuration, Is.EqualTo(original.CornerPauseDuration));
            Assert.That(deserialized.CornerAngleThreshold, Is.EqualTo(original.CornerAngleThreshold));
            Assert.That(deserialized.Pose, Is.EqualTo(original.Pose));
            Assert.That(deserialized.EnableForPmcs, Is.EqualTo(original.EnableForPmcs));
            Assert.That(deserialized.EnableForScavs, Is.EqualTo(original.EnableForScavs));
            Assert.That(deserialized.EnableForPscavs, Is.EqualTo(original.EnableForPscavs));
        });
    }

    [Test]
    public void Serialize_IncludesJsonPropertyNames()
    {
        var config = new RoomClearConfig();
        var json = JsonConvert.SerializeObject(config);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("enabled"));
            Assert.That(json, Does.Contain("duration_min"));
            Assert.That(json, Does.Contain("duration_max"));
            Assert.That(json, Does.Contain("corner_pause_duration"));
            Assert.That(json, Does.Contain("corner_angle_threshold"));
            Assert.That(json, Does.Contain("pose"));
            Assert.That(json, Does.Contain("enable_for_pmcs"));
            Assert.That(json, Does.Contain("enable_for_scavs"));
            Assert.That(json, Does.Contain("enable_for_pscavs"));
        });
    }
}
