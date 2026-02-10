using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration
{
    [TestFixture]
    public class LingerConfigTests
    {
        [Test]
        public void DefaultConstructor_SetsCorrectDefaults()
        {
            var config = new LingerConfig();

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.BaseScore, Is.EqualTo(0.45f));
                Assert.That(config.DurationMin, Is.EqualTo(10.0f));
                Assert.That(config.DurationMax, Is.EqualTo(30.0f));
                Assert.That(config.HeadScanIntervalMin, Is.EqualTo(3.0f));
                Assert.That(config.HeadScanIntervalMax, Is.EqualTo(8.0f));
                Assert.That(config.Pose, Is.EqualTo(0.7f));
                Assert.That(config.EnableForPmcs, Is.True);
                Assert.That(config.EnableForScavs, Is.True);
                Assert.That(config.EnableForPscavs, Is.True);
            });
        }

        [Test]
        public void Deserialize_EmptyJson_UsesDefaults()
        {
            var config = JsonConvert.DeserializeObject<LingerConfig>("{}");

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.BaseScore, Is.EqualTo(0.45f));
                Assert.That(config.DurationMin, Is.EqualTo(10.0f));
                Assert.That(config.DurationMax, Is.EqualTo(30.0f));
                Assert.That(config.HeadScanIntervalMin, Is.EqualTo(3.0f));
                Assert.That(config.HeadScanIntervalMax, Is.EqualTo(8.0f));
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
                    ""base_score"": 0.6,
                    ""duration_min"": 5,
                    ""duration_max"": 15,
                    ""head_scan_interval_min"": 2,
                    ""head_scan_interval_max"": 6,
                    ""pose"": 0.5,
                    ""enable_for_pmcs"": false,
                    ""enable_for_scavs"": false,
                    ""enable_for_pscavs"": false
                }";
            var config = JsonConvert.DeserializeObject<LingerConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.BaseScore, Is.EqualTo(0.6f).Within(0.01f));
                Assert.That(config.DurationMin, Is.EqualTo(5f));
                Assert.That(config.DurationMax, Is.EqualTo(15f));
                Assert.That(config.HeadScanIntervalMin, Is.EqualTo(2f));
                Assert.That(config.HeadScanIntervalMax, Is.EqualTo(6f));
                Assert.That(config.Pose, Is.EqualTo(0.5f).Within(0.01f));
                Assert.That(config.EnableForPmcs, Is.False);
                Assert.That(config.EnableForScavs, Is.False);
                Assert.That(config.EnableForPscavs, Is.False);
            });
        }

        [Test]
        public void Deserialize_PartialOverride_OtherDefaultsIntact()
        {
            var json = @"{ ""enabled"": false, ""base_score"": 0.3 }";
            var config = JsonConvert.DeserializeObject<LingerConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.BaseScore, Is.EqualTo(0.3f).Within(0.01f));
                Assert.That(config.DurationMin, Is.EqualTo(10.0f));
                Assert.That(config.DurationMax, Is.EqualTo(30.0f));
                Assert.That(config.HeadScanIntervalMin, Is.EqualTo(3.0f));
                Assert.That(config.HeadScanIntervalMax, Is.EqualTo(8.0f));
                Assert.That(config.Pose, Is.EqualTo(0.7f));
                Assert.That(config.EnableForPmcs, Is.True);
                Assert.That(config.EnableForScavs, Is.True);
                Assert.That(config.EnableForPscavs, Is.True);
            });
        }

        [Test]
        public void Deserialize_FloatProperties_CorrectPrecision()
        {
            var json =
                @"{
                    ""base_score"": 0.55,
                    ""duration_min"": 7.5,
                    ""duration_max"": 22.5,
                    ""head_scan_interval_min"": 2.5,
                    ""head_scan_interval_max"": 7.5,
                    ""pose"": 0.65
                }";
            var config = JsonConvert.DeserializeObject<LingerConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.BaseScore, Is.EqualTo(0.55f).Within(0.01f));
                Assert.That(config.DurationMin, Is.EqualTo(7.5f).Within(0.01f));
                Assert.That(config.DurationMax, Is.EqualTo(22.5f).Within(0.01f));
                Assert.That(config.HeadScanIntervalMin, Is.EqualTo(2.5f).Within(0.01f));
                Assert.That(config.HeadScanIntervalMax, Is.EqualTo(7.5f).Within(0.01f));
                Assert.That(config.Pose, Is.EqualTo(0.65f).Within(0.01f));
            });
        }

        [Test]
        public void RoundTrip_PreservesAllValues()
        {
            var original = new LingerConfig
            {
                Enabled = false,
                BaseScore = 0.6f,
                DurationMin = 5f,
                DurationMax = 15f,
                HeadScanIntervalMin = 2f,
                HeadScanIntervalMax = 6f,
                Pose = 0.5f,
                EnableForPmcs = false,
                EnableForScavs = false,
                EnableForPscavs = false,
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<LingerConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Enabled, Is.EqualTo(original.Enabled));
                Assert.That(deserialized.BaseScore, Is.EqualTo(original.BaseScore));
                Assert.That(deserialized.DurationMin, Is.EqualTo(original.DurationMin));
                Assert.That(deserialized.DurationMax, Is.EqualTo(original.DurationMax));
                Assert.That(deserialized.HeadScanIntervalMin, Is.EqualTo(original.HeadScanIntervalMin));
                Assert.That(deserialized.HeadScanIntervalMax, Is.EqualTo(original.HeadScanIntervalMax));
                Assert.That(deserialized.Pose, Is.EqualTo(original.Pose));
                Assert.That(deserialized.EnableForPmcs, Is.EqualTo(original.EnableForPmcs));
                Assert.That(deserialized.EnableForScavs, Is.EqualTo(original.EnableForScavs));
                Assert.That(deserialized.EnableForPscavs, Is.EqualTo(original.EnableForPscavs));
            });
        }

        [Test]
        public void Serialize_IncludesJsonPropertyNames()
        {
            var config = new LingerConfig();
            var json = JsonConvert.SerializeObject(config);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("enabled"));
                Assert.That(json, Does.Contain("base_score"));
                Assert.That(json, Does.Contain("duration_min"));
                Assert.That(json, Does.Contain("duration_max"));
                Assert.That(json, Does.Contain("head_scan_interval_min"));
                Assert.That(json, Does.Contain("head_scan_interval_max"));
                Assert.That(json, Does.Contain("pose"));
                Assert.That(json, Does.Contain("enable_for_pmcs"));
                Assert.That(json, Does.Contain("enable_for_scavs"));
                Assert.That(json, Does.Contain("enable_for_pscavs"));
            });
        }
    }
}
