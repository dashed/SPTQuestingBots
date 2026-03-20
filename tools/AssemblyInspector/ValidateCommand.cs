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

        using var assembly = AssemblyDefinition.ReadAssembly(dllPath, new ReaderParameters { ReadSymbols = false });

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
        var allTypes = assembly.MainModule.Types.SelectMany(FlattenNestedTypes).ToList();

        // Find the type by short name (case-insensitive)
        var type = allTypes.FirstOrDefault(t => t.Name.Equals(entry.TypeName, StringComparison.OrdinalIgnoreCase));

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

    internal enum Confidence
    {
        Low,
        Medium,
        High,
    }

    internal record SuggestionEntry(FieldDefinition Field, int FieldIndex, Confidence Confidence, string Reason);

    internal static void SuggestCandidates(TypeDefinition type, string missingFieldName)
    {
        if (type.Fields.Count == 0)
        {
            Console.WriteLine($"        (type has no fields)");
            return;
        }

        string? basePattern = ExtractBasePattern(missingFieldName);
        string? inferredType = InferFieldType(missingFieldName);
        int? missingIndex = ExtractIndex(missingFieldName);

        var suggestions = new List<SuggestionEntry>();

        for (int i = 0; i < type.Fields.Count; i++)
        {
            var field = type.Fields[i];
            string? candidateBase = ExtractBasePattern(field.Name);
            bool sameBase =
                basePattern != null && candidateBase != null && basePattern.Equals(candidateBase, StringComparison.OrdinalIgnoreCase);

            bool sameInferredType = false;
            if (inferredType != null && sameBase)
            {
                string actualType = InspectCommand.FormatTypeName(field.FieldType);
                sameInferredType = actualType.Equals(inferredType, StringComparison.OrdinalIgnoreCase);
            }

            bool adjacentIndex = false;
            if (missingIndex.HasValue && sameBase)
            {
                int? candidateIndex = ExtractIndex(field.Name);
                if (candidateIndex.HasValue && Math.Abs(candidateIndex.Value - missingIndex.Value) <= 1)
                {
                    adjacentIndex = true;
                }
            }

            if (sameBase && sameInferredType && adjacentIndex)
            {
                string actualType = InspectCommand.FormatTypeName(field.FieldType);
                int? idx = ExtractIndex(field.Name);
                int diff = idx.HasValue ? idx.Value - missingIndex!.Value : 0;
                string sign = diff >= 0 ? "+" : "";
                suggestions.Add(new SuggestionEntry(field, i, Confidence.High, $"same type: {actualType}, index {sign}{diff}"));
            }
            else if (sameBase && sameInferredType)
            {
                string actualType = InspectCommand.FormatTypeName(field.FieldType);
                suggestions.Add(new SuggestionEntry(field, i, Confidence.Medium, $"same type: {actualType}"));
            }
            else if (sameBase)
            {
                string actualType = InspectCommand.FormatTypeName(field.FieldType);
                suggestions.Add(new SuggestionEntry(field, i, Confidence.Low, $"same base pattern, type: {actualType}"));
            }
        }

        if (suggestions.Count > 0)
        {
            // Sort by confidence descending (High first)
            suggestions.Sort((a, b) => b.Confidence.CompareTo(a.Confidence));

            foreach (var s in suggestions)
            {
                Console.WriteLine(
                    $"        Suggestion ({s.Confidence.ToString().ToUpperInvariant()}): "
                        + $"{missingFieldName} -> {s.Field.Name} ({s.Reason})"
                );
                Console.WriteLine($"        Fix: In ReflectionHelper.cs, change \"{missingFieldName}\" to \"{s.Field.Name}\"");
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

    internal static string? ExtractBasePattern(string fieldName)
    {
        // Match patterns like "float_2" -> "float_", "Vector3_0" -> "Vector3_"
        var match = Regex.Match(fieldName, @"^(.+_)\d+$");
        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// Infer the expected C# type from the obfuscated field naming pattern.
    /// "float_2" -> "float", "Vector3_0" -> "Vector3", "List_1" -> "List"
    /// </summary>
    internal static string? InferFieldType(string fieldName)
    {
        var match = Regex.Match(fieldName, @"^(.+)_\d+$");
        if (!match.Success)
        {
            return null;
        }

        string baseName = match.Groups[1].Value;

        // Map common obfuscated patterns to their C# type names
        return baseName.ToLowerInvariant() switch
        {
            "float" => "float",
            "double" => "double",
            "int" => "int",
            "bool" => "bool",
            "string" => "string",
            "long" => "long",
            "byte" => "byte",
            "short" => "short",
            _ => baseName, // For types like "Vector3", "List", etc., use as-is
        };
    }

    internal static int? ExtractIndex(string fieldName)
    {
        var match = Regex.Match(fieldName, @"_(\d+)$");
        if (match.Success && int.TryParse(match.Groups[1].Value, out int index))
        {
            return index;
        }

        return null;
    }

    internal static List<KnownFieldEntry> ParseKnownFields(string sourcePath)
    {
        string source = File.ReadAllText(sourcePath);
        var entries = new List<KnownFieldEntry>();

        // Match lines like: (typeof(BotSpawner), "Bots", "BotDiedPatch ___Bots"),
        var regex = new Regex(@"\(typeof\((\w+)\),\s*""([^""]+)"",\s*""([^""]+)""\)", RegexOptions.Compiled);

        foreach (Match match in regex.Matches(source))
        {
            entries.Add(
                new KnownFieldEntry(TypeName: match.Groups[1].Value, FieldName: match.Groups[2].Value, Context: match.Groups[3].Value)
            );
        }

        return entries;
    }

    private static IEnumerable<TypeDefinition> FlattenNestedTypes(TypeDefinition type) => AssemblyHelper.FlattenNestedTypes(type);
}
