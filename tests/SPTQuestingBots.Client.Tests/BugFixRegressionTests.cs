using System.IO;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests;

/// <summary>
/// Regression tests for bug fixes in the QuestingBots client plugin.
///
/// These tests read the actual source files and verify that the correct
/// field names and null-check patterns are in place, preventing
/// regressions if someone accidentally reverts a fix.
/// </summary>
[TestFixture]
public class BugFixRegressionTests
{
    // All paths are relative to the repo root. FindRepoRoot() locates it at runtime.
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

        // Fallback: assume we are run from within the repo
        return TestContext.CurrentContext.TestDirectory;
    }

    private string ReadSourceFile(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot, relativePath);
        Assert.That(File.Exists(fullPath), Is.True, $"Source file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    #region Reflection field name regression tests

    [Test]
    public void TrySpawnFreeAndDelayPatch_UsesFloat2_WithLowercaseF()
    {
        // Bug fix: NonWavesSpawnScenario is a named EFT namespace type — deobfuscator uses
        // camelCase for these (float_2, not Float_2). PascalCase is for GClass/abstract types.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Spawning/ScavLimits/TrySpawnFreeAndDelayPatch.cs");

        Assert.That(source, Does.Contain("\"float_2\""), "Field name should be float_2 (camelCase, EFT namespace type)");
        Assert.That(source, Does.Not.Contain("\"Float_2\""), "PascalCase Float_2 is wrong for EFT namespace types");
    }

    [Test]
    public void BotPathingHelpers_UsesVector3_0_WithPascalCase()
    {
        // Bug fix: SPT 4.x deobfuscator uses PascalCase "Vector3_0" for obfuscated fields.
        // The field is on BotCurrentPathAbstractClass and holds path corner points (Vector3[]).
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Helpers/BotPathingHelpers.cs");

        Assert.That(source, Does.Contain("\"Vector3_0\""), "Field name should be Vector3_0 (PascalCase)");
        Assert.That(source, Does.Not.Contain("\"vector3_0\""), "Old field name vector3_0 (camelCase) should not be present");
    }

    [TestCase(
        "src/SPTQuestingBots.Client/Patches/Spawning/ScavLimits/TrySpawnFreeAndDelayPatch.cs",
        "float_2",
        "NonWavesSpawnScenario retry delay"
    )]
    [TestCase("src/SPTQuestingBots.Client/Helpers/BotPathingHelpers.cs", "Vector3_0", "BotCurrentPathAbstractClass path points")]
    [TestCase("src/SPTQuestingBots.Client/BotLogic/LogicLayerMonitor.cs", "List_0", "AICoreStrategyAbstractClass brain layer list")]
    [TestCase("src/SPTQuestingBots.Client/Patches/PScavProfilePatch.cs", "List_0", "BotsPresets profile list")]
    [TestCase("src/SPTQuestingBots.Client/Patches/Spawning/GameStartPatch.cs", "wavesSpawnScenario_0", "LocalGame waves spawn scenario")]
    [TestCase("src/SPTQuestingBots.Client/BotLogic/LogicLayerMonitor.cs", "customLayer", "BigBrain CustomLayerWrapper inner field")]
    public void ReflectionFieldName_ExistsInSourceFile(string relPath, string expectedFieldName, string description)
    {
        // Verify each reflection field lookup string is present in the source file.
        // If a field name gets accidentally changed, this test will catch it.
        var source = ReadSourceFile(relPath);
        Assert.That(
            source,
            Does.Contain($"\"{expectedFieldName}\""),
            $"Expected reflection field \"{expectedFieldName}\" ({description}) in {relPath}"
        );
    }

    [Test]
    public void BotDiedPatch_UsesCorrectHarmonyFieldName_ForBots()
    {
        // Bug fix: BotSpawner has field "Bots" (no underscore), not "_bots".
        // Harmony parameter "____bots" (4 underscores) resolved to field "_bots" which doesn't exist.
        // Correct: "___Bots" (3 underscores + field name "Bots").
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Spawning/Advanced/BotDiedPatch.cs");

        Assert.That(source, Does.Contain("___Bots"), "Harmony field parameter should be ___Bots (3 underscores + Bots)");
        Assert.That(source, Does.Not.Contain("____bots"), "Old parameter ____bots resolved to non-existent field _bots");
    }

    [Test]
    public void SetNewBossPatch_UsesCorrectHarmonyFieldName_ForBoss()
    {
        // Bug fix: BossGroup has field "Boss_1" (PascalCase obfuscated), not "_boss".
        // Harmony parameter "____boss" (4 underscores) resolved to field "_boss" which doesn't exist.
        // Correct: "___Boss_1" (3 underscores + field name "Boss_1").
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Spawning/SetNewBossPatch.cs");

        Assert.That(source, Does.Contain("___Boss_1"), "Harmony field parameter should be ___Boss_1 (3 underscores + Boss_1)");
        Assert.That(source, Does.Not.Contain("____boss"), "Old parameter ____boss resolved to non-existent field _boss");
    }

    [Test]
    public void GetAllBossPlayersPatch_UsesCorrectHarmonyFieldName_ForAllPlayers()
    {
        // Bug fix: BotSpawner has field "AllPlayers" (PascalCase), not "_allPlayers".
        // Harmony parameter "____allPlayers" (4 underscores) resolved to field "_allPlayers" which doesn't exist.
        // Correct: "___AllPlayers" (3 underscores + field name "AllPlayers").
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Spawning/GetAllBossPlayersPatch.cs");

        Assert.That(source, Does.Contain("___AllPlayers"), "Harmony field parameter should be ___AllPlayers (3 underscores + AllPlayers)");
        Assert.That(source, Does.Not.Contain("____allPlayers"), "Old parameter ____allPlayers resolved to non-existent field _allPlayers");
    }

    #endregion

    #region LogicLayerMonitor null-check regression test

    [Test]
    public void GetExternalCustomLayer_NullCheckIsOnCustomLayer_NotLayer()
    {
        // Bug fix: A copy-paste error checked "layer == null" instead of "customLayer == null"
        // after the line: CustomLayer customLayer = (CustomLayer)customLayerField.GetValue(layer);
        //
        // The variable "layer" was already validated at the top of the method, so checking it
        // again was a no-op, leaving customLayer unchecked.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/LogicLayerMonitor.cs");

        // The GetExternalCustomLayer method should check "customLayer == null" after GetValue
        Assert.That(source, Does.Contain("if (customLayer == null)"), "Null check after GetValue should be on customLayer, not layer");

        // Count occurrences of "if (layer == null)" — there should be exactly one
        // (the parameter guard at the top of GetExternalCustomLayer), not two.
        int layerNullCheckCount = CountOccurrences(source, "if (layer == null)");
        Assert.That(
            layerNullCheckCount,
            Is.EqualTo(1),
            "There should be exactly one 'if (layer == null)' check (the parameter guard). "
                + "A second one would indicate the copy-paste bug has been reintroduced."
        );
    }

    #endregion

    #region BotQuestBuilder null-safety regression tests

    [Test]
    public void BotQuestBuilder_Awake_HasNullCheckForLocationData()
    {
        // Bug fix: GetComponent<LocationData>() can return null if the component
        // hasn't been added yet. The Awake method now checks for null before using it.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/BotQuestBuilder.cs");

        Assert.That(source, Does.Contain("if (locationData == null)"), "BotQuestBuilder.Awake should have a null check for locationData");
    }

    [Test]
    public void BotQuestBuilder_LoadAllQuests_HasNullCheckForSession()
    {
        // Bug fix: GetSession() can return null. The LoadAllQuests coroutine
        // now checks for null and does yield break to stop the coroutine.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/BotQuestBuilder.cs");

        Assert.That(source, Does.Contain("if (session == null)"), "BotQuestBuilder.LoadAllQuests should have a null check for session");
        Assert.That(source, Does.Contain("yield break"), "BotQuestBuilder.LoadAllQuests should yield break when session is null");
    }

    #endregion

    #region BotsControllerStopPatch null-safety regression test

    [Test]
    public void BotsControllerStopPatch_HasNullCheckForBotQuestBuilder()
    {
        // Bug fix: GetComponent<BotQuestBuilder>() can return null if the component
        // was never added (e.g. questing disabled) or its Awake failed.
        // The old code directly accessed .HaveQuestsBeenBuilt on the result,
        // which caused a NullReferenceException that blocked raid extraction.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/BotsControllerStopPatch.cs");

        Assert.That(
            source,
            Does.Contain("questBuilder != null"),
            "BotsControllerStopPatch should null-check the BotQuestBuilder component before accessing HaveQuestsBeenBuilt"
        );
    }

    #endregion

    #region QuestingBotsPlugin static singleton regression tests

    [Test]
    public void QuestingBotsPlugin_HasStaticInstanceProperty()
    {
        // Bug fix: FindObjectOfType<QuestingBotsPlugin>() returned null in raid because
        // BepInEx plugin GameObjects live in DontDestroyOnLoad and may not be found.
        // The static singleton pattern is the standard BepInEx approach.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/QuestingBotsPlugin.cs");

        Assert.That(
            source,
            Does.Contain("public static QuestingBotsPlugin Instance"),
            "QuestingBotsPlugin should have a static Instance property"
        );
        Assert.That(source, Does.Contain("Instance = this"), "QuestingBotsPlugin.Awake should set Instance = this");
    }

    [Test]
    public void QuestingBotsPlugin_Awake_PerformsVersionPreflight_BeforeConfigFetch_AndPatchEnable()
    {
        var source = ReadSourceFile("src/SPTQuestingBots.Client/QuestingBotsPlugin.cs");

        int modNameIndex = source.IndexOf("ModName = Info.Metadata.Name;", System.StringComparison.Ordinal);
        int preflightIndex = source.IndexOf(
            "if (!Patches.TarkovInitPatch.IsCurrentVersionSupported(out string currentVersion))",
            System.StringComparison.Ordinal
        );
        int buildErrorIndex = source.IndexOf(
            "Patches.TarkovInitPatch.BuildVersionErrorMessage(ModName, currentVersion)",
            System.StringComparison.Ordinal
        );
        int addDependencyErrorIndex = source.IndexOf("Chainloader.DependencyErrors.Add(versionError);", System.StringComparison.Ordinal);
        int logErrorIndex = source.IndexOf("Logger.LogError(versionError);", System.StringComparison.Ordinal);
        int configFetchIndex = source.IndexOf("ConfigController.GetConfig()", System.StringComparison.Ordinal);
        int menuPatchIndex = source.IndexOf("new Patches.MenuShowPatch().Enable();", System.StringComparison.Ordinal);
        int initPatchEnableIndex = source.IndexOf("new Patches.TarkovInitPatch().Enable();", System.StringComparison.Ordinal);

        Assert.That(modNameIndex, Is.GreaterThanOrEqualTo(0), "QuestingBotsPlugin.Awake must initialize ModName before version preflight");
        Assert.That(preflightIndex, Is.GreaterThan(modNameIndex), "Version preflight must run after ModName is known");
        Assert.That(
            buildErrorIndex,
            Is.GreaterThan(preflightIndex),
            "QuestingBotsPlugin must build the user-facing version error from the helper"
        );
        Assert.That(
            addDependencyErrorIndex,
            Is.GreaterThan(buildErrorIndex),
            "Unsupported versions must be reported through Chainloader.DependencyErrors before Awake exits"
        );
        Assert.That(logErrorIndex, Is.GreaterThan(addDependencyErrorIndex), "Unsupported versions should also be logged before returning");
        Assert.That(
            configFetchIndex,
            Is.GreaterThan(logErrorIndex),
            "ConfigController.GetConfig must not run before version compatibility is preflighted"
        );
        Assert.That(
            menuPatchIndex,
            Is.GreaterThan(configFetchIndex),
            "Patch enablement must stay behind config fetch after version preflight succeeds"
        );
        Assert.That(
            initPatchEnableIndex,
            Is.GreaterThan(menuPatchIndex),
            "TarkovInitPatch.Enable should remain downstream of the version preflight and config load path"
        );
    }

    [Test]
    public void TarkovInitPatch_ExposesVersionPreflightHelpers_AndDoesNotLateGateInPostfix()
    {
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/TarkovInitPatch.cs");

        Assert.That(
            source,
            Does.Contain("public static bool IsCurrentVersionSupported(out string currentVersion)"),
            "TarkovInitPatch must expose IsCurrentVersionSupported for plugin startup preflight"
        );
        Assert.That(
            source,
            Does.Contain("public static string BuildVersionErrorMessage(string modName, string currentVersion)"),
            "TarkovInitPatch must expose BuildVersionErrorMessage for the unsupported-version dependency error"
        );

        string postfixBody = ExtractMethodBody(source, "protected static void PatchPostfix");
        Assert.That(
            postfixBody,
            Does.Not.Contain("IsCurrentVersionSupported"),
            "PatchPostfix should not perform late version gating after patches are already enabled"
        );
        Assert.That(
            postfixBody,
            Does.Not.Contain("BuildVersionErrorMessage"),
            "PatchPostfix should not build dependency errors after the plugin has already proceeded past preflight"
        );
    }

    [Test]
    public void LocationData_Awake_UsesStaticInstance_NotFindObjectOfType()
    {
        // Bug fix: FindObjectOfType<QuestingBotsPlugin>() returned null, breaking LocationData
        // initialization. Now uses QuestingBotsPlugin.Instance static singleton.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/LocationData.cs");

        Assert.That(
            source,
            Does.Contain("QuestingBotsPlugin.Instance"),
            "LocationData.Awake should use QuestingBotsPlugin.Instance instead of FindObjectOfType"
        );
        Assert.That(
            source,
            Does.Not.Contain("FindObjectOfType<QuestingBotsPlugin>"),
            "LocationData should not use FindObjectOfType<QuestingBotsPlugin>()"
        );
    }

    [Test]
    public void LocationData_Awake_HasNullCheckForPlugin()
    {
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/LocationData.cs");

        Assert.That(source, Does.Contain("if (plugin == null)"), "LocationData.Awake should null-check the plugin instance");
    }

    [Test]
    public void LocationData_Awake_HasNullCheckForTarkovData()
    {
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/LocationData.cs");

        Assert.That(source, Does.Contain("if (tarkovData == null)"), "LocationData.Awake should null-check the TarkovData component");
    }

    [Test]
    public void BotQuestBuilder_LoadAllQuests_UsesStaticInstance_NotFindObjectOfType()
    {
        // Bug fix: Same FindObjectOfType issue as LocationData. BotQuestBuilder.LoadAllQuests
        // also used FindObjectOfType<QuestingBotsPlugin>() to get TarkovData/session.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/BotQuestBuilder.cs");

        Assert.That(
            source,
            Does.Contain("QuestingBotsPlugin.Instance"),
            "BotQuestBuilder should use QuestingBotsPlugin.Instance instead of FindObjectOfType"
        );
        Assert.That(
            source,
            Does.Not.Contain("FindObjectOfType<QuestingBotsPlugin>"),
            "BotQuestBuilder should not use FindObjectOfType<QuestingBotsPlugin>()"
        );
    }

    #endregion

    #region SleepingLayer null-safety regression test

    [Test]
    public void SleepingLayer_IsActive_HasNullCheckForLocationData()
    {
        // Bug fix: SleepingLayer.IsActive() (priority 99, checked first) accessed
        // LocationData.CurrentLocation.Id without null checks. When LocationData.Awake
        // failed, this NullRef crashed BigBrain's brain update for every bot,
        // leaving all bots braindead with no combat/movement/aggro.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/Sleep/SleepingLayer.cs");

        Assert.That(
            source,
            Does.Contain("locationData?.CurrentLocation == null"),
            "SleepingLayer.IsActive should null-check LocationData and CurrentLocation"
        );
    }

    #endregion

    #region BotHearingMonitor null-safety regression test

    [Test]
    public void BotHearingMonitor_UpdateMaxSuspiciousTime_HasNullCheckForLocationData()
    {
        // Bug fix: When LocationData.Awake fails, CurrentLocation is null.
        // The old code accessed .CurrentLocation.Id directly, causing a
        // NullReferenceException on every bot spawn (repeated ~20+ times per bot).
        var source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/BotMonitor/Monitors/BotHearingMonitor.cs");

        Assert.That(
            source,
            Does.Contain("locationData?.CurrentLocation == null"),
            "updateMaxSuspiciousTime should null-check LocationData and CurrentLocation before accessing .Id"
        );
    }

    #endregion

    #region GetComponent null-safety regression tests (batch 2)

    [Test]
    public void BotHiveMindMonitor_Update_HasNullSafeLocationDataAccess()
    {
        // Bug fix: BotHiveMindMonitor.Update() accessed GetComponent<LocationData>().CurrentLocation
        // without checking if GetComponent returned null. If LocationData wasn't added to GameWorld,
        // this NullRef'd on every HiveMind tick — a hot path.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindMonitor.cs");

        Assert.That(
            source,
            Does.Contain("locationData?.CurrentLocation == null"),
            "BotHiveMindMonitor.Update should null-safe access LocationData and CurrentLocation"
        );
    }

    [Test]
    public void BotObjectiveManager_SetInitialObjective_HasNullSafeBotQuestBuilderAccess()
    {
        // Bug fix: GetComponent<BotQuestBuilder>().HaveQuestsBeenBuilt NullRef'd if
        // BotQuestBuilder wasn't on GameWorld. Runs per-bot initialization.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/BotObjectiveManager.cs");

        Assert.That(
            source,
            Does.Contain("GetComponent<Components.BotQuestBuilder>()?.HaveQuestsBeenBuilt"),
            "BotObjectiveManager.setInitialObjective should use null-conditional on GetComponent<BotQuestBuilder>()"
        );
    }

    [Test]
    public void BotOwnerBrainActivatePatch_HasNullSafeDebugDataAccess()
    {
        // Bug fix: GetComponent<DebugData>().RegisterBot() NullRef'd if DebugData component
        // wasn't on GameWorld (e.g. LocationData.Awake failed before adding it).
        // Runs on every bot spawn.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/BotOwnerBrainActivatePatch.cs");

        Assert.That(
            source,
            Does.Contain("GetComponent<Components.DebugData>()?.RegisterBot"),
            "BotOwnerBrainActivatePatch should use null-conditional on GetComponent<DebugData>()"
        );
    }

    [Test]
    public void DebugData_Update_HasNullSafeBotQuestBuilderAccess()
    {
        // Bug fix: DebugData.Update() accessed GetComponent<BotQuestBuilder>().HaveQuestsBeenBuilt
        // without null-conditional.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/DebugData.cs");

        Assert.That(
            source,
            Does.Contain("GetComponent<BotQuestBuilder>()?.HaveQuestsBeenBuilt"),
            "DebugData.Update should use null-conditional on GetComponent<BotQuestBuilder>()"
        );
    }

    [Test]
    public void AirdropLandPatch_HasNullSafeBotQuestBuilderAccess()
    {
        // Bug fix: AirdropLandPatch accessed GetComponent<BotQuestBuilder>().AddAirdropChaserQuest()
        // without null-check. NullRef'd if questing wasn't initialized.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/AirdropLandPatch.cs");

        Assert.That(
            source,
            Does.Contain("questBuilder != null"),
            "AirdropLandPatch should null-check BotQuestBuilder before calling AddAirdropChaserQuest"
        );
    }

    [Test]
    public void BotPathData_HasNullCheckForBotQuestBuilder()
    {
        // Bug fix: BotPathData accessed GetComponent<BotQuestBuilder>() then called
        // .GetStaticPaths() without checking for null. NullRef'd during pathfinding
        // if questing wasn't initialized.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Models/Pathing/BotPathData.cs");

        Assert.That(
            source,
            Does.Contain("if (botQuestBuilder == null)"),
            "BotPathData should null-check BotQuestBuilder before calling GetStaticPaths"
        );
        Assert.That(
            source,
            Does.Not.Contain("break;"),
            "BotPathData should not use break (invalid outside loop/switch) — use else block instead"
        );
    }

    #endregion

    #region UpdateMaxTotalBots null-safety regression test

    [Test]
    public void LocationData_UpdateMaxTotalBots_HasNullSafeBotsControllerAccess()
    {
        // Bug fix: Singleton<IBotGame>.Instance.BotsController NullRef'd during
        // LocationData.Awake() because BotsController may not be initialized yet
        // when the component is first added by BotsControllerSetSettingsPatch.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/LocationData.cs");

        Assert.That(
            source,
            Does.Contain("Singleton<IBotGame>.Instance?.BotsController"),
            "UpdateMaxTotalBots should use null-conditional on Singleton<IBotGame>.Instance"
        );
        Assert.That(
            source,
            Does.Contain("if (botControllerClass == null)"),
            "UpdateMaxTotalBots should null-check botControllerClass before accessing MaxCount"
        );
    }

    #endregion

    #region Voice callout edge-trigger regression test

    [Test]
    public void BotHiveMindMonitor_VoiceCommands_UsesPreStrategyVersionSnapshot()
    {
        // Bug fix: updateSquadVoiceCommands() checked leader.LastSeenObjectiveVersion
        // against objective.Version to detect new-objective edges. But
        // _squadStrategyManager.Update() (which runs BEFORE voice commands) already
        // syncs leader.LastSeenObjectiveVersion = objective.Version in
        // GotoObjectiveStrategy.AssignNewObjective(). The edge trigger therefore
        // never fires because the version is always equal by the time the check runs.
        //
        // Fix: snapshot versions into _preStrategyObjectiveVersions BEFORE the
        // strategy update, then compare the snapshot in voice commands.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/HiveMind/BotHiveMindMonitor.cs");

        Assert.That(
            source,
            Does.Contain("_preStrategyObjectiveVersions"),
            "BotHiveMindMonitor should use _preStrategyObjectiveVersions dictionary to snapshot versions before strategy update"
        );
        Assert.That(
            source,
            Does.Contain("preStrategyVersion != objective.Version"),
            "Voice command edge trigger should compare preStrategyVersion (snapshot) against objective.Version"
        );
        // Ensure the old broken pattern is gone: the voice command section should NOT
        // compare leader.LastSeenObjectiveVersion directly against objective.Version.
        // (leader.LastSeenObjectiveVersion IS used elsewhere, e.g. in the snapshot loop,
        // but the edge-trigger comparison must use preStrategyVersion.)
        Assert.That(
            source,
            Does.Not.Contain("leader.LastSeenObjectiveVersion != objective.Version"),
            "Voice commands must not compare leader.LastSeenObjectiveVersion directly — use preStrategyVersion snapshot"
        );
    }

    #endregion

    #region Linger RNG regression test

    [Test]
    public void BotEntityBridge_LingerDuration_UsesSharedRng()
    {
        // Bug fix: SyncQuestState used `new System.Random().NextDouble()` to generate
        // linger durations. System.Random() seeds from Environment.TickCount, so two
        // bots completing objectives in the same millisecond get identical linger values.
        //
        // Fix: use a shared static `_lingerRng` field seeded once at class load.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ECS/BotEntityBridge.cs");

        Assert.That(
            source,
            Does.Contain("_lingerRng"),
            "BotEntityBridge should use shared _lingerRng field for linger duration randomness"
        );
        Assert.That(
            source,
            Does.Not.Contain("new System.Random().NextDouble()"),
            "BotEntityBridge must not use `new System.Random().NextDouble()` — poorly seeded per-call RNG"
        );
    }

    #endregion

    #region Empty assignments sentinel corruption regression test

    [Test]
    public void BotJobAssignmentFactory_UsesAddJobAssignment_NotGetJobAssignmentsAdd()
    {
        // Bug fix: BotJobAssignmentFactory called GetJobAssignments(bot).Add(assignment)
        // to register a new job. GetJobAssignments() returns a shared _emptyAssignments
        // list when the bot isn't registered, so .Add() on that sentinel corrupts it
        // for all future callers.
        //
        // Fix: use dedicated AddJobAssignment() which handles unregistered bots safely.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Controllers/BotJobAssignmentFactory.cs");

        Assert.That(
            source,
            Does.Contain("AddJobAssignment(bot, assignment)"),
            "BotJobAssignmentFactory should use AddJobAssignment() to safely add assignments"
        );
        Assert.That(
            source,
            Does.Not.Contain("GetJobAssignments(bot).Add("),
            "BotJobAssignmentFactory must not call GetJobAssignments().Add() — corrupts shared sentinel list"
        );
    }

    [Test]
    public void BotEntityBridge_HasAddJobAssignmentMethod()
    {
        // Verify the AddJobAssignment method exists and handles unregistered bots.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ECS/BotEntityBridge.cs");

        Assert.That(
            source,
            Does.Contain("public static void AddJobAssignment(BotOwner bot, BotJobAssignment assignment)"),
            "BotEntityBridge should have an AddJobAssignment method"
        );
        Assert.That(
            source,
            Does.Contain("assignment discarded"),
            "AddJobAssignment should log a warning when called for unregistered bots"
        );
    }

    #endregion

    #region TrySpawnFreeAndDelayPatch missing return regression test

    [Test]
    public void TrySpawnFreeAndDelayPatch_MaxTotalBots0_ReturnsAllowSpawn()
    {
        // Bug fix: When locationData.MaxTotalBots == 0, the code called
        // allowSpawn(pendingScavCount) without a 'return' keyword, causing
        // the method to fall through to the rate-limiting code below.
        // This meant Factory (which can have MaxTotalBots=0) would incorrectly
        // rate-limit scav spawns even when rate limiting should be skipped.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Spawning/ScavLimits/TrySpawnFreeAndDelayPatch.cs");

        Assert.That(
            source,
            Does.Contain("return allowSpawn(pendingScavCount);"),
            "TrySpawnFreeAndDelayPatch should return the result of allowSpawn when MaxTotalBots == 0"
        );
    }

    #endregion

    #region TimeHasComeScreenClassChangeStatusPatch NRE regression test

    [Test]
    public void TimeHasComeScreenClassChangeStatusPatch_CheckForInstances_DoesNotDereferenceNullInstance()
    {
        // Bug fix: checkForInstances() threw NullReferenceException when instance was null
        // because it called instance.GetType().Name inside the null check.
        // Fixed to use nameof(MatchmakerPlayerControllerClass) instead.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Spawning/TimeHasComeScreenClassChangeStatusPatch.cs");

        Assert.That(
            source,
            Does.Contain("nameof(MatchmakerPlayerControllerClass)"),
            "checkForInstances should use nameof() instead of instance.GetType().Name to avoid NRE"
        );
        Assert.That(source, Does.Not.Contain("instance.GetType().Name"), "checkForInstances must not dereference instance when it is null");
    }

    #endregion

    #region BotOwnerBrainActivatePatch random range regression test

    [Test]
    public void BotOwnerBrainActivatePatch_HostileChance_UsesCorrectRandomRange()
    {
        // Bug fix: random.Next(1, 100) produces values 1-99, so a chance of 80
        // yields 79/99 = 79.8% instead of 80/100 = 80%. The upper bound is exclusive
        // in Random.Next, so it should be random.Next(1, 101) for a 1-100 range.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/BotOwnerBrainActivatePatch.cs");

        Assert.That(
            source,
            Does.Contain("_sharedRandom.Next(1, 101)"),
            "shouldMakeBotGroupHostileTowardAllBosses should use _sharedRandom.Next(1, 101) for correct 1-100 range"
        );
        Assert.That(source, Does.Not.Contain(".Next(1, 100)"), ".Next(1, 100) gives 1-99 range (off-by-one), should be .Next(1, 101)");
    }

    #endregion

    #region MineDirectionalShouldExplodePatch result inversion regression test

    [Test]
    public void MineDirectionalShouldExplodePatch_QuestBots_MinesDoNotExplode()
    {
        // Bug fix: The patch set __result = true for bots with quests on Lightkeeper Island,
        // meaning "should explode = true". This is the opposite of the intended behavior --
        // bots going to the island for quests should pass through mines safely.
        // Fixed to __result = false so mines do NOT explode for quest bots.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Lighthouse/MineDirectionalShouldExplodePatch.cs");

        // After the check for BotsWithQuestsOnLightkeeperIsland, __result should be false
        Assert.That(
            source,
            Does.Contain("__result = false;"),
            "MineDirectionalShouldExplodePatch should set __result = false to prevent mines from exploding for quest bots"
        );
        Assert.That(
            source,
            Does.Not.Contain("__result = true;"),
            "__result = true would cause mines to explode FOR quest bots — the opposite of intent"
        );
    }

    #endregion

    #region LighthouseTraderZonePlayerAttackPatch null-safety regression test

    [Test]
    public void LighthouseTraderZonePlayerAttackPatch_HasNullCheckForAggressor()
    {
        // Bug fix: GetAlivePlayerByProfileID can return null if the aggressor died
        // between the attack event and the lookup. The code then called
        // .HasAGreenOrYellowDSP() on the null reference, causing an NRE.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Lighthouse/LighthouseTraderZonePlayerAttackPatch.cs");

        Assert.That(
            source,
            Does.Contain("lastAgressorPlayer == null"),
            "LighthouseTraderZonePlayerAttackPatch should null-check lastAgressorPlayer before calling methods on it"
        );
    }

    #endregion

    #region OnBeenKilledByAggressorPatch null-safety regression test

    [Test]
    public void OnBeenKilledByAggressorPatch_HasNullCheckForAliveInitialPMCs()
    {
        // Bug fix: AliveBots()?.ToArray() can return null if AliveBots() returns null.
        // The code then accessed .Length on the null array, causing an NRE.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/OnBeenKilledByAggressorPatch.cs");

        Assert.That(
            source,
            Does.Contain("if (aliveInitialPMCs != null)"),
            "OnBeenKilledByAggressorPatch should null-check aliveInitialPMCs before accessing .Length"
        );
    }

    #endregion

    #region TryLoadBotsProfilesOnStartPatch static state cleanup regression test

    [Test]
    public void TryLoadBotsProfilesOnStartPatch_HasClearMethod()
    {
        // Bug fix: GenerateBotsTasks is static and was never cleared between raids,
        // accumulating stale task references that leak memory and cause
        // RemainingBotGenerationTasks to report incorrect counts.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Spawning/TryLoadBotsProfilesOnStartPatch.cs");

        Assert.That(
            source,
            Does.Contain("public static void Clear()"),
            "TryLoadBotsProfilesOnStartPatch should have a Clear() method to reset static state between raids"
        );
        Assert.That(source, Does.Contain("GenerateBotsTasks.Clear()"), "Clear() should clear the GenerateBotsTasks list");
    }

    [Test]
    public void BotsControllerStopPatch_ClearsTryLoadBotsProfilesTasks()
    {
        // Verify that BotsControllerStopPatch calls TryLoadBotsProfilesOnStartPatch.Clear()
        // during raid cleanup to prevent static state leaking between raids.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/BotsControllerStopPatch.cs");

        Assert.That(
            source,
            Does.Contain("TryLoadBotsProfilesOnStartPatch.Clear()"),
            "BotsControllerStopPatch should clear TryLoadBotsProfilesOnStartPatch static state during raid cleanup"
        );
    }

    #endregion

    #region SpawnPointIsValidPatch empty sequence regression test

    [Test]
    public void SpawnPointIsValidPatch_HandlesEmptyPlayerList()
    {
        // Bug fix: .Min() on an empty IEnumerable throws InvalidOperationException.
        // HumanAndSimulatedPlayers() can return an empty sequence before any players
        // are registered. The code now checks .Any() first.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Spawning/ScavLimits/SpawnPointIsValidPatch.cs");

        Assert.That(
            source,
            Does.Contain("if (!humanPlayers.Any())"),
            "SpawnPointIsValidPatch should check for empty player collection before calling .Min()"
        );
    }

    #endregion

    #region LocationData FindLockedDoorsNearPosition null-key crash regression test

    [Test]
    public void LocationData_FindLockedDoorsNearPosition_HasContinueAfterNullRemoval()
    {
        // Bug fix: When a WorldInteractiveObject is destroyed (e.g. Backdoor Bandit),
        // the key becomes null. The code removes the null key from the dictionary but
        // was missing a 'continue' statement, causing the loop to proceed with the
        // null object on the next line — NullReferenceException on accessing .DoorState.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/LocationData.cs");

        // Verify the null check + remove + continue pattern exists
        Assert.That(
            source,
            Does.Contain("areLockedDoorsUnlocked.Remove(worldInteractiveObject);\n                    continue;"),
            "FindLockedDoorsNearPosition must have 'continue' immediately after removing null key from areLockedDoorsUnlocked"
        );
    }

    #endregion

    #region QuestMinLevelFinder null templateId cache lookup regression test

    [Test]
    public void QuestMinLevelFinder_FindMinLevel_NullSafeTemplateIdCacheLookup()
    {
        // Bug fix: Dictionary.ContainsKey(null) throws ArgumentNullException.
        // quest.Template?.Id can be null for custom quests (no EFT template).
        // The old code passed quest.Template?.Id directly to ContainsKey without
        // null-checking, crashing on any custom quest without a template.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/QuestMinLevelFinder.cs");

        // Verify templateId is extracted and null-checked before cache lookup
        Assert.That(
            source,
            Does.Contain("string templateId = quest.Template?.Id;"),
            "FindMinLevel should extract templateId before using it in ContainsKey"
        );
        Assert.That(
            source,
            Does.Contain("if (templateId != null && cachedMinLevelsForQuestIds.ContainsKey(templateId))"),
            "FindMinLevel should null-check templateId before passing to ContainsKey"
        );
    }

    #endregion

    #region ConfigController GetJson null lastException regression test

    [Test]
    public void ConfigController_GetJson_NullChecksLastExceptionBeforeAccess()
    {
        // Bug fix: If all 5 retry attempts return null without throwing an exception
        // (e.g. RequestHandler.GetJson returns null directly), lastException remains null.
        // The old code accessed lastException.Message unconditionally, causing an NRE.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Controllers/ConfigController.cs");

        // Verify the null check wraps the exception logging
        Assert.That(
            source,
            Does.Contain("if (lastException != null)"),
            "GetJson should null-check lastException before accessing .Message/.StackTrace"
        );
    }

    #endregion

    #region LightkeeperIslandMonitor revertAlliances KeyNotFoundException regression test

    [Test]
    public void LightkeeperIslandMonitor_RevertAlliances_UsesTryGetValueForAllies()
    {
        // Bug fix: revertAlliances accessed originalAllies[player] directly, which throws
        // KeyNotFoundException if the player was never added to originalAllies. This happens
        // when a player is added to playersOnIsland but their BotsGroup was null during
        // setTemporaryAlliances, causing setOriginalAllies to early-return without adding
        // the player to the dictionary.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/LightkeeperIslandMonitor.cs");

        Assert.That(
            source,
            Does.Contain("originalAllies.TryGetValue(player, out IPlayer[] allies)"),
            "revertAlliances should use TryGetValue for originalAllies instead of direct indexer access"
        );
        Assert.That(
            source,
            Does.Contain("originalEnemies.TryGetValue(player, out IPlayer[] enemies)"),
            "revertAlliances should use TryGetValue for originalEnemies instead of direct indexer access"
        );
    }

    #endregion

    #region LocationData OnDestroy event handler leak regression test

    [Test]
    public void LocationData_HasOnDestroyThatUnsubscribesSwitchEvents()
    {
        // Bug fix: LocationData subscribes to OnDoorStateChanged for each switch in
        // FindAllSwitches but never unsubscribes. When the GameWorld is destroyed between
        // raids, the event delegates keep the LocationData instance alive (GC root via
        // the switch's event invocation list), leaking the entire component and all its
        // referenced data structures.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Components/LocationData.cs");

        Assert.That(
            source,
            Does.Contain("protected void OnDestroy()"),
            "LocationData should have an OnDestroy method to clean up event subscriptions"
        );
        Assert.That(
            source,
            Does.Contain("OnDoorStateChanged -= reportSwitchChange"),
            "LocationData.OnDestroy should unsubscribe from OnDoorStateChanged events"
        );
    }

    #endregion

    #region Helpers

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0;
        int index = 0;
        while ((index = text.IndexOf(pattern, index, System.StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += pattern.Length;
        }
        return count;
    }

    private static string ExtractMethodBody(string source, string methodSignature)
    {
        int methodStart = source.IndexOf(methodSignature, System.StringComparison.Ordinal);
        Assert.That(methodStart, Is.GreaterThan(-1), $"Method signature not found: {methodSignature}");

        int braceStart = source.IndexOf('{', methodStart);
        Assert.That(braceStart, Is.GreaterThan(-1), $"Opening brace not found for method: {methodSignature}");

        int braceCount = 1;
        int pos = braceStart + 1;
        while (braceCount > 0 && pos < source.Length)
        {
            if (source[pos] == '{')
            {
                braceCount++;
            }
            else if (source[pos] == '}')
            {
                braceCount--;
            }
            pos++;
        }

        Assert.That(braceCount, Is.EqualTo(0), $"Failed to parse method body for: {methodSignature}");
        return source.Substring(braceStart, pos - braceStart);
    }

    #endregion
}
