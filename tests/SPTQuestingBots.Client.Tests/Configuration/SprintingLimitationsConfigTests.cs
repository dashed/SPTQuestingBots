using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class SprintingLimitationsConfigTests
{
    #region Default constructor

    [Test]
    public void DefaultConstructor_SetsCorrectDefaults()
    {
        var config = new SprintingLimitationsConfig();

        Assert.Multiple(() =>
        {
            Assert.That(config.EnableDebounceTime, Is.EqualTo(3f));
            Assert.That(config.Stamina, Is.Not.Null);
            Assert.That(config.SharpPathCorners, Is.Not.Null);
            Assert.That(config.ApproachingClosedDoors, Is.Not.Null);
            Assert.That(config.PostCombatCooldownSeconds, Is.EqualTo(20f));
            Assert.That(config.LateRaidNoSprintThreshold, Is.EqualTo(0.15f));
            Assert.That(config.EnablePostCombatSprintBlock, Is.True);
            Assert.That(config.EnableLateRaidSprintBlock, Is.True);
            Assert.That(config.EnableSuspicionSprintBlock, Is.True);
        });
    }

    #endregion

    #region Config toggle defaults — sprint allowed when toggle is off

    [Test]
    public void EnablePostCombatSprintBlock_DefaultsToTrue()
    {
        var config = new SprintingLimitationsConfig();
        Assert.That(config.EnablePostCombatSprintBlock, Is.True);
    }

    [Test]
    public void EnableLateRaidSprintBlock_DefaultsToTrue()
    {
        var config = new SprintingLimitationsConfig();
        Assert.That(config.EnableLateRaidSprintBlock, Is.True);
    }

    [Test]
    public void EnableSuspicionSprintBlock_DefaultsToTrue()
    {
        var config = new SprintingLimitationsConfig();
        Assert.That(config.EnableSuspicionSprintBlock, Is.True);
    }

    #endregion

    #region JSON deserialization — empty/missing fields

    [Test]
    public void Deserialize_EmptyJson_AllTogglesDefaultTrue()
    {
        var config = JsonConvert.DeserializeObject<SprintingLimitationsConfig>("{}");

        Assert.Multiple(() =>
        {
            Assert.That(config.EnablePostCombatSprintBlock, Is.True);
            Assert.That(config.EnableLateRaidSprintBlock, Is.True);
            Assert.That(config.EnableSuspicionSprintBlock, Is.True);
        });
    }

    [Test]
    public void Deserialize_MissingToggleFields_DefaultsToTrue()
    {
        // Simulates an old config.json that predates the toggle fields
        var json =
            @"{
                ""post_combat_cooldown_seconds"": 20,
                ""late_raid_no_sprint_threshold"": 0.15
            }";
        var config = JsonConvert.DeserializeObject<SprintingLimitationsConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.EnablePostCombatSprintBlock, Is.True);
            Assert.That(config.EnableLateRaidSprintBlock, Is.True);
            Assert.That(config.EnableSuspicionSprintBlock, Is.True);
            Assert.That(config.PostCombatCooldownSeconds, Is.EqualTo(20f));
            Assert.That(config.LateRaidNoSprintThreshold, Is.EqualTo(0.15f));
        });
    }

    [Test]
    public void Deserialize_EmptyJson_AllFieldsUseDefaults()
    {
        var config = JsonConvert.DeserializeObject<SprintingLimitationsConfig>("{}");

        Assert.Multiple(() =>
        {
            Assert.That(config.EnableDebounceTime, Is.EqualTo(3f));
            Assert.That(config.Stamina, Is.Not.Null);
            Assert.That(config.SharpPathCorners, Is.Not.Null);
            Assert.That(config.ApproachingClosedDoors, Is.Not.Null);
            Assert.That(config.PostCombatCooldownSeconds, Is.EqualTo(20f));
            Assert.That(config.LateRaidNoSprintThreshold, Is.EqualTo(0.15f));
            Assert.That(config.EnablePostCombatSprintBlock, Is.True);
            Assert.That(config.EnableLateRaidSprintBlock, Is.True);
            Assert.That(config.EnableSuspicionSprintBlock, Is.True);
        });
    }

    #endregion

    #region Config toggle ON/OFF combinations

    [Test]
    public void Deserialize_AllTogglesOff()
    {
        var json =
            @"{
                ""enable_post_combat_sprint_block"": false,
                ""enable_late_raid_sprint_block"": false,
                ""enable_suspicion_sprint_block"": false
            }";
        var config = JsonConvert.DeserializeObject<SprintingLimitationsConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.EnablePostCombatSprintBlock, Is.False);
            Assert.That(config.EnableLateRaidSprintBlock, Is.False);
            Assert.That(config.EnableSuspicionSprintBlock, Is.False);
        });
    }

    [Test]
    public void Deserialize_AllTogglesOn()
    {
        var json =
            @"{
                ""enable_post_combat_sprint_block"": true,
                ""enable_late_raid_sprint_block"": true,
                ""enable_suspicion_sprint_block"": true
            }";
        var config = JsonConvert.DeserializeObject<SprintingLimitationsConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.EnablePostCombatSprintBlock, Is.True);
            Assert.That(config.EnableLateRaidSprintBlock, Is.True);
            Assert.That(config.EnableSuspicionSprintBlock, Is.True);
        });
    }

    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    [TestCase(false, false, true)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    [TestCase(false, true, true)]
    public void Deserialize_IndividualToggleCombinations(bool postCombat, bool lateRaid, bool suspicion)
    {
        var json =
            $@"{{
                ""enable_post_combat_sprint_block"": {postCombat.ToString().ToLower()},
                ""enable_late_raid_sprint_block"": {lateRaid.ToString().ToLower()},
                ""enable_suspicion_sprint_block"": {suspicion.ToString().ToLower()}
            }}";
        var config = JsonConvert.DeserializeObject<SprintingLimitationsConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.EnablePostCombatSprintBlock, Is.EqualTo(postCombat));
            Assert.That(config.EnableLateRaidSprintBlock, Is.EqualTo(lateRaid));
            Assert.That(config.EnableSuspicionSprintBlock, Is.EqualTo(suspicion));
        });
    }

    #endregion

    #region Partial override — toggles off, other defaults intact

    [Test]
    public void Deserialize_PartialOverride_TogglesOnlyOtherDefaultsIntact()
    {
        var json =
            @"{
                ""enable_post_combat_sprint_block"": false,
                ""enable_late_raid_sprint_block"": false,
                ""enable_suspicion_sprint_block"": false
            }";
        var config = JsonConvert.DeserializeObject<SprintingLimitationsConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.EnablePostCombatSprintBlock, Is.False);
            Assert.That(config.EnableLateRaidSprintBlock, Is.False);
            Assert.That(config.EnableSuspicionSprintBlock, Is.False);
            // Other fields should retain defaults
            Assert.That(config.EnableDebounceTime, Is.EqualTo(3f));
            Assert.That(config.PostCombatCooldownSeconds, Is.EqualTo(20f));
            Assert.That(config.LateRaidNoSprintThreshold, Is.EqualTo(0.15f));
        });
    }

    #endregion

    #region Full deserialization

    [Test]
    public void Deserialize_AllProperties_FromJson()
    {
        var json =
            @"{
                ""enable_debounce_time"": 3,
                ""stamina"": { ""min"": 0.1, ""max"": 0.5 },
                ""sharp_path_corners"": { ""distance"": 2, ""angle"": 45 },
                ""approaching_closed_doors"": { ""distance"": 3, ""angle"": 60 },
                ""post_combat_cooldown_seconds"": 30,
                ""late_raid_no_sprint_threshold"": 0.2,
                ""enable_post_combat_sprint_block"": false,
                ""enable_late_raid_sprint_block"": false,
                ""enable_suspicion_sprint_block"": false
            }";
        var config = JsonConvert.DeserializeObject<SprintingLimitationsConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.EnableDebounceTime, Is.EqualTo(3f));
            Assert.That(config.Stamina.Min, Is.EqualTo(0.1).Within(0.01));
            Assert.That(config.Stamina.Max, Is.EqualTo(0.5).Within(0.01));
            Assert.That(config.SharpPathCorners.Distance, Is.EqualTo(2f));
            Assert.That(config.SharpPathCorners.Angle, Is.EqualTo(45f));
            Assert.That(config.ApproachingClosedDoors.Distance, Is.EqualTo(3f));
            Assert.That(config.ApproachingClosedDoors.Angle, Is.EqualTo(60f));
            Assert.That(config.PostCombatCooldownSeconds, Is.EqualTo(30f));
            Assert.That(config.LateRaidNoSprintThreshold, Is.EqualTo(0.2f).Within(0.01f));
            Assert.That(config.EnablePostCombatSprintBlock, Is.False);
            Assert.That(config.EnableLateRaidSprintBlock, Is.False);
            Assert.That(config.EnableSuspicionSprintBlock, Is.False);
        });
    }

    #endregion

    #region Round-trip serialization

    [Test]
    public void RoundTrip_PreservesAllValues()
    {
        var original = new SprintingLimitationsConfig
        {
            EnableDebounceTime = 5f,
            PostCombatCooldownSeconds = 30f,
            LateRaidNoSprintThreshold = 0.25f,
            EnablePostCombatSprintBlock = false,
            EnableLateRaidSprintBlock = false,
            EnableSuspicionSprintBlock = false,
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<SprintingLimitationsConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.EnableDebounceTime, Is.EqualTo(original.EnableDebounceTime));
            Assert.That(deserialized.PostCombatCooldownSeconds, Is.EqualTo(original.PostCombatCooldownSeconds));
            Assert.That(deserialized.LateRaidNoSprintThreshold, Is.EqualTo(original.LateRaidNoSprintThreshold));
            Assert.That(deserialized.EnablePostCombatSprintBlock, Is.EqualTo(original.EnablePostCombatSprintBlock));
            Assert.That(deserialized.EnableLateRaidSprintBlock, Is.EqualTo(original.EnableLateRaidSprintBlock));
            Assert.That(deserialized.EnableSuspicionSprintBlock, Is.EqualTo(original.EnableSuspicionSprintBlock));
        });
    }

    [Test]
    public void RoundTrip_TogglesTrue_PreservesValues()
    {
        var original = new SprintingLimitationsConfig
        {
            EnablePostCombatSprintBlock = true,
            EnableLateRaidSprintBlock = true,
            EnableSuspicionSprintBlock = true,
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<SprintingLimitationsConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.EnablePostCombatSprintBlock, Is.True);
            Assert.That(deserialized.EnableLateRaidSprintBlock, Is.True);
            Assert.That(deserialized.EnableSuspicionSprintBlock, Is.True);
        });
    }

    #endregion

    #region JSON property names

    [Test]
    public void Serialize_IncludesJsonPropertyNames()
    {
        var config = new SprintingLimitationsConfig();
        var json = JsonConvert.SerializeObject(config);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("enable_debounce_time"));
            Assert.That(json, Does.Contain("stamina"));
            Assert.That(json, Does.Contain("sharp_path_corners"));
            Assert.That(json, Does.Contain("approaching_closed_doors"));
            Assert.That(json, Does.Contain("post_combat_cooldown_seconds"));
            Assert.That(json, Does.Contain("late_raid_no_sprint_threshold"));
            Assert.That(json, Does.Contain("enable_post_combat_sprint_block"));
            Assert.That(json, Does.Contain("enable_late_raid_sprint_block"));
            Assert.That(json, Does.Contain("enable_suspicion_sprint_block"));
        });
    }

    #endregion
}
