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
    public void TrySpawnFreeAndDelayPatch_UsesFloat2_WithCapitalF()
    {
        // Bug fix: "float_2" was changed to "Float_2" to match BSG deobfuscator output.
        // The field is on NonWavesSpawnScenario and controls retry delay.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Patches/Spawning/ScavLimits/TrySpawnFreeAndDelayPatch.cs");

        Assert.That(source, Does.Contain("\"Float_2\""), "Field name should be Float_2 (capital F)");
        Assert.That(source, Does.Not.Contain("\"float_2\""), "Old field name float_2 (lowercase f) should not be present");
    }

    [Test]
    public void BotPathingHelpers_UsesVector3_0_WithLowercaseV()
    {
        // Bug fix: "Vector3_0" was changed to "vector3_0" to match BSG deobfuscator output.
        // The field is on BotCurrentPathAbstractClass and holds path corner points.
        var source = ReadSourceFile("src/SPTQuestingBots.Client/Helpers/BotPathingHelpers.cs");

        Assert.That(source, Does.Contain("\"vector3_0\""), "Field name should be vector3_0 (lowercase v)");
        Assert.That(source, Does.Not.Contain("\"Vector3_0\""), "Old field name Vector3_0 (capital V) should not be present");
    }

    [TestCase(
        "src/SPTQuestingBots.Client/Patches/Spawning/ScavLimits/TrySpawnFreeAndDelayPatch.cs",
        "Float_2",
        "NonWavesSpawnScenario retry delay"
    )]
    [TestCase("src/SPTQuestingBots.Client/Helpers/BotPathingHelpers.cs", "vector3_0", "BotCurrentPathAbstractClass path points")]
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
