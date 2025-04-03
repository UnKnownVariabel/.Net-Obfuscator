using Mono.Cecil;
using Mono.Cecil.Cil;

class Obfuscator
{
    static void Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: sudo dotnet run <inputAssembly>");
            return;
        }

        string inputAssembly = args[0];
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
                type.Name = "Obf_" + Guid.NewGuid().ToString();

                foreach (var method in type.Methods)
                {
                    // Rename method
                    method.Name = "Obf_" + Guid.NewGuid().ToString();

                    // Example of adding an unnecessary NOP instruction
                    var ilProcessor = method.Body.GetILProcessor();
                    var firstInstruction = method.Body.Instructions[0];
                    ilProcessor.InsertBefore(firstInstruction, ilProcessor.Create(OpCodes.Nop));
                }
            }
        }

        // Save the modified assembly
        assembly.Write(outputAssemblyPath);
    }
}
// See https://aka.ms/new-console-template for more information
// Console.WriteLine("Hello, World!");
