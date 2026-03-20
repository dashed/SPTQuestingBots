using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace AssemblyInspector;

/// <summary>
/// Finds all methods that reference a specific field or method by scanning IL instructions.
/// </summary>
internal static class XrefCommand
{
    internal enum UsageKind
    {
        Read,
        Write,
        Call,
        Other,
    }

    internal enum MemberKind
    {
        Field,
        Method,
        All,
    }

    internal record XrefEntry(
        string DeclaringType,
        string MethodName,
        UsageKind Usage,
        int IlOffset,
        string Opcode
    );

    internal record XrefResult(
        string TargetName,
        List<XrefEntry> References
    );

    internal record XrefOptions(
        string Target,
        string DllPath,
        MemberKind Kind
    );

    public static int Run(XrefOptions options)
    {
        using var assembly = AssemblyDefinition.ReadAssembly(
            options.DllPath,
            new ReaderParameters { ReadSymbols = false }
        );

        var result = FindReferences(assembly, options.Target, options.Kind);

        PrintResult(result);

        return 0;
    }

    internal static XrefResult FindReferences(
        AssemblyDefinition assembly,
        string target,
        MemberKind kind
    )
    {
        // Parse target: "TypeName.MemberName" or just "MemberName"
        string? targetType = null;
        string targetMember;

        int dotIndex = target.LastIndexOf('.');
        if (dotIndex >= 0)
        {
            targetType = target.Substring(0, dotIndex);
            targetMember = target.Substring(dotIndex + 1);
        }
        else
        {
            targetMember = target;
        }

        var references = new List<XrefEntry>();

        var allTypes = assembly.MainModule.Types.SelectMany(AssemblyHelper.FlattenNestedTypes);

        foreach (var type in allTypes)
        {
            foreach (var method in type.Methods)
            {
                if (!method.HasBody)
                {
                    continue;
                }

                foreach (var instruction in method.Body.Instructions)
                {
                    var entry = CheckInstruction(
                        instruction,
                        targetType,
                        targetMember,
                        kind,
                        type,
                        method
                    );

                    if (entry != null)
                    {
                        references.Add(entry);
                    }
                }
            }
        }

        return new XrefResult(target, references);
    }

    private static XrefEntry? CheckInstruction(
        Instruction instruction,
        string? targetType,
        string targetMember,
        MemberKind kind,
        TypeDefinition declaringType,
        MethodDefinition method
    )
    {
        // Check field references
        if (kind != MemberKind.Method && instruction.Operand is FieldReference fieldRef)
        {
            if (MatchesMember(fieldRef.DeclaringType, fieldRef.Name, targetType, targetMember))
            {
                var usage = ClassifyFieldUsage(instruction.OpCode);
                if (usage != null)
                {
                    return new XrefEntry(
                        declaringType.FullName,
                        method.Name,
                        usage.Value,
                        instruction.Offset,
                        instruction.OpCode.Name
                    );
                }
            }
        }

        // Check method references
        if (kind != MemberKind.Field && instruction.Operand is MethodReference methodRef)
        {
            if (MatchesMember(
                    methodRef.DeclaringType,
                    methodRef.Name,
                    targetType,
                    targetMember
                ))
            {
                var usage = ClassifyMethodUsage(instruction.OpCode);
                if (usage != null)
                {
                    return new XrefEntry(
                        declaringType.FullName,
                        method.Name,
                        usage.Value,
                        instruction.Offset,
                        instruction.OpCode.Name
                    );
                }
            }
        }

        return null;
    }

    private static bool MatchesMember(
        TypeReference declaringType,
        string memberName,
        string? targetType,
        string targetMember
    )
    {
        if (!memberName.Equals(targetMember, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (targetType == null)
        {
            return true;
        }

        // Match against short name or full name
        return declaringType.Name.Equals(targetType, StringComparison.OrdinalIgnoreCase)
            || declaringType.FullName.Equals(targetType, StringComparison.OrdinalIgnoreCase);
    }

    internal static UsageKind? ClassifyFieldUsage(OpCode opCode)
    {
        if (opCode == OpCodes.Ldfld
            || opCode == OpCodes.Ldsfld
            || opCode == OpCodes.Ldflda
            || opCode == OpCodes.Ldsflda)
        {
            return UsageKind.Read;
        }

        if (opCode == OpCodes.Stfld || opCode == OpCodes.Stsfld)
        {
            return UsageKind.Write;
        }

        return null;
    }

    internal static UsageKind? ClassifyMethodUsage(OpCode opCode)
    {
        if (opCode == OpCodes.Call || opCode == OpCodes.Callvirt)
        {
            return UsageKind.Call;
        }

        if (opCode == OpCodes.Newobj || opCode == OpCodes.Ldftn || opCode == OpCodes.Ldvirtftn)
        {
            return UsageKind.Other;
        }

        return null;
    }

    private static void PrintResult(XrefResult result)
    {
        if (result.References.Count == 0)
        {
            Console.WriteLine($"No cross-references found for: {result.TargetName}");
            return;
        }

        Console.WriteLine($"Cross-references to {result.TargetName}:");
        Console.WriteLine();

        foreach (var entry in result.References)
        {
            string usage = entry.Usage.ToString().ToUpperInvariant();
            string location = $"{entry.DeclaringType}::{entry.MethodName}()";
            Console.WriteLine(
                $"  {usage,-7} {location,-50} IL_{entry.IlOffset:X4} {entry.Opcode}"
            );
        }

        Console.WriteLine();

        int reads = result.References.Count(r => r.Usage == UsageKind.Read);
        int writes = result.References.Count(r => r.Usage == UsageKind.Write);
        int calls = result.References.Count(r => r.Usage == UsageKind.Call);
        int other = result.References.Count(r => r.Usage == UsageKind.Other);

        var parts = new List<string>();
        if (reads > 0)
            parts.Add($"{reads} read{(reads != 1 ? "s" : "")}");
        if (writes > 0)
            parts.Add($"{writes} write{(writes != 1 ? "s" : "")}");
        if (calls > 0)
            parts.Add($"{calls} call{(calls != 1 ? "s" : "")}");
        if (other > 0)
            parts.Add($"{other} other");

        Console.WriteLine(
            $"Total: {result.References.Count} reference{(result.References.Count != 1 ? "s" : "")} ({string.Join(", ", parts)})"
        );
    }
}
