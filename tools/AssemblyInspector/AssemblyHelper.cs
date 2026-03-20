using System.Collections.Generic;
using Mono.Cecil;

namespace AssemblyInspector;

/// <summary>
/// Shared utilities for Mono.Cecil assembly operations.
/// </summary>
internal static class AssemblyHelper
{
    /// <summary>
    /// Recursively yields a type and all its nested types.
    /// </summary>
    internal static IEnumerable<TypeDefinition> FlattenNestedTypes(TypeDefinition type)
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
