using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Components.Spawning;

[TestFixture]
public class SpawnGenerationRecoveryTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    private static string FindRepoRoot()
    {
        var dir = TestContext.CurrentContext.TestDirectory;
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir, ".git")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return TestContext.CurrentContext.TestDirectory;
    }

    private static string ReadSource(string relativePath)
    {
        string fullPath = Path.Combine(RepoRoot, relativePath);
        Assert.That(File.Exists(fullPath), Is.True, $"Source file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    private static string ExtractMethodBody(string source, string signature)
    {
        int methodStart = source.IndexOf(signature, StringComparison.Ordinal);
        Assert.That(methodStart, Is.GreaterThanOrEqualTo(0), $"Method signature not found: {signature}");

        int braceStart = source.IndexOf('{', methodStart);
        Assert.That(braceStart, Is.GreaterThanOrEqualTo(0), $"Opening brace not found for: {signature}");

        int depth = 1;
        for (int i = braceStart + 1; i < source.Length; i++)
        {
            if (source[i] == '{')
            {
                depth++;
            }
            else if (source[i] == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return source.Substring(braceStart, i - braceStart + 1);
                }
            }
        }

        Assert.Fail($"Could not extract method body for: {signature}");
        return string.Empty;
    }

    [Test]
    public void GenerateBotGroup_UsesBoundedRetryLoop_InsteadOfUnboundedNullSentinel()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");

        Assert.That(source, Does.Contain("internal const int MaxGenerateBotGroupAttempts = 10;"));
        Assert.That(source, Does.Contain("for (int attempt = 1; attempt <= MaxGenerateBotGroupAttempts; attempt++)"));
        Assert.That(source, Does.Not.Contain("while (botSpawnInfo == null)"));
    }

    [Test]
    public void GenerateBotGroup_BacksOffBetweenRetries_AndFailsAfterRetryBudgetIsExhausted()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string methodBody = ExtractMethodBody(
            source,
            "protected async Task<Models.BotSpawnInfo> GenerateBotGroup(WildSpawnType spawnType, BotDifficulty botdifficulty, int bots)"
        );

        Assert.That(methodBody, Does.Contain("await Task.Delay(GetGenerateBotGroupRetryDelayMs(attempt));"));
        Assert.That(methodBody, Does.Contain("Failed to generate "));
        Assert.That(
            source,
            Does.Contain("return Math.Min(MaxGenerateBotGroupRetryDelayMs, Math.Max(1, attempt) * 25);"),
            "Retry backoff should clamp growth instead of sleeping unboundedly"
        );
    }

    [Test]
    public void GenerateBotGroup_RetriesShortProfileGenerations_BeforeFinalFailure()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string methodBody = ExtractMethodBody(
            source,
            "protected async Task<Models.BotSpawnInfo> GenerateBotGroup(WildSpawnType spawnType, BotDifficulty botdifficulty, int bots)"
        );

        Assert.That(methodBody, Does.Contain("if (botSpawnData.Profiles.Count != bots)"));
        Assert.That(methodBody, Does.Contain("if (attempt == MaxGenerateBotGroupAttempts)"));
        Assert.That(methodBody, Does.Contain("were generated. Trying again..."));
    }

    [Test]
    public void PScavGenerator_AlwaysClearsForcePScavs_InFinally()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/PScavGenerator.cs");
        string methodBody = ExtractMethodBody(source, "protected override async Task<Models.BotSpawnInfo> GenerateBotGroupTask()");

        Assert.That(
            methodBody,
            Does.Match(
                new Regex(
                    @"RaidHelpers\.ForcePScavs = true;\s*Models\.BotSpawnInfo group;\s*try\s*\{\s*group = await GenerateBotGroup\(WildSpawnType\.assault, botDifficulty, botsInGroup\);\s*\}\s*finally\s*\{\s*RaidHelpers\.ForcePScavs = false;\s*\}",
                    RegexOptions.Singleline
                )
            ),
            "PScav generation must always reset the global ForcePScavs flag even when generation throws"
        );
    }
}
