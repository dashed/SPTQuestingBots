using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Components.Spawning;

/// <summary>
/// Integration-level source-scanning tests that verify the end-to-end spawning pipeline
/// correctly wires up all 6 improvements (Items 7, 8, 9, 12, 13, 14).
/// </summary>
[TestFixture]
public class SpawnSystemIntegrationTests
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

    // ---- Pipeline ordering: zone check before position add ----

    [Test]
    public void SpawnBots_ChecksZoneCapacity_BeforeAddingPositions()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "private bool SpawnBots(");

        int zoneCheckIdx = body.IndexOf("HasZoneFreeSpace", StringComparison.Ordinal);
        int addPositionIdx = body.IndexOf("Data.AddPosition(", StringComparison.Ordinal);

        Assert.That(zoneCheckIdx, Is.GreaterThan(0), "Zone capacity check must exist");
        Assert.That(addPositionIdx, Is.GreaterThan(0), "AddPosition must exist");
        Assert.That(
            zoneCheckIdx,
            Is.LessThan(addPositionIdx),
            "Zone capacity check must come BEFORE AddPosition — no point adding positions if zone is full"
        );
    }

    [Test]
    public void SpawnBots_ValidatesPositions_BeforeAddingThem()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "private bool SpawnBots(");

        int validateIdx = body.IndexOf("ValidateSpawnPositions", StringComparison.Ordinal);
        int addPositionIdx = body.IndexOf("Data.AddPosition(", StringComparison.Ordinal);

        Assert.That(validateIdx, Is.GreaterThan(0), "Position validation must exist");
        Assert.That(validateIdx, Is.LessThan(addPositionIdx), "Position validation must come BEFORE AddPosition");
    }

    // ---- Pipeline ordering: AllowedToSpawnBots checks ----

    [Test]
    public void AllowedToSpawnBots_ChecksNonWaves_AfterBossWaves_BeforeRaidTime()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "public bool AllowedToSpawnBots()");

        int bossCheckIdx = body.IndexOf("HaveInitialBossWavesSpawned", StringComparison.Ordinal);
        int nonWavesIdx = body.IndexOf("IsNonWavesInBurstPhase", StringComparison.Ordinal);
        int raidTimeIdx = body.IndexOf("GetSecondsSinceSpawning", StringComparison.Ordinal);

        Assert.That(bossCheckIdx, Is.GreaterThan(0), "Boss wave check must exist");
        Assert.That(nonWavesIdx, Is.GreaterThan(0), "NonWaves phase check must exist");
        Assert.That(raidTimeIdx, Is.GreaterThan(0), "Raid time check must exist");
        Assert.That(nonWavesIdx, Is.GreaterThan(bossCheckIdx), "NonWaves check should come after boss wave check");
        Assert.That(nonWavesIdx, Is.LessThan(raidTimeIdx), "NonWaves check should come before raid time check");
    }

    // ---- Capacity calculation includes delays ----

    [Test]
    public void BotsAllowedToSpawn_AccountsForAlive_AndDelayed()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "public int BotsAllowedToSpawnForGeneratorType()");

        // Must subtract both alive bots and delayed/loading bots
        Assert.That(body, Does.Contain("AliveBots()"), "Must count alive bots");
        Assert.That(body, Does.Contain("GetDelayedAndLoadingBotCount()"), "Must count delayed/loading bots");

        // The formula should be: MaxAliveBots - alive - delayedAndLoading
        Assert.That(
            body,
            Does.Contain("MaxAliveBots - alive - delayedAndLoading"),
            "Capacity formula: MaxAliveBots - alive - delayedAndLoading"
        );
    }

    // ---- BotSpawnLimiter called in the right place ----

    [Test]
    public void CreateBotCallback_NotifiesLimiter_AfterBotActivation_BeforeLogging()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "public void CreateBotCallback(BotOwner bot)");

        int limiterIdx = body.IndexOf("NotifyBotSpawnLimiter", StringComparison.Ordinal);
        int logIdx = body.IndexOf("Spawned bot", StringComparison.Ordinal);

        Assert.That(limiterIdx, Is.GreaterThan(0), "Must notify BotSpawnLimiter");
        Assert.That(logIdx, Is.GreaterThan(0), "Must log spawn");
        Assert.That(limiterIdx, Is.LessThan(logIdx), "BotSpawnLimiter notification should come before spawn log");
    }

    // ---- Profile pre-warming wired up ----

    [Test]
    public void GenerateAllBotsTask_CallsPreWarmProfiles_BeforeGenerationLoop()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");

        // Find generateAllBotsTask and verify PreWarmProfiles is called before the while loop
        int preWarmIdx = source.IndexOf("PreWarmProfiles()", StringComparison.Ordinal);
        int whileIdx = source.IndexOf("while (GeneratedBotCount < MaxGeneratedBots)", StringComparison.Ordinal);

        Assert.That(preWarmIdx, Is.GreaterThan(0), "PreWarmProfiles must be called");
        Assert.That(whileIdx, Is.GreaterThan(0), "Generation loop must exist");
        Assert.That(preWarmIdx, Is.LessThan(whileIdx), "Pre-warming must happen BEFORE the generation loop");
    }

    // ---- PMC and PScav generators pre-warm appropriate types ----

    [Test]
    public void PMCGenerator_PreWarmsBothFactions()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/PMCGenerator.cs");
        string body = ExtractMethodBody(source, "protected override void PreWarmProfiles()");

        Assert.That(body, Does.Contain("pmcBEAR"), "Must pre-warm BEAR");
        Assert.That(body, Does.Contain("pmcUSEC"), "Must pre-warm USEC");
        Assert.That(body, Does.Contain("PreWarmBotProfiles"), "Must call PreWarmBotProfiles");
    }

    [Test]
    public void PScavGenerator_PreWarmsAssaultType()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/PScavGenerator.cs");
        string body = ExtractMethodBody(source, "protected override void PreWarmProfiles()");

        Assert.That(body, Does.Contain("assault"), "Must pre-warm assault (scav)");
        Assert.That(body, Does.Contain("PreWarmBotProfiles"), "Must call PreWarmBotProfiles");
    }

    // ---- Cleanup wiring ----

    [Test]
    public void BotsControllerStopPatch_ClearsSpawnSystemHelper()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Patches/Spawning/ScavLimits/BotsControllerStopPatch.cs");

        int nonWavesClearIdx = source.IndexOf("NonWavesSpawnScenarioCreatePatch.Clear()", StringComparison.Ordinal);
        int helperClearIdx = source.IndexOf("SpawnSystemHelper.Clear()", StringComparison.Ordinal);

        Assert.That(helperClearIdx, Is.GreaterThan(0), "SpawnSystemHelper.Clear must be called");
        Assert.That(
            helperClearIdx,
            Is.GreaterThan(nonWavesClearIdx),
            "SpawnSystemHelper.Clear should come after NonWavesSpawnScenarioCreatePatch.Clear"
        );
    }

    // ---- No game API calls without try/catch ----

    [Test]
    public void SpawnSystemHelper_NoUncaughtGameAPICalls()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");

        // Count the number of public static methods and the number of try blocks
        var publicMethods = Regex.Matches(source, @"public static \w+ \w+\(");
        var tryCatches = Regex.Matches(source, @"catch \(Exception");

        // Every public method except Clear() should have a try/catch
        int expectedTryCatches = publicMethods.Count - 1; // Clear() doesn't need try/catch
        Assert.That(
            tryCatches.Count,
            Is.GreaterThanOrEqualTo(expectedTryCatches),
            "Every public method (except Clear) must have try/catch"
        );
    }

    // ---- SpawnBots returns true on success path ----

    [Test]
    public void SpawnBots_ReturnsTrueOnSuccessPath()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "private bool SpawnBots(");

        // After ActivateBot, must return true
        int activateIdx = body.IndexOf("ActivateBot(", StringComparison.Ordinal);
        int returnTrueIdx = body.IndexOf("return true;", activateIdx, StringComparison.Ordinal);

        Assert.That(activateIdx, Is.GreaterThan(0), "Must call ActivateBot");
        Assert.That(returnTrueIdx, Is.GreaterThan(activateIdx), "Must return true after ActivateBot");
    }
}
