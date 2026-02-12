using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace AssemblyInspector;

internal static class ValidateCommand
{
    /// <summary>
    /// Parsed entry from the KnownFields array in ReflectionHelper.cs.
    /// </summary>
    internal record KnownFieldEntry(string TypeName, string FieldName, string Context);

    public static int Run(string dllPath, string sourcePath)
    {
        var knownFields = ParseKnownFields(sourcePath);
        if (knownFields.Count == 0)
        {
            Console.Error.WriteLine("No KnownFields entries found in source file.");
            return 1;
        }

        Console.WriteLine($"Parsed {knownFields.Count} KnownFields entries from {sourcePath}");
        Console.WriteLine();

        using var assembly = AssemblyDefinition.ReadAssembly(
            dllPath,
            new ReaderParameters { ReadSymbols = false }
        );

        int failures = 0;

        foreach (var entry in knownFields)
        {
            bool ok = ValidateField(assembly, entry);
            if (!ok)
            {
                failures++;
            }
        }

        Console.WriteLine();
        if (failures == 0)
        {
            Console.WriteLine($"PASS: All {knownFields.Count} field lookups validated.");
            return 0;
        }
        else
        {
            Console.WriteLine($"FAIL: {failures} of {knownFields.Count} field lookups failed.");
            return 1;
        }
    }

    private static bool ValidateField(AssemblyDefinition assembly, KnownFieldEntry entry)
    {
        var allTypes = assembly.MainModule.Types
            .SelectMany(FlattenNestedTypes)
            .ToList();

        // Find the type by short name (case-insensitive)
        var type = allTypes.FirstOrDefault(
            t => t.Name.Equals(entry.TypeName, StringComparison.OrdinalIgnoreCase)
        );

        if (type == null)
        {
            Console.WriteLine($"  FAIL: Type '{entry.TypeName}' not found in assembly.");
            Console.WriteLine($"        Context: {entry.Context}");
            return false;
        }

        // Look for the field (case-sensitive, matching how AccessTools.Field works)
        var field = type.Fields.FirstOrDefault(f => f.Name == entry.FieldName);
        if (field != null)
        {
            string fieldType = InspectCommand.FormatTypeName(field.FieldType);
            Console.WriteLine($"  OK:   {type.Name}.{entry.FieldName} ({fieldType})");
            return true;
        }

        Console.WriteLine($"  FAIL: Field '{entry.FieldName}' not found on {type.FullName}.");
        Console.WriteLine($"        Context: {entry.Context}");
        SuggestCandidates(type, entry.FieldName);
        return false;
    }

    private static void SuggestCandidates(TypeDefinition type, string missingFieldName)
    {
        if (type.Fields.Count == 0)
        {
            Console.WriteLine($"        (type has no fields)");
            return;
        }

        // Try to find fields with similar names or matching type patterns
        // The obfuscated naming convention uses TypeName_N patterns,
        // so extract the base type name from the missing field.
        string? basePattern = ExtractBasePattern(missingFieldName);

        var candidates = type.Fields
            .Where(f =>
            {
                // Match by similar name prefix
                if (f.Name.Contains(missingFieldName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Match by base pattern (e.g., "float_" for "float_2")
                if (basePattern != null
                    && f.Name.StartsWith(basePattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            })
            .ToList();

        if (candidates.Count > 0)
        {
            Console.WriteLine($"        Candidates with similar names:");
            foreach (var c in candidates)
            {
                string fieldType = InspectCommand.FormatTypeName(c.FieldType);
                Console.WriteLine($"          [{type.Fields.IndexOf(c)}] {c.Name} ({fieldType})");
            }
        }
        else
        {
            // Show all fields as a fallback
            Console.WriteLine($"        All fields on {type.Name}:");
            for (int i = 0; i < type.Fields.Count; i++)
            {
                var f = type.Fields[i];
                string fieldType = InspectCommand.FormatTypeName(f.FieldType);
                Console.WriteLine($"          [{i}] {f.Name} ({fieldType})");
            }
        }
    }

    private static string? ExtractBasePattern(string fieldName)
    {
        // Match patterns like "float_2" -> "float_", "Vector3_0" -> "Vector3_"
        var match = Regex.Match(fieldName, @"^(.+_)\d+$");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    internal static List<KnownFieldEntry> ParseKnownFields(string sourcePath)
    {
        string source = File.ReadAllText(sourcePath);
        var entries = new List<KnownFieldEntry>();

        // Match lines like: (typeof(BotSpawner), "Bots", "BotDiedPatch ___Bots"),
        var regex = new Regex(
            @"\(typeof\((\w+)\),\s*""([^""]+)"",\s*""([^""]+)""\)",
            RegexOptions.Compiled
        );

        foreach (Match match in regex.Matches(source))
        {
            entries.Add(new KnownFieldEntry(
                TypeName: match.Groups[1].Value,
                FieldName: match.Groups[2].Value,
                Context: match.Groups[3].Value
            ));
        }

        return entries;
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
