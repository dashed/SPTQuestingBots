using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration
{
    [TestFixture]
    public class SpawnEntryConfigTests
    {
        [Test]
        public void DefaultConstructor_SetsCorrectDefaults()
        {
            var config = new SpawnEntryConfig();

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.BaseDurationMin, Is.EqualTo(3.0f));
                Assert.That(config.BaseDurationMax, Is.EqualTo(5.0f));
                Assert.That(config.SquadStaggerPerMember, Is.EqualTo(1.5f));
                Assert.That(config.DirectionBiasDuration, Is.EqualTo(30.0f));
                Assert.That(config.DirectionBiasStrength, Is.EqualTo(0.05f));
                Assert.That(config.Pose, Is.EqualTo(0.85f));
            });
        }

        [Test]
        public void Deserialize_EmptyJson_UsesDefaults()
        {
            var config = JsonConvert.DeserializeObject<SpawnEntryConfig>("{}");

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.BaseDurationMin, Is.EqualTo(3.0f));
                Assert.That(config.BaseDurationMax, Is.EqualTo(5.0f));
                Assert.That(config.SquadStaggerPerMember, Is.EqualTo(1.5f));
                Assert.That(config.DirectionBiasDuration, Is.EqualTo(30.0f));
                Assert.That(config.DirectionBiasStrength, Is.EqualTo(0.05f));
                Assert.That(config.Pose, Is.EqualTo(0.85f));
            });
        }

        [Test]
        public void Deserialize_AllProperties_FromJson()
        {
            var json =
                @"{
                    ""enabled"": false,
                    ""base_duration_min"": 2.0,
                    ""base_duration_max"": 8.0,
                    ""squad_stagger_per_member"": 2.5,
                    ""direction_bias_duration"": 45.0,
                    ""direction_bias_strength"": 0.10,
                    ""pose"": 0.70
                }";
            var config = JsonConvert.DeserializeObject<SpawnEntryConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.BaseDurationMin, Is.EqualTo(2.0f).Within(0.01f));
                Assert.That(config.BaseDurationMax, Is.EqualTo(8.0f).Within(0.01f));
                Assert.That(config.SquadStaggerPerMember, Is.EqualTo(2.5f).Within(0.01f));
                Assert.That(config.DirectionBiasDuration, Is.EqualTo(45.0f).Within(0.01f));
                Assert.That(config.DirectionBiasStrength, Is.EqualTo(0.10f).Within(0.01f));
                Assert.That(config.Pose, Is.EqualTo(0.70f).Within(0.01f));
            });
        }

        [Test]
        public void Deserialize_PartialOverride_OtherDefaultsIntact()
        {
            var json = @"{ ""enabled"": false, ""base_duration_min"": 1.0 }";
            var config = JsonConvert.DeserializeObject<SpawnEntryConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.BaseDurationMin, Is.EqualTo(1.0f).Within(0.01f));
                Assert.That(config.BaseDurationMax, Is.EqualTo(5.0f));
                Assert.That(config.SquadStaggerPerMember, Is.EqualTo(1.5f));
                Assert.That(config.DirectionBiasDuration, Is.EqualTo(30.0f));
                Assert.That(config.DirectionBiasStrength, Is.EqualTo(0.05f));
                Assert.That(config.Pose, Is.EqualTo(0.85f));
            });
        }

        [Test]
        public void RoundTrip_PreservesAllValues()
        {
            var original = new SpawnEntryConfig
            {
                Enabled = false,
                BaseDurationMin = 2.0f,
                BaseDurationMax = 8.0f,
                SquadStaggerPerMember = 2.5f,
                DirectionBiasDuration = 45.0f,
                DirectionBiasStrength = 0.10f,
                Pose = 0.70f,
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<SpawnEntryConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Enabled, Is.EqualTo(original.Enabled));
                Assert.That(deserialized.BaseDurationMin, Is.EqualTo(original.BaseDurationMin));
                Assert.That(deserialized.BaseDurationMax, Is.EqualTo(original.BaseDurationMax));
                Assert.That(deserialized.SquadStaggerPerMember, Is.EqualTo(original.SquadStaggerPerMember));
                Assert.That(deserialized.DirectionBiasDuration, Is.EqualTo(original.DirectionBiasDuration));
                Assert.That(deserialized.DirectionBiasStrength, Is.EqualTo(original.DirectionBiasStrength));
                Assert.That(deserialized.Pose, Is.EqualTo(original.Pose));
            });
        }

        [Test]
        public void Serialize_IncludesJsonPropertyNames()
        {
            var config = new SpawnEntryConfig();
            var json = JsonConvert.SerializeObject(config);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("enabled"));
                Assert.That(json, Does.Contain("base_duration_min"));
                Assert.That(json, Does.Contain("base_duration_max"));
                Assert.That(json, Does.Contain("squad_stagger_per_member"));
                Assert.That(json, Does.Contain("direction_bias_duration"));
                Assert.That(json, Does.Contain("direction_bias_strength"));
                Assert.That(json, Does.Contain("pose"));
            });
        }
    }
}
