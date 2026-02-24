using System.Reflection;
using Newtonsoft.Json;
using NSubstitute;
using NUnit.Framework;
using SPTarkov.Server.Core.Models.Utils;
using SPTQuestingBots.Server.Configuration;

namespace SPTQuestingBots.Server.Tests.Configuration;

/// <summary>
/// Tests for <see cref="QuestingBotsConfigLoader"/>, focused on resilience
/// against corrupted or malformed JSON input.
/// </summary>
[TestFixture]
public class ConfigLoaderTests
{
    private ISptLogger<QuestingBotsConfigLoader> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = Substitute.For<ISptLogger<QuestingBotsConfigLoader>>();
    }

    // ── LoadJsonFile — corrupted JSON resilience ────────────────────
    // LoadJsonFile resolves paths via GetModPath() which uses Assembly.Location.
    // We write test files relative to that path to exercise the real method.

    [Test]
    public void LoadJsonFile_WithCorruptJson_ReturnsNull_DoesNotThrow()
    {
        var modPath = QuestingBotsConfigLoader.GetModPath();
        var testFile = Path.Combine(modPath, "test_corrupt.json");
        File.WriteAllText(testFile, "NOT VALID JSON AT ALL");

        try
        {
            var loader = new QuestingBotsConfigLoader(_logger);
            var result = loader.LoadJsonFile<Dictionary<string, object>>("test_corrupt.json");

            Assert.That(result, Is.Null);
            _logger.Received().Error(Arg.Is<string>(s => s.Contains("Failed to parse")));
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Test]
    public void LoadJsonFile_WithWhitespaceOnly_ReturnsNull_DoesNotThrow()
    {
        var modPath = QuestingBotsConfigLoader.GetModPath();
        var testFile = Path.Combine(modPath, "test_whitespace.json");
        File.WriteAllText(testFile, "   \n\t  ");

        try
        {
            var loader = new QuestingBotsConfigLoader(_logger);

            Assert.DoesNotThrow(() =>
            {
                var result = loader.LoadJsonFile<Dictionary<string, string>>("test_whitespace.json");
                Assert.That(result, Is.Null);
            });
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Test]
    public void LoadJsonFile_WithTruncatedJson_ReturnsNull_DoesNotThrow()
    {
        var modPath = QuestingBotsConfigLoader.GetModPath();
        var testFile = Path.Combine(modPath, "test_truncated.json");
        File.WriteAllText(testFile, """{ "key": "val""");

        try
        {
            var loader = new QuestingBotsConfigLoader(_logger);
            var result = loader.LoadJsonFile<Dictionary<string, string>>("test_truncated.json");

            Assert.That(result, Is.Null);
            _logger.Received().Error(Arg.Is<string>(s => s.Contains("Failed to parse")));
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Test]
    public void LoadJsonFile_WithEmptyString_ReturnsNull_DoesNotThrow()
    {
        var modPath = QuestingBotsConfigLoader.GetModPath();
        var testFile = Path.Combine(modPath, "test_empty.json");
        File.WriteAllText(testFile, "");

        try
        {
            var loader = new QuestingBotsConfigLoader(_logger);

            Assert.DoesNotThrow(() =>
            {
                var result = loader.LoadJsonFile<Dictionary<string, string>>("test_empty.json");
                // Empty string causes DeserializeObject to return null (not throw)
                Assert.That(result, Is.Null);
            });
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Test]
    public void LoadJsonFile_WithValidJson_DeserializesCorrectly()
    {
        var modPath = QuestingBotsConfigLoader.GetModPath();
        var testFile = Path.Combine(modPath, "test_valid.json");
        File.WriteAllText(testFile, """{ "hello": "world" }""");

        try
        {
            var loader = new QuestingBotsConfigLoader(_logger);
            var result = loader.LoadJsonFile<Dictionary<string, string>>("test_valid.json");

            Assert.That(result, Is.Not.Null);
            Assert.That(result!["hello"], Is.EqualTo("world"));
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    [Test]
    public void LoadJsonFile_WithMissingFile_ReturnsNull()
    {
        var loader = new QuestingBotsConfigLoader(_logger);
        var result = loader.LoadJsonFile<Dictionary<string, string>>("nonexistent_file_that_does_not_exist.json");

        Assert.That(result, Is.Null);
        _logger.Received().Error(Arg.Is<string>(s => s.Contains("File not found")));
    }

    [Test]
    public void LoadJsonFile_WithBrokenArrayJson_ReturnsNull_DoesNotThrow()
    {
        var modPath = QuestingBotsConfigLoader.GetModPath();
        var testFile = Path.Combine(modPath, "test_broken_array.json");
        File.WriteAllText(testFile, """{ "arr": [1, 2, }""");

        try
        {
            var loader = new QuestingBotsConfigLoader(_logger);
            var result = loader.LoadJsonFile<Dictionary<string, int[]>>("test_broken_array.json");

            Assert.That(result, Is.Null);
        }
        finally
        {
            File.Delete(testFile);
        }
    }

    // ── Config.Load — resilience verification ───────────────────────
    // We verify that JsonConvert.DeserializeObject throws on bad input
    // (confirming the fix is needed) and that our catch clause handles it.

    [Test]
    public void JsonConvert_OnCorruptJson_ThrowsJsonReaderException()
    {
        Assert.Throws<JsonReaderException>(() => JsonConvert.DeserializeObject<QuestingBotsConfig>("{{{bad}}}"));
    }

    [Test]
    public void JsonConvert_OnWhitespace_ReturnsNull()
    {
        // Newtonsoft treats whitespace-only as equivalent to empty string (returns null)
        var result = JsonConvert.DeserializeObject<QuestingBotsConfig>("   \t\n  ");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void JsonConvert_OnTruncatedJson_ThrowsJsonReaderException()
    {
        Assert.Throws<JsonReaderException>(() =>
            JsonConvert.DeserializeObject<QuestingBotsConfig>("""{ "enabled": true, "debug": { "enabl""")
        );
    }

    [Test]
    public void JsonConvert_OnEmptyString_ReturnsNull()
    {
        var result = JsonConvert.DeserializeObject<QuestingBotsConfig>("");
        Assert.That(result, Is.Null);
    }

    [Test]
    public void JsonConvert_OnNullLiteral_ReturnsNull()
    {
        var result = JsonConvert.DeserializeObject<QuestingBotsConfig>("null");
        Assert.That(result, Is.Null);
    }
}
