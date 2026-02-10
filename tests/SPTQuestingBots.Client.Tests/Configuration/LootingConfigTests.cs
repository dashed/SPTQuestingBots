using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration
{
    [TestFixture]
    public class LootingConfigTests
    {
        [Test]
        public void DefaultConstructor_SetsCorrectDefaults()
        {
            var config = new LootingConfig();

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.DetectContainerDistance, Is.EqualTo(60f));
                Assert.That(config.DetectItemDistance, Is.EqualTo(40f));
                Assert.That(config.DetectCorpseDistance, Is.EqualTo(50f));
                Assert.That(config.ScanIntervalSeconds, Is.EqualTo(5f));
                Assert.That(config.MinItemValue, Is.EqualTo(5000));
                Assert.That(config.MaxConcurrentLooters, Is.EqualTo(5));
                Assert.That(config.LootDuringCombat, Is.False);
                Assert.That(config.ContainerLootingEnabled, Is.True);
                Assert.That(config.LooseItemLootingEnabled, Is.True);
                Assert.That(config.CorpseLootingEnabled, Is.True);
                Assert.That(config.GearSwapEnabled, Is.True);
                Assert.That(config.SquadLootCoordination, Is.True);
                Assert.That(config.DisableWhenLootingBotsDetected, Is.True);
                Assert.That(config.ApproachDistance, Is.EqualTo(0.85f));
                Assert.That(config.ApproachYTolerance, Is.EqualTo(0.5f));
                Assert.That(config.MaxLootingTimeSeconds, Is.EqualTo(30f));
                Assert.That(config.LootCooldownSeconds, Is.EqualTo(15f));
                Assert.That(config.ValueScoreCap, Is.EqualTo(50000f));
                Assert.That(config.DistancePenaltyFactor, Is.EqualTo(0.001f));
                Assert.That(config.QuestProximityBonus, Is.EqualTo(0.15f));
                Assert.That(config.GearUpgradeScoreBonus, Is.EqualTo(0.3f));
            });
        }

        [Test]
        public void Deserialize_EmptyJson_UsesDefaults()
        {
            var config = JsonConvert.DeserializeObject<LootingConfig>("{}");

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.DetectContainerDistance, Is.EqualTo(60f));
                Assert.That(config.DetectItemDistance, Is.EqualTo(40f));
                Assert.That(config.DetectCorpseDistance, Is.EqualTo(50f));
                Assert.That(config.ScanIntervalSeconds, Is.EqualTo(5f));
                Assert.That(config.MinItemValue, Is.EqualTo(5000));
                Assert.That(config.MaxConcurrentLooters, Is.EqualTo(5));
                Assert.That(config.LootDuringCombat, Is.False);
                Assert.That(config.ContainerLootingEnabled, Is.True);
                Assert.That(config.DisableWhenLootingBotsDetected, Is.True);
            });
        }

        [Test]
        public void Deserialize_AllProperties_FromJson()
        {
            var json =
                @"{
                    ""enabled"": false,
                    ""detect_container_distance"": 80,
                    ""detect_item_distance"": 55,
                    ""detect_corpse_distance"": 70,
                    ""scan_interval_seconds"": 3,
                    ""min_item_value"": 10000,
                    ""max_concurrent_looters"": 3,
                    ""loot_during_combat"": true,
                    ""container_looting_enabled"": false,
                    ""loose_item_looting_enabled"": false,
                    ""corpse_looting_enabled"": false,
                    ""gear_swap_enabled"": false,
                    ""squad_loot_coordination"": false,
                    ""disable_when_lootingbots_detected"": false,
                    ""approach_distance"": 1.2,
                    ""approach_y_tolerance"": 0.8,
                    ""max_looting_time_seconds"": 45,
                    ""loot_cooldown_seconds"": 20,
                    ""value_score_cap"": 75000,
                    ""distance_penalty_factor"": 0.002,
                    ""quest_proximity_bonus"": 0.25,
                    ""gear_upgrade_score_bonus"": 0.5
                }";
            var config = JsonConvert.DeserializeObject<LootingConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.DetectContainerDistance, Is.EqualTo(80f));
                Assert.That(config.DetectItemDistance, Is.EqualTo(55f));
                Assert.That(config.DetectCorpseDistance, Is.EqualTo(70f));
                Assert.That(config.ScanIntervalSeconds, Is.EqualTo(3f));
                Assert.That(config.MinItemValue, Is.EqualTo(10000));
                Assert.That(config.MaxConcurrentLooters, Is.EqualTo(3));
                Assert.That(config.LootDuringCombat, Is.True);
                Assert.That(config.ContainerLootingEnabled, Is.False);
                Assert.That(config.LooseItemLootingEnabled, Is.False);
                Assert.That(config.CorpseLootingEnabled, Is.False);
                Assert.That(config.GearSwapEnabled, Is.False);
                Assert.That(config.SquadLootCoordination, Is.False);
                Assert.That(config.DisableWhenLootingBotsDetected, Is.False);
                Assert.That(config.ApproachDistance, Is.EqualTo(1.2f));
                Assert.That(config.ApproachYTolerance, Is.EqualTo(0.8f));
                Assert.That(config.MaxLootingTimeSeconds, Is.EqualTo(45f));
                Assert.That(config.LootCooldownSeconds, Is.EqualTo(20f));
                Assert.That(config.ValueScoreCap, Is.EqualTo(75000f));
                Assert.That(config.DistancePenaltyFactor, Is.EqualTo(0.002f));
                Assert.That(config.QuestProximityBonus, Is.EqualTo(0.25f));
                Assert.That(config.GearUpgradeScoreBonus, Is.EqualTo(0.5f));
            });
        }

        [Test]
        public void Deserialize_PartialOverride_OtherDefaultsIntact()
        {
            var json = @"{ ""enabled"": false, ""min_item_value"": 8000 }";
            var config = JsonConvert.DeserializeObject<LootingConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.MinItemValue, Is.EqualTo(8000));
                Assert.That(config.DetectContainerDistance, Is.EqualTo(60f));
                Assert.That(config.ScanIntervalSeconds, Is.EqualTo(5f));
                Assert.That(config.MaxConcurrentLooters, Is.EqualTo(5));
                Assert.That(config.ContainerLootingEnabled, Is.True);
                Assert.That(config.DisableWhenLootingBotsDetected, Is.True);
                Assert.That(config.ApproachDistance, Is.EqualTo(0.85f));
            });
        }

        [Test]
        public void Deserialize_BooleanProperties_AllVariants()
        {
            var json =
                @"{
                    ""enabled"": false,
                    ""loot_during_combat"": true,
                    ""container_looting_enabled"": false,
                    ""loose_item_looting_enabled"": false,
                    ""corpse_looting_enabled"": false,
                    ""gear_swap_enabled"": false,
                    ""squad_loot_coordination"": false,
                    ""disable_when_lootingbots_detected"": false
                }";
            var config = JsonConvert.DeserializeObject<LootingConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.LootDuringCombat, Is.True);
                Assert.That(config.ContainerLootingEnabled, Is.False);
                Assert.That(config.LooseItemLootingEnabled, Is.False);
                Assert.That(config.CorpseLootingEnabled, Is.False);
                Assert.That(config.GearSwapEnabled, Is.False);
                Assert.That(config.SquadLootCoordination, Is.False);
                Assert.That(config.DisableWhenLootingBotsDetected, Is.False);
            });
        }

        [Test]
        public void Deserialize_FloatProperties_CorrectPrecision()
        {
            var json =
                @"{
                    ""detect_container_distance"": 75.5,
                    ""detect_item_distance"": 42.3,
                    ""detect_corpse_distance"": 55.7,
                    ""scan_interval_seconds"": 2.5,
                    ""approach_distance"": 1.15,
                    ""approach_y_tolerance"": 0.65,
                    ""max_looting_time_seconds"": 25.5,
                    ""loot_cooldown_seconds"": 12.5,
                    ""value_score_cap"": 60000.5,
                    ""distance_penalty_factor"": 0.0015,
                    ""quest_proximity_bonus"": 0.2,
                    ""gear_upgrade_score_bonus"": 0.35
                }";
            var config = JsonConvert.DeserializeObject<LootingConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.DetectContainerDistance, Is.EqualTo(75.5f).Within(0.01f));
                Assert.That(config.DetectItemDistance, Is.EqualTo(42.3f).Within(0.01f));
                Assert.That(config.DetectCorpseDistance, Is.EqualTo(55.7f).Within(0.01f));
                Assert.That(config.ScanIntervalSeconds, Is.EqualTo(2.5f).Within(0.01f));
                Assert.That(config.ApproachDistance, Is.EqualTo(1.15f).Within(0.01f));
                Assert.That(config.ApproachYTolerance, Is.EqualTo(0.65f).Within(0.01f));
                Assert.That(config.MaxLootingTimeSeconds, Is.EqualTo(25.5f).Within(0.01f));
                Assert.That(config.LootCooldownSeconds, Is.EqualTo(12.5f).Within(0.01f));
                Assert.That(config.ValueScoreCap, Is.EqualTo(60000.5f).Within(0.1f));
                Assert.That(config.DistancePenaltyFactor, Is.EqualTo(0.0015f).Within(0.0001f));
                Assert.That(config.QuestProximityBonus, Is.EqualTo(0.2f).Within(0.01f));
                Assert.That(config.GearUpgradeScoreBonus, Is.EqualTo(0.35f).Within(0.01f));
            });
        }

        [Test]
        public void RoundTrip_PreservesAllValues()
        {
            var original = new LootingConfig
            {
                Enabled = false,
                DetectContainerDistance = 80f,
                DetectItemDistance = 55f,
                DetectCorpseDistance = 70f,
                ScanIntervalSeconds = 3f,
                MinItemValue = 10000,
                MaxConcurrentLooters = 3,
                LootDuringCombat = true,
                ContainerLootingEnabled = false,
                LooseItemLootingEnabled = false,
                CorpseLootingEnabled = false,
                GearSwapEnabled = false,
                SquadLootCoordination = false,
                DisableWhenLootingBotsDetected = false,
                ApproachDistance = 1.2f,
                ApproachYTolerance = 0.8f,
                MaxLootingTimeSeconds = 45f,
                LootCooldownSeconds = 20f,
                ValueScoreCap = 75000f,
                DistancePenaltyFactor = 0.002f,
                QuestProximityBonus = 0.25f,
                GearUpgradeScoreBonus = 0.5f,
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<LootingConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Enabled, Is.EqualTo(original.Enabled));
                Assert.That(deserialized.DetectContainerDistance, Is.EqualTo(original.DetectContainerDistance));
                Assert.That(deserialized.DetectItemDistance, Is.EqualTo(original.DetectItemDistance));
                Assert.That(deserialized.DetectCorpseDistance, Is.EqualTo(original.DetectCorpseDistance));
                Assert.That(deserialized.ScanIntervalSeconds, Is.EqualTo(original.ScanIntervalSeconds));
                Assert.That(deserialized.MinItemValue, Is.EqualTo(original.MinItemValue));
                Assert.That(deserialized.MaxConcurrentLooters, Is.EqualTo(original.MaxConcurrentLooters));
                Assert.That(deserialized.LootDuringCombat, Is.EqualTo(original.LootDuringCombat));
                Assert.That(deserialized.ContainerLootingEnabled, Is.EqualTo(original.ContainerLootingEnabled));
                Assert.That(deserialized.LooseItemLootingEnabled, Is.EqualTo(original.LooseItemLootingEnabled));
                Assert.That(deserialized.CorpseLootingEnabled, Is.EqualTo(original.CorpseLootingEnabled));
                Assert.That(deserialized.GearSwapEnabled, Is.EqualTo(original.GearSwapEnabled));
                Assert.That(deserialized.SquadLootCoordination, Is.EqualTo(original.SquadLootCoordination));
                Assert.That(deserialized.DisableWhenLootingBotsDetected, Is.EqualTo(original.DisableWhenLootingBotsDetected));
                Assert.That(deserialized.ApproachDistance, Is.EqualTo(original.ApproachDistance));
                Assert.That(deserialized.ApproachYTolerance, Is.EqualTo(original.ApproachYTolerance));
                Assert.That(deserialized.MaxLootingTimeSeconds, Is.EqualTo(original.MaxLootingTimeSeconds));
                Assert.That(deserialized.LootCooldownSeconds, Is.EqualTo(original.LootCooldownSeconds));
                Assert.That(deserialized.ValueScoreCap, Is.EqualTo(original.ValueScoreCap));
                Assert.That(deserialized.DistancePenaltyFactor, Is.EqualTo(original.DistancePenaltyFactor));
                Assert.That(deserialized.QuestProximityBonus, Is.EqualTo(original.QuestProximityBonus));
                Assert.That(deserialized.GearUpgradeScoreBonus, Is.EqualTo(original.GearUpgradeScoreBonus));
            });
        }

        [Test]
        public void Serialize_IncludesJsonPropertyNames()
        {
            var config = new LootingConfig();
            var json = JsonConvert.SerializeObject(config);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("enabled"));
                Assert.That(json, Does.Contain("detect_container_distance"));
                Assert.That(json, Does.Contain("detect_item_distance"));
                Assert.That(json, Does.Contain("detect_corpse_distance"));
                Assert.That(json, Does.Contain("scan_interval_seconds"));
                Assert.That(json, Does.Contain("min_item_value"));
                Assert.That(json, Does.Contain("max_concurrent_looters"));
                Assert.That(json, Does.Contain("loot_during_combat"));
                Assert.That(json, Does.Contain("container_looting_enabled"));
                Assert.That(json, Does.Contain("loose_item_looting_enabled"));
                Assert.That(json, Does.Contain("corpse_looting_enabled"));
                Assert.That(json, Does.Contain("gear_swap_enabled"));
                Assert.That(json, Does.Contain("squad_loot_coordination"));
                Assert.That(json, Does.Contain("disable_when_lootingbots_detected"));
                Assert.That(json, Does.Contain("approach_distance"));
                Assert.That(json, Does.Contain("approach_y_tolerance"));
                Assert.That(json, Does.Contain("max_looting_time_seconds"));
                Assert.That(json, Does.Contain("loot_cooldown_seconds"));
                Assert.That(json, Does.Contain("value_score_cap"));
                Assert.That(json, Does.Contain("distance_penalty_factor"));
                Assert.That(json, Does.Contain("quest_proximity_bonus"));
                Assert.That(json, Does.Contain("gear_upgrade_score_bonus"));
            });
        }
    }
}
