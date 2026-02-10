using System.IO;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Configuration
{
    [TestFixture]
    public class QuestingConfigTests
    {
        private static JObject LoadConfigQuesting()
        {
            // Walk up from test bin dir to repo root, then into config/
            var dir = TestContext.CurrentContext.TestDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "config", "config.json")))
                dir = Directory.GetParent(dir)?.FullName;

            Assert.That(dir, Is.Not.Null, "Could not find config/config.json from test directory");
            var json = File.ReadAllText(Path.Combine(dir, "config", "config.json"));
            var root = JObject.Parse(json);
            return (JObject)root["questing"];
        }

        [Test]
        public void ConfigJson_ContainsWaitTimeMinField()
        {
            var questing = LoadConfigQuesting();

            Assert.That(questing["wait_time_min"], Is.Not.Null, "wait_time_min missing from config.json");
            Assert.That(questing["wait_time_min"].Value<float>(), Is.EqualTo(5f));
        }

        [Test]
        public void ConfigJson_ContainsWaitTimeMaxField()
        {
            var questing = LoadConfigQuesting();

            Assert.That(questing["wait_time_max"], Is.Not.Null, "wait_time_max missing from config.json");
            Assert.That(questing["wait_time_max"].Value<float>(), Is.EqualTo(15f));
        }

        [Test]
        public void ConfigJson_SpawnPointWanderDesirabilityIsNonZero()
        {
            var questing = LoadConfigQuesting();
            var spawnWander = (JObject)questing["bot_quests"]["spawn_point_wander"];

            Assert.That(spawnWander, Is.Not.Null, "spawn_point_wander missing from config.json");
            Assert.That(spawnWander["desirability"].Value<float>(), Is.EqualTo(3f));
        }

        [Test]
        public void ConfigJson_WaitTimeMinLessThanMax()
        {
            var questing = LoadConfigQuesting();
            var min = questing["wait_time_min"].Value<float>();
            var max = questing["wait_time_max"].Value<float>();

            Assert.That(min, Is.LessThan(max), "wait_time_min should be less than wait_time_max");
        }

        [Test]
        public void ConfigJson_WaitTimesArePositive()
        {
            var questing = LoadConfigQuesting();

            Assert.Multiple(() =>
            {
                Assert.That(questing["wait_time_min"].Value<float>(), Is.GreaterThan(0f), "wait_time_min should be positive");
                Assert.That(questing["wait_time_max"].Value<float>(), Is.GreaterThan(0f), "wait_time_max should be positive");
            });
        }
    }
}
