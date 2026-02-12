using System;
using System.IO;

namespace AssemblyInspector;

internal static class Program
{
    private const string DefaultDllPath = "libs/Assembly-CSharp.dll";
    private const string DefaultSourcePath =
        "src/SPTQuestingBots.Client/Helpers/ReflectionHelper.cs";

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
            "validate" => RunValidate(commandArgs),
            "help" or "--help" or "-h" => PrintHelpAndSucceed(),
            _ => UnknownCommand(command),
        };
    }

    private static int RunInspect(string[] args)
    {
        string? typeName = null;
        string dllPath = DefaultDllPath;

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--dll" && i + 1 < args.Length)
            {
                dllPath = args[++i];
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
            Console.Error.WriteLine("Usage: AssemblyInspector inspect <TypeName> [--dll path]");
            Console.Error.WriteLine();
            Console.Error.WriteLine(
                "Inspects a type in Assembly-CSharp.dll and lists all its fields."
            );
            return 1;
        }

        if (!File.Exists(dllPath))
        {
            Console.Error.WriteLine($"DLL not found: {dllPath}");
            Console.Error.WriteLine(
                "Run 'make copy-libs' to copy game DLLs, or specify --dll path."
            );
            return 1;
        }

        return InspectCommand.Run(typeName, dllPath);
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
            Console.Error.WriteLine(
                "Run 'make copy-libs' to copy game DLLs, or specify --dll path."
            );
            return 1;
        }

        if (!File.Exists(sourcePath))
        {
            Console.Error.WriteLine($"Source file not found: {sourcePath}");
            return 1;
        }

        return ValidateCommand.Run(dllPath, sourcePath);
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
        Console.WriteLine("  inspect <TypeName> [--dll path]     List all fields on a type");
        Console.WriteLine("  validate [--dll path] [--source path]");
        Console.WriteLine("                                      Validate KnownFields against DLL");
        Console.WriteLine("  help                                Show this help message");
        Console.WriteLine();
        Console.WriteLine("Defaults:");
        Console.WriteLine($"  --dll     {DefaultDllPath}");
        Console.WriteLine($"  --source  {DefaultSourcePath}");
    }

    private static int UnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Run 'AssemblyInspector help' for usage information.");
        return 1;
    }
}
