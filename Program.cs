using Mono.Cecil;
using Mono.Cecil.Cil;

public static class GlobalSettings
{
    public static bool rename = false;
    public static bool extraInstructions = false;
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
                if( GlobalSettings.rename)
                    type.Name = "Obf_" + Guid.NewGuid().ToString();

                foreach (var method in type.Methods)
                {
                    // Rename method
                    if (GlobalSettings.rename)
                        method.Name = "Obf_" + Guid.NewGuid().ToString();

                    // Example of adding an unnecessary NOP instruction
                    if (GlobalSettings.extraInstructions) {
                        var ilProcessor = method.Body.GetILProcessor();
                        var firstInstruction = method.Body.Instructions[0];
                        ilProcessor.InsertBefore(firstInstruction, ilProcessor.Create(OpCodes.Nop));
                    }
                }
            }
        }

        // Save the modified assembly
        assembly.Write(outputAssemblyPath);
    }
}
// See https://aka.ms/new-console-template for more information
// Console.WriteLine("Hello, World!");
