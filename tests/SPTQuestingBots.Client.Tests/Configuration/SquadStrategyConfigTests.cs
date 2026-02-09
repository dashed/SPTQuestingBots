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
                Assert.That(config.EnablePositionValidation, Is.True);
                Assert.That(config.NavMeshSampleRadius, Is.EqualTo(2.0f));
                Assert.That(config.FallbackCandidateCount, Is.EqualTo(16));
                Assert.That(config.FallbackSearchRadius, Is.EqualTo(15.0f));
                Assert.That(config.EnableReachabilityCheck, Is.True);
                Assert.That(config.MaxPathLengthMultiplier, Is.EqualTo(2.5f));
                Assert.That(config.EnableLosCheck, Is.True);
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
                Assert.That(config.EnablePositionValidation, Is.True);
                Assert.That(config.NavMeshSampleRadius, Is.EqualTo(2.0f));
                Assert.That(config.FallbackCandidateCount, Is.EqualTo(16));
                Assert.That(config.FallbackSearchRadius, Is.EqualTo(15.0f));
                Assert.That(config.EnableReachabilityCheck, Is.True);
                Assert.That(config.MaxPathLengthMultiplier, Is.EqualTo(2.5f));
                Assert.That(config.EnableLosCheck, Is.True);
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
                EnablePositionValidation = false,
                NavMeshSampleRadius = 3.5f,
                FallbackCandidateCount = 24,
                FallbackSearchRadius = 20f,
                EnableReachabilityCheck = false,
                MaxPathLengthMultiplier = 3.0f,
                EnableLosCheck = false,
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
                Assert.That(deserialized.EnablePositionValidation, Is.False);
                Assert.That(deserialized.NavMeshSampleRadius, Is.EqualTo(3.5f));
                Assert.That(deserialized.FallbackCandidateCount, Is.EqualTo(24));
                Assert.That(deserialized.FallbackSearchRadius, Is.EqualTo(20f));
                Assert.That(deserialized.EnableReachabilityCheck, Is.False);
                Assert.That(deserialized.MaxPathLengthMultiplier, Is.EqualTo(3.0f));
                Assert.That(deserialized.EnableLosCheck, Is.False);
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
                Assert.That(json, Does.Contain("enable_position_validation"));
                Assert.That(json, Does.Contain("navmesh_sample_radius"));
                Assert.That(json, Does.Contain("fallback_candidate_count"));
                Assert.That(json, Does.Contain("fallback_search_radius"));
                Assert.That(json, Does.Contain("enable_reachability_check"));
                Assert.That(json, Does.Contain("max_path_length_multiplier"));
                Assert.That(json, Does.Contain("enable_los_check"));
            });
        }

        [Test]
        public void EnablePositionValidation_DefaultTrue()
        {
            var config = new SquadStrategyConfig();
            Assert.IsTrue(config.EnablePositionValidation);
        }

        [Test]
        public void NavMeshSampleRadius_Default2()
        {
            var config = new SquadStrategyConfig();
            Assert.AreEqual(2.0f, config.NavMeshSampleRadius);
        }

        [Test]
        public void FallbackCandidateCount_Default16()
        {
            var config = new SquadStrategyConfig();
            Assert.AreEqual(16, config.FallbackCandidateCount);
        }

        [Test]
        public void FallbackSearchRadius_Default15()
        {
            var config = new SquadStrategyConfig();
            Assert.AreEqual(15.0f, config.FallbackSearchRadius);
        }

        [Test]
        public void EnableReachabilityCheck_DefaultTrue()
        {
            var config = new SquadStrategyConfig();
            Assert.IsTrue(config.EnableReachabilityCheck);
        }

        [Test]
        public void MaxPathLengthMultiplier_Default25()
        {
            var config = new SquadStrategyConfig();
            Assert.AreEqual(2.5f, config.MaxPathLengthMultiplier, 0.01f);
        }

        [Test]
        public void EnableLosCheck_DefaultTrue()
        {
            var config = new SquadStrategyConfig();
            Assert.IsTrue(config.EnableLosCheck);
        }
    }
}
