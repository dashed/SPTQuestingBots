using System.IO;
using System.Linq;
using NUnit.Framework;

namespace SPTQuestingBots.Client.Tests.Telemetry;

/// <summary>
/// Verifies that the build output contains all DLLs required for SQLite
/// telemetry to work on Unity/Mono at runtime. These tests catch the
/// "Could not load file or assembly 'System.ValueTuple'" class of failures
/// that can't be detected by .NET 9 unit tests alone.
/// </summary>
[TestFixture]
public class TelemetryDeploymentTests
{
    private static readonly string BuildDir = Path.GetFullPath(
        Path.Combine(TestContext.CurrentContext.TestDirectory, "..", "..", "..", "..", "..", "build")
    );

    private static readonly string[] RequiredManagedDlls = new[]
    {
        "Microsoft.Data.Sqlite.dll",
        "SQLitePCLRaw.batteries_v2.dll",
        "SQLitePCLRaw.core.dll",
        "SQLitePCLRaw.provider.e_sqlite3.dll",
        "System.Memory.dll",
        "System.Buffers.dll",
        "System.Numerics.Vectors.dll",
        "System.Runtime.CompilerServices.Unsafe.dll",
    };

    [Test]
    public void BuildOutput_ContainsMainPluginDll()
    {
        string dll = Path.Combine(BuildDir, "SPTQuestingBots.dll");
        Assert.That(File.Exists(dll), Is.True, "SPTQuestingBots.dll missing from build/");
    }

    [TestCaseSource(nameof(RequiredManagedDlls))]
    public void BuildOutput_ContainsRequiredNuGetDll(string dllName)
    {
        string dll = Path.Combine(BuildDir, dllName);
        Assert.That(File.Exists(dll), Is.True, dllName + " missing from build/ — Unity/Mono will fail at runtime");
    }

    [Test]
    public void BuildOutput_AllSqliteDllsPresent()
    {
        var missing = RequiredManagedDlls
            .Where(dll => !File.Exists(Path.Combine(BuildDir, dll)))
            .ToList();

        Assert.That(missing, Is.Empty, "Missing SQLite runtime DLLs: " + string.Join(", ", missing));
    }

    [Test]
    public void NuGetCache_ContainsNativeSqliteDll()
    {
        // The native e_sqlite3.dll comes from the NuGet cache, not the build output.
        // Verify it exists where the Makefile expects it.
        string nugetCache = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            ".nuget",
            "packages"
        );

        string[] candidates = Directory.Exists(nugetCache)
            ? Directory.GetFiles(nugetCache, "e_sqlite3.dll", SearchOption.AllDirectories)
                .Where(p => p.Contains("win-x64"))
                .ToArray()
            : System.Array.Empty<string>();

        Assert.That(candidates, Is.Not.Empty, "e_sqlite3.dll (win-x64) not found in NuGet cache");
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
