using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration
{
    [TestFixture]
    public class LookVarianceConfigTests
    {
        [Test]
        public void DefaultConstructor_SetsCorrectDefaults()
        {
            var config = new LookVarianceConfig();

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.FlankCheckIntervalMin, Is.EqualTo(5.0f));
                Assert.That(config.FlankCheckIntervalMax, Is.EqualTo(15.0f));
                Assert.That(config.PoiGlanceIntervalMin, Is.EqualTo(8.0f));
                Assert.That(config.PoiGlanceIntervalMax, Is.EqualTo(20.0f));
                Assert.That(config.PoiDetectionRange, Is.EqualTo(20.0f));
                Assert.That(config.SquadGlanceRange, Is.EqualTo(15.0f));
                Assert.That(config.CombatEventLookChance, Is.EqualTo(0.7f));
            });
        }

        [Test]
        public void Deserialize_EmptyJson_UsesDefaults()
        {
            var config = JsonConvert.DeserializeObject<LookVarianceConfig>("{}");

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.FlankCheckIntervalMin, Is.EqualTo(5.0f));
                Assert.That(config.FlankCheckIntervalMax, Is.EqualTo(15.0f));
                Assert.That(config.PoiGlanceIntervalMin, Is.EqualTo(8.0f));
                Assert.That(config.PoiGlanceIntervalMax, Is.EqualTo(20.0f));
                Assert.That(config.PoiDetectionRange, Is.EqualTo(20.0f));
                Assert.That(config.SquadGlanceRange, Is.EqualTo(15.0f));
                Assert.That(config.CombatEventLookChance, Is.EqualTo(0.7f));
            });
        }

        [Test]
        public void Deserialize_AllProperties_FromJson()
        {
            var json =
                @"{
                    ""enabled"": false,
                    ""flank_check_interval_min"": 3.0,
                    ""flank_check_interval_max"": 10.0,
                    ""poi_glance_interval_min"": 5.0,
                    ""poi_glance_interval_max"": 12.0,
                    ""poi_detection_range"": 30.0,
                    ""squad_glance_range"": 20.0,
                    ""combat_event_look_chance"": 0.5
                }";
            var config = JsonConvert.DeserializeObject<LookVarianceConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.FlankCheckIntervalMin, Is.EqualTo(3.0f));
                Assert.That(config.FlankCheckIntervalMax, Is.EqualTo(10.0f));
                Assert.That(config.PoiGlanceIntervalMin, Is.EqualTo(5.0f));
                Assert.That(config.PoiGlanceIntervalMax, Is.EqualTo(12.0f));
                Assert.That(config.PoiDetectionRange, Is.EqualTo(30.0f));
                Assert.That(config.SquadGlanceRange, Is.EqualTo(20.0f));
                Assert.That(config.CombatEventLookChance, Is.EqualTo(0.5f).Within(0.01f));
            });
        }

        [Test]
        public void Deserialize_PartialOverride_OtherDefaultsIntact()
        {
            var json = @"{ ""enabled"": false, ""flank_check_interval_min"": 2.0 }";
            var config = JsonConvert.DeserializeObject<LookVarianceConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.FlankCheckIntervalMin, Is.EqualTo(2.0f));
                Assert.That(config.FlankCheckIntervalMax, Is.EqualTo(15.0f));
                Assert.That(config.PoiGlanceIntervalMin, Is.EqualTo(8.0f));
                Assert.That(config.PoiGlanceIntervalMax, Is.EqualTo(20.0f));
                Assert.That(config.PoiDetectionRange, Is.EqualTo(20.0f));
                Assert.That(config.SquadGlanceRange, Is.EqualTo(15.0f));
                Assert.That(config.CombatEventLookChance, Is.EqualTo(0.7f));
            });
        }

        [Test]
        public void RoundTrip_PreservesAllValues()
        {
            var original = new LookVarianceConfig
            {
                Enabled = false,
                FlankCheckIntervalMin = 3f,
                FlankCheckIntervalMax = 10f,
                PoiGlanceIntervalMin = 5f,
                PoiGlanceIntervalMax = 12f,
                PoiDetectionRange = 30f,
                SquadGlanceRange = 20f,
                CombatEventLookChance = 0.5f,
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<LookVarianceConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Enabled, Is.EqualTo(original.Enabled));
                Assert.That(deserialized.FlankCheckIntervalMin, Is.EqualTo(original.FlankCheckIntervalMin));
                Assert.That(deserialized.FlankCheckIntervalMax, Is.EqualTo(original.FlankCheckIntervalMax));
                Assert.That(deserialized.PoiGlanceIntervalMin, Is.EqualTo(original.PoiGlanceIntervalMin));
                Assert.That(deserialized.PoiGlanceIntervalMax, Is.EqualTo(original.PoiGlanceIntervalMax));
                Assert.That(deserialized.PoiDetectionRange, Is.EqualTo(original.PoiDetectionRange));
                Assert.That(deserialized.SquadGlanceRange, Is.EqualTo(original.SquadGlanceRange));
                Assert.That(deserialized.CombatEventLookChance, Is.EqualTo(original.CombatEventLookChance));
            });
        }

        [Test]
        public void Serialize_IncludesJsonPropertyNames()
        {
            var config = new LookVarianceConfig();
            var json = JsonConvert.SerializeObject(config);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("enabled"));
                Assert.That(json, Does.Contain("flank_check_interval_min"));
                Assert.That(json, Does.Contain("flank_check_interval_max"));
                Assert.That(json, Does.Contain("poi_glance_interval_min"));
                Assert.That(json, Does.Contain("poi_glance_interval_max"));
                Assert.That(json, Does.Contain("poi_detection_range"));
                Assert.That(json, Does.Contain("squad_glance_range"));
                Assert.That(json, Does.Contain("combat_event_look_chance"));
            });
        }
    }
}
