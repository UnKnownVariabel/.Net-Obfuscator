using Mono.Cecil;
using Mono.Cecil.Cil;
public static class ObfuscateStrings
{
    private static MethodDefinition ?_decodeMethod;
    public static void ObfuscateStringModule(ModuleDefinition module)
    {
        _decodeMethod = GetOrCreateDecodeMethod(module);
        foreach (var type in module.Types)
        {
            foreach (var method in type.Methods)
            {
                ObfuscateStringMethod(method);
            }
        }
    }
    private static void ObfuscateStringMethod(MethodDefinition method)
    {
        if (method.HasBody)
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
                    var decodeMethod = _decodeMethod ?? GetOrCreateDecodeMethod(method.Module); 
                    ilProcessor.InsertAfter(instruction, ilProcessor.Create(OpCodes.Call, decodeMethod));
                }
            }
        }
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
            
            ilProcessor.Append(ilProcessor.Create(OpCodes.Call, module.ImportReference(
                typeof(System.Text.Encoding).GetProperty("UTF8")!.GetGetMethod()
            ))); // returns Encoding
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ldarg_0)); // load encoded base64 string
            ilProcessor.Append(ilProcessor.Create(OpCodes.Call, module.ImportReference(
                typeof(Convert).GetMethod("FromBase64String", new[] { typeof(string) })
            ))); // returns byte[]
            ilProcessor.Append(ilProcessor.Create(OpCodes.Callvirt, module.ImportReference(
                typeof(System.Text.Encoding).GetMethod("GetString", new[] { typeof(byte[]) })
            ))); // returns string
            ilProcessor.Append(ilProcessor.Create(OpCodes.Ret));


            // Add the method to the module
            module.Types.First().Methods.Add(decodeMethod);

            return decodeMethod;
        }
}