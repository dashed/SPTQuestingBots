using Newtonsoft.Json;
using NUnit.Framework;
using SPTQuestingBots.Configuration;

namespace SPTQuestingBots.Client.Tests.Configuration
{
    [TestFixture]
    public class BotLodConfigTests
    {
        [Test]
        public void DefaultConstructor_SetsCorrectDefaults()
        {
            var config = new BotLodConfig();

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.ReducedDistance, Is.EqualTo(150f));
                Assert.That(config.MinimalDistance, Is.EqualTo(300f));
                Assert.That(config.ReducedFrameSkip, Is.EqualTo(2));
                Assert.That(config.MinimalFrameSkip, Is.EqualTo(4));
            });
        }

        [Test]
        public void Deserialize_EmptyJson_UsesDefaults()
        {
            var config = JsonConvert.DeserializeObject<BotLodConfig>("{}");

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.True);
                Assert.That(config.ReducedDistance, Is.EqualTo(150f));
                Assert.That(config.MinimalDistance, Is.EqualTo(300f));
                Assert.That(config.ReducedFrameSkip, Is.EqualTo(2));
                Assert.That(config.MinimalFrameSkip, Is.EqualTo(4));
            });
        }

        [Test]
        public void Deserialize_OverrideValues()
        {
            var json =
                """{ "enabled": false, "reduced_distance": 100, "minimal_distance": 200, "reduced_frame_skip": 3, "minimal_frame_skip": 6 }""";
            var config = JsonConvert.DeserializeObject<BotLodConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.ReducedDistance, Is.EqualTo(100f));
                Assert.That(config.MinimalDistance, Is.EqualTo(200f));
                Assert.That(config.ReducedFrameSkip, Is.EqualTo(3));
                Assert.That(config.MinimalFrameSkip, Is.EqualTo(6));
            });
        }

        [Test]
        public void RoundTrip_PreservesAllValues()
        {
            var original = new BotLodConfig
            {
                Enabled = false,
                ReducedDistance = 120f,
                MinimalDistance = 250f,
                ReducedFrameSkip = 3,
                MinimalFrameSkip = 7,
            };

            var json = JsonConvert.SerializeObject(original);
            var deserialized = JsonConvert.DeserializeObject<BotLodConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized.Enabled, Is.False);
                Assert.That(deserialized.ReducedDistance, Is.EqualTo(120f));
                Assert.That(deserialized.MinimalDistance, Is.EqualTo(250f));
                Assert.That(deserialized.ReducedFrameSkip, Is.EqualTo(3));
                Assert.That(deserialized.MinimalFrameSkip, Is.EqualTo(7));
            });
        }

        [Test]
        public void Serialize_IncludesJsonPropertyNames()
        {
            var config = new BotLodConfig();
            var json = JsonConvert.SerializeObject(config);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("enabled"));
                Assert.That(json, Does.Contain("reduced_distance"));
                Assert.That(json, Does.Contain("minimal_distance"));
                Assert.That(json, Does.Contain("reduced_frame_skip"));
                Assert.That(json, Does.Contain("minimal_frame_skip"));
            });
        }

        [Test]
        public void Deserialize_PartialOverride_OtherDefaultsIntact()
        {
            var json = """{ "enabled": false }""";
            var config = JsonConvert.DeserializeObject<BotLodConfig>(json);

            Assert.Multiple(() =>
            {
                Assert.That(config.Enabled, Is.False);
                Assert.That(config.ReducedDistance, Is.EqualTo(150f));
                Assert.That(config.MinimalDistance, Is.EqualTo(300f));
                Assert.That(config.ReducedFrameSkip, Is.EqualTo(2));
                Assert.That(config.MinimalFrameSkip, Is.EqualTo(4));
            });
        }
    }
}
