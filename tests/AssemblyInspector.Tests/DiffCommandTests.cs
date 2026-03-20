using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Mono.Cecil;
using NUnit.Framework;

namespace AssemblyInspector.Tests;

[TestFixture]
public class DiffCommandTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "DiffCommandTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    #region Helper Methods

    private string CreateAssembly(string name, Action<ModuleDefinition> configure)
    {
        string path = Path.Combine(_tempDir, name);
        using var module = ModuleDefinition.CreateModule(
            name,
            new ModuleParameters { Kind = ModuleKind.Dll, Runtime = TargetRuntime.Net_4_0 }
        );

        configure(module);
        module.Write(path);
        return path;
    }

    private static TypeDefinition CreateType(
        ModuleDefinition module,
        string ns,
        string name,
        TypeAttributes attrs = TypeAttributes.Public | TypeAttributes.Class
    )
    {
        var baseType = module.ImportReference(typeof(object));
        var typeDef = new TypeDefinition(ns, name, attrs, baseType);
        module.Types.Add(typeDef);
        return typeDef;
    }

    private static void AddField(
        ModuleDefinition module,
        TypeDefinition type,
        string name,
        Type fieldType,
        FieldAttributes attrs = FieldAttributes.Public
    )
    {
        var fieldRef = module.ImportReference(fieldType);
        var field = new FieldDefinition(name, attrs, fieldRef);
        type.Fields.Add(field);
    }

    #endregion

    #region Identical Assemblies

    [Test]
    public void IdenticalAssemblies_NoChangesDetected()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
                AddField(m, t, "int_1", typeof(int));
                AddField(m, t, "Bots", typeof(string));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
                AddField(m, t, "int_1", typeof(int));
                AddField(m, t, "Bots", typeof(string));
            }
        );

        var result = DiffCommand.ComputeDiff(oldDll, newDll, null);

        Assert.That(result.HasChanges, Is.False);
        Assert.That(result.OldOnlyTypes, Is.Empty);
        Assert.That(result.NewOnlyTypes, Is.Empty);
    }

    #endregion

    #region Field Added

    [Test]
    public void FieldAdded_DetectedAsAdded()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
                AddField(m, t, "int_1", typeof(int));
            }
        );

        var result = DiffCommand.ComputeDiff(oldDll, newDll, null);

        Assert.That(result.HasChanges, Is.True);
        var typeDiff = result.TypeDiffs.First(td => td.TypeName == "BotSpawner");
        var added = typeDiff.Changes.Where(c => c.Kind == DiffCommand.ChangeKind.Added).ToList();
        Assert.That(added, Has.Count.EqualTo(1));
        Assert.That(added[0].FieldName, Is.EqualTo("int_1"));
    }

    #endregion

    #region Field Removed

    [Test]
    public void FieldRemoved_DetectedAsRemoved()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
                AddField(m, t, "int_1", typeof(int));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
            }
        );

        var result = DiffCommand.ComputeDiff(oldDll, newDll, null);

        Assert.That(result.HasChanges, Is.True);
        var typeDiff = result.TypeDiffs.First(td => td.TypeName == "BotSpawner");
        var removed = typeDiff.Changes.Where(c => c.Kind == DiffCommand.ChangeKind.Removed).ToList();
        Assert.That(removed, Has.Count.EqualTo(1));
        Assert.That(removed[0].FieldName, Is.EqualTo("int_1"));
    }

    #endregion

    #region Field Renamed

    [Test]
    public void FieldRenamed_SameTypeAndPattern_HighConfidence()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_2", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_3", typeof(float));
            }
        );

        var result = DiffCommand.ComputeDiff(oldDll, newDll, null);

        Assert.That(result.HasChanges, Is.True);
        var typeDiff = result.TypeDiffs.First(td => td.TypeName == "BotSpawner");
        var renamed = typeDiff.Changes.Where(c => c.Kind == DiffCommand.ChangeKind.Renamed).ToList();
        Assert.That(renamed, Has.Count.EqualTo(1));
        Assert.That(renamed[0].FieldName, Is.EqualTo("float_2"));
        Assert.That(renamed[0].NewFieldName, Is.EqualTo("float_3"));
        Assert.That(renamed[0].Confidence, Is.EqualTo(DiffCommand.RenameConfidence.High));
    }

    [Test]
    public void FieldRenamed_SameTypeOnly_MediumConfidence()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "oldName", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "newName", typeof(float));
            }
        );

        var result = DiffCommand.ComputeDiff(oldDll, newDll, null);

        Assert.That(result.HasChanges, Is.True);
        var typeDiff = result.TypeDiffs.First(td => td.TypeName == "BotSpawner");
        var renamed = typeDiff.Changes.Where(c => c.Kind == DiffCommand.ChangeKind.Renamed).ToList();
        Assert.That(renamed, Has.Count.EqualTo(1));
        Assert.That(renamed[0].Confidence, Is.EqualTo(DiffCommand.RenameConfidence.Medium));
    }

    [Test]
    public void FieldRenamed_SamePatternDifferentType_LowConfidence()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "data_0", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "data_1", typeof(int));
            }
        );

        var result = DiffCommand.ComputeDiff(oldDll, newDll, null);

        Assert.That(result.HasChanges, Is.True);
        var typeDiff = result.TypeDiffs.First(td => td.TypeName == "BotSpawner");
        var renamed = typeDiff.Changes.Where(c => c.Kind == DiffCommand.ChangeKind.Renamed).ToList();
        Assert.That(renamed, Has.Count.EqualTo(1));
        Assert.That(renamed[0].Confidence, Is.EqualTo(DiffCommand.RenameConfidence.Low));
    }

    #endregion

    #region Type Added/Removed

    [Test]
    public void TypeAddedInNew_DetectedAsNewOnly()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                CreateType(m, "EFT", "BotSpawner");
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                CreateType(m, "EFT", "BotSpawner");
                CreateType(m, "EFT", "NewType");
            }
        );

        var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BotSpawner", "NewType" };
        var result = DiffCommand.ComputeDiff(oldDll, newDll, filter);

        Assert.That(result.NewOnlyTypes, Does.Contain("NewType"));
        Assert.That(result.HasChanges, Is.True);
    }

    [Test]
    public void TypeRemovedInNew_DetectedAsOldOnly()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                CreateType(m, "EFT", "BotSpawner");
                CreateType(m, "EFT", "OldType");
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                CreateType(m, "EFT", "BotSpawner");
            }
        );

        var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BotSpawner", "OldType" };
        var result = DiffCommand.ComputeDiff(oldDll, newDll, filter);

        Assert.That(result.OldOnlyTypes, Does.Contain("OldType"));
        Assert.That(result.HasChanges, Is.True);
    }

    #endregion

    #region Type Filter

    [Test]
    public void TypeFilter_OnlyDiffsFilteredTypes()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t1 = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t1, "float_0", typeof(float));
                var t2 = CreateType(m, "EFT", "Player");
                AddField(m, t2, "int_0", typeof(int));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t1 = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t1, "float_0", typeof(float));
                AddField(m, t1, "float_1", typeof(float));
                var t2 = CreateType(m, "EFT", "Player");
                AddField(m, t2, "int_0", typeof(int));
                AddField(m, t2, "int_1", typeof(int));
            }
        );

        // Only filter for BotSpawner
        var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "BotSpawner" };
        var result = DiffCommand.ComputeDiff(oldDll, newDll, filter);

        Assert.That(result.TypeDiffs, Has.Count.EqualTo(1));
        Assert.That(result.TypeDiffs[0].TypeName, Is.EqualTo("BotSpawner"));
    }

    #endregion

    #region JSON Output

    [Test]
    public void JsonOutput_ProducesValidJson()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_2", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_3", typeof(float));
            }
        );

        var options = new DiffCommand.DiffOptions(oldDll, newDll, null, false, "json");

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            int exitCode = DiffCommand.Run(options);
            string output = outWriter.ToString();

            Assert.That(exitCode, Is.EqualTo(2));

            // Verify it's valid JSON
            var doc = JsonDocument.Parse(output);
            Assert.That(doc.RootElement.TryGetProperty("summary", out var summary), Is.True);
            Assert.That(summary.GetProperty("fieldsRenamed").GetInt32(), Is.EqualTo(1));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region Table Output

    [Test]
    public void TableOutput_ShowsNoChangesMessage_WhenIdentical()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
            }
        );

        var options = new DiffCommand.DiffOptions(oldDll, newDll, null, false, "table");

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            int exitCode = DiffCommand.Run(options);
            string output = outWriter.ToString();

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output, Does.Contain("No field changes detected"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Test]
    public void TableOutput_ShowsSummary_WhenChangesExist()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "float_0", typeof(float));
                AddField(m, t, "int_1", typeof(int));
            }
        );

        var options = new DiffCommand.DiffOptions(oldDll, newDll, null, false, "table");

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            int exitCode = DiffCommand.Run(options);
            string output = outWriter.ToString();

            Assert.That(exitCode, Is.EqualTo(2));
            Assert.That(output, Does.Contain("Summary:"));
            Assert.That(output, Does.Contain("Fields added: 1"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region Exit Codes

    [Test]
    public void ExitCode_Zero_WhenNoChanges()
    {
        string dll = CreateAssembly(
            "same.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "x", typeof(int));
            }
        );

        var options = new DiffCommand.DiffOptions(dll, dll, null, false, "table");

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            int exitCode = DiffCommand.Run(options);
            Assert.That(exitCode, Is.EqualTo(0));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Test]
    public void ExitCode_One_WhenFileNotFound()
    {
        var options = new DiffCommand.DiffOptions("/nonexistent/old.dll", "/nonexistent/new.dll", null, false, "table");

        var originalErr = Console.Error;
        using var errWriter = new StringWriter();
        Console.SetError(errWriter);

        try
        {
            int exitCode = DiffCommand.Run(options);
            Assert.That(exitCode, Is.EqualTo(1));
        }
        finally
        {
            Console.SetError(originalErr);
        }
    }

    [Test]
    public void ExitCode_Two_WhenChangesDetected()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "x", typeof(int));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "BotSpawner");
                AddField(m, t, "y", typeof(int));
            }
        );

        var options = new DiffCommand.DiffOptions(oldDll, newDll, null, false, "table");

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            int exitCode = DiffCommand.Run(options);
            Assert.That(exitCode, Is.EqualTo(2));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region DiffFieldLists Unit Tests

    [Test]
    public void DiffFieldLists_EmptyLists_NoChanges()
    {
        var result = DiffCommand.DiffFieldLists("TestType", new List<DiffCommand.FieldInfo>(), new List<DiffCommand.FieldInfo>());

        Assert.That(result.HasChanges, Is.False);
        Assert.That(result.Changes, Is.Empty);
    }

    [Test]
    public void DiffFieldLists_AllRemoved_AllRemoved()
    {
        var oldFields = new List<DiffCommand.FieldInfo> { new("x", "int", 0), new("y", "float", 1) };

        var result = DiffCommand.DiffFieldLists("TestType", oldFields, new List<DiffCommand.FieldInfo>());

        Assert.That(result.HasChanges, Is.True);
        Assert.That(result.Changes.Count(c => c.Kind == DiffCommand.ChangeKind.Removed), Is.EqualTo(2));
    }

    [Test]
    public void DiffFieldLists_AllAdded_AllAdded()
    {
        var newFields = new List<DiffCommand.FieldInfo> { new("x", "int", 0), new("y", "float", 1) };

        var result = DiffCommand.DiffFieldLists("TestType", new List<DiffCommand.FieldInfo>(), newFields);

        Assert.That(result.HasChanges, Is.True);
        Assert.That(result.Changes.Count(c => c.Kind == DiffCommand.ChangeKind.Added), Is.EqualTo(2));
    }

    [Test]
    public void DiffFieldLists_MultipleRenames_MatchedCorrectly()
    {
        var oldFields = new List<DiffCommand.FieldInfo> { new("float_0", "float", 0), new("float_1", "float", 1), new("int_0", "int", 2) };

        var newFields = new List<DiffCommand.FieldInfo> { new("float_2", "float", 0), new("float_3", "float", 1), new("int_1", "int", 2) };

        var result = DiffCommand.DiffFieldLists("TestType", oldFields, newFields);

        Assert.That(result.HasChanges, Is.True);
        var renamed = result.Changes.Where(c => c.Kind == DiffCommand.ChangeKind.Renamed).ToList();
        Assert.That(renamed, Has.Count.EqualTo(3));
        Assert.That(renamed.All(r => r.Confidence == DiffCommand.RenameConfidence.High), Is.True);
    }

    #endregion

    #region HasSameBasePattern

    [Test]
    public void HasSameBasePattern_SamePrefix_ReturnsTrue()
    {
        Assert.That(DiffCommand.HasSameBasePattern("float_2", "float_3"), Is.True);
        Assert.That(DiffCommand.HasSameBasePattern("Vector3_0", "Vector3_1"), Is.True);
        Assert.That(DiffCommand.HasSameBasePattern("gclass123_0", "gclass123_5"), Is.True);
    }

    [Test]
    public void HasSameBasePattern_DifferentPrefix_ReturnsFalse()
    {
        Assert.That(DiffCommand.HasSameBasePattern("float_2", "int_3"), Is.False);
        Assert.That(DiffCommand.HasSameBasePattern("Vector3_0", "Vector2_1"), Is.False);
    }

    [Test]
    public void HasSameBasePattern_NoPattern_ReturnsFalse()
    {
        Assert.That(DiffCommand.HasSameBasePattern("Bots", "Players"), Is.False);
        Assert.That(DiffCommand.HasSameBasePattern("float_2", "Bots"), Is.False);
    }

    #endregion

    #region ExtractBasePattern

    [Test]
    public void ExtractBasePattern_ObfuscatedName_ExtractsPrefix()
    {
        Assert.That(DiffCommand.ExtractBasePattern("float_2"), Is.EqualTo("float_"));
        Assert.That(DiffCommand.ExtractBasePattern("Vector3_0"), Is.EqualTo("Vector3_"));
        Assert.That(DiffCommand.ExtractBasePattern("gclass123_5"), Is.EqualTo("gclass123_"));
    }

    [Test]
    public void ExtractBasePattern_NoPattern_ReturnsNull()
    {
        Assert.That(DiffCommand.ExtractBasePattern("Bots"), Is.Null);
        Assert.That(DiffCommand.ExtractBasePattern("float"), Is.Null);
        Assert.That(DiffCommand.ExtractBasePattern("_"), Is.Null);
    }

    #endregion

    #region Multiple Types

    [Test]
    public void MultipleTypes_IndependentDiffs()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t1 = CreateType(m, "EFT", "TypeA");
                AddField(m, t1, "x", typeof(int));
                var t2 = CreateType(m, "EFT", "TypeB");
                AddField(m, t2, "y", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t1 = CreateType(m, "EFT", "TypeA");
                AddField(m, t1, "x", typeof(int));
                AddField(m, t1, "z", typeof(double));
                var t2 = CreateType(m, "EFT", "TypeB");
                // y is removed
            }
        );

        var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TypeA", "TypeB" };
        var result = DiffCommand.ComputeDiff(oldDll, newDll, filter);

        Assert.That(result.HasChanges, Is.True);

        var typeA = result.TypeDiffs.First(td => td.TypeName == "TypeA");
        Assert.That(typeA.Changes.Any(c => c.Kind == DiffCommand.ChangeKind.Added && c.FieldName == "z"), Is.True);

        var typeB = result.TypeDiffs.First(td => td.TypeName == "TypeB");
        Assert.That(typeB.Changes.Any(c => c.Kind == DiffCommand.ChangeKind.Removed && c.FieldName == "y"), Is.True);
    }

    #endregion

    #region Edge Cases

    [Test]
    public void EmptyAssemblies_NoChanges()
    {
        string oldDll = CreateAssembly("old.dll", _ => { });
        string newDll = CreateAssembly("new.dll", _ => { });

        var result = DiffCommand.ComputeDiff(oldDll, newDll, null);

        Assert.That(result.HasChanges, Is.False);
    }

    [Test]
    public void TypeWithNoFields_NoChanges()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                CreateType(m, "EFT", "EmptyType");
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                CreateType(m, "EFT", "EmptyType");
            }
        );

        var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EmptyType" };
        var result = DiffCommand.ComputeDiff(oldDll, newDll, filter);

        Assert.That(result.HasChanges, Is.False);
    }

    [Test]
    public void FieldUnchangedAndRenamed_MixedCorrectly()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "MixedType");
                AddField(m, t, "Bots", typeof(string));
                AddField(m, t, "float_0", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "MixedType");
                AddField(m, t, "Bots", typeof(string));
                AddField(m, t, "float_1", typeof(float));
            }
        );

        var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MixedType" };
        var result = DiffCommand.ComputeDiff(oldDll, newDll, filter);

        Assert.That(result.HasChanges, Is.True);
        var typeDiff = result.TypeDiffs.First(td => td.TypeName == "MixedType");

        var unchanged = typeDiff.Changes.Where(c => c.Kind == DiffCommand.ChangeKind.Unchanged).ToList();
        Assert.That(unchanged, Has.Count.EqualTo(1));
        Assert.That(unchanged[0].FieldName, Is.EqualTo("Bots"));

        var renamed = typeDiff.Changes.Where(c => c.Kind == DiffCommand.ChangeKind.Renamed).ToList();
        Assert.That(renamed, Has.Count.EqualTo(1));
        Assert.That(renamed[0].FieldName, Is.EqualTo("float_0"));
        Assert.That(renamed[0].NewFieldName, Is.EqualTo("float_1"));
    }

    #endregion

    #region JSON Output Details

    [Test]
    public void JsonOutput_ExcludesUnchangedFields()
    {
        string oldDll = CreateAssembly(
            "old.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "TestType");
                AddField(m, t, "stable", typeof(int));
                AddField(m, t, "removed", typeof(float));
            }
        );

        string newDll = CreateAssembly(
            "new.dll",
            m =>
            {
                var t = CreateType(m, "EFT", "TestType");
                AddField(m, t, "stable", typeof(int));
                AddField(m, t, "added", typeof(double));
            }
        );

        var filter = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "TestType" };
        var options = new DiffCommand.DiffOptions(oldDll, newDll, filter, false, "json");

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            DiffCommand.Run(options);
            string output = outWriter.ToString();

            var doc = JsonDocument.Parse(output);
            var changes = doc
                .RootElement.GetProperty("typeDiffs")
                .EnumerateArray()
                .First()
                .GetProperty("changes")
                .EnumerateArray()
                .ToList();

            // Should not include "unchanged" entries in JSON
            Assert.That(changes.All(c => c.GetProperty("kind").GetString() != "unchanged"), Is.True);
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion
}
