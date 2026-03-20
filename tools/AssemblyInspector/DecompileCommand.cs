using System;
using System.Collections.Generic;
using System.Linq;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.TypeSystem;

namespace AssemblyInspector;

internal static class DecompileCommand
{
    public static int Run(string typeName, string dllPath, string? methodName)
    {
        var settings = new DecompilerSettings { ThrowOnAssemblyResolveErrors = false, ShowXmlDocumentation = false };

        CSharpDecompiler decompiler;
        try
        {
            decompiler = new CSharpDecompiler(dllPath, settings);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to load assembly: {ex.Message}");
            return 1;
        }

        var matches = FindTypes(decompiler, typeName);

        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"No type matching '{typeName}' found.");
            SuggestTypes(decompiler, typeName);
            return 1;
        }

        if (matches.Count > 1 && methodName == null)
        {
            Console.Error.WriteLine($"Multiple types match '{typeName}'. Please be more specific:");
            foreach (var t in matches)
            {
                Console.Error.WriteLine($"  {t.FullName}");
            }
            return 1;
        }

        foreach (var type in matches)
        {
            if (methodName != null)
            {
                int result = DecompileMethod(decompiler, type, methodName);
                if (result != 0)
                {
                    return result;
                }
            }
            else
            {
                string code = decompiler.DecompileTypeAsString(type.FullTypeName);
                Console.Write(code);
            }
        }

        return 0;
    }

    private static int DecompileMethod(CSharpDecompiler decompiler, ITypeDefinition type, string methodName)
    {
        var methods = type.Methods.Where(m => m.Name.Equals(methodName, StringComparison.OrdinalIgnoreCase)).ToList();

        if (methods.Count == 0)
        {
            Console.Error.WriteLine($"No method '{methodName}' found on type '{type.FullName}'.");

            var allMethods = type.Methods.Where(m => !m.IsCompilerGenerated()).Select(m => m.Name).Distinct().OrderBy(n => n).ToList();

            if (allMethods.Count > 0)
            {
                Console.Error.WriteLine("Available methods:");
                foreach (string name in allMethods)
                {
                    Console.Error.WriteLine($"  {name}");
                }
            }

            return 1;
        }

        foreach (var method in methods)
        {
            var handle = method.MetadataToken;
            string code = decompiler.DecompileAsString(handle);
            Console.Write(code);
        }

        return 0;
    }

    private static List<ITypeDefinition> FindTypes(CSharpDecompiler decompiler, string name)
    {
        var allTypes = decompiler.TypeSystem.MainModule.TypeDefinitions.Where(t => !t.Name.Contains('<')).ToList();

        // Try exact short-name match (case-insensitive)
        var matches = allTypes.Where(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count > 0)
        {
            return matches;
        }

        // Try full-name contains match (case-insensitive)
        matches = allTypes.Where(t => t.FullName.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();

        if (matches.Count > 0)
        {
            return matches;
        }

        // Try subsequence match
        matches = allTypes.Where(t => InspectCommand.IsSubsequenceMatch(t.Name, name)).ToList();

        return matches;
    }

    private static void SuggestTypes(CSharpDecompiler decompiler, string name)
    {
        var allTypes = decompiler.TypeSystem.MainModule.TypeDefinitions.Where(t => !t.Name.Contains('<')).ToList();

        string nameLower = name.ToLowerInvariant();

        var suggestions = allTypes
            .Where(t =>
                t.Name.Contains(nameLower, StringComparison.OrdinalIgnoreCase)
                || nameLower.Contains(t.Name.ToLowerInvariant())
                || InspectCommand.IsSubsequenceMatch(t.Name, name)
            )
            .OrderBy(t => t.Name.Length)
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
}
