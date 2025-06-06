﻿using Mono.Cecil;
using Mono.Cecil.Cil;

public static class GlobalSettings
{
    public static bool rename = false;
    public static bool extraInstructions = false;
    public static bool obfuscateStrings = false;
    public static bool flattenCode = false;
    public static bool shuffel = false;
    public static bool antiDebugging = false;
}

namespace Obfuscator {
    class Program
    {
        static void Main(string[] args)
        {
            string inputAssembly = string.Empty;
            string suffix = string.Empty;
            if (args.Length < 1)
            {
                Console.WriteLine("Usage: sudo dotnet run <flags> <inputAssembly>. Use --help for more information.");
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("--"))
                {
                    switch (args[i]) {
                        case "--rename":
                            GlobalSettings.rename = true;
                            Console.WriteLine("Renaming enabled");
                            break;
                        case "--extra-instructions":
                            GlobalSettings.extraInstructions = true;
                            Console.WriteLine("Extra instructions enabled");
                            break;
                        case "--obfuscate-strings":
                            GlobalSettings.obfuscateStrings = true;
                            Console.WriteLine("String obfuscation enabled");
                        break;
                        case "--flatten-code":
                            GlobalSettings.flattenCode = true;
                            Console.WriteLine("Code flattening enabled");
                                break;
                        case "--shuffle":
                            GlobalSettings.shuffel = true;
                            Console.WriteLine("Shuffeling enabled");
                            break;
                        case "--antidebugging":
                            GlobalSettings.antiDebugging = true;
                            Console.WriteLine("Anti-debugging enabled");
                            break;
                        case "--all":
                            GlobalSettings.rename = true;
                            GlobalSettings.extraInstructions = true;
                            GlobalSettings.obfuscateStrings = true;
                            GlobalSettings.flattenCode = true;
                            GlobalSettings.shuffel = true;
                            GlobalSettings.antiDebugging = true;
                            Console.WriteLine("All options enabled");
                            break;
                        case "--input":
                            if (i + 1 < args.Length)
                            {
                                inputAssembly = args[i + 1];
                                i++;
                            }
                            break;
                        case "--suffix":
                            if (i + 1 < args.Length)
                            {
                                suffix = args[i + 1];
                                i++;
                            }
                            break;
                        case "--help":
                            Console.WriteLine("Usage: sudo dotnet run <flags> <inputAssembly>");
                            Console.WriteLine("Flags:");
                            Console.WriteLine("--rename: Rename methods and types");
                            Console.WriteLine("--extra-instructions: Add extra instructions");
                            Console.WriteLine("--obfuscate-strings: Obfuscate strings");
                            Console.WriteLine("--flatten-code: Flatten code");
                            Console.WriteLine("--shuffle: Shuffle code");
                            Console.WriteLine("--antidebugging: Add anti-debugging code");
                            Console.WriteLine("--input <assembly>: Specify input assembly");
                            Console.WriteLine("--suffix <suffix>: Specify suffix for output assembly");
                            Console.WriteLine("--all: Enable all options");
                            Console.WriteLine("--help: Show this help message");
                            return;
                        default:
                            Console.WriteLine("Unknown flag: " + args[i]);
                            return;

                    }
                    continue;
                }
                else if (args[i].EndsWith(".dll") || args[i].EndsWith(".exe"))
                {
                    inputAssembly = args[i];
                }
                else
                {
                    Console.WriteLine("Unknown argument: " + args[i]);
                    return;
                }
            }

            if (inputAssembly == string.Empty)
            {
                Console.WriteLine("Please provide the path to the assembly to obfuscate.");
                return;
            }
            string outputAssembly = Path.Combine(Path.GetDirectoryName(inputAssembly)!,
                Path.GetFileNameWithoutExtension(inputAssembly) + ".Obfuscated_" + suffix + Path.GetExtension(inputAssembly));

            Console.WriteLine("Input assembly: " + inputAssembly);
            Console.WriteLine("Output assembly: " + outputAssembly);

            Obfuscate(inputAssembly, outputAssembly);

            // Copy runtime config if it exists
            string runtimeConfig = Path.ChangeExtension(inputAssembly, ".runtimeconfig.json");
            string outputRuntimeConfig = Path.ChangeExtension(outputAssembly, ".runtimeconfig.json");

            if (File.Exists(runtimeConfig))
            {
                File.Copy(runtimeConfig, outputRuntimeConfig, true);
                Console.WriteLine("Copied runtime config: " + outputRuntimeConfig);
            }
        }

        public static void Obfuscate(string inputAssemblyPath, string outputAssemblyPath)
        {
            // Load the assembly
            var assembly = AssemblyDefinition.ReadAssembly(inputAssemblyPath);

            // Change the assembly name
            assembly.Name.Name = Path.GetFileNameWithoutExtension(outputAssemblyPath);
            assembly.MainModule.Name = Path.GetFileName(outputAssemblyPath);

            // Adds unnecessary NOP instructions to methods
            // Does not really do anything useful
            foreach (var module in assembly.Modules)
            {
                foreach (var type in module.Types)
                {
                    foreach (var method in type.Methods)
                    {
                        // Example of adding an unnecessary NOP instruction
                        if (GlobalSettings.extraInstructions)
                        {
                            var ilProcessor = method.Body.GetILProcessor();
                            var firstInstruction = method.Body.Instructions[0];
                            ilProcessor.InsertBefore(firstInstruction, ilProcessor.Create(OpCodes.Nop));
                        }
                    }
                }
            }

            if (GlobalSettings.obfuscateStrings)
            {
                ObfuscateStrings.ObfuscateStringModule(assembly.MainModule);
            }
            if (GlobalSettings.flattenCode) {
                CodeFlattening.FlattenModule(assembly.MainModule, GlobalSettings.shuffel);
            }
            else if (GlobalSettings.shuffel) {
                Console.WriteLine("Can't shuffle without flattening");
                Console.WriteLine("Will automatically flatten");
                GlobalSettings.flattenCode = true;
                CodeFlattening.FlattenModule(assembly.MainModule, GlobalSettings.shuffel);
            }
            if (GlobalSettings.antiDebugging)
            {
                AntiDebugging.AntiDebugModule(assembly.MainModule);
            }
            if (GlobalSettings.rename)
            {
                Rename.RenameModule(assembly.MainModule);
            }

            // Save the modified assembly
            assembly.Write(outputAssemblyPath);
        }

    }
}
