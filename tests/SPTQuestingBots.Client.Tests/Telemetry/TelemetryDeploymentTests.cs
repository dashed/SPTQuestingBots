using System.IO;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Telemetry;

/// <summary>
/// Verifies build output and telemetry config defaults.
/// SQLite is now provided by Unity's built-in Mono.Data.Sqlite — no NuGet
/// runtime DLLs need to be deployed alongside the plugin.
/// </summary>
[TestFixture]
public class TelemetryDeploymentTests
{
    private static readonly string BuildDir = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "build")
    );

    [Test]
    public void BuildOutput_ContainsMainPluginDll()
    {
        Assume.That(Directory.Exists(BuildDir), "build/ directory not present — skipping (requires make build)");

        string dll = Path.Combine(BuildDir, "SPTQuestingBots.dll");
        Assert.That(File.Exists(dll), Is.True, "SPTQuestingBots.dll missing from build/");
    }

    [Test]
    public void BuildOutput_DoesNotContainObsoleteNuGetDlls()
    {
        Assume.That(Directory.Exists(BuildDir), "build/ directory not present — skipping (requires make build)");

        // These DLLs were needed when using Microsoft.Data.Sqlite + SQLitePCLRaw.
        // With Mono.Data.Sqlite they should no longer be in build output.
        string[] obsoleteDlls = new[]
        {
            "Microsoft.Data.Sqlite.dll",
            "SQLitePCLRaw.batteries_v2.dll",
            "SQLitePCLRaw.core.dll",
            "SQLitePCLRaw.provider.e_sqlite3.dll",
        };

        foreach (string dll in obsoleteDlls)
        {
            string path = Path.Combine(BuildDir, dll);
            Assert.That(File.Exists(path), Is.False, dll + " should not be in build/ — we use Mono.Data.Sqlite now");
        }
    }

    [Test]
    public void TelemetryConfig_DefaultDbPath_IsRelative()
    {
        var config = new SPTQuestingBots.Telemetry.TelemetryConfig();
        Assert.That(Path.IsPathRooted(config.DbPath), Is.False, "Default DB path should be relative");
        Assert.That(config.DbPath, Does.EndWith(".db"), "Default DB path should end with .db");
    }

    [Test]
    public void TelemetryConfig_DefaultEnabled_IsTrue()
    {
        var config = new SPTQuestingBots.Telemetry.TelemetryConfig();
        Assert.That(config.Enabled, Is.True);
    }
}
