using System.IO;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.E2E;

/// <summary>
/// Source-backed startup pipeline coverage for client bootstrap code that cannot
/// be executed directly in the test environment because it depends on Unity/EFT.
/// </summary>
[TestFixture]
public class ClientStartupPipelineTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Test]
    public void UnsupportedVersion_Preflight_StopsStartupBeforeConfigFetch_AndPatchEnablement()
    {
        string pluginSource = ReadSourceFile("src/SPTQuestingBots.Client/QuestingBotsPlugin.cs");
        string patchSource = ReadSourceFile("src/SPTQuestingBots.Client/Patches/TarkovInitPatch.cs");

        string preflightBlock = ExtractBlock(
            pluginSource,
            "if (!Patches.TarkovInitPatch.IsCurrentVersionSupported(out string currentVersion))"
        );

        int modNameIndex = pluginSource.IndexOf("ModName = Info.Metadata.Name;", System.StringComparison.Ordinal);
        int preflightIndex = pluginSource.IndexOf(
            "if (!Patches.TarkovInitPatch.IsCurrentVersionSupported(out string currentVersion))",
            System.StringComparison.Ordinal
        );
        int configFetchIndex = pluginSource.IndexOf("ConfigController.GetConfig()", System.StringComparison.Ordinal);
        int menuPatchIndex = pluginSource.IndexOf("new Patches.MenuShowPatch().Enable();", System.StringComparison.Ordinal);
        int initPatchEnableIndex = pluginSource.IndexOf("new Patches.TarkovInitPatch().Enable();", System.StringComparison.Ordinal);

        Assert.That(modNameIndex, Is.GreaterThanOrEqualTo(0), "Startup should resolve ModName before gating initialization");
        Assert.That(preflightIndex, Is.GreaterThan(modNameIndex), "Version preflight should run immediately after ModName is available");
        Assert.That(
            preflightBlock,
            Does.Contain("Patches.TarkovInitPatch.BuildVersionErrorMessage(ModName, currentVersion)"),
            "Unsupported-version startup should build its error via TarkovInitPatch.BuildVersionErrorMessage"
        );
        Assert.That(
            preflightBlock,
            Does.Contain("Chainloader.DependencyErrors.Add(versionError);")
                .And.Contain("Logger.LogError(versionError);")
                .And.Contain("return;"),
            "Unsupported-version startup should surface the dependency error and abort Awake before continuing"
        );
        Assert.That(
            configFetchIndex,
            Is.GreaterThan(preflightIndex),
            "Config fetch must stay behind version preflight so unsupported clients fail before server access"
        );
        Assert.That(
            menuPatchIndex,
            Is.GreaterThan(configFetchIndex),
            "Patch enablement should only begin after the version preflight and config fetch complete"
        );
        Assert.That(
            initPatchEnableIndex,
            Is.GreaterThan(menuPatchIndex),
            "TarkovInitPatch.Enable must remain downstream of the startup preflight gate"
        );

        Assert.That(
            patchSource,
            Does.Contain("public static bool IsCurrentVersionSupported(out string currentVersion)")
                .And.Contain("public static string BuildVersionErrorMessage(string modName, string currentVersion)"),
            "The startup pipeline depends on TarkovInitPatch exposing the preflight helper pair"
        );
        Assert.That(
            ExtractMethodBody(patchSource, "protected static void PatchPostfix"),
            Does.Not.Contain("IsCurrentVersionSupported").And.Not.Contain("BuildVersionErrorMessage"),
            "Late patch postfix execution must not repeat version gating after startup has already proceeded"
        );
    }

    [Test]
    public void PlayerScavChancePipeline_UsesLocalInterpolation_AndNoDynamicRoute()
    {
        string raidHelpersSource = ReadSourceFile("src/SPTQuestingBots.Client/Helpers/RaidHelpers.cs");
        string configControllerSource = ReadSourceFile("src/SPTQuestingBots.Client/Controllers/ConfigController.cs");
        string serverPluginSource = ReadSourceFile("src/SPTQuestingBots.Server/QuestingBotsServerPlugin.cs");
        string removedRouterPath = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Server", "Routers", "QuestingBotsDynamicRouter.cs");

        Assert.That(
            raidHelpersSource,
            Does.Contain("ConfigController.Config.AdjustPScavChance.ChanceVsTimeRemainingFraction"),
            "RaidHelpers should derive Player Scav chance from the configured local interpolation curve"
        );
        Assert.That(
            configControllerSource,
            Does.Not.Contain("/QuestingBots/AdjustPScavChance"),
            "Client startup should not depend on the removed AdjustPScavChance dynamic route"
        );
        Assert.That(
            serverPluginSource,
            Does.Contain("botConfig.ChanceAssaultScavHasPlayerScavName = 0;"),
            "Server startup should only zero SPT's built-in Player Scav conversion chance to avoid conflicts"
        );
        Assert.That(File.Exists(removedRouterPath), Is.False, "The obsolete AdjustPScavChance dynamic router should remain deleted");
    }

    [Test]
    public void DebugPatchPipeline_OnlyEnablesTrackedDebugPatches_WhenDebugConfigIsEnabled()
    {
        string pluginSource = ReadSourceFile("src/SPTQuestingBots.Client/QuestingBotsPlugin.cs");
        string debugGateBlock = ExtractBlock(pluginSource, "if (ConfigController.Config.Debug.Enabled)");

        string processSourceOcclusionPath = Path.Combine(
            RepoRoot,
            "src",
            "SPTQuestingBots.Client",
            "Patches",
            "Debug",
            "ProcessSourceOcclusionPatch.cs"
        );
        string handleFinishedTaskPath = Path.Combine(
            RepoRoot,
            "src",
            "SPTQuestingBots.Client",
            "Patches",
            "Debug",
            "HandleFinishedTaskPatch.cs"
        );

        Assert.Multiple(() =>
        {
            Assert.That(
                debugGateBlock,
                Does.Contain("new Patches.Debug.ProcessSourceOcclusionPatch().Enable();")
                    .And.Contain("//new Patches.Debug.HandleFinishedTaskPatch().Enable();")
                    .And.Contain("//new Patches.Debug.HandleFinishedTaskPatch2().Enable();"),
                "Debug-only patch registration should stay behind the debug config gate."
            );
            Assert.That(
                File.Exists(processSourceOcclusionPath),
                Is.True,
                "The ProcessSourceOcclusion debug patch source should be tracked."
            );
            Assert.That(File.Exists(handleFinishedTaskPath), Is.True, "The HandleFinishedTask debug patch source should be tracked.");
        });
    }

    [Test]
    public void SuccessfulStartup_BootstrapsLoggingAndConfigSyncInStableOrder()
    {
        string pluginSource = ReadSourceFile("src/SPTQuestingBots.Client/QuestingBotsPlugin.cs");

        int fileLoggerIndex = pluginSource.IndexOf("LoggingController.InitFileLogger();", System.StringComparison.Ordinal);
        int menuPatchIndex = pluginSource.IndexOf("new Patches.MenuShowPatch().Enable();", System.StringComparison.Ordinal);
        int initPatchEnableIndex = pluginSource.IndexOf("new Patches.TarkovInitPatch().Enable();", System.StringComparison.Ordinal);
        int buildOptionsIndex = pluginSource.IndexOf(
            "QuestingBotsPluginConfig.BuildConfigOptions(Config);",
            System.StringComparison.Ordinal
        );
        int configSyncIndex = pluginSource.IndexOf("ConfigSync.SyncToModConfig();", System.StringComparison.Ordinal);
        int settingChangedIndex = pluginSource.IndexOf(
            "Config.SettingChanged += (_, _) => ConfigSync.SyncToModConfig();",
            System.StringComparison.Ordinal
        );
        int tarkovDataIndex = pluginSource.IndexOf("this.GetOrAddComponent<TarkovData>();", System.StringComparison.Ordinal);

        Assert.Multiple(() =>
        {
            Assert.That(fileLoggerIndex, Is.GreaterThanOrEqualTo(0), "Successful startup should initialize dedicated file logging.");
            Assert.That(menuPatchIndex, Is.GreaterThan(fileLoggerIndex), "Menu setup should happen after file logging is initialized.");
            Assert.That(
                initPatchEnableIndex,
                Is.GreaterThan(menuPatchIndex),
                "Gameplay patch enablement should remain downstream of menu setup."
            );
            Assert.That(
                buildOptionsIndex,
                Is.GreaterThan(initPatchEnableIndex),
                "F12 option construction should stay after the main patch-enable path."
            );
            Assert.That(
                configSyncIndex,
                Is.GreaterThan(buildOptionsIndex),
                "Initial config sync should happen only after F12 options are built."
            );
            Assert.That(
                settingChangedIndex,
                Is.GreaterThan(configSyncIndex),
                "The SettingChanged subscription should remain downstream of the first config sync."
            );
            Assert.That(
                tarkovDataIndex,
                Is.GreaterThan(settingChangedIndex),
                "TarkovData should be added only after startup config synchronization is wired."
            );
        });
    }

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

    private static string ReadSourceFile(string relativePath)
    {
        var fullPath = Path.Combine(RepoRoot, relativePath);
        Assert.That(File.Exists(fullPath), Is.True, $"Source file not found: {fullPath}");
        return File.ReadAllText(fullPath);
    }

    private static string ExtractBlock(string source, string blockStart)
    {
        int blockIndex = source.IndexOf(blockStart, System.StringComparison.Ordinal);
        Assert.That(blockIndex, Is.GreaterThan(-1), $"Block start not found: {blockStart}");

        int braceStart = source.IndexOf('{', blockIndex);
        Assert.That(braceStart, Is.GreaterThan(-1), $"Opening brace not found for block: {blockStart}");

        return ExtractBalancedBody(source, braceStart);
    }

    private static string ExtractMethodBody(string source, string methodSignature)
    {
        int methodStart = source.IndexOf(methodSignature, System.StringComparison.Ordinal);
        Assert.That(methodStart, Is.GreaterThan(-1), $"Method signature not found: {methodSignature}");

        int braceStart = source.IndexOf('{', methodStart);
        Assert.That(braceStart, Is.GreaterThan(-1), $"Opening brace not found for method: {methodSignature}");

        return ExtractBalancedBody(source, braceStart);
    }

    private static string ExtractBalancedBody(string source, int braceStart)
    {
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

        Assert.That(braceCount, Is.EqualTo(0), "Failed to parse balanced source block");
        return source.Substring(braceStart, pos - braceStart);
    }
}
