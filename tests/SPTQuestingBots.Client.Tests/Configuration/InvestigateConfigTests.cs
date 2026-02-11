using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class InvestigateConfigTests
{
    [Test]
    public void DefaultValues_AreReasonable()
    {
        var config = new InvestigateConfig();

        Assert.That(config.Enabled, Is.True);
        Assert.That(config.BaseScore, Is.EqualTo(0.40f));
        Assert.That(config.IntensityThreshold, Is.EqualTo(5));
        Assert.That(config.DetectionRange, Is.EqualTo(120.0f));
        Assert.That(config.MovementTimeout, Is.EqualTo(45.0f));
        Assert.That(config.ApproachSpeed, Is.EqualTo(0.5f));
        Assert.That(config.ApproachPose, Is.EqualTo(0.6f));
        Assert.That(config.ArrivalDistance, Is.EqualTo(15.0f));
        Assert.That(config.LookAroundDuration, Is.EqualTo(8.0f));
        Assert.That(config.HeadScanIntervalMin, Is.EqualTo(2.0f));
        Assert.That(config.HeadScanIntervalMax, Is.EqualTo(5.0f));
        Assert.That(config.EnableForPmcs, Is.True);
        Assert.That(config.EnableForScavs, Is.True);
        Assert.That(config.EnableForPscavs, Is.False);
    }

    [Test]
    public void Deserialize_FromJson_OverridesDefaults()
    {
        var json =
            @"{
                ""enabled"": false,
                ""base_score"": 0.30,
                ""intensity_threshold"": 3,
                ""detection_range"": 80.0,
                ""movement_timeout"": 30.0,
                ""approach_speed"": 0.3,
                ""approach_pose"": 0.4,
                ""arrival_distance"": 10.0,
                ""look_around_duration"": 5.0,
                ""head_scan_interval_min"": 1.0,
                ""head_scan_interval_max"": 3.0,
                ""enable_for_pmcs"": false,
                ""enable_for_scavs"": false,
                ""enable_for_pscavs"": true
            }";

        var config = JsonConvert.DeserializeObject<InvestigateConfig>(json);

        Assert.That(config.Enabled, Is.False);
        Assert.That(config.BaseScore, Is.EqualTo(0.30f));
        Assert.That(config.IntensityThreshold, Is.EqualTo(3));
        Assert.That(config.DetectionRange, Is.EqualTo(80.0f));
        Assert.That(config.MovementTimeout, Is.EqualTo(30.0f));
        Assert.That(config.ApproachSpeed, Is.EqualTo(0.3f));
        Assert.That(config.ApproachPose, Is.EqualTo(0.4f));
        Assert.That(config.ArrivalDistance, Is.EqualTo(10.0f));
        Assert.That(config.LookAroundDuration, Is.EqualTo(5.0f));
        Assert.That(config.HeadScanIntervalMin, Is.EqualTo(1.0f));
        Assert.That(config.HeadScanIntervalMax, Is.EqualTo(3.0f));
        Assert.That(config.EnableForPmcs, Is.False);
        Assert.That(config.EnableForScavs, Is.False);
        Assert.That(config.EnableForPscavs, Is.True);
    }

    [Test]
    public void Serialize_RoundTrips()
    {
        var original = new InvestigateConfig { IntensityThreshold = 7, DetectionRange = 100f };
        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<InvestigateConfig>(json);

        Assert.That(deserialized.IntensityThreshold, Is.EqualTo(7));
        Assert.That(deserialized.DetectionRange, Is.EqualTo(100f));
    }
}
