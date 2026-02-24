using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.ResourceLifecycle;

/// <summary>
/// Tests that static state is properly cleared between raids to prevent cross-raid pollution.
///
/// Bug 2 (Round 16): GameStartPatch.localGameObj held a reference to the previous raid's
///   LocalGame object, preventing GC of the entire raid object graph.
///   Fix: Clear in ClearMissedWaves.
///
/// Bug 3 (Round 16): GameStartPatch.IsDelayingGameStart was only reset inside the
///   spawnMissedWaves coroutine. If the raid was aborted during loading, it stayed true.
///   Fix: Reset in ClearMissedWaves (called at raid end).
///
/// Bug 4 (Round 16): TimeHasComeScreenClassChangeStatusPatch.instance held a stale reference
///   to the previous raid's MatchmakerPlayerControllerClass.
///   Fix: Add Clear() method, call from raid-end cleanup.
///
/// Bug 5 (Round 16): PatrolTask.Reset() was never called between raids, causing routes
///   from map A to persist into map B.
///   Fix: Call PatrolTask.Reset() from BotHiveMindMonitor.Clear().
///
/// Bug 6 (Round 16): LoggingController.InitFileLogger() was only called from plugin Awake,
///   but DisposeFileLogger() was called at each raid end. After the first raid, the dedicated
///   log file was never re-opened.
///   Fix: Re-call InitFileLogger() at raid start in BotsControllerSetSettingsPatch.
/// </summary>
[TestFixture]
public class StaticStateCrossRaidTests
{
    private static readonly string ClientSrcDir = Path.Combine(FindRepoRoot(), "src", "SPTQuestingBots.Client");

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

    // ── Bug 2 + 3: GameStartPatch cleanup ──────────────────────────────

    [Test]
    public void GameStartPatch_ClearMissedWaves_ClearsLocalGameObj()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Patches", "Spawning", "GameStartPatch.cs"));

        // The ClearMissedWaves method must null out localGameObj to prevent memory leaks
        Assert.That(
            source,
            Does.Contain("localGameObj = null"),
            "ClearMissedWaves must set localGameObj = null to release the previous raid's LocalGame reference"
        );
    }

    [Test]
    public void GameStartPatch_ClearMissedWaves_ResetsIsDelayingGameStart()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Patches", "Spawning", "GameStartPatch.cs"));

        // The ClearMissedWaves method must reset IsDelayingGameStart
        Assert.That(source, Does.Contain("IsDelayingGameStart = false"), "ClearMissedWaves must reset IsDelayingGameStart for next raid");
    }

    // ── Bug 4: TimeHasComeScreenClassChangeStatusPatch stale instance ──

    [Test]
    public void TimeHasComeScreenClassChangeStatus_HasClearMethod()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Patches", "Spawning", "TimeHasComeScreenClassChangeStatusPatch.cs"));

        Assert.That(
            source,
            Does.Contain("public static void Clear()"),
            "TimeHasComeScreenClassChangeStatusPatch must have a Clear() method"
        );
        Assert.That(source, Does.Contain("instance = null"), "Clear must null out the stale instance reference");
    }

    [Test]
    public void TimeHasComeScreenClassChangeStatus_ClearedAtRaidEnd()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Patches", "Spawning", "ScavLimits", "BotsControllerStopPatch.cs"));

        Assert.That(
            source,
            Does.Contain("TimeHasComeScreenClassChangeStatusPatch.Clear()"),
            "ScavLimits.BotsControllerStopPatch must call TimeHasComeScreenClassChangeStatusPatch.Clear()"
        );
    }

    // ── Bug 5: PatrolTask routes not reset between raids ────────────────

    [Test]
    public void PatrolTask_HasResetMethod()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "ECS", "UtilityAI", "Tasks", "PatrolTask.cs"));

        Assert.That(source, Does.Contain("public static void Reset()"), "PatrolTask must have a Reset method");
        Assert.That(source, Does.Contain("RoutesLoaded = false"), "Reset must clear the RoutesLoaded flag");
    }

    [Test]
    public void PatrolTask_ResetCalledFromHiveMindClear()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "HiveMind", "BotHiveMindMonitor.cs"));

        Assert.That(
            source,
            Does.Contain("PatrolTask.Reset()"),
            "BotHiveMindMonitor.Clear() must call PatrolTask.Reset() to clear routes between raids"
        );
    }

    // ── Bug 6: File logging not re-initialized per raid ─────────────────

    [Test]
    public void FileLogger_ReInitializedAtRaidStart()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Patches", "BotsControllerSetSettingsPatch.cs"));

        Assert.That(
            source,
            Does.Contain("InitFileLogger()"),
            "BotsControllerSetSettingsPatch must call InitFileLogger() to reopen the log file for each raid"
        );
    }

    [Test]
    public void FileLogger_DisposedAtRaidEnd()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Patches", "BotsControllerStopPatch.cs"));

        Assert.That(source, Does.Contain("DisposeFileLogger()"), "BotsControllerStopPatch must call DisposeFileLogger() at raid end");
    }

    // ── Verify existing Clear() calls are in place ──────────────────────

    [Test]
    public void BotsControllerStopPatch_ClearsAllCriticalStaticState()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Patches", "BotsControllerStopPatch.cs"));

        // These are the critical Clear() calls that must exist at raid end
        Assert.That(source, Does.Contain("BotJobAssignmentFactory.Clear()"), "Must clear job assignments");
        Assert.That(source, Does.Contain("BotRegistrationManager.Clear()"), "Must clear bot registrations");
        Assert.That(source, Does.Contain("BotHiveMindMonitor.Clear()"), "Must clear hive mind state");
        Assert.That(source, Does.Contain("DoorCollisionHelper.Clear()"), "Must clear door collision cache");
        Assert.That(source, Does.Contain("DisposeFileLogger()"), "Must dispose file logger");
    }

    [Test]
    public void BotHiveMindMonitor_Clear_ClearsAllSubsystems()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "HiveMind", "BotHiveMindMonitor.cs"));

        // Extract the Clear() method body to verify it cleans up all subsystems
        Assert.That(source, Does.Contain("BotEntityBridge.Clear()"), "Must clear entity bridge");
        Assert.That(source, Does.Contain("HumanPlayerCache.Clear()"), "Must clear human player cache");
        Assert.That(source, Does.Contain("CombatEventRegistry.Clear()"), "Must clear combat events");
        Assert.That(source, Does.Contain("GrenadeExplosionSubscriber.Clear()"), "Must clear grenade subscriber");
        Assert.That(source, Does.Contain("PatrolTask.Reset()"), "Must reset patrol routes");
        Assert.That(source, Does.Contain("_lastScanTime.Clear()"), "Must clear scan times");
        Assert.That(source, Does.Contain("_preStrategyObjectiveVersions.Clear()"), "Must clear strategy versions");
    }

    [Test]
    public void BotEntityBridge_Clear_ClearsAllDictionaries()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "BotLogic", "ECS", "BotEntityBridge.cs"));

        Assert.That(source, Does.Contain("_ownerToEntity.Clear()"), "Must clear owner→entity map");
        Assert.That(source, Does.Contain("_entityToOwner.Clear()"), "Must clear entity→owner map");
        Assert.That(source, Does.Contain("_profileIdToEntity.Clear()"), "Must clear profile→entity map");
        Assert.That(source, Does.Contain("_entityFieldStates.Clear()"), "Must clear field states");
        Assert.That(source, Does.Contain("_jobAssignments.Clear()"), "Must clear job assignments");
        Assert.That(source, Does.Contain("_lootClaims.Clear()"), "Must clear loot claims");
        Assert.That(source, Does.Contain("_squadRegistry.Clear()"), "Must clear squad registry");
        Assert.That(source, Does.Contain("_registry.Clear()"), "Must clear entity registry");
    }

    [Test]
    public void BotRegistrationManager_Clear_ResetsAllCounters()
    {
        string source = File.ReadAllText(Path.Combine(ClientSrcDir, "Controllers", "BotRegistrationManager.cs"));

        // All counters must be reset to 0
        Assert.That(source, Does.Contain("SpawnedBossWaves = 0"), "Must reset SpawnedBossWaves");
        Assert.That(source, Does.Contain("SpawnedBotCount = 0"), "Must reset SpawnedBotCount");
        Assert.That(source, Does.Contain("SpawnedBossCount = 0"), "Must reset SpawnedBossCount");
        Assert.That(source, Does.Contain("SpawnedRogueCount = 0"), "Must reset SpawnedRogueCount");
        Assert.That(source, Does.Contain("ZeroWaveCount = 0"), "Must reset ZeroWaveCount");
        Assert.That(source, Does.Contain("ZeroWaveTotalBotCount = 0"), "Must reset ZeroWaveTotalBotCount");
        Assert.That(source, Does.Contain("ZeroWaveTotalRogueCount = 0"), "Must reset ZeroWaveTotalRogueCount");

        // Collections must be cleared
        Assert.That(source, Does.Contain("registeredPMCs.Clear()"), "Must clear PMC set");
        Assert.That(source, Does.Contain("registeredBosses.Clear()"), "Must clear boss set");
        Assert.That(source, Does.Contain("hostileGroups.Clear()"), "Must clear hostile groups");
    }
}
