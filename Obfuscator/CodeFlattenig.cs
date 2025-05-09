using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography; // Add this for shuffling

public static class CodeFlattening
{
    public static void FlattenMethod(MethodDefinition method)
    {
        if (method.IsConstructor)
            return;

        var body = method.Body;
        var il = body.GetILProcessor();

        var instructions = body.Instructions.ToList();

        // Create the dispatcher variable
        var stateVar = new VariableDefinition(method.Module.TypeSystem.Int32);
        body.Variables.Add(stateVar);
        body.InitLocals = true;

        var stateLabels = new Dictionary<int, Instruction>();
        var newInstructions = new List<Instruction>();

        int state = 0;

        // Build labels for each instruction
        foreach (var instr in instructions)
        {
            stateLabels[state++] = Instruction.Create(OpCodes.Nop);
        }

        // Shuffle the state labels to randomize their order
        var shuffledStateLabels = stateLabels.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToList();

        var switchInstr = Instruction.Create(OpCodes.Switch, stateLabels.Values.ToArray());
        var endInstr = Instruction.Create(OpCodes.Nop);

        // Bootstrap: set state = 0 and jump to switch
        newInstructions.Add(il.Create(OpCodes.Ldc_I4_0));
        newInstructions.Add(il.Create(OpCodes.Stloc, stateVar));
        newInstructions.Add(il.Create(OpCodes.Br, switchInstr));

        // Add dispatcher block
        var switchBlock = Instruction.Create(OpCodes.Nop);
        newInstructions.Add(switchBlock);
        newInstructions.Add(il.Create(OpCodes.Ldloc, stateVar));
        newInstructions.Add(switchInstr);

        // Add all flattened code blocks in shuffled order
        foreach (var kvp in shuffledStateLabels)
        {
            var stateIndex = kvp.Key;
            var label = kvp.Value;
            var original = instructions[stateIndex];

            newInstructions.Add(label);
            newInstructions.Add(original);

            if (stateIndex < instructions.Count - 1)
            {
                newInstructions.Add(il.Create(OpCodes.Ldc_I4, stateIndex + 1));
                newInstructions.Add(il.Create(OpCodes.Stloc, stateVar));
                newInstructions.Add(il.Create(OpCodes.Br, switchBlock));
            }
            else
            {
                newInstructions.Add(il.Create(OpCodes.Br, endInstr));
            }
        }
        newInstructions.Add(endInstr);

        // Replace old instructions
        body.Instructions.Clear();
        foreach (var instr in newInstructions)
            body.Instructions.Add(instr);
    }
}
