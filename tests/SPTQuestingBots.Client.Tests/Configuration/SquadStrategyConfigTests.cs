using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration
{
    [TestFixture]
    public class SquadStrategyConfigTests
    {
        [Test]
        public void DefaultConstructor_SetsCorrectDefaults()
        {
            var config = new SquadStrategyConfig();

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.GuardDistance, Is.EqualTo(8f));
                Assert.That(config.FlankDistance, Is.EqualTo(15f));
                Assert.That(config.OverwatchDistance, Is.EqualTo(25f));
                Assert.That(config.EscortDistance, Is.EqualTo(5f));
                Assert.That(config.ArrivalRadius, Is.EqualTo(3f));
                Assert.That(config.MaxDistanceFromBoss, Is.EqualTo(75f));
                Assert.That(config.StrategyPacingSeconds, Is.EqualTo(0.5f));
                Assert.That(config.UseQuestTypeRoles, Is.True);
                Assert.That(config.EnableCommunicationRange, Is.True);
                Assert.That(config.CommunicationRangeNoEarpiece, Is.EqualTo(35f));
                Assert.That(config.CommunicationRangeEarpiece, Is.EqualTo(200f));
                Assert.That(config.EnableSquadPersonality, Is.True);
            });
        }

        [Test]
        public void Deserialize_EmptyJson_UsesDefaults()
        {
            var config = JsonConvert.DeserializeObject<SquadStrategyConfig>("{}");

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.GuardDistance, Is.EqualTo(8f));
                Assert.That(config.FlankDistance, Is.EqualTo(15f));
                Assert.That(config.OverwatchDistance, Is.EqualTo(25f));
                Assert.That(config.EscortDistance, Is.EqualTo(5f));
                Assert.That(config.ArrivalRadius, Is.EqualTo(3f));
                Assert.That(config.MaxDistanceFromBoss, Is.EqualTo(75f));
                Assert.That(config.StrategyPacingSeconds, Is.EqualTo(0.5f));
                Assert.That(config.UseQuestTypeRoles, Is.True);
                Assert.That(config.EnableCommunicationRange, Is.True);
                Assert.That(config.CommunicationRangeNoEarpiece, Is.EqualTo(35f));
                Assert.That(config.CommunicationRangeEarpiece, Is.EqualTo(200f));
                Assert.That(config.EnableSquadPersonality, Is.True);
            });
        }

        [Test]
        public void Deserialize_OverrideValues()
        {
            var json = """{ "enabled": true, "guard_distance": 10, "flank_distance": 20, "overwatch_distance": 30 }""";
            var config = JsonConvert.DeserializeObject<SquadStrategyConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.GuardDistance, Is.EqualTo(10f));
                Assert.That(config.FlankDistance, Is.EqualTo(20f));
                Assert.That(config.OverwatchDistance, Is.EqualTo(30f));
                // Others remain default
                Assert.That(config.EscortDistance, Is.EqualTo(5f));
            });
        }

        [Test]
        public void Deserialize_CommunicationRangeOverrides()
        {
            var json =
                """{ "enable_communication_range": false, "communication_range_no_earpiece": 50, "communication_range_earpiece": 300, "enable_squad_personality": false }""";
            var config = JsonConvert.DeserializeObject<SquadStrategyConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.EnableCommunicationRange, Is.False);
                Assert.That(config.CommunicationRangeNoEarpiece, Is.EqualTo(50f));
                Assert.That(config.CommunicationRangeEarpiece, Is.EqualTo(300f));
                Assert.That(config.EnableSquadPersonality, Is.False);
            });
        }

        [Test]
        public void RoundTrip_PreservesAllValues()
        {
            var original = new SquadStrategyConfig
            {
                Enabled = true,
                GuardDistance = 12f,
                FlankDistance = 18f,
                OverwatchDistance = 30f,
                EscortDistance = 7f,
                ArrivalRadius = 4f,
                MaxDistanceFromBoss = 100f,
                StrategyPacingSeconds = 1f,
                UseQuestTypeRoles = false,
                EnableCommunicationRange = false,
                CommunicationRangeNoEarpiece = 50f,
                CommunicationRangeEarpiece = 300f,
                EnableSquadPersonality = false,
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<SquadStrategyConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Enabled, Is.True);
                Assert.That(deserialized.GuardDistance, Is.EqualTo(12f));
                Assert.That(deserialized.FlankDistance, Is.EqualTo(18f));
                Assert.That(deserialized.OverwatchDistance, Is.EqualTo(30f));
                Assert.That(deserialized.EscortDistance, Is.EqualTo(7f));
                Assert.That(deserialized.ArrivalRadius, Is.EqualTo(4f));
                Assert.That(deserialized.MaxDistanceFromBoss, Is.EqualTo(100f));
                Assert.That(deserialized.StrategyPacingSeconds, Is.EqualTo(1f));
                Assert.That(deserialized.UseQuestTypeRoles, Is.False);
                Assert.That(deserialized.EnableCommunicationRange, Is.False);
                Assert.That(deserialized.CommunicationRangeNoEarpiece, Is.EqualTo(50f));
                Assert.That(deserialized.CommunicationRangeEarpiece, Is.EqualTo(300f));
                Assert.That(deserialized.EnableSquadPersonality, Is.False);
            });
        }

        [Test]
        public void Serialize_IncludesJsonPropertyNames()
        {
            var config = new SquadStrategyConfig();
            var json = JsonConvert.SerializeObject(config);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("enabled"));
                Assert.That(json, Does.Contain("guard_distance"));
                Assert.That(json, Does.Contain("flank_distance"));
                Assert.That(json, Does.Contain("overwatch_distance"));
                Assert.That(json, Does.Contain("escort_distance"));
                Assert.That(json, Does.Contain("arrival_radius"));
                Assert.That(json, Does.Contain("max_distance_from_boss"));
                Assert.That(json, Does.Contain("strategy_pacing_seconds"));
                Assert.That(json, Does.Contain("use_quest_type_roles"));
                Assert.That(json, Does.Contain("enable_communication_range"));
                Assert.That(json, Does.Contain("communication_range_no_earpiece"));
                Assert.That(json, Does.Contain("communication_range_earpiece"));
                Assert.That(json, Does.Contain("enable_squad_personality"));
            });
        }
    }
}
