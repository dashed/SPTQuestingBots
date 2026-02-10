using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration
{
    [TestFixture]
    public class DynamicObjectiveConfigTests
    {
        [Test]
        public void DefaultConstructor_SetsCorrectDefaults()
        {
            var config = new DynamicObjectiveConfig();

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.ScanIntervalSec, Is.EqualTo(30f));
                Assert.That(config.MaxActiveQuests, Is.EqualTo(10));
                Assert.That(config.FirefightEnabled, Is.True);
                Assert.That(config.FirefightMinIntensity, Is.EqualTo(20));
                Assert.That(config.FirefightDesirability, Is.EqualTo(8f));
                Assert.That(config.FirefightMaxAgeSec, Is.EqualTo(120f));
                Assert.That(config.FirefightClusterRadius, Is.EqualTo(50f));
                Assert.That(config.CorpseEnabled, Is.True);
                Assert.That(config.CorpseDesirability, Is.EqualTo(6f));
                Assert.That(config.CorpseMaxAgeSec, Is.EqualTo(180f));
                Assert.That(config.BuildingClearEnabled, Is.True);
                Assert.That(config.BuildingClearDesirability, Is.EqualTo(4f));
                Assert.That(config.BuildingClearHoldMin, Is.EqualTo(15f));
                Assert.That(config.BuildingClearHoldMax, Is.EqualTo(30f));
            });
        }

        [Test]
        public void Deserialize_EmptyJson_UsesDefaults()
        {
            var config = JsonConvert.DeserializeObject<DynamicObjectiveConfig>("{}");

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.ScanIntervalSec, Is.EqualTo(30f));
                Assert.That(config.MaxActiveQuests, Is.EqualTo(10));
                Assert.That(config.FirefightEnabled, Is.True);
                Assert.That(config.FirefightMinIntensity, Is.EqualTo(20));
                Assert.That(config.CorpseEnabled, Is.True);
                Assert.That(config.BuildingClearEnabled, Is.True);
            });
        }

        [Test]
        public void Deserialize_AllProperties_FromJson()
        {
            var json =
                @"{
                    ""enabled"": false,
                    ""scan_interval_sec"": 60,
                    ""max_active_quests"": 5,
                    ""firefight_enabled"": false,
                    ""firefight_min_intensity"": 30,
                    ""firefight_desirability"": 10,
                    ""firefight_max_age_sec"": 90,
                    ""firefight_cluster_radius"": 75,
                    ""corpse_enabled"": false,
                    ""corpse_desirability"": 3,
                    ""corpse_max_age_sec"": 240,
                    ""building_clear_enabled"": false,
                    ""building_clear_desirability"": 2,
                    ""building_clear_hold_min"": 20,
                    ""building_clear_hold_max"": 45
                }";
            var config = JsonConvert.DeserializeObject<DynamicObjectiveConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.ScanIntervalSec, Is.EqualTo(60f));
                Assert.That(config.MaxActiveQuests, Is.EqualTo(5));
                Assert.That(config.FirefightEnabled, Is.False);
                Assert.That(config.FirefightMinIntensity, Is.EqualTo(30));
                Assert.That(config.FirefightDesirability, Is.EqualTo(10f));
                Assert.That(config.FirefightMaxAgeSec, Is.EqualTo(90f));
                Assert.That(config.FirefightClusterRadius, Is.EqualTo(75f));
                Assert.That(config.CorpseEnabled, Is.False);
                Assert.That(config.CorpseDesirability, Is.EqualTo(3f));
                Assert.That(config.CorpseMaxAgeSec, Is.EqualTo(240f));
                Assert.That(config.BuildingClearEnabled, Is.False);
                Assert.That(config.BuildingClearDesirability, Is.EqualTo(2f));
                Assert.That(config.BuildingClearHoldMin, Is.EqualTo(20f));
                Assert.That(config.BuildingClearHoldMax, Is.EqualTo(45f));
            });
        }

        [Test]
        public void Deserialize_PartialOverride_OtherDefaultsIntact()
        {
            var json = @"{ ""enabled"": false, ""firefight_desirability"": 12, ""corpse_max_age_sec"": 300 }";
            var config = JsonConvert.DeserializeObject<DynamicObjectiveConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.FirefightDesirability, Is.EqualTo(12f));
                Assert.That(config.CorpseMaxAgeSec, Is.EqualTo(300f));
                Assert.That(config.ScanIntervalSec, Is.EqualTo(30f));
                Assert.That(config.MaxActiveQuests, Is.EqualTo(10));
                Assert.That(config.FirefightEnabled, Is.True);
                Assert.That(config.CorpseEnabled, Is.True);
                Assert.That(config.BuildingClearEnabled, Is.True);
            });
        }

        [Test]
        public void RoundTrip_PreservesAllValues()
        {
            var original = new DynamicObjectiveConfig
            {
                Enabled = false,
                ScanIntervalSec = 45f,
                MaxActiveQuests = 15,
                FirefightEnabled = false,
                FirefightMinIntensity = 25,
                FirefightDesirability = 12f,
                FirefightMaxAgeSec = 150f,
                FirefightClusterRadius = 80f,
                CorpseEnabled = false,
                CorpseDesirability = 8f,
                CorpseMaxAgeSec = 300f,
                BuildingClearEnabled = false,
                BuildingClearDesirability = 5f,
                BuildingClearHoldMin = 20f,
                BuildingClearHoldMax = 45f,
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<DynamicObjectiveConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Enabled, Is.EqualTo(original.Enabled));
                Assert.That(deserialized.ScanIntervalSec, Is.EqualTo(original.ScanIntervalSec));
                Assert.That(deserialized.MaxActiveQuests, Is.EqualTo(original.MaxActiveQuests));
                Assert.That(deserialized.FirefightEnabled, Is.EqualTo(original.FirefightEnabled));
                Assert.That(deserialized.FirefightMinIntensity, Is.EqualTo(original.FirefightMinIntensity));
                Assert.That(deserialized.FirefightDesirability, Is.EqualTo(original.FirefightDesirability));
                Assert.That(deserialized.FirefightMaxAgeSec, Is.EqualTo(original.FirefightMaxAgeSec));
                Assert.That(deserialized.FirefightClusterRadius, Is.EqualTo(original.FirefightClusterRadius));
                Assert.That(deserialized.CorpseEnabled, Is.EqualTo(original.CorpseEnabled));
                Assert.That(deserialized.CorpseDesirability, Is.EqualTo(original.CorpseDesirability));
                Assert.That(deserialized.CorpseMaxAgeSec, Is.EqualTo(original.CorpseMaxAgeSec));
                Assert.That(deserialized.BuildingClearEnabled, Is.EqualTo(original.BuildingClearEnabled));
                Assert.That(deserialized.BuildingClearDesirability, Is.EqualTo(original.BuildingClearDesirability));
                Assert.That(deserialized.BuildingClearHoldMin, Is.EqualTo(original.BuildingClearHoldMin));
                Assert.That(deserialized.BuildingClearHoldMax, Is.EqualTo(original.BuildingClearHoldMax));
            });
        }

        [Test]
        public void Serialize_IncludesJsonPropertyNames()
        {
            var config = new DynamicObjectiveConfig();
            var json = JsonConvert.SerializeObject(config);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("enabled"));
                Assert.That(json, Does.Contain("scan_interval_sec"));
                Assert.That(json, Does.Contain("max_active_quests"));
                Assert.That(json, Does.Contain("firefight_enabled"));
                Assert.That(json, Does.Contain("firefight_min_intensity"));
                Assert.That(json, Does.Contain("firefight_desirability"));
                Assert.That(json, Does.Contain("firefight_max_age_sec"));
                Assert.That(json, Does.Contain("firefight_cluster_radius"));
                Assert.That(json, Does.Contain("corpse_enabled"));
                Assert.That(json, Does.Contain("corpse_desirability"));
                Assert.That(json, Does.Contain("corpse_max_age_sec"));
                Assert.That(json, Does.Contain("building_clear_enabled"));
                Assert.That(json, Does.Contain("building_clear_desirability"));
                Assert.That(json, Does.Contain("building_clear_hold_min"));
                Assert.That(json, Does.Contain("building_clear_hold_max"));
            });
        }

        [Test]
        public void Deserialize_FloatProperties_CorrectPrecision()
        {
            var json =
                @"{
                    ""scan_interval_sec"": 45.5,
                    ""firefight_desirability"": 7.5,
                    ""firefight_max_age_sec"": 90.5,
                    ""firefight_cluster_radius"": 65.3,
                    ""corpse_desirability"": 5.5,
                    ""corpse_max_age_sec"": 200.5,
                    ""building_clear_desirability"": 3.5,
                    ""building_clear_hold_min"": 12.5,
                    ""building_clear_hold_max"": 35.7
                }";
            var config = JsonConvert.DeserializeObject<DynamicObjectiveConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.ScanIntervalSec, Is.EqualTo(45.5f).Within(0.01f));
                Assert.That(config.FirefightDesirability, Is.EqualTo(7.5f).Within(0.01f));
                Assert.That(config.FirefightMaxAgeSec, Is.EqualTo(90.5f).Within(0.01f));
                Assert.That(config.FirefightClusterRadius, Is.EqualTo(65.3f).Within(0.01f));
                Assert.That(config.CorpseDesirability, Is.EqualTo(5.5f).Within(0.01f));
                Assert.That(config.CorpseMaxAgeSec, Is.EqualTo(200.5f).Within(0.01f));
                Assert.That(config.BuildingClearDesirability, Is.EqualTo(3.5f).Within(0.01f));
                Assert.That(config.BuildingClearHoldMin, Is.EqualTo(12.5f).Within(0.01f));
                Assert.That(config.BuildingClearHoldMax, Is.EqualTo(35.7f).Within(0.01f));
            });
        }
    }
}
