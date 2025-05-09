using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Obfuscator.Anti
{
   public static class AntiDebugging
   {
        private static Random _random = new Random();

        public static void AddAntiDebug(Mono.Cecil.MethodDefinition method)
        {
            var ilProcessor = method.Body.GetILProcessor();
            var instructions = method.Body.Instructions;
            var instruction = instructions[_random.Next(instructions.Count)];

            var isDebuggerPresent = new MethodDefinition(
            "IsDebuggerPresent",
            MethodAttributes.Static | MethodAttributes.PInvokeImpl,
            method.Module.TypeSystem.Boolean)
            {
                HasThis = false,
                CallingConvention = MethodCallingConvention.StdCall,
                PInvokeInfo = new PInvokeInfo(PInvokeAttributes.CharSetAnsi, "IsDebuggerPresent", new ModuleReference("kernel32.dll"))
            };

            method.Module.Types[0].Methods.Add(isDebuggerPresent);
            method.Module.ImportReference(isDebuggerPresent);

            var exitMethod = method.Module.ImportReference(typeof(Environment).GetMethod("Exit", new[] { typeof(int) }));
            var skipLabel = ilProcessor.Create(OpCodes.Nop);
        
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Call, isDebuggerPresent));
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Brfalse_S, skipLabel));  // Skip if no debugger
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Ldc_I4_0)); // Argument for Exit (exit code)
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Call, exitMethod));  // Call Environment.Exit(0)
            ilProcessor.InsertBefore(instruction, skipLabel);
        }
   } 
}