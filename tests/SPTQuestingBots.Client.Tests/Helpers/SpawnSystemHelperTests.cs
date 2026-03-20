using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Helpers;

/// <summary>
/// Source-scanning tests that validate SpawnSystemHelper integration patterns
/// and the 6 spawning improvements (Items 7, 8, 9, 12, 13, 14).
/// </summary>
[TestFixture]
public class SpawnSystemHelperTests
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
        int methodStart = source.IndexOf(signature, System.StringComparison.Ordinal);
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

    // ---- SpawnSystemHelper structure ----

    [Test]
    public void SpawnSystemHelper_Exists_WithAllRequiredMethods()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");

        Assert.That(source, Does.Contain("public static bool HasZoneFreeSpace("), "Item 7: zone capacity check");
        Assert.That(source, Does.Contain("public static int GetDelayedAndLoadingBotCount()"), "Item 8: delayed bot count");
        Assert.That(source, Does.Contain("public static bool ValidateSpawnPositions("), "Item 9: spawn position validation");
        Assert.That(source, Does.Contain("public static void NotifyBotSpawnLimiter("), "Item 12: BotSpawnLimiter integration");
        Assert.That(source, Does.Contain("public static void PreWarmBotProfiles("), "Item 13: profile pre-warming");
        Assert.That(source, Does.Contain("public static bool IsNonWavesInBurstPhase()"), "Item 14: NonWaves phase check");
        Assert.That(source, Does.Contain("public static void Clear()"), "Raid cleanup method");
    }

    // ---- Item 7: Zone capacity check ----

    [Test]
    public void HasZoneFreeSpace_HasNullSafety_ForZoneParameter()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static bool HasZoneFreeSpace(");

        Assert.That(body, Does.Contain("zone == null"), "Must null-check zone");
        Assert.That(body, Does.Contain("return true"), "Must fallback to true on null zone");
    }

    [Test]
    public void HasZoneFreeSpace_CallsHaveFreeSpace()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static bool HasZoneFreeSpace(");

        Assert.That(body, Does.Contain("zone.HaveFreeSpace(count)"), "Must call BotZone.HaveFreeSpace");
    }

    [Test]
    public void HasZoneFreeSpace_HasTryCatch_WithFallback()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static bool HasZoneFreeSpace(");

        Assert.That(body, Does.Contain("catch (Exception"), "Must have try/catch");
    }

    [Test]
    public void SpawnBots_ChecksZoneCapacity_BeforeSpawning()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "private bool SpawnBots(");

        Assert.That(
            body,
            Does.Contain("SpawnSystemHelper.HasZoneFreeSpace(closestBotZone, positions.Length)"),
            "SpawnBots must check zone capacity"
        );
        Assert.That(body, Does.Contain("return false"), "Must return false when zone is full");
    }

    // ---- Item 8: SpawnDelaysService integration ----

    [Test]
    public void GetDelayedAndLoadingBotCount_ReadsWaitCount_AndInSpawnProcess()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static int GetDelayedAndLoadingBotCount()");

        Assert.That(body, Does.Contain("SpawnDelaysService"), "Must read SpawnDelaysService");
        Assert.That(body, Does.Contain("WaitCount"), "Must read WaitCount from SpawnDelaysService");
        Assert.That(body, Does.Contain("InSpawnProcess"), "Must read InSpawnProcess from BotSpawner");
    }

    [Test]
    public void GetDelayedAndLoadingBotCount_ReturnsZero_WhenBotSpawnerNull()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static int GetDelayedAndLoadingBotCount()");

        Assert.That(body, Does.Contain("botSpawner == null"), "Must null-check BotSpawner");
        Assert.That(body, Does.Contain("return 0"), "Must return 0 on null BotSpawner");
    }

    [Test]
    public void BotsAllowedToSpawnForGeneratorType_SubtractsDelayedBots()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "public int BotsAllowedToSpawnForGeneratorType()");

        Assert.That(
            body,
            Does.Contain("SpawnSystemHelper.GetDelayedAndLoadingBotCount()"),
            "Must subtract delayed/loading bots from capacity"
        );
    }

    // ---- Item 9: ISpawnSystem validation ----

    [Test]
    public void ValidateSpawnPositions_UsesSpawnSystem()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static bool ValidateSpawnPositions(");

        Assert.That(body, Does.Contain("ValidateSpawnPosition("), "Must call ISpawnSystem.ValidateSpawnPosition");
    }

    [Test]
    public void ValidateSpawnPositions_FallsBackToTrue_WhenSpawnSystemNull()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static bool ValidateSpawnPositions(");

        Assert.That(body, Does.Contain("spawnSystem == null"), "Must null-check ISpawnSystem");
    }

    [Test]
    public void SpawnBots_ValidatesPositions_ViaSpawnSystem()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "private bool SpawnBots(");

        Assert.That(body, Does.Contain("SpawnSystemHelper.ValidateSpawnPositions("), "SpawnBots must validate positions via ISpawnSystem");
    }

    // ---- Item 12: BotSpawnLimiter integration ----

    [Test]
    public void NotifyBotSpawnLimiter_CallsIncreaseUsedPlayerSpawns()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static void NotifyBotSpawnLimiter(");

        Assert.That(body, Does.Contain("IncreaseUsedPlayerSpawns"), "Must call IncreaseUsedPlayerSpawns");
        Assert.That(body, Does.Contain("BotSpawnLimiter"), "Must access BotSpawnLimiter");
    }

    [Test]
    public void NotifyBotSpawnLimiter_HasNullSafety()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static void NotifyBotSpawnLimiter(");

        Assert.That(body, Does.Contain("bot == null"), "Must null-check bot");
        Assert.That(body, Does.Contain("data == null"), "Must null-check data");
        Assert.That(body, Does.Contain("limiter == null"), "Must null-check limiter");
        Assert.That(body, Does.Contain("catch (Exception"), "Must have try/catch");
    }

    [Test]
    public void CreateBotCallback_NotifiesBotSpawnLimiter()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "public void CreateBotCallback(BotOwner bot)");

        Assert.That(
            body,
            Does.Contain("SpawnSystemHelper.NotifyBotSpawnLimiter(bot, botSpawnInfo.Data)"),
            "CreateBotCallback must notify BotSpawnLimiter"
        );
    }

    // ---- Item 13: Profile pre-warming ----

    [Test]
    public void PreWarmBotProfiles_CallsAddToTargetBackup()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static void PreWarmBotProfiles(");

        Assert.That(body, Does.Contain("AddToTargetBackup(difficulty, role, count)"), "Must call AddToTargetBackup");
    }

    [Test]
    public void PreWarmBotProfiles_HasNullSafety()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static void PreWarmBotProfiles(");

        Assert.That(body, Does.Contain("botSpawner == null"), "Must null-check BotSpawner");
        Assert.That(body, Does.Contain("count <= 0"), "Must check for non-positive count");
    }

    [Test]
    public void PMCGenerator_OverridesPreWarmProfiles()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/PMCGenerator.cs");

        Assert.That(source, Does.Contain("protected override void PreWarmProfiles()"), "PMCGenerator must override PreWarmProfiles");
        Assert.That(source, Does.Contain("WildSpawnType.pmcBEAR"), "Must pre-warm BEAR profiles");
        Assert.That(source, Does.Contain("WildSpawnType.pmcUSEC"), "Must pre-warm USEC profiles");
    }

    [Test]
    public void PScavGenerator_OverridesPreWarmProfiles()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/PScavGenerator.cs");

        Assert.That(source, Does.Contain("protected override void PreWarmProfiles()"), "PScavGenerator must override PreWarmProfiles");
        Assert.That(source, Does.Contain("WildSpawnType.assault"), "Must pre-warm assault (scav) profiles");
    }

    [Test]
    public void BotGenerator_CallsPreWarmProfiles_BeforeGeneration()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");

        // PreWarmProfiles should be called before the generation loop
        int preWarmIdx = source.IndexOf("PreWarmProfiles()", System.StringComparison.Ordinal);
        int generatingIdx = source.IndexOf("Generating \" + MaxGeneratedBots", System.StringComparison.Ordinal);

        Assert.That(preWarmIdx, Is.GreaterThan(0), "PreWarmProfiles must be called");
        Assert.That(preWarmIdx, Is.LessThan(generatingIdx), "PreWarmProfiles must be called before generation starts");
    }

    // ---- Item 14: NonWaves phase coordination ----

    [Test]
    public void IsNonWavesInBurstPhase_ReadsBool2_ViaReflection()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static bool IsNonWavesInBurstPhase()");

        Assert.That(body, Does.Contain("bool_2"), "Must access NonWavesSpawnScenario.bool_2 field");
        Assert.That(body, Does.Contain("ReflectionHelper.RequireField("), "Must use ReflectionHelper.RequireField for reflection");
        Assert.That(body, Does.Contain("scenario.Enabled"), "Must check if NonWaves is Enabled");
    }

    [Test]
    public void IsNonWavesInBurstPhase_CachesFieldInfo()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");

        Assert.That(source, Does.Contain("_nonWavesFieldResolved"), "Must cache field resolution to avoid repeated reflection");
        Assert.That(source, Does.Contain("_nonWavesSpawnPhaseField"), "Must cache FieldInfo");
    }

    [Test]
    public void IsNonWavesInBurstPhase_FallsBackToFalse_OnFailure()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");
        string body = ExtractMethodBody(source, "public static bool IsNonWavesInBurstPhase()");

        Assert.That(body, Does.Contain("catch (Exception"), "Must have try/catch");
        Assert.That(body, Does.Contain("return false"), "Must fallback to false (allow spawning)");
    }

    [Test]
    public void AllowedToSpawnBots_ChecksNonWavesBurstPhase()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "public bool AllowedToSpawnBots()");

        Assert.That(body, Does.Contain("SpawnSystemHelper.IsNonWavesInBurstPhase()"), "AllowedToSpawnBots must check NonWaves burst phase");
    }

    // ---- Cleanup and lifecycle ----

    [Test]
    public void SpawnSystemHelper_Clear_CalledOnBotControllerStop()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Patches/Spawning/ScavLimits/BotsControllerStopPatch.cs");

        Assert.That(source, Does.Contain("SpawnSystemHelper.Clear()"), "Must clear SpawnSystemHelper between raids");
    }

    [Test]
    public void NonWavesBool2_RegisteredInKnownFields()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/ReflectionHelper.cs");

        Assert.That(
            source,
            Does.Contain("\"bool_2\", \"SpawnSystemHelper"),
            "NonWavesSpawnScenario.bool_2 must be registered in KnownFields"
        );
    }

    // ---- SpawnBots return type ----

    [Test]
    public void SpawnBots_ReturnsBool_InsteadOfVoid()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");

        Assert.That(
            source,
            Does.Contain("private bool SpawnBots(Models.BotSpawnInfo botSpawnInfo, Vector3[] positions)"),
            "SpawnBots must return bool to signal zone/validation failures"
        );
    }

    [Test]
    public void SpawnBotGroup_HandlesSpawnBotsFailure()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");
        string body = ExtractMethodBody(source, "private IEnumerator spawnBotGroup(");

        Assert.That(body, Does.Contain("if (!SpawnBots(botGroup, spawnPositions))"), "spawnBotGroup must check SpawnBots return value");
    }

    // ---- Safety: all helper methods have try/catch ----

    [Test]
    public void AllSpawnSystemHelperMethods_HaveTryCatch()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Helpers/SpawnSystemHelper.cs");

        string[] methodSignatures = new[]
        {
            "public static bool HasZoneFreeSpace(",
            "public static int GetDelayedAndLoadingBotCount()",
            "public static bool ValidateSpawnPositions(",
            "public static void NotifyBotSpawnLimiter(",
            "public static void PreWarmBotProfiles(",
            "public static bool IsNonWavesInBurstPhase()",
        };

        foreach (var sig in methodSignatures)
        {
            string body = ExtractMethodBody(source, sig);
            Assert.That(body, Does.Contain("catch (Exception"), $"Method {sig} must have try/catch for fault tolerance");
        }
    }

    // ---- BotGenerator has virtual PreWarmProfiles ----

    [Test]
    public void BotGenerator_HasVirtualPreWarmProfiles()
    {
        string source = ReadSource("src/SPTQuestingBots.Client/Components/Spawning/BotGenerator.cs");

        Assert.That(
            source,
            Does.Contain("protected virtual void PreWarmProfiles()"),
            "BotGenerator must have virtual PreWarmProfiles for subclass override"
        );
    }
}
