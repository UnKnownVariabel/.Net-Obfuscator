
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Obfuscator.Anti
{
   public static class AntiDebugging
   {
        private static Random _random = new Random();
        private static MethodReference? _exitMethod;
        private static MethodReference? _isDebuggerAttachedMethod;

        public static void AntiDebugModule(ModuleDefinition module)
        {
            _exitMethod = module.ImportReference(typeof(Environment).GetMethod("Exit", new[] { typeof(int) }));
            _isDebuggerAttachedMethod = module.ImportReference(
                typeof(System.Diagnostics.Debugger).GetProperty("IsAttached")!.GetGetMethod());

            foreach (var type in module.Types)
            {
                foreach (var method in type.Methods)
                {
                    AddAntiDebug(method);
                }
            }
        }

        private static void AddAntiDebug(MethodDefinition method)
        {
            var ilProcessor = method.Body.GetILProcessor();
            var instructions = method.Body.Instructions;
            var instruction = instructions[0];

            var skipLabel = ilProcessor.Create(OpCodes.Nop);
        
            // ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Call, isDebuggerPresent));
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Call, _isDebuggerAttachedMethod)); // Check if debugger is attached
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Brfalse_S, skipLabel));  // Skip if no debugger
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Ldc_I4_0)); // Argument for Exit (exit code)
            ilProcessor.InsertBefore(instruction, Instruction.Create(OpCodes.Call, _exitMethod));  // Call Environment.Exit(0)
            ilProcessor.InsertBefore(instruction, skipLabel);
        }
   } 
}