using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using NUnit.Framework;

namespace AssemblyInspector.Tests;

/// <summary>
/// Integration tests that run against the actual Assembly-CSharp.dll.
/// These tests are skipped if the DLL is not present (e.g., in CI without game DLLs).
/// </summary>
[TestFixture]
public class IntegrationTests
{
    // Relative to the repo root — tests run from the test bin directory,
    // so we walk up to find the repo root.
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string DllPath = Path.Combine(RepoRoot, "libs", "Assembly-CSharp.dll");
    private static readonly string SourcePath = Path.Combine(RepoRoot, "src", "SPTQuestingBots.Client", "Helpers", "ReflectionHelper.cs");

    private static string FindRepoRoot()
    {
        string dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "SPTQuestingBots.sln")))
            {
                return dir;
            }

            string? parent = Path.GetDirectoryName(dir);
            if (parent == dir)
            {
                break;
            }

            dir = parent!;
        }

        // Fallback: try working directory
        return Directory.GetCurrentDirectory();
    }

    [Test]
    public void InspectBotSpawner_FindsKnownFields()
    {
        if (!File.Exists(DllPath))
        {
            Assert.Ignore($"Assembly-CSharp.dll not found at {DllPath}. Run 'make copy-libs'.");
        }

        using var assembly = AssemblyDefinition.ReadAssembly(DllPath, new ReaderParameters { ReadSymbols = false });

        var allTypes = assembly.MainModule.Types.SelectMany(FlattenNestedTypes).ToList();

        var botSpawner = allTypes.FirstOrDefault(t => t.Name.Equals("BotSpawner", StringComparison.OrdinalIgnoreCase));
        Assert.That(botSpawner, Is.Not.Null, "BotSpawner type should exist in Assembly-CSharp.dll");

        // Verify the known fields we reference in ReflectionHelper.cs
        var fieldNames = botSpawner!.Fields.Select(f => f.Name).ToList();
        Assert.That(fieldNames, Does.Contain("Bots"), "BotSpawner should have 'Bots' field");
        Assert.That(fieldNames, Does.Contain("OnBotRemoved"), "BotSpawner should have 'OnBotRemoved' field");
        Assert.That(fieldNames, Does.Contain("AllPlayers"), "BotSpawner should have 'AllPlayers' field");
    }

    [Test]
    public void ValidateCommand_AllFieldsPass()
    {
        if (!File.Exists(DllPath))
        {
            Assert.Ignore($"Assembly-CSharp.dll not found at {DllPath}. Run 'make copy-libs'.");
        }

        if (!File.Exists(SourcePath))
        {
            Assert.Ignore($"ReflectionHelper.cs not found at {SourcePath}.");
        }

        // Capture console output
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);

        try
        {
            int exitCode = ValidateCommand.Run(DllPath, SourcePath);

            string output = outWriter.ToString();
            string errors = errWriter.ToString();

            Assert.That(exitCode, Is.EqualTo(0), $"Validate should pass. Errors: {errors}");
            Assert.That(output, Does.Contain("PASS"), "Output should contain PASS");
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Test]
    public void InspectCommand_ReturnsZeroForKnownType()
    {
        if (!File.Exists(DllPath))
        {
            Assert.Ignore($"Assembly-CSharp.dll not found at {DllPath}. Run 'make copy-libs'.");
        }

        // Capture console output
        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            int exitCode = InspectCommand.Run("BotSpawner", DllPath);

            string output = outWriter.ToString();
            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output, Does.Contain("BotSpawner"));
            Assert.That(output, Does.Contain("Fields:"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Test]
    public void InspectCommand_ReturnsOneForUnknownType()
    {
        if (!File.Exists(DllPath))
        {
            Assert.Ignore($"Assembly-CSharp.dll not found at {DllPath}. Run 'make copy-libs'.");
        }

        // Capture console output
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);

        try
        {
            int exitCode = InspectCommand.Run("CompletelyFakeTypeThatDoesNotExist12345", DllPath);
            Assert.That(exitCode, Is.EqualTo(1));
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }

    [Test]
    public void ParseKnownFields_ParsesActualReflectionHelper()
    {
        if (!File.Exists(SourcePath))
        {
            Assert.Ignore($"ReflectionHelper.cs not found at {SourcePath}.");
        }

        var entries = ValidateCommand.ParseKnownFields(SourcePath);

        Assert.That(entries.Count, Is.GreaterThanOrEqualTo(10), "Should parse at least 10 entries");
        Assert.That(entries.Any(e => e.TypeName == "BotSpawner" && e.FieldName == "Bots"), Is.True, "Should find BotSpawner.Bots entry");
    }

    private static IEnumerable<TypeDefinition> FlattenNestedTypes(TypeDefinition type)
    {
        yield return type;
        foreach (var nested in type.NestedTypes)
        {
            foreach (var t in FlattenNestedTypes(nested))
            {
                yield return t;
            }
        }
    }
}
