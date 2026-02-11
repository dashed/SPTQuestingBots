using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class VultureConfigTests
{
    [Test]
    public void DefaultConstructor_SetsCorrectDefaults()
    {
        var config = new VultureConfig();

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.BaseDetectionRange, Is.EqualTo(150.0f));
            Assert.That(config.NightRangeMultiplier, Is.EqualTo(0.65f));
            Assert.That(config.EnableTimeOfDay, Is.True);
            Assert.That(config.MultiShotIntensityBonus, Is.EqualTo(5));
            Assert.That(config.IntensityWindow, Is.EqualTo(15.0f));
            Assert.That(config.CourageThreshold, Is.EqualTo(15));
            Assert.That(config.AmbushDuration, Is.EqualTo(90.0f));
            Assert.That(config.AmbushDistanceMin, Is.EqualTo(25.0f));
            Assert.That(config.AmbushDistanceMax, Is.EqualTo(30.0f));
            Assert.That(config.SilenceTriggerDuration, Is.EqualTo(45.0f));
            Assert.That(config.EnableGreed, Is.True);
            Assert.That(config.EnableSilentApproach, Is.True);
            Assert.That(config.SilentApproachDistance, Is.EqualTo(35.0f));
            Assert.That(config.EnableFlashlightDiscipline, Is.True);
            Assert.That(config.EnableParanoia, Is.True);
            Assert.That(config.ParanoiaIntervalMin, Is.EqualTo(3.0f));
            Assert.That(config.ParanoiaIntervalMax, Is.EqualTo(6.0f));
            Assert.That(config.ParanoiaAngleRange, Is.EqualTo(45.0f));
            Assert.That(config.EnableBaiting, Is.True);
            Assert.That(config.BaitingChance, Is.EqualTo(25));
            Assert.That(config.EnableBossAvoidance, Is.True);
            Assert.That(config.BossAvoidanceRadius, Is.EqualTo(75.0f));
            Assert.That(config.BossZoneDecay, Is.EqualTo(120.0f));
            Assert.That(config.EnableAirdropVulturing, Is.True);
            Assert.That(config.EnableSquadVulturing, Is.True);
            Assert.That(config.EnableForPmcs, Is.True);
            Assert.That(config.EnableForScavs, Is.False);
            Assert.That(config.EnableForPscavs, Is.False);
            Assert.That(config.EnableForRaiders, Is.False);
            Assert.That(config.MaxEventAge, Is.EqualTo(300.0f));
            Assert.That(config.EventBufferSize, Is.EqualTo(128));
            Assert.That(config.CooldownOnReject, Is.EqualTo(180.0f));
            Assert.That(config.MovementTimeout, Is.EqualTo(90.0f));
        });
    }

    [Test]
    public void Deserialize_EmptyJson_UsesDefaults()
    {
        var config = JsonConvert.DeserializeObject<VultureConfig>("{}");

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.True);
            Assert.That(config.BaseDetectionRange, Is.EqualTo(150.0f));
            Assert.That(config.NightRangeMultiplier, Is.EqualTo(0.65f));
            Assert.That(config.EnableTimeOfDay, Is.True);
            Assert.That(config.MultiShotIntensityBonus, Is.EqualTo(5));
            Assert.That(config.CourageThreshold, Is.EqualTo(15));
            Assert.That(config.AmbushDuration, Is.EqualTo(90.0f));
            Assert.That(config.EnableGreed, Is.True);
            Assert.That(config.EnableForPmcs, Is.True);
            Assert.That(config.EnableForScavs, Is.False);
        });
    }

    [Test]
    public void Deserialize_AllProperties_FromJson()
    {
        var json =
            @"{
                    ""enabled"": false,
                    ""base_detection_range"": 200,
                    ""night_range_multiplier"": 0.5,
                    ""enable_time_of_day"": false,
                    ""multi_shot_intensity_bonus"": 10,
                    ""intensity_window"": 20,
                    ""courage_threshold"": 25,
                    ""ambush_duration"": 120,
                    ""ambush_distance_min"": 30,
                    ""ambush_distance_max"": 40,
                    ""silence_trigger_duration"": 60,
                    ""enable_greed"": false,
                    ""enable_silent_approach"": false,
                    ""silent_approach_distance"": 50,
                    ""enable_flashlight_discipline"": false,
                    ""enable_paranoia"": false,
                    ""paranoia_interval_min"": 5,
                    ""paranoia_interval_max"": 10,
                    ""paranoia_angle_range"": 60,
                    ""enable_baiting"": false,
                    ""baiting_chance"": 50,
                    ""enable_boss_avoidance"": false,
                    ""boss_avoidance_radius"": 100,
                    ""boss_zone_decay"": 180,
                    ""enable_airdrop_vulturing"": false,
                    ""enable_squad_vulturing"": false,
                    ""enable_for_pmcs"": false,
                    ""enable_for_scavs"": true,
                    ""enable_for_pscavs"": true,
                    ""enable_for_raiders"": true,
                    ""max_event_age"": 600,
                    ""event_buffer_size"": 256,
                    ""cooldown_on_reject"": 300,
                    ""movement_timeout"": 120
                }";
        var config = JsonConvert.DeserializeObject<VultureConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.False);
            Assert.That(config.BaseDetectionRange, Is.EqualTo(200f));
            Assert.That(config.NightRangeMultiplier, Is.EqualTo(0.5f));
            Assert.That(config.EnableTimeOfDay, Is.False);
            Assert.That(config.MultiShotIntensityBonus, Is.EqualTo(10));
            Assert.That(config.IntensityWindow, Is.EqualTo(20f));
            Assert.That(config.CourageThreshold, Is.EqualTo(25));
            Assert.That(config.AmbushDuration, Is.EqualTo(120f));
            Assert.That(config.AmbushDistanceMin, Is.EqualTo(30f));
            Assert.That(config.AmbushDistanceMax, Is.EqualTo(40f));
            Assert.That(config.SilenceTriggerDuration, Is.EqualTo(60f));
            Assert.That(config.EnableGreed, Is.False);
            Assert.That(config.EnableSilentApproach, Is.False);
            Assert.That(config.SilentApproachDistance, Is.EqualTo(50f));
            Assert.That(config.EnableFlashlightDiscipline, Is.False);
            Assert.That(config.EnableParanoia, Is.False);
            Assert.That(config.ParanoiaIntervalMin, Is.EqualTo(5f));
            Assert.That(config.ParanoiaIntervalMax, Is.EqualTo(10f));
            Assert.That(config.ParanoiaAngleRange, Is.EqualTo(60f));
            Assert.That(config.EnableBaiting, Is.False);
            Assert.That(config.BaitingChance, Is.EqualTo(50));
            Assert.That(config.EnableBossAvoidance, Is.False);
            Assert.That(config.BossAvoidanceRadius, Is.EqualTo(100f));
            Assert.That(config.BossZoneDecay, Is.EqualTo(180f));
            Assert.That(config.EnableAirdropVulturing, Is.False);
            Assert.That(config.EnableSquadVulturing, Is.False);
            Assert.That(config.EnableForPmcs, Is.False);
            Assert.That(config.EnableForScavs, Is.True);
            Assert.That(config.EnableForPscavs, Is.True);
            Assert.That(config.EnableForRaiders, Is.True);
            Assert.That(config.MaxEventAge, Is.EqualTo(600f));
            Assert.That(config.EventBufferSize, Is.EqualTo(256));
            Assert.That(config.CooldownOnReject, Is.EqualTo(300f));
            Assert.That(config.MovementTimeout, Is.EqualTo(120f));
        });
    }

    [Test]
    public void Deserialize_PartialOverride_OtherDefaultsIntact()
    {
        var json = @"{ ""enabled"": false, ""base_detection_range"": 200, ""courage_threshold"": 30 }";
        var config = JsonConvert.DeserializeObject<VultureConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.False);
            Assert.That(config.BaseDetectionRange, Is.EqualTo(200f));
            Assert.That(config.CourageThreshold, Is.EqualTo(30));
            Assert.That(config.NightRangeMultiplier, Is.EqualTo(0.65f));
            Assert.That(config.EnableTimeOfDay, Is.True);
            Assert.That(config.AmbushDuration, Is.EqualTo(90.0f));
            Assert.That(config.EnableGreed, Is.True);
            Assert.That(config.EnableForPmcs, Is.True);
            Assert.That(config.MaxEventAge, Is.EqualTo(300.0f));
        });
    }

    [Test]
    public void Deserialize_BooleanProperties_AllVariants()
    {
        var json =
            @"{
                    ""enabled"": false,
                    ""enable_time_of_day"": false,
                    ""enable_greed"": false,
                    ""enable_silent_approach"": false,
                    ""enable_flashlight_discipline"": false,
                    ""enable_paranoia"": false,
                    ""enable_baiting"": false,
                    ""enable_boss_avoidance"": false,
                    ""enable_airdrop_vulturing"": false,
                    ""enable_squad_vulturing"": false,
                    ""enable_for_pmcs"": false,
                    ""enable_for_scavs"": true,
                    ""enable_for_pscavs"": true,
                    ""enable_for_raiders"": true
                }";
        var config = JsonConvert.DeserializeObject<VultureConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.Enabled, Is.False);
            Assert.That(config.EnableTimeOfDay, Is.False);
            Assert.That(config.EnableGreed, Is.False);
            Assert.That(config.EnableSilentApproach, Is.False);
            Assert.That(config.EnableFlashlightDiscipline, Is.False);
            Assert.That(config.EnableParanoia, Is.False);
            Assert.That(config.EnableBaiting, Is.False);
            Assert.That(config.EnableBossAvoidance, Is.False);
            Assert.That(config.EnableAirdropVulturing, Is.False);
            Assert.That(config.EnableSquadVulturing, Is.False);
            Assert.That(config.EnableForPmcs, Is.False);
            Assert.That(config.EnableForScavs, Is.True);
            Assert.That(config.EnableForPscavs, Is.True);
            Assert.That(config.EnableForRaiders, Is.True);
        });
    }

    [Test]
    public void Deserialize_FloatProperties_CorrectPrecision()
    {
        var json =
            @"{
                    ""base_detection_range"": 175.5,
                    ""night_range_multiplier"": 0.72,
                    ""intensity_window"": 12.5,
                    ""ambush_duration"": 85.3,
                    ""ambush_distance_min"": 22.5,
                    ""ambush_distance_max"": 35.7,
                    ""silence_trigger_duration"": 50.5,
                    ""silent_approach_distance"": 40.2,
                    ""paranoia_interval_min"": 2.5,
                    ""paranoia_interval_max"": 8.5,
                    ""paranoia_angle_range"": 55.5,
                    ""boss_avoidance_radius"": 80.3,
                    ""boss_zone_decay"": 150.5,
                    ""max_event_age"": 450.5,
                    ""cooldown_on_reject"": 200.5,
                    ""movement_timeout"": 75.5
                }";
        var config = JsonConvert.DeserializeObject<VultureConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.BaseDetectionRange, Is.EqualTo(175.5f).Within(0.01f));
            Assert.That(config.NightRangeMultiplier, Is.EqualTo(0.72f).Within(0.01f));
            Assert.That(config.IntensityWindow, Is.EqualTo(12.5f).Within(0.01f));
            Assert.That(config.AmbushDuration, Is.EqualTo(85.3f).Within(0.01f));
            Assert.That(config.AmbushDistanceMin, Is.EqualTo(22.5f).Within(0.01f));
            Assert.That(config.AmbushDistanceMax, Is.EqualTo(35.7f).Within(0.01f));
            Assert.That(config.SilenceTriggerDuration, Is.EqualTo(50.5f).Within(0.01f));
            Assert.That(config.SilentApproachDistance, Is.EqualTo(40.2f).Within(0.01f));
            Assert.That(config.ParanoiaIntervalMin, Is.EqualTo(2.5f).Within(0.01f));
            Assert.That(config.ParanoiaIntervalMax, Is.EqualTo(8.5f).Within(0.01f));
            Assert.That(config.ParanoiaAngleRange, Is.EqualTo(55.5f).Within(0.01f));
            Assert.That(config.BossAvoidanceRadius, Is.EqualTo(80.3f).Within(0.01f));
            Assert.That(config.BossZoneDecay, Is.EqualTo(150.5f).Within(0.01f));
            Assert.That(config.MaxEventAge, Is.EqualTo(450.5f).Within(0.1f));
            Assert.That(config.CooldownOnReject, Is.EqualTo(200.5f).Within(0.01f));
            Assert.That(config.MovementTimeout, Is.EqualTo(75.5f).Within(0.01f));
        });
    }

    [Test]
    public void RoundTrip_PreservesAllValues()
    {
        var original = new VultureConfig
        {
            Enabled = false,
            BaseDetectionRange = 200f,
            NightRangeMultiplier = 0.5f,
            EnableTimeOfDay = false,
            MultiShotIntensityBonus = 10,
            IntensityWindow = 20f,
            CourageThreshold = 25,
            AmbushDuration = 120f,
            AmbushDistanceMin = 30f,
            AmbushDistanceMax = 40f,
            SilenceTriggerDuration = 60f,
            EnableGreed = false,
            EnableSilentApproach = false,
            SilentApproachDistance = 50f,
            EnableFlashlightDiscipline = false,
            EnableParanoia = false,
            ParanoiaIntervalMin = 5f,
            ParanoiaIntervalMax = 10f,
            ParanoiaAngleRange = 60f,
            EnableBaiting = false,
            BaitingChance = 50,
            EnableBossAvoidance = false,
            BossAvoidanceRadius = 100f,
            BossZoneDecay = 180f,
            EnableAirdropVulturing = false,
            EnableSquadVulturing = false,
            EnableForPmcs = false,
            EnableForScavs = true,
            EnableForPscavs = true,
            EnableForRaiders = true,
            MaxEventAge = 600f,
            EventBufferSize = 256,
            CooldownOnReject = 300f,
            MovementTimeout = 120f,
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<VultureConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.Enabled, Is.EqualTo(original.Enabled));
            Assert.That(deserialized.BaseDetectionRange, Is.EqualTo(original.BaseDetectionRange));
            Assert.That(deserialized.NightRangeMultiplier, Is.EqualTo(original.NightRangeMultiplier));
            Assert.That(deserialized.EnableTimeOfDay, Is.EqualTo(original.EnableTimeOfDay));
            Assert.That(deserialized.MultiShotIntensityBonus, Is.EqualTo(original.MultiShotIntensityBonus));
            Assert.That(deserialized.IntensityWindow, Is.EqualTo(original.IntensityWindow));
            Assert.That(deserialized.CourageThreshold, Is.EqualTo(original.CourageThreshold));
            Assert.That(deserialized.AmbushDuration, Is.EqualTo(original.AmbushDuration));
            Assert.That(deserialized.AmbushDistanceMin, Is.EqualTo(original.AmbushDistanceMin));
            Assert.That(deserialized.AmbushDistanceMax, Is.EqualTo(original.AmbushDistanceMax));
            Assert.That(deserialized.SilenceTriggerDuration, Is.EqualTo(original.SilenceTriggerDuration));
            Assert.That(deserialized.EnableGreed, Is.EqualTo(original.EnableGreed));
            Assert.That(deserialized.EnableSilentApproach, Is.EqualTo(original.EnableSilentApproach));
            Assert.That(deserialized.SilentApproachDistance, Is.EqualTo(original.SilentApproachDistance));
            Assert.That(deserialized.EnableFlashlightDiscipline, Is.EqualTo(original.EnableFlashlightDiscipline));
            Assert.That(deserialized.EnableParanoia, Is.EqualTo(original.EnableParanoia));
            Assert.That(deserialized.ParanoiaIntervalMin, Is.EqualTo(original.ParanoiaIntervalMin));
            Assert.That(deserialized.ParanoiaIntervalMax, Is.EqualTo(original.ParanoiaIntervalMax));
            Assert.That(deserialized.ParanoiaAngleRange, Is.EqualTo(original.ParanoiaAngleRange));
            Assert.That(deserialized.EnableBaiting, Is.EqualTo(original.EnableBaiting));
            Assert.That(deserialized.BaitingChance, Is.EqualTo(original.BaitingChance));
            Assert.That(deserialized.EnableBossAvoidance, Is.EqualTo(original.EnableBossAvoidance));
            Assert.That(deserialized.BossAvoidanceRadius, Is.EqualTo(original.BossAvoidanceRadius));
            Assert.That(deserialized.BossZoneDecay, Is.EqualTo(original.BossZoneDecay));
            Assert.That(deserialized.EnableAirdropVulturing, Is.EqualTo(original.EnableAirdropVulturing));
            Assert.That(deserialized.EnableSquadVulturing, Is.EqualTo(original.EnableSquadVulturing));
            Assert.That(deserialized.EnableForPmcs, Is.EqualTo(original.EnableForPmcs));
            Assert.That(deserialized.EnableForScavs, Is.EqualTo(original.EnableForScavs));
            Assert.That(deserialized.EnableForPscavs, Is.EqualTo(original.EnableForPscavs));
            Assert.That(deserialized.EnableForRaiders, Is.EqualTo(original.EnableForRaiders));
            Assert.That(deserialized.MaxEventAge, Is.EqualTo(original.MaxEventAge));
            Assert.That(deserialized.EventBufferSize, Is.EqualTo(original.EventBufferSize));
            Assert.That(deserialized.CooldownOnReject, Is.EqualTo(original.CooldownOnReject));
            Assert.That(deserialized.MovementTimeout, Is.EqualTo(original.MovementTimeout));
        });
    }

    [Test]
    public void Serialize_IncludesJsonPropertyNames()
    {
        var config = new VultureConfig();
        var json = JsonConvert.SerializeObject(config);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("enabled"));
            Assert.That(json, Does.Contain("base_detection_range"));
            Assert.That(json, Does.Contain("night_range_multiplier"));
            Assert.That(json, Does.Contain("enable_time_of_day"));
            Assert.That(json, Does.Contain("multi_shot_intensity_bonus"));
            Assert.That(json, Does.Contain("intensity_window"));
            Assert.That(json, Does.Contain("courage_threshold"));
            Assert.That(json, Does.Contain("ambush_duration"));
            Assert.That(json, Does.Contain("ambush_distance_min"));
            Assert.That(json, Does.Contain("ambush_distance_max"));
            Assert.That(json, Does.Contain("silence_trigger_duration"));
            Assert.That(json, Does.Contain("enable_greed"));
            Assert.That(json, Does.Contain("enable_silent_approach"));
            Assert.That(json, Does.Contain("silent_approach_distance"));
            Assert.That(json, Does.Contain("enable_flashlight_discipline"));
            Assert.That(json, Does.Contain("enable_paranoia"));
            Assert.That(json, Does.Contain("paranoia_interval_min"));
            Assert.That(json, Does.Contain("paranoia_interval_max"));
            Assert.That(json, Does.Contain("paranoia_angle_range"));
            Assert.That(json, Does.Contain("enable_baiting"));
            Assert.That(json, Does.Contain("baiting_chance"));
            Assert.That(json, Does.Contain("enable_boss_avoidance"));
            Assert.That(json, Does.Contain("boss_avoidance_radius"));
            Assert.That(json, Does.Contain("boss_zone_decay"));
            Assert.That(json, Does.Contain("enable_airdrop_vulturing"));
            Assert.That(json, Does.Contain("enable_squad_vulturing"));
            Assert.That(json, Does.Contain("enable_for_pmcs"));
            Assert.That(json, Does.Contain("enable_for_scavs"));
            Assert.That(json, Does.Contain("enable_for_pscavs"));
            Assert.That(json, Does.Contain("enable_for_raiders"));
            Assert.That(json, Does.Contain("max_event_age"));
            Assert.That(json, Does.Contain("event_buffer_size"));
            Assert.That(json, Does.Contain("cooldown_on_reject"));
            Assert.That(json, Does.Contain("movement_timeout"));
        });
    }
}
