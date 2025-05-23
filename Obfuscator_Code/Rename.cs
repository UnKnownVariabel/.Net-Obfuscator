using Mono.Cecil;


public static class Rename
{
    public static void RenameModule(ModuleDefinition module)
    {
        // Generate a random name for the module
        foreach (var type in module.Types)
        {
            type.Name = "Obf_" + Guid.NewGuid().ToString();
            foreach (var method in type.Methods)
            {
                // Generate a random name for the method
                if (!method.IsConstructor && !method.IsGetter && !method.IsSetter)
                {
                    method.Name = "Obf_" + Guid.NewGuid().ToString();
                }
            }
        }
    }
}