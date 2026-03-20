using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using NUnit.Framework;

namespace AssemblyInspector.Tests;

[TestFixture]
public class XrefCommandTests
{
    private string _tempDir = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "XrefCommandTests_" + Guid.NewGuid().ToString("N"));
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

    /// <summary>
    /// Creates an in-memory assembly with types that have known field/method references,
    /// writes it to disk, and returns the path.
    /// </summary>
    private string CreateAssemblyWithReferences(string name, Action<ModuleDefinition> configure)
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

    private static FieldDefinition AddField(
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
        return field;
    }

    private static MethodDefinition AddMethod(
        ModuleDefinition module,
        TypeDefinition type,
        string name,
        Type returnType,
        MethodAttributes attrs = MethodAttributes.Public | MethodAttributes.HideBySig
    )
    {
        var retTypeRef = module.ImportReference(returnType);
        var method = new MethodDefinition(name, attrs, retTypeRef);
        method.Body = new Mono.Cecil.Cil.MethodBody(method);
        method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        type.Methods.Add(method);
        return method;
    }

    /// <summary>
    /// Creates an assembly where TypeB.Reader reads TypeA.Data, and TypeB.Writer writes TypeA.Data.
    /// </summary>
    private string CreateFieldRefAssembly()
    {
        return CreateAssemblyWithReferences(
            "fieldref.dll",
            module =>
            {
                var typeA = CreateType(module, "Test", "TypeA");
                var dataField = AddField(module, typeA, "Data", typeof(int));

                var typeB = CreateType(module, "Test", "TypeB");

                // Reader method: loads TypeA.Data
                var reader = AddMethod(module, typeB, "Reader", typeof(int));
                var il = reader.Body.GetILProcessor();
                il.InsertBefore(reader.Body.Instructions[0], il.Create(OpCodes.Ldsfld, dataField));

                // Writer method: stores TypeA.Data
                var writer = AddMethod(module, typeB, "Writer", typeof(void));
                il = writer.Body.GetILProcessor();
                il.InsertBefore(writer.Body.Instructions[0], il.Create(OpCodes.Ldc_I4, 42));
                il.InsertBefore(writer.Body.Instructions[1], il.Create(OpCodes.Stsfld, dataField));
            }
        );
    }

    /// <summary>
    /// Creates an assembly where TypeB.Caller calls TypeA.DoWork via Call opcode.
    /// </summary>
    private string CreateMethodRefAssembly()
    {
        return CreateAssemblyWithReferences(
            "methodref.dll",
            module =>
            {
                var typeA = CreateType(module, "Test", "TypeA");
                var doWork = AddMethod(
                    module,
                    typeA,
                    "DoWork",
                    typeof(void),
                    MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig
                );

                var typeB = CreateType(module, "Test", "TypeB");

                // Caller method: calls TypeA.DoWork
                var caller = AddMethod(module, typeB, "Caller", typeof(void));
                var il = caller.Body.GetILProcessor();
                il.InsertBefore(caller.Body.Instructions[0], il.Create(OpCodes.Call, doWork));
            }
        );
    }

    #endregion

    #region Field Read References

    [Test]
    public void FindReferences_FieldRead_DetectsLdsfld()
    {
        string dll = CreateFieldRefAssembly();
        using var assembly = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { ReadSymbols = false });

        var result = XrefCommand.FindReferences(assembly, "TypeA.Data", XrefCommand.MemberKind.All);

        var reads = result.References.Where(r => r.Usage == XrefCommand.UsageKind.Read).ToList();

        Assert.That(reads, Has.Count.EqualTo(1));
        Assert.That(reads[0].MethodName, Is.EqualTo("Reader"));
        Assert.That(reads[0].Opcode, Is.EqualTo("ldsfld"));
    }

    #endregion

    #region Field Write References

    [Test]
    public void FindReferences_FieldWrite_DetectsStsfld()
    {
        string dll = CreateFieldRefAssembly();
        using var assembly = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { ReadSymbols = false });

        var result = XrefCommand.FindReferences(assembly, "TypeA.Data", XrefCommand.MemberKind.All);

        var writes = result.References.Where(r => r.Usage == XrefCommand.UsageKind.Write).ToList();

        Assert.That(writes, Has.Count.EqualTo(1));
        Assert.That(writes[0].MethodName, Is.EqualTo("Writer"));
        Assert.That(writes[0].Opcode, Is.EqualTo("stsfld"));
    }

    #endregion

    #region Method Call References

    [Test]
    public void FindReferences_MethodCall_DetectsCall()
    {
        string dll = CreateMethodRefAssembly();
        using var assembly = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { ReadSymbols = false });

        var result = XrefCommand.FindReferences(assembly, "TypeA.DoWork", XrefCommand.MemberKind.All);

        var calls = result.References.Where(r => r.Usage == XrefCommand.UsageKind.Call).ToList();

        Assert.That(calls, Has.Count.EqualTo(1));
        Assert.That(calls[0].MethodName, Is.EqualTo("Caller"));
        Assert.That(calls[0].Opcode, Is.EqualTo("call"));
    }

    #endregion

    #region No References

    [Test]
    public void FindReferences_NoReferences_EmptyResult()
    {
        string dll = CreateFieldRefAssembly();
        using var assembly = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { ReadSymbols = false });

        var result = XrefCommand.FindReferences(assembly, "TypeA.NonExistent", XrefCommand.MemberKind.All);

        Assert.That(result.References, Is.Empty);
    }

    #endregion

    #region Kind Filtering

    [Test]
    public void FindReferences_KindField_ExcludesMethodRefs()
    {
        // Build an assembly with both field refs and method calls
        string dll = CreateAssemblyWithReferences(
            "mixed.dll",
            module =>
            {
                var typeA = CreateType(module, "Test", "TypeA");
                var field = AddField(module, typeA, "Value", typeof(int));
                var method = AddMethod(
                    module,
                    typeA,
                    "Value",
                    typeof(void),
                    MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig
                );

                var typeB = CreateType(module, "Test", "TypeB");

                // ReadField: reads TypeA.Value (field)
                var readField = AddMethod(module, typeB, "ReadField", typeof(int));
                var il = readField.Body.GetILProcessor();
                il.InsertBefore(readField.Body.Instructions[0], il.Create(OpCodes.Ldsfld, field));

                // CallMethod: calls TypeA.Value (method)
                var callMethod = AddMethod(module, typeB, "CallMethod", typeof(void));
                il = callMethod.Body.GetILProcessor();
                il.InsertBefore(callMethod.Body.Instructions[0], il.Create(OpCodes.Call, method));
            }
        );

        using var assembly = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { ReadSymbols = false });

        var fieldOnly = XrefCommand.FindReferences(assembly, "TypeA.Value", XrefCommand.MemberKind.Field);

        // Should only find the field read, not the method call
        Assert.That(fieldOnly.References, Has.Count.EqualTo(1));
        Assert.That(fieldOnly.References[0].Usage, Is.EqualTo(XrefCommand.UsageKind.Read));
    }

    [Test]
    public void FindReferences_KindMethod_ExcludesFieldRefs()
    {
        string dll = CreateAssemblyWithReferences(
            "mixed2.dll",
            module =>
            {
                var typeA = CreateType(module, "Test", "TypeA");
                var field = AddField(module, typeA, "Value", typeof(int));
                var method = AddMethod(
                    module,
                    typeA,
                    "Value",
                    typeof(void),
                    MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig
                );

                var typeB = CreateType(module, "Test", "TypeB");

                // ReadField: reads TypeA.Value (field)
                var readField = AddMethod(module, typeB, "ReadField", typeof(int));
                var il = readField.Body.GetILProcessor();
                il.InsertBefore(readField.Body.Instructions[0], il.Create(OpCodes.Ldsfld, field));

                // CallMethod: calls TypeA.Value (method)
                var callMethod = AddMethod(module, typeB, "CallMethod", typeof(void));
                il = callMethod.Body.GetILProcessor();
                il.InsertBefore(callMethod.Body.Instructions[0], il.Create(OpCodes.Call, method));
            }
        );

        using var assembly = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { ReadSymbols = false });

        var methodOnly = XrefCommand.FindReferences(assembly, "TypeA.Value", XrefCommand.MemberKind.Method);

        Assert.That(methodOnly.References, Has.Count.EqualTo(1));
        Assert.That(methodOnly.References[0].Usage, Is.EqualTo(XrefCommand.UsageKind.Call));
    }

    #endregion

    #region Member Name Only (no type qualifier)

    [Test]
    public void FindReferences_MemberNameOnly_MatchesAcrossTypes()
    {
        string dll = CreateAssemblyWithReferences(
            "notype.dll",
            module =>
            {
                var typeA = CreateType(module, "Test", "TypeA");
                var fieldA = AddField(module, typeA, "Shared", typeof(int));

                var typeB = CreateType(module, "Test", "TypeB");
                var fieldB = AddField(module, typeB, "Shared", typeof(int));

                var typeC = CreateType(module, "Test", "TypeC");

                var reader = AddMethod(module, typeC, "ReadBoth", typeof(void));
                var il = reader.Body.GetILProcessor();
                il.InsertBefore(reader.Body.Instructions[0], il.Create(OpCodes.Ldsfld, fieldA));
                il.InsertBefore(reader.Body.Instructions[1], il.Create(OpCodes.Ldsfld, fieldB));
            }
        );

        using var assembly = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { ReadSymbols = false });

        // No type qualifier — should find refs to both TypeA.Shared and TypeB.Shared
        var result = XrefCommand.FindReferences(assembly, "Shared", XrefCommand.MemberKind.All);

        Assert.That(result.References, Has.Count.EqualTo(2));
    }

    #endregion

    #region Multiple References in Same Method

    [Test]
    public void FindReferences_MultipleRefsInSameMethod_AllDetected()
    {
        string dll = CreateAssemblyWithReferences(
            "multi.dll",
            module =>
            {
                var typeA = CreateType(module, "Test", "TypeA");
                var field = AddField(module, typeA, "Counter", typeof(int));

                var typeB = CreateType(module, "Test", "TypeB");

                // Method that reads then writes the same field
                var method = AddMethod(module, typeB, "IncrementCounter", typeof(void));
                var il = method.Body.GetILProcessor();
                il.InsertBefore(method.Body.Instructions[0], il.Create(OpCodes.Ldsfld, field));
                il.InsertBefore(method.Body.Instructions[1], il.Create(OpCodes.Ldc_I4_1));
                il.InsertBefore(method.Body.Instructions[2], il.Create(OpCodes.Add));
                il.InsertBefore(method.Body.Instructions[3], il.Create(OpCodes.Stsfld, field));
            }
        );

        using var assembly = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { ReadSymbols = false });

        var result = XrefCommand.FindReferences(assembly, "TypeA.Counter", XrefCommand.MemberKind.All);

        Assert.That(result.References, Has.Count.EqualTo(2));
        Assert.That(result.References.Count(r => r.Usage == XrefCommand.UsageKind.Read), Is.EqualTo(1));
        Assert.That(result.References.Count(r => r.Usage == XrefCommand.UsageKind.Write), Is.EqualTo(1));
    }

    #endregion

    #region ClassifyFieldUsage

    [Test]
    public void ClassifyFieldUsage_ReadOpcodes_ReturnRead()
    {
        Assert.That(XrefCommand.ClassifyFieldUsage(OpCodes.Ldfld), Is.EqualTo(XrefCommand.UsageKind.Read));
        Assert.That(XrefCommand.ClassifyFieldUsage(OpCodes.Ldsfld), Is.EqualTo(XrefCommand.UsageKind.Read));
        Assert.That(XrefCommand.ClassifyFieldUsage(OpCodes.Ldflda), Is.EqualTo(XrefCommand.UsageKind.Read));
        Assert.That(XrefCommand.ClassifyFieldUsage(OpCodes.Ldsflda), Is.EqualTo(XrefCommand.UsageKind.Read));
    }

    [Test]
    public void ClassifyFieldUsage_WriteOpcodes_ReturnWrite()
    {
        Assert.That(XrefCommand.ClassifyFieldUsage(OpCodes.Stfld), Is.EqualTo(XrefCommand.UsageKind.Write));
        Assert.That(XrefCommand.ClassifyFieldUsage(OpCodes.Stsfld), Is.EqualTo(XrefCommand.UsageKind.Write));
    }

    [Test]
    public void ClassifyFieldUsage_UnrelatedOpcode_ReturnsNull()
    {
        Assert.That(XrefCommand.ClassifyFieldUsage(OpCodes.Nop), Is.Null);
        Assert.That(XrefCommand.ClassifyFieldUsage(OpCodes.Ret), Is.Null);
    }

    #endregion

    #region ClassifyMethodUsage

    [Test]
    public void ClassifyMethodUsage_CallOpcodes_ReturnCall()
    {
        Assert.That(XrefCommand.ClassifyMethodUsage(OpCodes.Call), Is.EqualTo(XrefCommand.UsageKind.Call));
        Assert.That(XrefCommand.ClassifyMethodUsage(OpCodes.Callvirt), Is.EqualTo(XrefCommand.UsageKind.Call));
    }

    [Test]
    public void ClassifyMethodUsage_OtherOpcodes_ReturnOther()
    {
        Assert.That(XrefCommand.ClassifyMethodUsage(OpCodes.Newobj), Is.EqualTo(XrefCommand.UsageKind.Other));
        Assert.That(XrefCommand.ClassifyMethodUsage(OpCodes.Ldftn), Is.EqualTo(XrefCommand.UsageKind.Other));
        Assert.That(XrefCommand.ClassifyMethodUsage(OpCodes.Ldvirtftn), Is.EqualTo(XrefCommand.UsageKind.Other));
    }

    [Test]
    public void ClassifyMethodUsage_UnrelatedOpcode_ReturnsNull()
    {
        Assert.That(XrefCommand.ClassifyMethodUsage(OpCodes.Nop), Is.Null);
        Assert.That(XrefCommand.ClassifyMethodUsage(OpCodes.Ret), Is.Null);
    }

    #endregion

    #region Output Formatting

    [Test]
    public void Run_NoReferences_PrintsNoRefsMessage()
    {
        string dll = CreateAssemblyWithReferences(
            "empty.dll",
            module =>
            {
                CreateType(module, "Test", "Empty");
            }
        );

        var options = new XrefCommand.XrefOptions("NonExistent.Field", dll, XrefCommand.MemberKind.All);

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            int exitCode = XrefCommand.Run(options);
            string output = outWriter.ToString();

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output, Does.Contain("No cross-references found"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    [Test]
    public void Run_WithReferences_PrintsSummary()
    {
        string dll = CreateFieldRefAssembly();

        var options = new XrefCommand.XrefOptions("TypeA.Data", dll, XrefCommand.MemberKind.All);

        var originalOut = Console.Out;
        using var outWriter = new StringWriter();
        Console.SetOut(outWriter);

        try
        {
            int exitCode = XrefCommand.Run(options);
            string output = outWriter.ToString();

            Assert.That(exitCode, Is.EqualTo(0));
            Assert.That(output, Does.Contain("Cross-references to TypeA.Data"));
            Assert.That(output, Does.Contain("Total: 2 references"));
            Assert.That(output, Does.Contain("1 read"));
            Assert.That(output, Does.Contain("1 write"));
        }
        finally
        {
            Console.SetOut(originalOut);
        }
    }

    #endregion

    #region IL Offset Tracking

    [Test]
    public void FindReferences_IlOffsets_AreCorrect()
    {
        string dll = CreateFieldRefAssembly();
        using var assembly = AssemblyDefinition.ReadAssembly(dll, new ReaderParameters { ReadSymbols = false });

        var result = XrefCommand.FindReferences(assembly, "TypeA.Data", XrefCommand.MemberKind.All);

        // All references should have non-negative IL offsets
        Assert.That(result.References.All(r => r.IlOffset >= 0), Is.True);
    }

    #endregion
}
