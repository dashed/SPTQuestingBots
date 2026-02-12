using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace AssemblyInspector;

internal static class InspectCommand
{
    public static int Run(string typeName, string dllPath)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(
            dllPath,
            new ReaderParameters { ReadSymbols = false }
        );

        var matches = FindTypes(assembly, typeName);

        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"No type matching '{typeName}' found.");
            SuggestTypes(assembly, typeName);
            return 1;
        }

        foreach (var type in matches)
        {
            PrintTypeFields(type);
            Console.WriteLine();
        }

        return 0;
    }

    private static List<TypeDefinition> FindTypes(AssemblyDefinition assembly, string name)
    {
        var allTypes = assembly.MainModule.Types
            .SelectMany(FlattenNestedTypes)
            .ToList();

        // Try exact short-name match (case-insensitive)
        var matches = allTypes
            .Where(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matches.Count > 0)
        {
            return matches;
        }

        // Try full-name contains match (case-insensitive)
        matches = allTypes
            .Where(t => t.FullName.Contains(name, StringComparison.OrdinalIgnoreCase))
            .ToList();

        return matches;
    }

    private static void SuggestTypes(AssemblyDefinition assembly, string name)
    {
        var allTypes = assembly.MainModule.Types
            .SelectMany(FlattenNestedTypes)
            .ToList();

        string nameLower = name.ToLowerInvariant();

        // Find types with similar names (contains any word from the search term)
        var suggestions = allTypes
            .Where(t => t.Name.Contains(nameLower, StringComparison.OrdinalIgnoreCase)
                || nameLower.Contains(t.Name.ToLowerInvariant()))
            .Select(t => t.FullName)
            .Take(10)
            .ToList();

        if (suggestions.Count > 0)
        {
            Console.Error.WriteLine("Did you mean one of these?");
            foreach (string s in suggestions)
            {
                Console.Error.WriteLine($"  {s}");
            }
        }
    }

    private static void PrintTypeFields(TypeDefinition type)
    {
        Console.WriteLine($"Type: {type.FullName}");
        Console.WriteLine($"Base: {type.BaseType?.FullName ?? "(none)"}");
        Console.WriteLine(
            $"Fields: {type.Fields.Count}"
        );
        Console.WriteLine(new string('-', 80));
        Console.WriteLine(
            $"  {"#",-4} {"Visibility",-12} {"FieldType",-40} {"Name"}"
        );
        Console.WriteLine(new string('-', 80));

        for (int i = 0; i < type.Fields.Count; i++)
        {
            var field = type.Fields[i];
            string visibility = GetVisibility(field);
            string fieldType = FormatTypeName(field.FieldType);
            Console.WriteLine($"  {i,-4} {visibility,-12} {fieldType,-40} {field.Name}");
        }
    }

    private static string GetVisibility(FieldDefinition field)
    {
        if (field.IsPublic)
        {
            return "public";
        }

        if (field.IsPrivate)
        {
            return "private";
        }

        if (field.IsFamily)
        {
            return "protected";
        }

        if (field.IsAssembly)
        {
            return "internal";
        }

        if (field.IsFamilyOrAssembly)
        {
            return "prot+int";
        }

        return "unknown";
    }

    internal static string FormatTypeName(TypeReference typeRef)
    {
        // Handle generic instance types like List<string>
        if (typeRef is GenericInstanceType generic)
        {
            // Nullable<T> → T?
            if (generic.ElementType.FullName == "System.Nullable`1")
            {
                return FormatTypeName(generic.GenericArguments[0]) + "?";
            }

            string baseName = FormatBaseName(generic.ElementType);
            string args = string.Join(", ", generic.GenericArguments.Select(FormatTypeName));
            return $"{baseName}<{args}>";
        }

        // Handle array types (including multi-dimensional)
        if (typeRef is ArrayType array)
        {
            string brackets = array.Rank == 1 ? "[]" : "[" + new string(',', array.Rank - 1) + "]";
            return FormatTypeName(array.ElementType) + brackets;
        }

        // Handle by-reference types
        if (typeRef is ByReferenceType byRef)
        {
            return FormatTypeName(byRef.ElementType) + "&";
        }

        // Handle pointer types
        if (typeRef is PointerType pointer)
        {
            return FormatTypeName(pointer.ElementType) + "*";
        }

        return MapBuiltinType(typeRef.FullName) ?? typeRef.Name;
    }

    private static string FormatBaseName(TypeReference typeRef)
    {
        string name = typeRef.Name;
        // Strip the `N suffix from generic type names
        int backtick = name.IndexOf('`');
        if (backtick >= 0)
        {
            name = name.Substring(0, backtick);
        }

        return name;
    }

    private static string? MapBuiltinType(string fullName)
    {
        return fullName switch
        {
            "System.Void" => "void",
            "System.Boolean" => "bool",
            "System.Byte" => "byte",
            "System.SByte" => "sbyte",
            "System.Int16" => "short",
            "System.UInt16" => "ushort",
            "System.Int32" => "int",
            "System.UInt32" => "uint",
            "System.Int64" => "long",
            "System.UInt64" => "ulong",
            "System.Single" => "float",
            "System.Double" => "double",
            "System.Decimal" => "decimal",
            "System.Char" => "char",
            "System.String" => "string",
            "System.Object" => "object",
            _ => null,
        };
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
