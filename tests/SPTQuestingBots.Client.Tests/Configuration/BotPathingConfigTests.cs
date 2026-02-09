using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration;

[TestFixture]
public class BotPathingConfigTests
{
    [Test]
    public void DefaultConstructor_BypassDoorColliders_IsTrue()
    {
        var config = new BotPathingConfig();
        Assert.That(config.BypassDoorColliders, Is.True);
    }

    [Test]
    public void DefaultConstructor_UseCustomMover_IsTrue()
    {
        var config = new BotPathingConfig();
        Assert.That(config.UseCustomMover, Is.True);
    }

    [Test]
    public void DefaultConstructor_MaxStartPositionDiscrepancy_IsHalf()
    {
        var config = new BotPathingConfig();
        Assert.That(config.MaxStartPositionDiscrepancy, Is.EqualTo(0.5f));
    }

    [Test]
    public void Deserialize_EmptyJson_UsesDefaults()
    {
        var config = JsonConvert.DeserializeObject<BotPathingConfig>("{}");

        Assert.Multiple(() =>
        {
            Assert.That(config.UseCustomMover, Is.True);
            Assert.That(config.BypassDoorColliders, Is.True);
            Assert.That(config.MaxStartPositionDiscrepancy, Is.EqualTo(0.5f));
            Assert.That(config.IncompletePathRetryInterval, Is.EqualTo(5));
        });
    }

    [Test]
    public void Deserialize_BypassDoorCollidersFalse_OverridesDefault()
    {
        var json = """{ "bypass_door_colliders": false }""";
        var config = JsonConvert.DeserializeObject<BotPathingConfig>(json);
        Assert.That(config.BypassDoorColliders, Is.False);
    }

    [Test]
    public void Deserialize_BypassDoorCollidersTrue_ExplicitlySet()
    {
        var json = """{ "bypass_door_colliders": true }""";
        var config = JsonConvert.DeserializeObject<BotPathingConfig>(json);
        Assert.That(config.BypassDoorColliders, Is.True);
    }

    [Test]
    public void Deserialize_UseCustomMoverFalse_OverridesDefault()
    {
        var json = """{ "use_custom_mover": false }""";
        var config = JsonConvert.DeserializeObject<BotPathingConfig>(json);
        Assert.That(config.UseCustomMover, Is.False);
    }

    [Test]
    public void RoundTrip_PreservesAllValues()
    {
        var original = new BotPathingConfig
        {
            MaxStartPositionDiscrepancy = 1.0f,
            IncompletePathRetryInterval = 10,
            UseCustomMover = false,
            BypassDoorColliders = false,
        };

        var json = JsonConvert.SerializeObject(original);
        var deserialized = JsonConvert.DeserializeObject<BotPathingConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(deserialized.MaxStartPositionDiscrepancy, Is.EqualTo(1.0f));
            Assert.That(deserialized.IncompletePathRetryInterval, Is.EqualTo(10));
            Assert.That(deserialized.UseCustomMover, Is.False);
            Assert.That(deserialized.BypassDoorColliders, Is.False);
        });
    }

    [Test]
    public void Serialize_IncludesJsonPropertyNames()
    {
        var config = new BotPathingConfig();
        var json = JsonConvert.SerializeObject(config);

        Assert.Multiple(() =>
        {
            Assert.That(json, Does.Contain("bypass_door_colliders"));
            Assert.That(json, Does.Contain("use_custom_mover"));
            Assert.That(json, Does.Contain("max_start_position_discrepancy"));
            Assert.That(json, Does.Contain("incomplete_path_retry_interval"));
        });
    }

    [Test]
    public void Deserialize_RealConfigJsonBotPathing_WorksWithOptionalFields()
    {
        // Simulates the actual config.json bot_pathing section (which omits client-only fields)
        var json = """
            {
                "max_start_position_discrepancy": 0.5,
                "incomplete_path_retry_interval": 5
            }
            """;

        var config = JsonConvert.DeserializeObject<BotPathingConfig>(json);

        Assert.Multiple(() =>
        {
            Assert.That(config.MaxStartPositionDiscrepancy, Is.EqualTo(0.5f));
            Assert.That(config.IncompletePathRetryInterval, Is.EqualTo(5));
            // Client-only fields should use defaults when absent from JSON
            Assert.That(config.UseCustomMover, Is.True);
            Assert.That(config.BypassDoorColliders, Is.True);
        });
    }
}
