using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class DebugConfigTests
{
    [Test]
    public void DefaultConstructor_SetsCorrectDefaults()
    {
        var config = new DebugConfig();

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.AlwaysSpawnPMCs, Is.False);
            Assert.That(config.AlwaysSpawnPScavs, Is.False);
            Assert.That(config.ShowZoneOutlines, Is.False);
            Assert.That(config.ShowFailedPaths, Is.False);
            Assert.That(config.ShowDoorInteractionTestPoints, Is.False);
            Assert.That(config.AllowZeroDistanceSleeping, Is.False);
            Assert.That(config.DedicatedLogFile, Is.True);
        });
    }

    [Test]
    public void Deserialize_EmptyJson_UsesDefaults()
    {
        var config = JsonConvert.DeserializeObject<DebugConfig>("{}");

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.AlwaysSpawnPMCs, Is.False);
            Assert.That(config.DedicatedLogFile, Is.True);
        });
    }

    [Test]
    public void Deserialize_OverrideValues()
    {
        var json = """{ "enabled": false, "always_spawn_pmcs": true, "dedicated_log_file": false }""";
        var config = JsonConvert.DeserializeObject<DebugConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.False);
            Assert.That(config.AlwaysSpawnPMCs, Is.True);
            Assert.That(config.DedicatedLogFile, Is.False);
        });
    }

    [Test]
    public void RoundTrip_PreservesAllValues()
    {
        var original = new DebugConfig
        {
            Enabled = false,
            AlwaysSpawnPMCs = true,
            AlwaysSpawnPScavs = true,
            ShowZoneOutlines = true,
            ShowFailedPaths = true,
            ShowDoorInteractionTestPoints = true,
            AllowZeroDistanceSleeping = true,
            DedicatedLogFile = false,
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<DebugConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.Enabled, Is.False);
            Assert.That(deserialized.AlwaysSpawnPMCs, Is.True);
            Assert.That(deserialized.AlwaysSpawnPScavs, Is.True);
            Assert.That(deserialized.ShowZoneOutlines, Is.True);
            Assert.That(deserialized.ShowFailedPaths, Is.True);
            Assert.That(deserialized.ShowDoorInteractionTestPoints, Is.True);
            Assert.That(deserialized.AllowZeroDistanceSleeping, Is.True);
            Assert.That(deserialized.DedicatedLogFile, Is.False);
        });
    }

    [Test]
    public void Serialize_IncludesJsonPropertyNames()
    {
        var config = new DebugConfig();
        var json = JsonConvert.SerializeObject(config);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("enabled"));
            Assert.That(json, Does.Contain("always_spawn_pmcs"));
            Assert.That(json, Does.Contain("always_spawn_pscavs"));
            Assert.That(json, Does.Contain("show_zone_outlines"));
            Assert.That(json, Does.Contain("show_failed_paths"));
            Assert.That(json, Does.Contain("show_door_interaction_test_points"));
            Assert.That(json, Does.Contain("allow_zero_distance_sleeping"));
            Assert.That(json, Does.Contain("dedicated_log_file"));
        });
    }

    [Test]
    public void Deserialize_PartialOverride_OtherDefaultsIntact()
    {
        var json = """{ "dedicated_log_file": false }""";
        var config = JsonConvert.DeserializeObject<DebugConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.DedicatedLogFile, Is.False);
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.AlwaysSpawnPMCs, Is.False);
            Assert.That(config.ShowZoneOutlines, Is.False);
        });
    }

    [Test]
    public void DedicatedLogFile_DefaultTrue_MatchesConfigJson()
    {
        // The dedicated_log_file config option defaults to true so that
        // users get the dedicated log file out of the box
        var config = new DebugConfig();
        Assert.That(config.DedicatedLogFile, Is.True);
    }
}
