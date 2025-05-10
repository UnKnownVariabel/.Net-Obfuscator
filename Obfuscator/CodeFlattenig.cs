using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

public static class CodeFlattening
{
    public static void FlattenMethod(MethodDefinition method, bool shuffle = true)
    {
        // Skip methods that are unsuitable for flattening
        if (method.IsConstructor || !method.HasBody || method.Body.Instructions.Count <= 1)
            return;

        // Skip methods with exception handlers for now - more complex to handle
        if (method.Body.HasExceptionHandlers)
            return;

        var body = method.Body;
        var il = body.GetILProcessor();
        body.SimplifyMacros();

        List<Instruction> instructions = body.Instructions.ToList();
        List<List<Instruction>> blocks = CreateBalancedStackBlocks(instructions, method);

        List<Instruction> newInstructions = new List<Instruction>();
        VariableDefinition stateVar = new VariableDefinition(method.Module.TypeSystem.Int32);
        body.Variables.Add(stateVar);
        method.Body.InitLocals = true;

        // Create instruction mapping for branch targets
        var oldToNewInstructions = new Dictionary<Instruction, Instruction>();

        //shuffel
        Random rand = new Random();
        IEnumerable<int> values = Enumerable.Range(0, blocks.Count);
        values = shuffle ? values.OrderBy(_ => rand.Next()) : values;
        Dictionary<int, int> order = values.Select((val, index) => new { index, val }).ToDictionary(x => x.index, x => x.val);


        var stateBlocks = new Dictionary<int, Instruction>();

        // Create state jump table
        for(int i = 0; i < order.Count; i++) {
            Instruction jumpTarget = Instruction.Create(OpCodes.Nop);
            stateBlocks[i] = jumpTarget;
        }


        var loadState = Instruction.Create(OpCodes.Ldloc, stateVar);
        var switchInstr = Instruction.Create(OpCodes.Switch, stateBlocks.Values.ToArray());
        int startPos = order.FirstOrDefault(pair => pair.Value == 0).Key;
        newInstructions.Add(il.Create(OpCodes.Ldc_I4, startPos));
        newInstructions.Add(il.Create(OpCodes.Stloc, stateVar));
        newInstructions.Add(il.Create(OpCodes.Br, loadState));
        
        // Add the switch instruction to the newInstructions list
        newInstructions.Add(loadState);
        newInstructions.Add(switchInstr);
        
        // Default case for switch - just return

        newInstructions.Add(il.Create(OpCodes.Br, loadState));

        for (int i = 0; i < order.Count; i++)
        {
            var block = blocks[order[i]];
            var blockState = stateBlocks[i];
            newInstructions.Add(blockState);
            foreach (var instr in block)
            {
                if (instr.OpCode.OperandType == OperandType.InlineBrTarget || 
                    instr.OpCode.OperandType == OperandType.ShortInlineBrTarget)
                {
                    // For branch instructions, set state and branch to dispatcher instead
                    if (instr.Operand is Instruction branchTarget)
                    {
                        
                        // this is most wrong but fix later
                        int targetIndex = GetBlockState(branchTarget, blocks);
                        
                        
                        // Add instructions to set state and jump to switch
                        Instruction setToTarget = Instruction.Create(OpCodes.Ldc_I4, order.FirstOrDefault(pair => pair.Value == targetIndex).Key);
                        instr.Operand = setToTarget;
                        newInstructions.Add(instr);
                        newInstructions.Add(il.Create(OpCodes.Ldc_I4, order.FirstOrDefault(pair => pair.Value == order[i]+1).Key ));
                        newInstructions.Add(il.Create(OpCodes.Stloc, stateVar));
                        newInstructions.Add(il.Create(OpCodes.Br, loadState));

                        newInstructions.Add(setToTarget);
                        newInstructions.Add(il.Create(OpCodes.Stloc, stateVar));
                        newInstructions.Add(il.Create(OpCodes.Br, loadState));
                        continue;
                    }
                }
                else if (instr.OpCode == OpCodes.Ret)
                {
                    // Preserve original return instruction
                    newInstructions.Add(instr);
                    continue;
                }
                else
                {
                    // For regular instructions, add them as is
                    newInstructions.Add(instr);
                }

            }
            // Add a branch to the next state
            newInstructions.Add(il.Create(OpCodes.Ldc_I4, order.FirstOrDefault(pair => pair.Value == order[i]+1).Key));
            newInstructions.Add(il.Create(OpCodes.Stloc, stateVar));
            newInstructions.Add(il.Create(OpCodes.Br, loadState));

        }

        // Replace old instructions
        body.Instructions.Clear();
        foreach (var instr in newInstructions)
            body.Instructions.Add(instr);

        body.OptimizeMacros();
    }

    private static int GetBlockState(Instruction target, List<List<Instruction>> blocks)
    {
        for (int i = 0; i < blocks.Count; i++)
        {
            if (blocks[i].Contains(target))
            {
                return i;
            }
        }
        throw new InvalidOperationException("Target instruction not found in any block.");
    }
    private static List<List<Instruction>> CreateBalancedStackBlocks(
        IList<Instruction> instructions,
        MethodDefinition containingMethod)
    {
        var allBlocks = new List<List<Instruction>>();
        if (instructions == null || !instructions.Any())
        {
            return allBlocks;
        }

        var currentBlock = new List<Instruction>();
        int currentStackDelta = 0;

        foreach (var instr in instructions)
        {
            currentBlock.Add(instr);

            int pushes = GetPushCount(instr);
            int pops = GetPopCount(instr, containingMethod);
            currentStackDelta += pushes;
            currentStackDelta -= pops;

            if (currentStackDelta == 0 && currentBlock.Any())
            {
                allBlocks.Add(new List<Instruction>(currentBlock)); // Add a copy of the current block
                currentBlock.Clear();
                // currentStackDelta is already 0, reset for the next block
            }
        }

        // If the loop finishes and currentBlock is not empty,
        // it means the remaining instructions form an unbalanced block (or the whole sequence was unbalanced).
        // According to the requirement "each block should have a balanced stack",
        // we don't add this potentially unbalanced trailing block.
        // If the very last instruction completed a balanced block, it would have been added inside the loop.

        return allBlocks;
    }

    private static int GetPushCount(Instruction instruction)
    {
        var opCode = instruction.OpCode;
        switch (opCode.StackBehaviourPush)
        {
            case StackBehaviour.Push0: return 0;
            case StackBehaviour.Push1:
            case StackBehaviour.Pushi:
            case StackBehaviour.Pushi8:
            case StackBehaviour.Pushr4:
            case StackBehaviour.Pushr8:
            case StackBehaviour.Pushref:
                return 1;
            case StackBehaviour.Push1_push1:
                return 2;
            case StackBehaviour.Varpush: // For call, callvirt, newobj, calli
                if (instruction.Operand is MethodReference methodRef)
                {
                    if (opCode.Code == Code.Newobj) return 1; // Newobj always pushes the new object reference
                    // For call/callvirt
                    return methodRef.MethodReturnType.ReturnType.FullName == "System.Void" ? 0 : 1;
                }
                else if (instruction.Operand is CallSite callSiteRef) // For calli
                {
                    return callSiteRef.ReturnType.FullName == "System.Void" ? 0 : 1;
                }
                // This indicates an unexpected Varpush scenario
                System.Diagnostics.Debug.WriteLine($"Warning: Varpush encountered with unhandled operand type for OpCode {opCode.Name}: {instruction.Operand?.GetType().FullName}");
                return 0;
            default:
                System.Diagnostics.Debug.WriteLine($"Warning: Unexpected StackBehaviourPush: {opCode.StackBehaviourPush} for OpCode {opCode.Name}");
                return 0; // Or throw an exception for unhandled cases
        }
    }

    private static int GetPopCount(Instruction instruction, MethodDefinition containingMethod)
    {
        var opCode = instruction.OpCode;
        switch (opCode.StackBehaviourPop)
        {
            case StackBehaviour.Pop0: return 0;
            case StackBehaviour.Pop1:
            case StackBehaviour.Popi:
            case StackBehaviour.Popref:
                return 1;
            case StackBehaviour.Pop1_pop1:
            case StackBehaviour.Popi_pop1:
            case StackBehaviour.Popi_popi:
            case StackBehaviour.Popi_popi8:
            case StackBehaviour.Popi_popr4:
            case StackBehaviour.Popi_popr8:
            case StackBehaviour.Popref_pop1:
            case StackBehaviour.Popref_popi:
                return 2;
            case StackBehaviour.Popref_popi_popi:
            case StackBehaviour.Popref_popi_popi8:
            case StackBehaviour.Popref_popi_popr4:
            case StackBehaviour.Popref_popi_popr8:
            case StackBehaviour.Popref_popi_popref:
                return 3;
            case StackBehaviour.Varpop: // For ret, call, callvirt, newobj, calli
                if (opCode.Code == Code.Ret)
                {
                    return containingMethod.MethodReturnType.ReturnType.FullName == "System.Void" ? 0 : 1;
                }
                if (instruction.Operand is MethodReference methodRef)
                {
                    int pops = methodRef.Parameters.Count;
                    if (opCode.Code != Code.Newobj) // For call, callvirt
                    {
                        if (methodRef.HasThis) // Instance method call also pops 'this'
                        {
                            pops++;
                        }
                    }
                    // For Newobj, only arguments are popped from the evaluation stack.
                    return pops;
                }
                else if (instruction.Operand is CallSite callSiteRef) // For calli
                {
                    int pops = callSiteRef.Parameters.Count;
                    if (callSiteRef.HasThis)
                    {
                        pops++;
                    }
                    pops++; // For the function pointer itself
                    return pops;
                }
                // This indicates an unexpected Varpop scenario
                System.Diagnostics.Debug.WriteLine($"Warning: Varpop encountered with unhandled operand type for OpCode {opCode.Name}: {instruction.Operand?.GetType().FullName}");
                return 0;
            default:
                System.Diagnostics.Debug.WriteLine($"Warning: Unexpected StackBehaviourPop: {opCode.StackBehaviourPop} for OpCode {opCode.Name}");
                return 0; // Or throw an exception for unhandled cases
        }
    }
    private static Instruction CloneInstruction(ILProcessor il, Instruction oldInstr)
    {
        if (oldInstr == null)
            throw new ArgumentNullException(nameof(oldInstr));

        var opcode = oldInstr.OpCode;
        var operand = oldInstr.Operand;

        return operand switch
        {
            null                     => il.Create(opcode),
            Instruction t            => il.Create(opcode, t ?? null),       // Handle null target
            Instruction[] ts         => il.Create(opcode, ts ?? null),      // Handle null array
            VariableDefinition v     => il.Create(opcode, v),
            ParameterDefinition p    => il.Create(opcode, p),
            MethodReference m        => il.Create(opcode, m),
            FieldReference f         => il.Create(opcode, f),
            TypeReference t          => il.Create(opcode, t),
            CallSite cs              => il.Create(opcode, cs),
            string s                 => il.Create(opcode, s),
            sbyte sb                 => il.Create(opcode, sb),
            byte b                   => il.Create(opcode, b),
            int i                    => il.Create(opcode, i),
            long l                   => il.Create(opcode, l),
            float f32                => il.Create(opcode, f32),
            double f64               => il.Create(opcode, f64),
            _ => throw new NotSupportedException($"Unsupported operand type: {operand?.GetType()}")
        };
    }
}
