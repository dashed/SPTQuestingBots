using System.IO;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.E2E;

[TestFixture]
public class ExternalModCompatibilityPipelineTests
{
    private static readonly string RepoRoot = FindRepoRoot();

    [Test]
    public void ExternalModHandler_EmitsStartupSummary_AndEscalatesRequiredInteropFailures()
    {
        string handlerSource = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ExternalMods/ExternalModHandler.cs");

        Assert.That(
            handlerSource,
            Does.Contain("External mod compatibility summary:")
                .And.Contain("logStartupCompatibilitySummary(installedMods);")
                .And.Contain("modInfo.RecordCompatibilityResult(isCompatible);")
                .And.Contain("modInfo.UsesInterop && !modInfo.CheckInteropAvailability()")
                .And.Contain("if (modInfo.IsInteropRequiredForCurrentConfig)")
                .And.Contain("addDependencyError(interopMessage);")
                .And.Contain("modInfo.BuildStartupSummary(modInfo.CompatibilitySatisfied)"),
            "Client startup should summarize external-mod health and escalate config-required interop failures."
        );
    }

    [Test]
    public void NativeLootingFallback_RemainsEnabled_WhenLootingBotsInteropIsUnavailable()
    {
        string handlerSource = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ExternalMods/ExternalModHandler.cs");
        string lootingModSource = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ExternalMods/ModInfo/LootingBotsModInfo.cs");

        Assert.Multiple(() =>
        {
            Assert.That(
                ExtractMethodBody(handlerSource, "public static bool IsNativeLootingEnabled()"),
                Does.Contain("return !LootingBotsModInfo.ShouldUseExternalLooting();"),
                "Native looting should stay enabled when LootingBots is detected but its API cannot be used."
            );
            Assert.That(
                lootingModSource,
                Does.Contain("Falling back to QuestingBots native looting behavior.")
                    .And.Contain("IsInteropRequiredForCurrentConfig => shouldDisableNativeLootingWhenLootingBotsDetected()")
                    .And.Contain("ShouldUseExternalLooting()")
                    .And.Contain("using QuestingBots native looting because LootingBots interop is unavailable"),
                "LootingBots compatibility messaging should make the native-looting fallback explicit."
            );
        });
    }

    [Test]
    public void SAINCompatibilityPipeline_MakesRequiredInteropAndFallbackExplicit()
    {
        string sainModSource = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ExternalMods/ModInfo/SAINModInfo.cs");

        Assert.That(
            sainModSource,
            Does.Contain("IsInteropRequiredForCurrentConfig => shouldUseSainForExtracting()")
                .And.Contain("Falling back to QuestingBots extraction behavior.")
                .And.Contain("using SAIN extraction and hearing integration")
                .And.Contain("using QuestingBots extraction while keeping SAIN hearing integration")
                .And.Contain("using QuestingBots extraction and hearing behavior"),
            "SAIN compatibility handling should declare when interop is required and what fallback path QuestingBots uses."
        );
    }

    [Test]
    public void ExternalModHandler_DedupesDependencyErrors_AndKeepsNoModsSummaryVisible()
    {
        string handlerSource = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ExternalMods/ExternalModHandler.cs");

        Assert.That(
            handlerSource,
            Does.Contain("if (!Chainloader.DependencyErrors.Contains(message))")
                .And.Contain("Chainloader.DependencyErrors.Add(message);")
                .And.Contain("External mod compatibility summary: no supported external mods detected.")
                .And.Contain(
                    "LoggingController.LogInfo(\"External mod compatibility summary: no supported external mods detected.\", true);"
                )
                .And.Contain("LoggingController.LogInfo(\"External mod compatibility summary:\", true);"),
            "External-mod startup should dedupe dependency errors and keep the summary visible even when no supported mods are installed."
        );
    }

    [Test]
    public void ExternalModSummary_CarriesInteropStatusMetadata_IntoTheStartupReport()
    {
        string modInfoSource = ReadSourceFile("src/SPTQuestingBots.Client/BotLogic/ExternalMods/ModInfo/AbstractExternalModInfo.cs");

        Assert.That(
            modInfoSource,
            Does.Contain("public virtual string InteropStatusMessage")
                .And.Contain("protected bool SetInteropAvailability")
                .And.Contain("CompatibilitySatisfied")
                .And.Contain("BuildStartupSummary")
                .And.Contain("InteropStatusMessage"),
            "The startup summary should keep the detailed interop-status message that explains why a detected mod is healthy or degraded."
        );
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
        string fullPath = Path.Combine(RepoRoot, relativePath);
        Assert.That(File.Exists(fullPath), Is.True, $"Source file not found: {fullPath}");
        return File.ReadAllText(fullPath);
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

        Assert.That(braceCount, Is.EqualTo(0), "Failed to parse balanced source block");
        return source.Substring(braceStart, pos - braceStart);
    }
}
