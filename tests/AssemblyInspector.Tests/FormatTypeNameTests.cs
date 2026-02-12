using Mono.Cecil;
using NUnit.Framework;

namespace AssemblyInspector.Tests;

[TestFixture]
public class FormatTypeNameTests
{
    private ModuleDefinition _module = null!;

    [OneTimeSetUp]
    public void SetUp()
    {
        // Load the test assembly itself to get real TypeReferences
        string assemblyPath = typeof(FormatTypeNameTests).Assembly.Location;
        _module = ModuleDefinition.ReadModule(assemblyPath);
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        _module?.Dispose();
    }

    [TestCase("System.Int32", "int")]
    [TestCase("System.Single", "float")]
    [TestCase("System.Double", "double")]
    [TestCase("System.Boolean", "bool")]
    [TestCase("System.String", "string")]
    [TestCase("System.Object", "object")]
    [TestCase("System.Byte", "byte")]
    [TestCase("System.Int64", "long")]
    [TestCase("System.Void", "void")]
    [TestCase("System.Char", "char")]
    [TestCase("System.Decimal", "decimal")]
    [TestCase("System.Int16", "short")]
    [TestCase("System.UInt32", "uint")]
    [TestCase("System.UInt64", "ulong")]
    [TestCase("System.UInt16", "ushort")]
    [TestCase("System.SByte", "sbyte")]
    public void FormatsPrimitiveTypes(string fullName, string expected)
    {
        var typeRef = _module.ImportReference(typeof(int)).Module.TypeSystem.Object;
        // Create a simple TypeReference with the given full name
        var parts = fullName.Split('.');
        string ns = string.Join(".", parts.Take(parts.Length - 1));
        string name = parts.Last();
        typeRef = new TypeReference(ns, name, _module, _module.TypeSystem.CoreLibrary);

        string result = InspectCommand.FormatTypeName(typeRef);
        Assert.That(result, Is.EqualTo(expected));
    }

    [Test]
    public void FormatsNonPrimitiveTypeAsShortName()
    {
        // A type like "EFT.BotSpawner" should display as "BotSpawner"
        var typeRef = new TypeReference("EFT", "BotSpawner", _module, _module.TypeSystem.CoreLibrary);
        string result = InspectCommand.FormatTypeName(typeRef);
        Assert.That(result, Is.EqualTo("BotSpawner"));
    }

    [Test]
    public void FormatsArrayType()
    {
        var elementType = new TypeReference("UnityEngine", "Vector3", _module, _module.TypeSystem.CoreLibrary);
        var arrayType = new ArrayType(elementType);
        string result = InspectCommand.FormatTypeName(arrayType);
        Assert.That(result, Is.EqualTo("Vector3[]"));
    }

    [Test]
    public void FormatsPrimitiveArrayType()
    {
        var arrayType = new ArrayType(_module.TypeSystem.Int32);
        string result = InspectCommand.FormatTypeName(arrayType);
        Assert.That(result, Is.EqualTo("int[]"));
    }

    [Test]
    public void FormatsGenericInstanceType()
    {
        // Create a List<string> type reference
        var listType = new TypeReference(
            "System.Collections.Generic",
            "List`1",
            _module,
            _module.TypeSystem.CoreLibrary
        );
        listType.GenericParameters.Add(new GenericParameter("T", listType));

        var genericInstance = new GenericInstanceType(listType);
        genericInstance.GenericArguments.Add(_module.TypeSystem.String);

        string result = InspectCommand.FormatTypeName(genericInstance);
        Assert.That(result, Is.EqualTo("List<string>"));
    }

    [Test]
    public void FormatsGenericWithNonPrimitiveArgument()
    {
        // Create a List<BotOwner> type reference
        var listType = new TypeReference(
            "System.Collections.Generic",
            "List`1",
            _module,
            _module.TypeSystem.CoreLibrary
        );
        listType.GenericParameters.Add(new GenericParameter("T", listType));

        var botOwnerType = new TypeReference("EFT", "BotOwner", _module, _module.TypeSystem.CoreLibrary);
        var genericInstance = new GenericInstanceType(listType);
        genericInstance.GenericArguments.Add(botOwnerType);

        string result = InspectCommand.FormatTypeName(genericInstance);
        Assert.That(result, Is.EqualTo("List<BotOwner>"));
    }

    [Test]
    public void FormatsDictionaryType()
    {
        // Create Dictionary<string, int>
        var dictType = new TypeReference(
            "System.Collections.Generic",
            "Dictionary`2",
            _module,
            _module.TypeSystem.CoreLibrary
        );
        dictType.GenericParameters.Add(new GenericParameter("TKey", dictType));
        dictType.GenericParameters.Add(new GenericParameter("TValue", dictType));

        var genericInstance = new GenericInstanceType(dictType);
        genericInstance.GenericArguments.Add(_module.TypeSystem.String);
        genericInstance.GenericArguments.Add(_module.TypeSystem.Int32);

        string result = InspectCommand.FormatTypeName(genericInstance);
        Assert.That(result, Is.EqualTo("Dictionary<string, int>"));
    }

    [Test]
    public void FormatsNullableIntAsQuestionMark()
    {
        // Nullable<int> should display as int?
        var nullableType = new TypeReference(
            "System",
            "Nullable`1",
            _module,
            _module.TypeSystem.CoreLibrary
        );
        nullableType.GenericParameters.Add(new GenericParameter("T", nullableType));

        var genericInstance = new GenericInstanceType(nullableType);
        genericInstance.GenericArguments.Add(_module.TypeSystem.Int32);

        string result = InspectCommand.FormatTypeName(genericInstance);
        Assert.That(result, Is.EqualTo("int?"));
    }

    [Test]
    public void FormatsNullableCustomTypeAsQuestionMark()
    {
        // Nullable<Vector3> should display as Vector3?
        var nullableType = new TypeReference(
            "System",
            "Nullable`1",
            _module,
            _module.TypeSystem.CoreLibrary
        );
        nullableType.GenericParameters.Add(new GenericParameter("T", nullableType));

        var vector3Type = new TypeReference("UnityEngine", "Vector3", _module, _module.TypeSystem.CoreLibrary);
        var genericInstance = new GenericInstanceType(nullableType);
        genericInstance.GenericArguments.Add(vector3Type);

        string result = InspectCommand.FormatTypeName(genericInstance);
        Assert.That(result, Is.EqualTo("Vector3?"));
    }

    [Test]
    public void FormatsMultiDimensionalArray_Rank2()
    {
        var arrayType = new ArrayType(_module.TypeSystem.Int32, 2);
        string result = InspectCommand.FormatTypeName(arrayType);
        Assert.That(result, Is.EqualTo("int[,]"));
    }

    [Test]
    public void FormatsMultiDimensionalArray_Rank3()
    {
        var arrayType = new ArrayType(_module.TypeSystem.Single, 3);
        string result = InspectCommand.FormatTypeName(arrayType);
        Assert.That(result, Is.EqualTo("float[,,]"));
    }

    [Test]
    public void FormatsByRefType()
    {
        var byRefType = new ByReferenceType(_module.TypeSystem.Int32);
        string result = InspectCommand.FormatTypeName(byRefType);
        Assert.That(result, Is.EqualTo("int&"));
    }

    [Test]
    public void FormatsPointerType()
    {
        var pointerType = new PointerType(_module.TypeSystem.Byte);
        string result = InspectCommand.FormatTypeName(pointerType);
        Assert.That(result, Is.EqualTo("byte*"));
    }
}
