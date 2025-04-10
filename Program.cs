﻿using Mono.Cecil;
using Mono.Cecil.Cil;

public static class GlobalSettings
{
    public static bool rename = false;
    public static bool extraInstructions = false;
    public static bool obfuscateStrings = true;
}

class Obfuscator
{
    static void Main(string[] args)
    {
        string inputAssembly = string.Empty;
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: sudo dotnet run <inputAssembly>");
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
                }
                continue;
            }
            else
            {
                inputAssembly = args[i];
                break;// Handle any flags here
            }
        }

        if (inputAssembly == string.Empty)
        {
            Console.WriteLine("Please provide the path to the assembly to obfuscate.");
            return;
        }
        string outputAssembly = Path.Combine(Path.GetDirectoryName(inputAssembly)!,
            Path.GetFileNameWithoutExtension(inputAssembly) + ".Obfuscated" + Path.GetExtension(inputAssembly));

        Obfuscate(inputAssembly, outputAssembly);

        Console.WriteLine("Obfuscation completed. Obfuscated assembly saved as: " + outputAssembly);

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

        foreach (var module in assembly.Modules)
        {
            foreach (var type in module.Types)
            {
                // Rename types
                if (GlobalSettings.rename)
                    type.Name = "Obf_" + Guid.NewGuid().ToString();

                foreach (var method in type.Methods)
                {
                    // Rename method
                    if (GlobalSettings.rename)
                        method.Name = "Obf_" + Guid.NewGuid().ToString();

                    // Example of adding an unnecessary NOP instruction
                    if (GlobalSettings.extraInstructions)
                    {
                        var ilProcessor = method.Body.GetILProcessor();
                        var firstInstruction = method.Body.Instructions[0];
                        ilProcessor.InsertBefore(firstInstruction, ilProcessor.Create(OpCodes.Nop));
                    }

                    // Obfuscate strings
                    if (method.HasBody && GlobalSettings.obfuscateStrings)
                    {
                        var ilProcessor = method.Body.GetILProcessor();
                        for (int i = 0; i < method.Body.Instructions.Count; i++)
                        {
                            var instruction = method.Body.Instructions[i];
                            if (instruction.OpCode == OpCodes.Ldstr)
                            {
                                string originalString = (string)instruction.Operand;
                                string encodedString = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(originalString));

                                // Replace the string with the encoded version
                                instruction.Operand = encodedString;

                                // Insert a call to the decoding method
                                var decodeMethod = GetOrCreateDecodeMethod(module);
                                ilProcessor.InsertAfter(instruction, ilProcessor.Create(OpCodes.Call, decodeMethod));
                            }
                        }
                    }
                }
            }
        }

        // Save the modified assembly
        assembly.Write(outputAssemblyPath);
    }

    private static MethodDefinition GetOrCreateDecodeMethod(ModuleDefinition module)
    {
        // Check if the decode method already exists
        var existingMethod = module.Types
            .SelectMany(t => t.Methods)
            .FirstOrDefault(m => m.Name == "DecodeString");

        if (existingMethod != null)
            return existingMethod;

        // Create a new decode method
        var decodeMethod = new MethodDefinition(
            "DecodeString",
            MethodAttributes.Public | MethodAttributes.Static,
            module.ImportReference(typeof(string))
        );

        var stringType = module.ImportReference(typeof(string));
        var byteArrayType = module.ImportReference(typeof(byte[]));
        var encodingType = module.ImportReference(typeof(System.Text.Encoding));

        decodeMethod.Parameters.Add(new ParameterDefinition("encodedString", ParameterAttributes.None, stringType));

        var ilProcessor = decodeMethod.Body.GetILProcessor();
        ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
        ilProcessor.Append(ilProcessor.Create(OpCodes.Call, module.ImportReference(encodingType.Resolve().Methods.First(m => m.Name == "get_UTF8"))));
        ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0));
        ilProcessor.Append(ilProcessor.Create(OpCodes.Call, module.ImportReference(typeof(Convert).GetMethod("FromBase64String"))));
        ilProcessor.Append(ilProcessor.Create(OpCodes.Callvirt, module.ImportReference(encodingType.Resolve().Methods.First(m => m.Name == "GetString" && m.Parameters.Count == 1))));
        ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));

        // Add the method to the module
        module.Types.First().Methods.Add(decodeMethod);

        return decodeMethod;
    }
}
// See https://aka.ms/new-console-template for more information
// Console.WriteLine("Hello, World!");
