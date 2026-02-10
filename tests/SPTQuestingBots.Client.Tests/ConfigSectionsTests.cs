using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests
{
    [TestFixture]
    public class ConfigSectionsTests
    {
        private static readonly FieldInfo[] SectionFields = typeof(ConfigSections)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .ToArray();

        [Test]
        public void AllSections_MatchNumberedPrefixPattern()
        {
            var pattern = new Regex(@"^\d{2}\. .+$");
            foreach (var field in SectionFields)
            {
                var value = (string)field.GetValue(null);
                Assert.That(pattern.IsMatch(value), $"Section '{field.Name}' value '{value}' does not match pattern 'NN. Name'");
            }
        }

        [Test]
        public void AllSections_HaveUniqueNumericPrefixes()
        {
            var prefixes = SectionFields.Select(f => ((string)f.GetValue(null)).Substring(0, 2)).ToList();

            Assert.That(
                prefixes.Distinct().Count(),
                Is.EqualTo(prefixes.Count),
                "Duplicate numeric prefixes found: "
                    + string.Join(", ", prefixes.GroupBy(p => p).Where(g => g.Count() > 1).Select(g => g.Key))
            );
        }

        [Test]
        public void AllSections_AreInAscendingOrder()
        {
            var values = SectionFields.Select(f => (string)f.GetValue(null)).ToList();

            for (int i = 1; i < values.Count; i++)
            {
                Assert.That(
                    string.Compare(values[i - 1], values[i], System.StringComparison.Ordinal),
                    Is.LessThan(0),
                    $"Section '{values[i - 1]}' should come before '{values[i]}' but doesn't"
                );
            }
        }

        [Test]
        public void AllSections_HaveUniqueValues()
        {
            var values = SectionFields.Select(f => (string)f.GetValue(null)).ToList();

            Assert.That(values.Distinct().Count(), Is.EqualTo(values.Count), "Duplicate section values found");
        }

        [Test]
        public void SectionCount_IsExactly21()
        {
            Assert.That(SectionFields.Length, Is.EqualTo(21));
        }

        [Test]
        public void NumericPrefixes_AreContiguousFrom01To21()
        {
            var prefixes = SectionFields.Select(f => int.Parse(((string)f.GetValue(null)).Substring(0, 2))).OrderBy(n => n).ToList();

            var expected = Enumerable.Range(1, 21).ToList();
            Assert.That(prefixes, Is.EqualTo(expected));
        }

        [TestCase(nameof(ConfigSections.General), "01. General")]
        [TestCase(nameof(ConfigSections.BotSpawns), "02. Bot Spawns")]
        [TestCase(nameof(ConfigSections.PMCSpawns), "03. PMC Spawns")]
        [TestCase(nameof(ConfigSections.PScavSpawns), "04. PScav Spawns")]
        [TestCase(nameof(ConfigSections.ScavLimits), "05. Scav Limits")]
        [TestCase(nameof(ConfigSections.BotPathing), "06. Bot Pathing")]
        [TestCase(nameof(ConfigSections.BotLOD), "07. Bot LOD")]
        [TestCase(nameof(ConfigSections.Looting), "08. Looting")]
        [TestCase(nameof(ConfigSections.Vulture), "09. Vulture")]
        [TestCase(nameof(ConfigSections.Investigate), "10. Investigate")]
        [TestCase(nameof(ConfigSections.Linger), "11. Linger")]
        [TestCase(nameof(ConfigSections.SpawnEntry), "12. Spawn Entry")]
        [TestCase(nameof(ConfigSections.RoomClear), "13. Room Clear")]
        [TestCase(nameof(ConfigSections.Patrol), "14. Patrol")]
        [TestCase(nameof(ConfigSections.DynamicObjectives), "15. Dynamic Objectives")]
        [TestCase(nameof(ConfigSections.Personality), "16. Personality")]
        [TestCase(nameof(ConfigSections.LookVariance), "17. Look Variance")]
        [TestCase(nameof(ConfigSections.ZoneMovement), "18. Zone Movement")]
        [TestCase(nameof(ConfigSections.AILimiter), "19. AI Limiter")]
        [TestCase(nameof(ConfigSections.Debug), "20. Debug")]
        [TestCase(nameof(ConfigSections.CustomQuestLocations), "21. Custom Quest Locations")]
        public void Section_HasExpectedValue(string fieldName, string expectedValue)
        {
            var field = typeof(ConfigSections).GetField(fieldName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(field, Is.Not.Null, $"Field '{fieldName}' not found");
            Assert.That(field.GetValue(null), Is.EqualTo(expectedValue));
        }
    }
}
