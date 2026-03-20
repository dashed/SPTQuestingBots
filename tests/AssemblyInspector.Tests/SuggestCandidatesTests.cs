using System;
using System.IO;
using Mono.Cecil;
using NUnit.Framework;

namespace AssemblyInspector.Tests;

[TestFixture]
public class SuggestCandidatesTests
{
    private ModuleDefinition _module = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        string assemblyPath = typeof(SuggestCandidatesTests).Assembly.Location;
        _module = ModuleDefinition.ReadModule(assemblyPath);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _module?.Dispose();
    }

    private TypeDefinition CreateTypeWithFields(params (string Name, TypeReference Type)[] fields)
    {
        var type = new TypeDefinition("Test", "TestType", TypeAttributes.Class | TypeAttributes.Public, _module.TypeSystem.Object);

        foreach (var (name, fieldType) in fields)
        {
            type.Fields.Add(new FieldDefinition(name, FieldAttributes.Private, fieldType));
        }

        return type;
    }

    [Test]
    public void MissingFloat2_WithFloat3Available_HighConfidenceSuggestion()
    {
        var type = CreateTypeWithFields(
            ("float_0", _module.TypeSystem.Single),
            ("float_1", _module.TypeSystem.Single),
            ("float_3", _module.TypeSystem.Single)
        );

        var (output, _) = CaptureOutput(() => ValidateCommand.SuggestCandidates(type, "float_2"));

        Assert.That(output, Does.Contain("HIGH"));
        Assert.That(output, Does.Contain("float_3"));
        Assert.That(output, Does.Contain("float_2 -> float_3"));
    }

    [Test]
    public void MissingVector3_0_WithVector3_1Available_HighConfidenceSuggestion()
    {
        var vectorType = new TypeReference("UnityEngine", "Vector3", _module, _module.TypeSystem.CoreLibrary);

        var type = CreateTypeWithFields(("Vector3_1", vectorType));

        var (output, _) = CaptureOutput(() => ValidateCommand.SuggestCandidates(type, "Vector3_0"));

        Assert.That(output, Does.Contain("HIGH"));
        Assert.That(output, Does.Contain("Vector3_1"));
        Assert.That(output, Does.Contain("Vector3_0 -> Vector3_1"));
    }

    [Test]
    public void MissingField_WithNoSimilarCandidates_ShowsAllFields()
    {
        var type = CreateTypeWithFields(("Bots", _module.TypeSystem.Object), ("AllPlayers", _module.TypeSystem.Object));

        var (output, _) = CaptureOutput(() => ValidateCommand.SuggestCandidates(type, "float_2"));

        Assert.That(output, Does.Contain("All fields on TestType:"));
        Assert.That(output, Does.Contain("Bots"));
        Assert.That(output, Does.Contain("AllPlayers"));
    }

    [Test]
    public void ConfidenceRanking_HighBeforeMediumBeforeLow()
    {
        var type = CreateTypeWithFields(
            ("float_5", _module.TypeSystem.Single), // same base + same type, but not adjacent => MEDIUM
            ("float_3", _module.TypeSystem.Single), // same base + same type + adjacent => HIGH
            ("float_10", _module.TypeSystem.Int32) // same base + different type => LOW
        );

        var (output, _) = CaptureOutput(() => ValidateCommand.SuggestCandidates(type, "float_2"));

        int highPos = output.IndexOf("HIGH", StringComparison.Ordinal);
        int mediumPos = output.IndexOf("MEDIUM", StringComparison.Ordinal);
        int lowPos = output.IndexOf("LOW", StringComparison.Ordinal);

        Assert.That(highPos, Is.GreaterThanOrEqualTo(0), "Should have HIGH suggestion");
        Assert.That(mediumPos, Is.GreaterThanOrEqualTo(0), "Should have MEDIUM suggestion");
        Assert.That(lowPos, Is.GreaterThanOrEqualTo(0), "Should have LOW suggestion");
        Assert.That(highPos, Is.LessThan(mediumPos), "HIGH should appear before MEDIUM");
        Assert.That(mediumPos, Is.LessThan(lowPos), "MEDIUM should appear before LOW");
    }

    [Test]
    public void ExtractBasePattern_FloatPattern()
    {
        Assert.That(ValidateCommand.ExtractBasePattern("float_2"), Is.EqualTo("float_"));
    }

    [Test]
    public void ExtractBasePattern_Vector3Pattern()
    {
        Assert.That(ValidateCommand.ExtractBasePattern("Vector3_0"), Is.EqualTo("Vector3_"));
    }

    [Test]
    public void ExtractBasePattern_ListPattern()
    {
        Assert.That(ValidateCommand.ExtractBasePattern("List_1"), Is.EqualTo("List_"));
    }

    [Test]
    public void ExtractBasePattern_NoUnderscoreDigit_ReturnsNull()
    {
        Assert.That(ValidateCommand.ExtractBasePattern("Bots"), Is.Null);
    }

    [Test]
    public void ExtractBasePattern_TrailingUnderscore_NoDigit_ReturnsNull()
    {
        Assert.That(ValidateCommand.ExtractBasePattern("float_"), Is.Null);
    }

    [Test]
    public void ExtractBasePattern_MultipleUnderscores()
    {
        Assert.That(ValidateCommand.ExtractBasePattern("some_field_3"), Is.EqualTo("some_field_"));
    }

    [Test]
    public void InferFieldType_FloatPattern()
    {
        Assert.That(ValidateCommand.InferFieldType("float_2"), Is.EqualTo("float"));
    }

    [Test]
    public void InferFieldType_Vector3Pattern()
    {
        Assert.That(ValidateCommand.InferFieldType("Vector3_0"), Is.EqualTo("Vector3"));
    }

    [Test]
    public void InferFieldType_IntPattern()
    {
        Assert.That(ValidateCommand.InferFieldType("int_5"), Is.EqualTo("int"));
    }

    [Test]
    public void InferFieldType_BoolPattern()
    {
        Assert.That(ValidateCommand.InferFieldType("bool_0"), Is.EqualTo("bool"));
    }

    [Test]
    public void InferFieldType_NonPatternField_ReturnsNull()
    {
        Assert.That(ValidateCommand.InferFieldType("Bots"), Is.Null);
    }

    [Test]
    public void ExtractIndex_ValidIndex()
    {
        Assert.That(ValidateCommand.ExtractIndex("float_2"), Is.EqualTo(2));
        Assert.That(ValidateCommand.ExtractIndex("Vector3_0"), Is.EqualTo(0));
        Assert.That(ValidateCommand.ExtractIndex("int_15"), Is.EqualTo(15));
    }

    [Test]
    public void ExtractIndex_NoIndex_ReturnsNull()
    {
        Assert.That(ValidateCommand.ExtractIndex("Bots"), Is.Null);
        Assert.That(ValidateCommand.ExtractIndex("float_"), Is.Null);
    }

    [Test]
    public void EmptyType_ShowsNoFieldsMessage()
    {
        var type = CreateTypeWithFields(); // no fields

        var (output, _) = CaptureOutput(() => ValidateCommand.SuggestCandidates(type, "float_2"));

        Assert.That(output, Does.Contain("(type has no fields)"));
    }

    [Test]
    public void FixSuggestion_ContainsReflectionHelperReference()
    {
        var type = CreateTypeWithFields(("float_3", _module.TypeSystem.Single));

        var (output, _) = CaptureOutput(() => ValidateCommand.SuggestCandidates(type, "float_2"));

        Assert.That(output, Does.Contain("Fix: In ReflectionHelper.cs"));
        Assert.That(output, Does.Contain("change \"float_2\" to \"float_3\""));
    }

    [Test]
    public void AdjacentIndex_BothDirections()
    {
        // float_1 is adjacent to float_2 (index -1)
        // float_3 is adjacent to float_2 (index +1)
        var type = CreateTypeWithFields(("float_1", _module.TypeSystem.Single), ("float_3", _module.TypeSystem.Single));

        var (output, _) = CaptureOutput(() => ValidateCommand.SuggestCandidates(type, "float_2"));

        // Both should be HIGH confidence
        var lines = output.Split('\n');
        int highCount = lines.Count(l => l.Contains("HIGH"));
        Assert.That(highCount, Is.EqualTo(2), "Both adjacent indices should be HIGH");
    }

    private static (string StdOut, string StdErr) CaptureOutput(Action action)
    {
        var originalOut = Console.Out;
        var originalErr = Console.Error;
        using var outWriter = new StringWriter();
        using var errWriter = new StringWriter();
        Console.SetOut(outWriter);
        Console.SetError(errWriter);

        try
        {
            action();
            return (outWriter.ToString(), errWriter.ToString());
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalErr);
        }
    }
}
