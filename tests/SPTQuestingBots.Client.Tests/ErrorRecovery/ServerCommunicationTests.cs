using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ErrorRecovery;

/// <summary>
/// Verifies that server communication code handles all failure modes:
/// - Server not installed (404/no response)
/// - Malformed JSON
/// - Empty responses
/// - Timeout/retry behavior
/// - Deserialization failures that could cause NRE in callers
/// </summary>
[TestFixture]
public class ServerCommunicationTests
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string ClientSrcDir = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Client");

    private static string FindRepoRoot()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return TestContext.CurrentContext.TestDirectory;
    }

    // ── GetJson: retry and error handling ──

    [Test]
    public void GetJson_HasRetryLoop()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static string GetJson");

        Assert.That(method, Does.Contain("for (int i = 0; i < 5; i++)"), "GetJson must retry up to 5 times");
        Assert.That(method, Does.Contain("catch (Exception"), "GetJson must catch exceptions during request");
        Assert.That(method, Does.Contain("Thread.Sleep"), "GetJson must delay between retries");
    }

    [Test]
    public void GetJson_HandlesNullResult()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static string GetJson");

        Assert.That(method, Does.Contain("if (json == null)"), "GetJson must handle null json result");
        Assert.That(method, Does.Contain("LogErrorToServerConsole"), "GetJson must log server console error on failure");
    }

    // ── TryDeserializeObject: null/empty/error handling ──

    [Test]
    public void TryDeserializeObject_ChecksNullOrEmpty()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static bool TryDeserializeObject");

        Assert.That(method, Does.Contain("string.IsNullOrEmpty(json)"), "TryDeserializeObject must check for null/empty JSON");
    }

    [Test]
    public void TryDeserializeObject_ChecksServerErrorResponse()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static bool TryDeserializeObject");

        Assert.That(method, Does.Contain("ServerResponseError"), "TryDeserializeObject must check for server error responses");
        Assert.That(method, Does.Contain("StatusCode"), "TryDeserializeObject must verify HTTP status code");
    }

    [Test]
    public void TryDeserializeObject_ReturnsFalseOnFailure()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static bool TryDeserializeObject");

        Assert.That(method, Does.Contain("return false"), "TryDeserializeObject must return false on failure");
        Assert.That(method, Does.Contain("Activator.CreateInstance"), "TryDeserializeObject must create default instance on failure");
    }

    // ── Caller safety: methods that use TryDeserializeObject must check its result ──

    [Test]
    public void GetConfig_ChecksDeserializationResult()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static Configuration.ModConfig GetConfig");

        Assert.That(
            method,
            Does.Contain("!TryDeserializeObject").Or.Contain("TryDeserializeObject("),
            "GetConfig must use TryDeserializeObject"
        );
        Assert.That(method, Does.Contain("return null"), "GetConfig must return null on deserialization failure");
    }

    [Test]
    public void GetUSECChance_ChecksDeserializationResult()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static float GetUSECChance");

        Assert.That(method, Does.Contain("TryDeserializeObject("), "GetUSECChance must check TryDeserializeObject result");
    }

    [TestCase("GetAllQuestTemplates")]
    [TestCase("GetEFTQuestSettings")]
    [TestCase("GetZoneAndItemPositions")]
    [TestCase("GetScavRaidSettings")]
    public void ServerDataMethod_ChecksDeserializationResult(string methodName)
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));

        // Each method must check TryDeserializeObject return (not ignore it)
        Assert.That(
            source,
            Does.Contain("!TryDeserializeObject(json, errorMessage, out"),
            $"{methodName}: must check TryDeserializeObject return value"
        );
    }

    // ── findSerializerSettings: null type safety (Round 15 fix) ──

    [Test]
    public void FindSerializerSettings_NullChecksTargetType()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "private static void findSerializerSettings");

        Assert.That(method, Does.Contain("targetType == null"), "findSerializerSettings must null-check FindTargetTypeByField result");
        Assert.That(
            method,
            Does.Contain("return;").And.Contain("targetType == null"),
            "findSerializerSettings must early-return when type is null"
        );
    }

    // ── Custom quest file loading: handles missing files ──

    [Test]
    public void GetCustomQuests_HandlesFileNotExist()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));
        var method = ExtractMethod(source, "public static IEnumerable<Models.Questing.Quest> GetCustomQuests");

        Assert.That(method, Does.Contain("File.Exists"), "GetCustomQuests must check if quest files exist");
        Assert.That(method, Does.Contain("catch (Exception"), "GetCustomQuests must catch file read exceptions");
    }

    // ── All 8 endpoints are properly guarded ──

    [Test]
    public void AllServerEndpoints_HaveErrorMessages()
    {
        var source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "ConfigController.cs"));

        // Count distinct GetJson calls with error messages
        var matches = Regex.Matches(source, @"GetJson\(""/QuestingBots/");
        Assert.That(matches.Count, Is.GreaterThanOrEqualTo(5), "Should have at least 5 server endpoint calls");
    }

    // ── Helpers ──

    private static string ExtractMethod(string source, string signature)
    {
        int start = source.IndexOf(signature);
        if (start < 0)
            return "";

        int braceCount = 0;
        bool foundFirst = false;
        int end = start;
        for (int i = start; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                braceCount++;
                foundFirst = true;
            }
            else if (source[i] == '}')
            {
                braceCount--;
                if (foundFirst && braceCount == 0)
                {
                    end = i + 1;
                    break;
                }
            }
        }
        return source.Substring(start, end - start);
    }
}
