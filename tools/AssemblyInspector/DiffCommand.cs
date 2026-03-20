using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mono.Cecil;

namespace AssemblyInspector;

/// <summary>
/// Compares two versions of a .NET assembly, reporting field-level changes
/// for types relevant to the mod (or all types).
/// </summary>
internal static class DiffCommand
{
    internal enum ChangeKind
    {
        Unchanged,
        Added,
        Removed,
        Renamed,
    }

    internal enum RenameConfidence
    {
        None,
        Low,
        Medium,
        High,
    }

    internal enum MemberCategory
    {
        Field,
        Method,
        Property,
    }

    internal record FieldInfo(string Name, string TypeName, int Index);

    internal record MemberInfo(string Signature, MemberCategory Category);

    internal record FieldChange(
        ChangeKind Kind,
        string FieldName,
        string FieldType,
        string? NewFieldName = null,
        RenameConfidence Confidence = RenameConfidence.None
    );

    internal record MemberChange(ChangeKind Kind, MemberCategory Category, string Signature);

    internal record TypeDiff(
        string TypeName,
        List<FieldChange> Changes,
        List<MemberChange>? MemberChanges = null
    )
    {
        public bool HasChanges =>
            Changes.Any(c => c.Kind != ChangeKind.Unchanged)
            || (MemberChanges != null && MemberChanges.Any(mc => mc.Kind != ChangeKind.Unchanged));
    }

    internal record DiffResult(List<TypeDiff> TypeDiffs, List<string> OldOnlyTypes, List<string> NewOnlyTypes)
    {
        public bool HasChanges => TypeDiffs.Any(td => td.HasChanges) || OldOnlyTypes.Count > 0 || NewOnlyTypes.Count > 0;
    }

    internal record DiffOptions(
        string OldDllPath,
        string NewDllPath,
        HashSet<string>? TypeFilter,
        bool KnownFieldsOnly,
        string Format,
        bool IncludeMethods = false,
        bool IncludeProperties = false,
        HashSet<string>? NamespaceFilter = null
    );

    /// <summary>
    /// Entry point called from Program.cs.
    /// Returns 0 = no changes, 1 = error, 2 = changes detected.
    /// </summary>
    public static int Run(DiffOptions options)
    {
        if (!File.Exists(options.OldDllPath))
        {
            Console.Error.WriteLine($"Old DLL not found: {options.OldDllPath}");
            return 1;
        }

        if (!File.Exists(options.NewDllPath))
        {
            Console.Error.WriteLine($"New DLL not found: {options.NewDllPath}");
            return 1;
        }

        HashSet<string>? typeFilter = options.TypeFilter;

        if (options.KnownFieldsOnly)
        {
            string sourcePath = Path.Combine(FindRepoRoot(), "src", "SPTQuestingBots.Client", "Helpers", "ReflectionHelper.cs");
            if (!File.Exists(sourcePath))
            {
                Console.Error.WriteLine($"ReflectionHelper.cs not found at {sourcePath}. Cannot use --known-fields-only.");
                return 1;
            }

            var knownFields = ValidateCommand.ParseKnownFields(sourcePath);
            var knownTypes = new HashSet<string>(knownFields.Select(kf => kf.TypeName), StringComparer.OrdinalIgnoreCase);

            // Merge with any explicit --types filter
            if (typeFilter != null)
            {
                knownTypes.IntersectWith(typeFilter);
            }

            typeFilter = knownTypes;
        }

        DiffResult result;
        try
        {
            result = ComputeDiff(
                options.OldDllPath,
                options.NewDllPath,
                typeFilter,
                options.IncludeMethods,
                options.IncludeProperties,
                options.NamespaceFilter
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error reading assemblies: {ex.Message}");
            return 1;
        }

        if (options.Format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            PrintJson(result);
        }
        else
        {
            PrintTable(result);
        }

        return result.HasChanges ? 2 : 0;
    }

    /// <summary>
    /// Core diff algorithm: compare fields on matching types between two assemblies.
    /// </summary>
    internal static DiffResult ComputeDiff(
        string oldDllPath,
        string newDllPath,
        HashSet<string>? typeFilter,
        bool includeMethods = false,
        bool includeProperties = false,
        HashSet<string>? namespaceFilter = null
    )
    {
        using var oldAssembly = AssemblyDefinition.ReadAssembly(oldDllPath, new ReaderParameters { ReadSymbols = false });
        using var newAssembly = AssemblyDefinition.ReadAssembly(newDllPath, new ReaderParameters { ReadSymbols = false });

        var oldTypes = BuildTypeMap(oldAssembly, namespaceFilter);
        var newTypes = BuildTypeMap(newAssembly, namespaceFilter);

        // Determine which type names to process
        var allTypeNames = new HashSet<string>(oldTypes.Keys, StringComparer.OrdinalIgnoreCase);
        allTypeNames.UnionWith(newTypes.Keys);

        if (typeFilter != null && typeFilter.Count > 0)
        {
            allTypeNames.IntersectWith(typeFilter);
        }

        var typeDiffs = new List<TypeDiff>();
        var oldOnlyTypes = new List<string>();
        var newOnlyTypes = new List<string>();

        foreach (string typeName in allTypeNames.OrderBy(n => n, StringComparer.OrdinalIgnoreCase))
        {
            bool inOld = oldTypes.ContainsKey(typeName);
            bool inNew = newTypes.ContainsKey(typeName);

            if (inOld && !inNew)
            {
                oldOnlyTypes.Add(typeName);
            }
            else if (!inOld && inNew)
            {
                newOnlyTypes.Add(typeName);
            }
            else
            {
                var diff = DiffTypeMembers(
                    typeName,
                    oldTypes[typeName],
                    newTypes[typeName],
                    includeMethods,
                    includeProperties
                );
                typeDiffs.Add(diff);
            }
        }

        return new DiffResult(typeDiffs, oldOnlyTypes, newOnlyTypes);
    }

    /// <summary>
    /// Build a dictionary of short type name -> TypeDefinition for all types in the assembly.
    /// Uses short name as key. If there are duplicates, uses FullName.
    /// Optionally filters by namespace prefix.
    /// </summary>
    internal static Dictionary<string, TypeDefinition> BuildTypeMap(
        AssemblyDefinition assembly,
        HashSet<string>? namespaceFilter = null
    )
    {
        var allTypes = assembly.MainModule.Types.SelectMany(AssemblyHelper.FlattenNestedTypes);

        if (namespaceFilter != null && namespaceFilter.Count > 0)
        {
            allTypes = allTypes.Where(t =>
                namespaceFilter.Any(ns =>
                    t.Namespace != null
                    && (
                        t.Namespace.Equals(ns, StringComparison.OrdinalIgnoreCase)
                        || t.Namespace.StartsWith(ns + ".", StringComparison.OrdinalIgnoreCase)
                    )
                )
            );
        }

        var map = new Dictionary<string, TypeDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in allTypes)
        {
            // Prefer short name, fall back to full name on collision
            string key = type.Name;
            if (map.ContainsKey(key))
            {
                key = type.FullName;
            }

            map[key] = type;
        }

        return map;
    }

    /// <summary>
    /// Compare all requested member kinds between old and new versions of the same type.
    /// </summary>
    internal static TypeDiff DiffTypeMembers(
        string typeName,
        TypeDefinition oldType,
        TypeDefinition newType,
        bool includeMethods = false,
        bool includeProperties = false
    )
    {
        var fieldDiff = DiffTypeFields(typeName, oldType, newType);

        List<MemberChange>? memberChanges = null;

        if (includeMethods || includeProperties)
        {
            memberChanges = new List<MemberChange>();

            if (includeMethods)
            {
                var oldMethods = ExtractMethodSignatures(oldType);
                var newMethods = ExtractMethodSignatures(newType);
                memberChanges.AddRange(DiffMemberLists(oldMethods, newMethods, MemberCategory.Method));
            }

            if (includeProperties)
            {
                var oldProps = ExtractPropertySignatures(oldType);
                var newProps = ExtractPropertySignatures(newType);
                memberChanges.AddRange(DiffMemberLists(oldProps, newProps, MemberCategory.Property));
            }
        }

        return new TypeDiff(typeName, fieldDiff.Changes, memberChanges);
    }

    /// <summary>
    /// Compare fields between old and new versions of the same type.
    /// Detects additions, removals, and renames (with confidence levels).
    /// </summary>
    internal static TypeDiff DiffTypeFields(string typeName, TypeDefinition oldType, TypeDefinition newType)
    {
        var oldFields = oldType.Fields.Select((f, i) => new FieldInfo(f.Name, InspectCommand.FormatTypeName(f.FieldType), i)).ToList();

        var newFields = newType.Fields.Select((f, i) => new FieldInfo(f.Name, InspectCommand.FormatTypeName(f.FieldType), i)).ToList();

        return DiffFieldLists(typeName, oldFields, newFields);
    }

    /// <summary>
    /// Core field-list diffing logic, factored out for testability.
    /// </summary>
    internal static TypeDiff DiffFieldLists(string typeName, List<FieldInfo> oldFields, List<FieldInfo> newFields)
    {
        var oldByName = oldFields.ToDictionary(f => f.Name);
        var newByName = newFields.ToDictionary(f => f.Name);

        var changes = new List<FieldChange>();

        // Track which new fields have been matched (for rename detection)
        var matchedNewFields = new HashSet<string>();

        // 1. Find unchanged and removed fields
        var removedFields = new List<FieldInfo>();
        foreach (var oldField in oldFields)
        {
            if (newByName.ContainsKey(oldField.Name))
            {
                changes.Add(new FieldChange(ChangeKind.Unchanged, oldField.Name, oldField.TypeName));
                matchedNewFields.Add(oldField.Name);
            }
            else
            {
                removedFields.Add(oldField);
            }
        }

        // 2. Find added fields (not matched to any old field by name)
        var addedFields = newFields.Where(f => !matchedNewFields.Contains(f.Name)).ToList();

        // 3. Try to match removed → added as renames
        var unmatchedRemoved = new List<FieldInfo>(removedFields);
        var unmatchedAdded = new List<FieldInfo>(addedFields);

        // Sort by confidence: try HIGH matches first, then MEDIUM, then LOW
        var renames = new List<FieldChange>();

        // Pass 1: HIGH confidence — same type + same base pattern prefix
        MatchRenames(
            unmatchedRemoved,
            unmatchedAdded,
            renames,
            RenameConfidence.High,
            (old, @new) => old.TypeName == @new.TypeName && HasSameBasePattern(old.Name, @new.Name)
        );

        // Pass 2: MEDIUM confidence — same type only
        MatchRenames(unmatchedRemoved, unmatchedAdded, renames, RenameConfidence.Medium, (old, @new) => old.TypeName == @new.TypeName);

        // Pass 3: LOW confidence — same base pattern only
        MatchRenames(
            unmatchedRemoved,
            unmatchedAdded,
            renames,
            RenameConfidence.Low,
            (old, @new) => HasSameBasePattern(old.Name, @new.Name)
        );

        // Add renames
        changes.AddRange(renames);

        // Add remaining unmatched as pure removed/added
        foreach (var removed in unmatchedRemoved)
        {
            changes.Add(new FieldChange(ChangeKind.Removed, removed.Name, removed.TypeName));
        }

        foreach (var added in unmatchedAdded)
        {
            changes.Add(new FieldChange(ChangeKind.Added, added.Name, added.TypeName));
        }

        return new TypeDiff(typeName, changes);
    }

    /// <summary>
    /// Extract method signatures from a type (excludes constructors and property accessors).
    /// </summary>
    internal static List<string> ExtractMethodSignatures(TypeDefinition type)
    {
        return type
            .Methods.Where(m => !m.IsConstructor && !m.IsGetter && !m.IsSetter && !m.IsAddOn && !m.IsRemoveOn)
            .Select(GetMethodSignature)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Extract property signatures from a type.
    /// </summary>
    internal static List<string> ExtractPropertySignatures(TypeDefinition type)
    {
        return type
            .Properties.Select(GetPropertySignature)
            .OrderBy(s => s, StringComparer.Ordinal)
            .ToList();
    }

    internal static string GetMethodSignature(MethodDefinition m)
    {
        string returnType = InspectCommand.FormatTypeName(m.ReturnType);
        string parameters = string.Join(
            ", ",
            m.Parameters.Select(p => InspectCommand.FormatTypeName(p.ParameterType))
        );
        return $"{returnType} {m.Name}({parameters})";
    }

    internal static string GetPropertySignature(PropertyDefinition p)
    {
        string propType = InspectCommand.FormatTypeName(p.PropertyType);
        string accessors = "";
        if (p.GetMethod != null && p.SetMethod != null)
        {
            accessors = " { get; set; }";
        }
        else if (p.GetMethod != null)
        {
            accessors = " { get; }";
        }
        else if (p.SetMethod != null)
        {
            accessors = " { set; }";
        }

        return $"{propType} {p.Name}{accessors}";
    }

    /// <summary>
    /// Diff two lists of member signatures, producing Added/Removed/Unchanged changes.
    /// </summary>
    internal static List<MemberChange> DiffMemberLists(
        List<string> oldMembers,
        List<string> newMembers,
        MemberCategory category
    )
    {
        var oldSet = new HashSet<string>(oldMembers);
        var newSet = new HashSet<string>(newMembers);

        var changes = new List<MemberChange>();

        foreach (string sig in oldMembers)
        {
            if (newSet.Contains(sig))
            {
                changes.Add(new MemberChange(ChangeKind.Unchanged, category, sig));
            }
            else
            {
                changes.Add(new MemberChange(ChangeKind.Removed, category, sig));
            }
        }

        foreach (string sig in newMembers)
        {
            if (!oldSet.Contains(sig))
            {
                changes.Add(new MemberChange(ChangeKind.Added, category, sig));
            }
        }

        return changes;
    }

    private static void MatchRenames(
        List<FieldInfo> unmatchedRemoved,
        List<FieldInfo> unmatchedAdded,
        List<FieldChange> renames,
        RenameConfidence confidence,
        Func<FieldInfo, FieldInfo, bool> matcher
    )
    {
        var toRemoveFromRemoved = new List<FieldInfo>();
        var toRemoveFromAdded = new List<FieldInfo>();

        foreach (var removed in unmatchedRemoved)
        {
            // Find the best match in added fields
            var match = unmatchedAdded.FirstOrDefault(added => !toRemoveFromAdded.Contains(added) && matcher(removed, added));

            if (match != null)
            {
                renames.Add(new FieldChange(ChangeKind.Renamed, removed.Name, removed.TypeName, match.Name, confidence));
                toRemoveFromRemoved.Add(removed);
                toRemoveFromAdded.Add(match);
            }
        }

        foreach (var r in toRemoveFromRemoved)
        {
            unmatchedRemoved.Remove(r);
        }

        foreach (var a in toRemoveFromAdded)
        {
            unmatchedAdded.Remove(a);
        }
    }

    /// <summary>
    /// Check if two field names share the same base pattern (e.g., "float_2" and "float_3").
    /// </summary>
    internal static bool HasSameBasePattern(string name1, string name2)
    {
        string? base1 = ExtractBasePattern(name1);
        string? base2 = ExtractBasePattern(name2);

        if (base1 == null || base2 == null)
        {
            return false;
        }

        return base1.Equals(base2, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extract the base pattern from an obfuscated field name.
    /// e.g., "float_2" -> "float_", "Vector3_0" -> "Vector3_"
    /// </summary>
    internal static string? ExtractBasePattern(string fieldName)
    {
        var match = Regex.Match(fieldName, @"^(.+_)\d+$");
        return match.Success ? match.Groups[1].Value : null;
    }

    private static void PrintTable(DiffResult result)
    {
        bool anyOutput = false;

        if (result.OldOnlyTypes.Count > 0)
        {
            Console.WriteLine("Types only in OLD assembly:");
            foreach (string t in result.OldOnlyTypes)
            {
                Console.WriteLine($"  - {t}");
            }

            Console.WriteLine();
            anyOutput = true;
        }

        if (result.NewOnlyTypes.Count > 0)
        {
            Console.WriteLine("Types only in NEW assembly:");
            foreach (string t in result.NewOnlyTypes)
            {
                Console.WriteLine($"  + {t}");
            }

            Console.WriteLine();
            anyOutput = true;
        }

        foreach (var typeDiff in result.TypeDiffs)
        {
            if (!typeDiff.HasChanges)
            {
                continue;
            }

            Console.WriteLine($"Type: {typeDiff.TypeName}");
            Console.WriteLine(new string('-', 80));
            Console.WriteLine($"  {"Status", -10} {"FieldType", -30} {"Field", -20} {"Details"}");
            Console.WriteLine(new string('-', 80));

            foreach (var change in typeDiff.Changes)
            {
                string status = change.Kind switch
                {
                    ChangeKind.Unchanged => " ",
                    ChangeKind.Added => "+",
                    ChangeKind.Removed => "-",
                    ChangeKind.Renamed => "~",
                    _ => "?",
                };

                string details = change.Kind == ChangeKind.Renamed ? $"-> {change.NewFieldName} ({change.Confidence})" : "";

                Console.WriteLine($"  {status, -10} {change.FieldType, -30} {change.FieldName, -20} {details}");
            }

            if (typeDiff.MemberChanges != null)
            {
                var membersByCategory = typeDiff
                    .MemberChanges.GroupBy(mc => mc.Category)
                    .OrderBy(g => g.Key);

                foreach (var group in membersByCategory)
                {
                    var changedMembers = group.Where(mc => mc.Kind != ChangeKind.Unchanged).ToList();
                    if (changedMembers.Count == 0)
                    {
                        continue;
                    }

                    Console.WriteLine();
                    Console.WriteLine($"  {group.Key}s:");

                    foreach (var mc in group)
                    {
                        string mcStatus = mc.Kind switch
                        {
                            ChangeKind.Unchanged => " ",
                            ChangeKind.Added => "+",
                            ChangeKind.Removed => "-",
                            _ => "?",
                        };
                        Console.WriteLine($"    {mcStatus} {mc.Signature}");
                    }
                }
            }

            Console.WriteLine();
            anyOutput = true;
        }

        if (!anyOutput)
        {
            Console.WriteLine("No field changes detected.");
        }
        else
        {
            // Print summary
            int totalChanged = result.TypeDiffs.Count(td => td.HasChanges);
            int totalAdded = result.TypeDiffs.SelectMany(td => td.Changes).Count(c => c.Kind == ChangeKind.Added);
            int totalRemoved = result.TypeDiffs.SelectMany(td => td.Changes).Count(c => c.Kind == ChangeKind.Removed);
            int totalRenamed = result.TypeDiffs.SelectMany(td => td.Changes).Count(c => c.Kind == ChangeKind.Renamed);

            int methodsAdded = result.TypeDiffs
                .SelectMany(td => td.MemberChanges ?? Enumerable.Empty<MemberChange>())
                .Count(mc => mc.Kind == ChangeKind.Added && mc.Category == MemberCategory.Method);
            int methodsRemoved = result.TypeDiffs
                .SelectMany(td => td.MemberChanges ?? Enumerable.Empty<MemberChange>())
                .Count(mc => mc.Kind == ChangeKind.Removed && mc.Category == MemberCategory.Method);
            int propsAdded = result.TypeDiffs
                .SelectMany(td => td.MemberChanges ?? Enumerable.Empty<MemberChange>())
                .Count(mc => mc.Kind == ChangeKind.Added && mc.Category == MemberCategory.Property);
            int propsRemoved = result.TypeDiffs
                .SelectMany(td => td.MemberChanges ?? Enumerable.Empty<MemberChange>())
                .Count(mc => mc.Kind == ChangeKind.Removed && mc.Category == MemberCategory.Property);

            Console.WriteLine("Summary:");
            Console.WriteLine($"  Types changed: {totalChanged}");
            Console.WriteLine($"  Types only in old: {result.OldOnlyTypes.Count}");
            Console.WriteLine($"  Types only in new: {result.NewOnlyTypes.Count}");
            Console.WriteLine($"  Fields added: {totalAdded}");
            Console.WriteLine($"  Fields removed: {totalRemoved}");
            Console.WriteLine($"  Fields renamed: {totalRenamed}");

            if (methodsAdded > 0 || methodsRemoved > 0)
            {
                Console.WriteLine($"  Methods added: {methodsAdded}");
                Console.WriteLine($"  Methods removed: {methodsRemoved}");
            }

            if (propsAdded > 0 || propsRemoved > 0)
            {
                Console.WriteLine($"  Properties added: {propsAdded}");
                Console.WriteLine($"  Properties removed: {propsRemoved}");
            }
        }
    }

    private static void PrintJson(DiffResult result)
    {
        var allMemberChanges = result.TypeDiffs.SelectMany(td => td.MemberChanges ?? Enumerable.Empty<MemberChange>());

        var output = new
        {
            typesOnlyInOld = result.OldOnlyTypes,
            typesOnlyInNew = result.NewOnlyTypes,
            typeDiffs = result
                .TypeDiffs.Where(td => td.HasChanges)
                .Select(td => new
                {
                    typeName = td.TypeName,
                    changes = td
                        .Changes.Where(c => c.Kind != ChangeKind.Unchanged)
                        .Select(c => new
                        {
                            kind = c.Kind.ToString().ToLowerInvariant(),
                            fieldName = c.FieldName,
                            fieldType = c.FieldType,
                            newFieldName = c.NewFieldName,
                            confidence = c.Kind == ChangeKind.Renamed ? c.Confidence.ToString().ToLowerInvariant() : null,
                        }),
                    memberChanges = (td.MemberChanges ?? Enumerable.Empty<MemberChange>())
                        .Where(mc => mc.Kind != ChangeKind.Unchanged)
                        .Select(mc => new
                        {
                            kind = mc.Kind.ToString().ToLowerInvariant(),
                            category = mc.Category.ToString().ToLowerInvariant(),
                            signature = mc.Signature,
                        }),
                }),
            summary = new
            {
                typesChanged = result.TypeDiffs.Count(td => td.HasChanges),
                typesOnlyInOld = result.OldOnlyTypes.Count,
                typesOnlyInNew = result.NewOnlyTypes.Count,
                fieldsAdded = result.TypeDiffs.SelectMany(td => td.Changes).Count(c => c.Kind == ChangeKind.Added),
                fieldsRemoved = result.TypeDiffs.SelectMany(td => td.Changes).Count(c => c.Kind == ChangeKind.Removed),
                fieldsRenamed = result.TypeDiffs.SelectMany(td => td.Changes).Count(c => c.Kind == ChangeKind.Renamed),
                methodsAdded = allMemberChanges.Count(mc => mc.Kind == ChangeKind.Added && mc.Category == MemberCategory.Method),
                methodsRemoved = allMemberChanges.Count(mc => mc.Kind == ChangeKind.Removed && mc.Category == MemberCategory.Method),
                propertiesAdded = allMemberChanges.Count(mc => mc.Kind == ChangeKind.Added && mc.Category == MemberCategory.Property),
                propertiesRemoved = allMemberChanges.Count(mc => mc.Kind == ChangeKind.Removed && mc.Category == MemberCategory.Property),
            },
        };

        string json = JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine(json);
    }

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

        return Directory.GetCurrentDirectory();
    }
}
