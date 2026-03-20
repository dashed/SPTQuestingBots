using System;
using System.Collections.Generic;
using System.IO;

namespace AssemblyInspector;

internal static class Program
{
    private const string DefaultDllPath = "libs/Assembly-CSharp.dll";
    private const string DefaultSourcePath = "src/SPTQuestingBots.Client/Helpers/ReflectionHelper.cs";

    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintHelp();
            return 1;
        }

        string command = args[0].ToLowerInvariant();
        string[] commandArgs = args.Length > 1 ? args[1..] : Array.Empty<string>();

        return command switch
        {
            "inspect" => RunInspect(commandArgs),
            "decompile" => RunDecompile(commandArgs),
            "validate" => RunValidate(commandArgs),
            "diff" => RunDiff(commandArgs),
            "xref" => RunXref(commandArgs),
            "help" or "--help" or "-h" => PrintHelpAndSucceed(),
            _ => UnknownCommand(command),
        };
    }

    private static int RunInspect(string[] args)
    {
        string? typeName = null;
        string dllPath = DefaultDllPath;
        bool includeInherited = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dll" && i + 1 < args.Length)
            {
                dllPath = args[++i];
            }
            else if (args[i] == "--include-inherited")
            {
                includeInherited = true;
            }
            else if (!args[i].StartsWith("-"))
            {
                typeName = args[i];
            }
            else
            {
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                return 1;
            }
        }

        if (typeName == null)
        {
            Console.Error.WriteLine("Usage: AssemblyInspector inspect <TypeName> [--dll path] [--include-inherited]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Inspects a type in Assembly-CSharp.dll and lists all its fields.");
            return 1;
        }

        if (!File.Exists(dllPath))
        {
            Console.Error.WriteLine($"DLL not found: {dllPath}");
            Console.Error.WriteLine("Run 'make copy-libs' to copy game DLLs, or specify --dll path.");
            return 1;
        }

        return InspectCommand.Run(typeName, dllPath, includeInherited);
    }

    private static int RunDecompile(string[] args)
    {
        string? typeName = null;
        string dllPath = DefaultDllPath;
        string? methodName = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dll" && i + 1 < args.Length)
            {
                dllPath = args[++i];
            }
            else if (args[i] == "--method" && i + 1 < args.Length)
            {
                methodName = args[++i];
            }
            else if (!args[i].StartsWith("-"))
            {
                typeName = args[i];
            }
            else
            {
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                return 1;
            }
        }

        if (typeName == null)
        {
            Console.Error.WriteLine("Usage: AssemblyInspector decompile <TypeName> [--method MethodName] [--dll path]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Decompiles a type (or a specific method) to readable C# source.");
            return 1;
        }

        if (!File.Exists(dllPath))
        {
            Console.Error.WriteLine($"DLL not found: {dllPath}");
            Console.Error.WriteLine("Run 'make copy-libs' to copy game DLLs, or specify --dll path.");
            return 1;
        }

        return DecompileCommand.Run(typeName, dllPath, methodName);
    }

    private static int RunValidate(string[] args)
    {
        string dllPath = DefaultDllPath;
        string sourcePath = DefaultSourcePath;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dll" && i + 1 < args.Length)
            {
                dllPath = args[++i];
            }
            else if (args[i] == "--source" && i + 1 < args.Length)
            {
                sourcePath = args[++i];
            }
            else
            {
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                return 1;
            }
        }

        if (!File.Exists(dllPath))
        {
            Console.Error.WriteLine($"DLL not found: {dllPath}");
            Console.Error.WriteLine("Run 'make copy-libs' to copy game DLLs, or specify --dll path.");
            return 1;
        }

        if (!File.Exists(sourcePath))
        {
            Console.Error.WriteLine($"Source file not found: {sourcePath}");
            return 1;
        }

        return ValidateCommand.Run(dllPath, sourcePath);
    }

    private static int RunDiff(string[] args)
    {
        string? oldDll = null;
        string? newDll = null;
        HashSet<string>? typeFilter = null;
        bool knownFieldsOnly = false;
        string format = "table";
        bool includeMethods = false;
        bool includeProperties = false;
        HashSet<string>? namespaceFilter = null;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--old" && i + 1 < args.Length)
            {
                oldDll = args[++i];
            }
            else if (args[i] == "--new" && i + 1 < args.Length)
            {
                newDll = args[++i];
            }
            else if (args[i] == "--types" && i + 1 < args.Length)
            {
                typeFilter = new HashSet<string>(
                    args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.OrdinalIgnoreCase
                );
            }
            else if (args[i] == "--known-fields-only")
            {
                knownFieldsOnly = true;
            }
            else if (args[i] == "--format" && i + 1 < args.Length)
            {
                format = args[++i];
                if (format != "table" && format != "json")
                {
                    Console.Error.WriteLine($"Unknown format: {format}. Use 'table' or 'json'.");
                    return 1;
                }
            }
            else if (args[i] == "--include-methods")
            {
                includeMethods = true;
            }
            else if (args[i] == "--include-properties")
            {
                includeProperties = true;
            }
            else if (args[i] == "--include-all")
            {
                includeMethods = true;
                includeProperties = true;
            }
            else if (args[i] == "--namespace" && i + 1 < args.Length)
            {
                namespaceFilter = new HashSet<string>(
                    args[++i].Split(',', StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.OrdinalIgnoreCase
                );
            }
            else
            {
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                return 1;
            }
        }

        if (oldDll == null || newDll == null)
        {
            Console.Error.WriteLine(
                "Usage: AssemblyInspector diff --old <path> --new <path> [options]"
            );
            Console.Error.WriteLine();
            Console.Error.WriteLine("Options:");
            Console.Error.WriteLine("  --types T1,T2          Only diff specified types");
            Console.Error.WriteLine("  --known-fields-only    Only diff types used in KnownFields");
            Console.Error.WriteLine("  --include-methods      Compare method signatures");
            Console.Error.WriteLine("  --include-properties   Compare properties");
            Console.Error.WriteLine("  --include-all          Compare fields + methods + properties");
            Console.Error.WriteLine("  --namespace NS1,NS2    Only diff types in specified namespace(s)");
            Console.Error.WriteLine("  --format table|json    Output format (default: table)");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Exit codes: 0 = no changes, 1 = error, 2 = changes detected");
            return 1;
        }

        var options = new DiffCommand.DiffOptions(
            oldDll,
            newDll,
            typeFilter,
            knownFieldsOnly,
            format,
            includeMethods,
            includeProperties,
            namespaceFilter
        );

        return DiffCommand.Run(options);
    }

    private static int RunXref(string[] args)
    {
        string? target = null;
        string dllPath = DefaultDllPath;
        var kind = XrefCommand.MemberKind.All;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dll" && i + 1 < args.Length)
            {
                dllPath = args[++i];
            }
            else if (args[i] == "--kind" && i + 1 < args.Length)
            {
                string kindStr = args[++i].ToLowerInvariant();
                kind = kindStr switch
                {
                    "field" => XrefCommand.MemberKind.Field,
                    "method" => XrefCommand.MemberKind.Method,
                    "all" => XrefCommand.MemberKind.All,
                    _ => XrefCommand.MemberKind.All,
                };

                if (kindStr != "field" && kindStr != "method" && kindStr != "all")
                {
                    Console.Error.WriteLine($"Unknown kind: {kindStr}. Use 'field', 'method', or 'all'.");
                    return 1;
                }
            }
            else if (!args[i].StartsWith("-"))
            {
                target = args[i];
            }
            else
            {
                Console.Error.WriteLine($"Unknown option: {args[i]}");
                return 1;
            }
        }

        if (target == null)
        {
            Console.Error.WriteLine("Usage: AssemblyInspector xref <TypeName.MemberName> [--dll path] [--kind field|method|all]");
            Console.Error.WriteLine();
            Console.Error.WriteLine("Finds all methods referencing a specific field or method.");
            return 1;
        }

        if (!File.Exists(dllPath))
        {
            Console.Error.WriteLine($"DLL not found: {dllPath}");
            Console.Error.WriteLine("Run 'make copy-libs' to copy game DLLs, or specify --dll path.");
            return 1;
        }

        var options = new XrefCommand.XrefOptions(target, dllPath, kind);
        return XrefCommand.Run(options);
    }

    private static int PrintHelpAndSucceed()
    {
        PrintHelp();
        return 0;
    }

    private static void PrintHelp()
    {
        Console.WriteLine("AssemblyInspector - Inspect .NET assemblies for obfuscated field changes");
        Console.WriteLine();
        Console.WriteLine("Usage: AssemblyInspector <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  inspect <TypeName> [--dll path] [--include-inherited]");
        Console.WriteLine("                                      List all fields on a type");
        Console.WriteLine("  decompile <TypeName> [--method MethodName] [--dll path]");
        Console.WriteLine("                                      Decompile a type or method to C# source");
        Console.WriteLine("  validate [--dll path] [--source path]");
        Console.WriteLine("                                      Validate KnownFields against DLL");
        Console.WriteLine("  diff --old <path> --new <path> [--types T1,T2] [--known-fields-only] [--format table|json]");
        Console.WriteLine("       [--include-methods] [--include-properties] [--include-all] [--namespace NS]");
        Console.WriteLine("                                      Compare changes between assembly versions");
        Console.WriteLine("  xref <TypeName.MemberName> [--dll path] [--kind field|method|all]");
        Console.WriteLine("                                      Find all methods referencing a field or method");
        Console.WriteLine("  help                                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Defaults:");
        Console.WriteLine($"  --dll     {DefaultDllPath}");
        Console.WriteLine($"  --source  {DefaultSourcePath}");
        Console.WriteLine();
        Console.WriteLine("Exit codes (diff): 0 = no changes, 1 = error, 2 = changes detected");
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Run 'AssemblyInspector help' for usage information.");
        return 1;
    }
}
