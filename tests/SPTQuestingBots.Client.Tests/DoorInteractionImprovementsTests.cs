using System.IO;
using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests;

/// <summary>
/// Tests for door interaction improvements: sprint pause toggle, forced-open timer config,
/// door sound config, breach chance integration, and room clear parameter tuning.
/// </summary>
[TestFixture]
public class DoorInteractionImprovementsTests
{
    #region Item 1: Door Sprint Pause Config Toggle

    [Test]
    public void SprintingLimitationsConfig_EnableDoorSprintPause_DefaultsToTrue()
    {
        var config = new SprintingLimitationsConfig();
        Assert.That(config.EnableDoorSprintPause, Is.True);
    }

    [Test]
    public void SprintingLimitationsConfig_EnableDoorSprintPause_DeserializesFromJson()
    {
        var json = @"{ ""enable_door_sprint_pause"": false }";
        var config = JsonConvert.DeserializeObject<SprintingLimitationsConfig>(json);
        Assert.That(config.EnableDoorSprintPause, Is.False);
    }

    [Test]
    public void SprintingLimitationsConfig_EnableDoorSprintPause_SerializesWithCorrectName()
    {
        var config = new SprintingLimitationsConfig { EnableDoorSprintPause = true };
        var json = JsonConvert.SerializeObject(config);
        Assert.That(json, Does.Contain("enable_door_sprint_pause"));
    }

    [Test]
    public void SprintingLimitationsConfig_AllToggles_Present()
    {
        var config = new SprintingLimitationsConfig();
        Assert.Multiple(() =>
        {
            Assert.That(config.EnablePostCombatSprintBlock, Is.True);
            Assert.That(config.EnableLateRaidSprintBlock, Is.True);
            Assert.That(config.EnableSuspicionSprintBlock, Is.True);
            Assert.That(config.EnableDoorSprintPause, Is.True);
        });
    }

    #endregion

    #region Item 2: Forced-Open Timer Config

    [Test]
    public void UnlockingDoorsConfig_ForcedOpenPeriod_DefaultsTo15()
    {
        var config = new UnlockingDoorsConfig();
        Assert.That(config.ForcedOpenPeriod, Is.EqualTo(15f));
    }

    [Test]
    public void UnlockingDoorsConfig_ForcedOpenPeriod_DeserializesFromJson()
    {
        var json = @"{ ""forced_open_period"": 20 }";
        var config = JsonConvert.DeserializeObject<UnlockingDoorsConfig>(json);
        Assert.That(config.ForcedOpenPeriod, Is.EqualTo(20f));
    }

    [Test]
    public void UnlockingDoorsConfig_ForcedOpenPeriod_SerializesWithCorrectName()
    {
        var config = new UnlockingDoorsConfig();
        var json = JsonConvert.SerializeObject(config);
        Assert.That(json, Does.Contain("forced_open_period"));
    }

    #endregion

    #region Item 3: Door Sound Config

    [Test]
    public void UnlockingDoorsConfig_DoorSoundPower_DefaultsTo50()
    {
        var config = new UnlockingDoorsConfig();
        Assert.That(config.DoorSoundPower, Is.EqualTo(50f));
    }

    [Test]
    public void UnlockingDoorsConfig_EnableDoorSoundEvents_DefaultsToTrue()
    {
        var config = new UnlockingDoorsConfig();
        Assert.That(config.EnableDoorSoundEvents, Is.True);
    }

    [Test]
    public void UnlockingDoorsConfig_DoorSoundConfig_DeserializesFromJson()
    {
        var json = @"{ ""door_sound_power"": 75, ""enable_door_sound_events"": false }";
        var config = JsonConvert.DeserializeObject<UnlockingDoorsConfig>(json);
        Assert.Multiple(() =>
        {
            Assert.That(config.DoorSoundPower, Is.EqualTo(75f));
            Assert.That(config.EnableDoorSoundEvents, Is.False);
        });
    }

    [Test]
    public void UnlockingDoorsConfig_DoorSoundConfig_SerializesWithCorrectNames()
    {
        var config = new UnlockingDoorsConfig();
        var json = JsonConvert.SerializeObject(config);
        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("door_sound_power"));
            Assert.That(json, Does.Contain("enable_door_sound_events"));
        });
    }

    #endregion

    #region Item 4: Breach Chance (source code structure test)

    [Test]
    public void UnlockDoorAction_ReferencesBreachChance()
    {
        // Verify the source code reads BREACH_CHANCE_100 from bot settings
        var sourcePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SPTQuestingBots.Client",
            "BotLogic",
            "Objective",
            "UnlockDoorAction.cs"
        );
        var source = File.ReadAllText(sourcePath);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("BREACH_CHANCE_100"), "UnlockDoorAction should read BREACH_CHANCE_100 from bot settings");
            Assert.That(
                source,
                Does.Contain("BotOwner.Settings.FileSettings.Move.BREACH_CHANCE_100"),
                "Should access breach chance via BotOwner.Settings.FileSettings.Move path"
            );
        });
    }

    [Test]
    public void UnlockDoorAction_BreachDecision_UsesSharedRandom()
    {
        // Verify breach decision uses SharedRandom (not new Random per call)
        var sourcePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SPTQuestingBots.Client",
            "BotLogic",
            "Objective",
            "UnlockDoorAction.cs"
        );
        var source = File.ReadAllText(sourcePath);

        Assert.That(
            source,
            Does.Contain("SharedRandom.Next(0, 100)"),
            "Breach roll should use SharedRandom.Next(0, 100) for proper distribution"
        );
    }

    #endregion

    #region Item 5: Room Clear Parameter Tuning

    [Test]
    public void RoomClearConfig_WalkThroughDistance_DefaultsTo075()
    {
        var config = new RoomClearConfig();
        Assert.That(config.WalkThroughDistance, Is.EqualTo(0.75f));
    }

    [Test]
    public void RoomClearConfig_LookRaycastDistance_DefaultsTo30()
    {
        var config = new RoomClearConfig();
        Assert.That(config.LookRaycastDistance, Is.EqualTo(30.0f));
    }

    [Test]
    public void RoomClearConfig_LookDuration_DefaultsTo12()
    {
        var config = new RoomClearConfig();
        Assert.That(config.LookDuration, Is.EqualTo(1.2f));
    }

    [Test]
    public void RoomClearConfig_CornerPauseDuration_AlignedWithBsg()
    {
        // BSG uses 1.2s for look duration per direction
        var config = new RoomClearConfig();
        Assert.That(config.CornerPauseDuration, Is.EqualTo(1.2f), "Corner pause should match BSG's 1.2s look duration");
    }

    [Test]
    public void RoomClearConfig_NewFields_DeserializeFromJson()
    {
        var json =
            @"{
            ""walk_through_distance"": 1.0,
            ""look_raycast_distance"": 25.0,
            ""look_duration"": 1.5
        }";
        var config = JsonConvert.DeserializeObject<RoomClearConfig>(json);
        Assert.Multiple(() =>
        {
            Assert.That(config.WalkThroughDistance, Is.EqualTo(1.0f));
            Assert.That(config.LookRaycastDistance, Is.EqualTo(25.0f));
            Assert.That(config.LookDuration, Is.EqualTo(1.5f));
        });
    }

    [Test]
    public void RoomClearConfig_NewFields_SerializeWithCorrectNames()
    {
        var config = new RoomClearConfig();
        var json = JsonConvert.SerializeObject(config);
        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("walk_through_distance"));
            Assert.That(json, Does.Contain("look_raycast_distance"));
            Assert.That(json, Does.Contain("look_duration"));
        });
    }

    [Test]
    public void RoomClearConfig_NewFields_RoundTrip()
    {
        var original = new RoomClearConfig
        {
            WalkThroughDistance = 1.5f,
            LookRaycastDistance = 40f,
            LookDuration = 2.0f,
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<RoomClearConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.WalkThroughDistance, Is.EqualTo(1.5f));
            Assert.That(deserialized.LookRaycastDistance, Is.EqualTo(40f));
            Assert.That(deserialized.LookDuration, Is.EqualTo(2.0f));
        });
    }

    #endregion

    #region Config JSON Consistency

    [Test]
    public void ConfigJson_SprintingLimitations_HasDoorSprintPause()
    {
        var configJson = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "config", "config.json")
        );

        Assert.That(configJson, Does.Contain("enable_door_sprint_pause"));
    }

    [Test]
    public void ConfigJson_UnlockingDoors_HasForcedOpenPeriod()
    {
        var configJson = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "config", "config.json")
        );

        Assert.Multiple(() =>
        {
            Assert.That(configJson, Does.Contain("forced_open_period"));
            Assert.That(configJson, Does.Contain("door_sound_power"));
            Assert.That(configJson, Does.Contain("enable_door_sound_events"));
        });
    }

    [Test]
    public void ConfigJson_RoomClear_HasNewParameters()
    {
        var configJson = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "config", "config.json")
        );

        Assert.Multiple(() =>
        {
            Assert.That(configJson, Does.Contain("walk_through_distance"));
            Assert.That(configJson, Does.Contain("look_raycast_distance"));
            Assert.That(configJson, Does.Contain("look_duration"));
        });
    }

    [Test]
    public void ConfigJson_ApproachingClosedDoors_MatchesBsgValues()
    {
        var configJson = File.ReadAllText(
            Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "config", "config.json")
        );
        var configObj = JsonConvert.DeserializeObject<dynamic>(configJson);

        float distance = (float)configObj.questing.sprinting_limitations.approaching_closed_doors.distance;
        float angle = (float)configObj.questing.sprinting_limitations.approaching_closed_doors.angle;

        // BSG: STOP_SPRINT_DIST_SQR = 27.04 -> sqrt = 5.2m, angle ~20 degrees
        Assert.Multiple(() =>
        {
            Assert.That(distance, Is.EqualTo(5.2f).Within(0.1f), "Door sprint pause distance should match BSG's 5.2m");
            Assert.That(angle, Is.EqualTo(20f), "Door sprint pause angle should match BSG's ~20 degree facing threshold");
        });
    }

    #endregion

    #region DoorInteractionSubscriber Source Structure

    [Test]
    public void DoorInteractionSubscriber_SubscribesToOnDoorInteracted()
    {
        var sourcePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SPTQuestingBots.Client",
            "Helpers",
            "DoorInteractionSubscriber.cs"
        );
        var source = File.ReadAllText(sourcePath);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("OnDoorInteracted"), "Should subscribe to BotEventHandler.OnDoorInteracted");
            Assert.That(source, Does.Contain("CombatEventRegistry.RecordEvent"), "Should record combat events for door sounds");
            Assert.That(source, Does.Contain("CombatEventType.DoorOpen"), "Should use DoorOpen event type");
            Assert.That(source, Does.Contain("_doorOpenTimes"), "Should track door open times");
            Assert.That(source, Does.Contain("WasRecentlyOpened"), "Should expose WasRecentlyOpened query method");
        });
    }

    [Test]
    public void CloseNearbyDoorsAction_ChecksForcedOpenPeriod()
    {
        var sourcePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SPTQuestingBots.Client",
            "BotLogic",
            "Objective",
            "CloseNearbyDoorsAction.cs"
        );
        var source = File.ReadAllText(sourcePath);

        Assert.Multiple(() =>
        {
            Assert.That(source, Does.Contain("ForcedOpenPeriod"), "Should read ForcedOpenPeriod from config");
            Assert.That(
                source,
                Does.Contain("DoorInteractionSubscriber.WasRecentlyOpened"),
                "Should check if door was recently opened before closing"
            );
        });
    }

    #endregion

    #region Integration: LocationData and HiveMind lifecycle

    [Test]
    public void LocationData_SubscribesDoorInteractionSubscriber()
    {
        var sourcePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SPTQuestingBots.Client",
            "Components",
            "LocationData.cs"
        );
        var source = File.ReadAllText(sourcePath);

        Assert.That(source, Does.Contain("DoorInteractionSubscriber.Subscribe()"), "LocationData should subscribe at raid start");
    }

    [Test]
    public void BotHiveMindMonitor_ClearsDoorInteractionSubscriber()
    {
        var sourcePath = Path.Combine(
            TestContext.CurrentContext.TestDirectory,
            "..",
            "..",
            "..",
            "..",
            "..",
            "src",
            "SPTQuestingBots.Client",
            "BotLogic",
            "HiveMind",
            "BotHiveMindMonitor.cs"
        );
        var source = File.ReadAllText(sourcePath);

        Assert.That(
            source,
            Does.Contain("DoorInteractionSubscriber.Clear()"),
            "BotHiveMindMonitor should clear DoorInteractionSubscriber at raid end"
        );
    }

    #endregion
}
