using System;
using System.IO;
using NUnit.Framework;

namespace AssemblyInspector.Tests;

[TestFixture]
public class DecompileCommandTests
{
    // Use the AssemblyInspector DLL itself as the test target — always available.
    private static readonly string TestDllPath = typeof(DecompileCommand).Assembly.Location;

    [Test]
    public void DecompileType_KnownType_ReturnsZeroAndContainsClassName()
    {
        var (exitCode, output, _) = CaptureRun("AssemblyHelper", null);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("AssemblyHelper"));
        Assert.That(output, Does.Contain("FlattenNestedTypes"));
    }

    [Test]
    public void DecompileType_IncludesFields()
    {
        // Program has static fields like DefaultDllPath
        var (exitCode, output, _) = CaptureRun("Program", null);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("Program"));
        Assert.That(output, Does.Contain("DefaultDllPath"));
    }

    [Test]
    public void DecompileMethod_KnownMethod_ReturnsZeroAndContainsBody()
    {
        var (exitCode, output, _) = CaptureRun("InspectCommand", "IsSubsequenceMatch");

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("IsSubsequenceMatch"));
        Assert.That(output, Does.Contain("searchTerm"));
    }

    [Test]
    public void DecompileMethod_UnknownMethod_ReturnsOne()
    {
        var (exitCode, _, errors) = CaptureRun("AssemblyHelper", "NonExistentMethod12345");

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(errors, Does.Contain("No method"));
    }

    [Test]
    public void DecompileType_UnknownType_ReturnsOne()
    {
        var (exitCode, _, errors) = CaptureRun("CompletelyFakeTypeThatDoesNotExist12345", null);

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(errors, Does.Contain("No type matching"));
    }

    [Test]
    public void DecompileType_PartialNameMatch_Works()
    {
        // "AssemblyHelp" should match "AssemblyHelper" via contains
        var (exitCode, output, _) = CaptureRun("AssemblyHelp", null);

        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(output, Does.Contain("AssemblyHelper"));
    }

    [Test]
    public void DecompileMethod_ListsAvailableMethods_OnNotFound()
    {
        var (exitCode, _, errors) = CaptureRun("AssemblyHelper", "Fake");

        Assert.That(exitCode, Is.EqualTo(1));
        Assert.That(errors, Does.Contain("Available methods:"));
        Assert.That(errors, Does.Contain("FlattenNestedTypes"));
    }

    private static (int ExitCode, string Output, string Errors) CaptureRun(string typeName, string? methodName)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);

        try
        {
            int exitCode = DecompileCommand.Run(typeName, TestDllPath, methodName);
            return (exitCode, outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
