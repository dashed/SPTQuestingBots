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

    #endregion
}
