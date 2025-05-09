using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.Linq;

public static class CodeFlattening
{
    public static void FlattenMethod(MethodDefinition method)
    {
        if (method.IsConstructor)
            return;

        var body = method.Body;
        var il = body.GetILProcessor();

        // Split into basic blocks
        var basicBlocks = SplitIntoBasicBlocks(body.Instructions);
        // If there's only one block, flattening is unnecessary and can confuse decompilers
        Console.WriteLine("Basic blocks: {0}", basicBlocks.Count);
        foreach (var block in basicBlocks)
        {
            Console.WriteLine("Block: {0}", string.Join(", ", block.Select(i => i.ToString())));
        }
        if (basicBlocks.Count <= 1)
            return;

        // Map each block index to a label and each instruction to its block label
        var blockLabels = new Dictionary<int, Instruction>();
        var instrToLabel = new Dictionary<Instruction, Instruction>();
        for (int i = 0; i < basicBlocks.Count; i++)
        {
            var lbl = Instruction.Create(OpCodes.Nop);
            blockLabels[i] = lbl;
            foreach (var instr in basicBlocks[i])
                instrToLabel[instr] = lbl;
        }

        // Create state variable
        var stateVar = new VariableDefinition(method.Module.TypeSystem.Int32);
        body.Variables.Add(stateVar);
        body.InitLocals = true;

        // Build switch dispatcher
        var switchEntry = Instruction.Create(OpCodes.Ldloc, stateVar);
        var switchInstr = Instruction.Create(OpCodes.Switch,
            blockLabels.OrderBy(kv => kv.Key).Select(kv => kv.Value).ToArray());

        // Emit new instruction sequence
        var newInst = new List<Instruction>();
        // Bootstrap
        newInst.Add(il.Create(OpCodes.Ldc_I4_0));
        newInst.Add(il.Create(OpCodes.Stloc, stateVar));
        newInst.Add(il.Create(OpCodes.Br, switchEntry));
        // Dispatcher
        newInst.Add(switchEntry);
        newInst.Add(switchInstr);

        // Emit each block
        for (int i = 0; i < basicBlocks.Count; i++)
        {
            var block = basicBlocks[i];
            newInst.Add(blockLabels[i]);
            foreach (var orig in block)
                newInst.Add(CloneInstruction(orig));

            var last = block.Last();
            if (last.OpCode != OpCodes.Ret && last.OpCode != OpCodes.Throw)
            {
                // Transition to next block
                newInst.Add(il.Create(OpCodes.Ldc_I4, i + 1));
                newInst.Add(il.Create(OpCodes.Stloc, stateVar));
                newInst.Add(il.Create(OpCodes.Br, switchEntry));
            }
        }

        // Replace method body
        body.Instructions.Clear();
        foreach (var instr in newInst)
            body.Instructions.Add(instr);

        // Remap exception handlers
        if (body.HasExceptionHandlers)
        {
            foreach (var handler in body.ExceptionHandlers)
            {
                handler.TryStart = GetLabel(handler.TryStart);
                handler.TryEnd = GetLabel(handler.TryEnd);
                handler.HandlerStart = GetLabel(handler.HandlerStart);
                handler.HandlerEnd = GetLabel(handler.HandlerEnd);
                if (handler.FilterStart != null)
                    handler.FilterStart = GetLabel(handler.FilterStart);
            }
        }


        // Local function to map any instruction to its block label or return original
        Instruction GetLabel(Instruction old)
            => instrToLabel.TryGetValue(old, out var lbl) ? lbl : old;
}

    private static List<List<Instruction>> SplitIntoBasicBlocks(Mono.Collections.Generic.Collection<Instruction> instructions)
    {
        var leaders = new HashSet<Instruction>();
        // First instruction is a leader
        if (instructions.Count > 0)
            leaders.Add(instructions[0]);
        // Identify leaders
        for (int i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (instr.Operand is Instruction target)
                leaders.Add(target);
            else if (instr.Operand is Instruction[] targets)
                foreach (var t in targets)
                    leaders.Add(t);

            var fc = instr.OpCode.FlowControl;
            if (fc == FlowControl.Branch || fc == FlowControl.Cond_Branch ||
                fc == FlowControl.Return || fc == FlowControl.Throw)
            {
                if (i + 1 < instructions.Count)
                    leaders.Add(instructions[i + 1]);
            }
        }
        
        // Build blocks
        var blocks = new List<List<Instruction>>();
        List<Instruction> current = null;
        foreach (var instr in instructions)
        {
            if (leaders.Contains(instr))
            {
                if (current != null && current.Count > 0)
                    blocks.Add(current);
                current = new List<Instruction>();
            }
            current.Add(instr);
        }
        if (current != null && current.Count > 0)
            blocks.Add(current);

        return blocks;
    }

        private static Instruction CloneInstruction(Instruction instr)
    {
        if (instr.Operand == null)
            return Instruction.Create(instr.OpCode);

        return instr.Operand switch
        {
            string s => Instruction.Create(instr.OpCode, s),
            int i => Instruction.Create(instr.OpCode, i),
            long l => Instruction.Create(instr.OpCode, l),
            float f => Instruction.Create(instr.OpCode, f),
            double d => Instruction.Create(instr.OpCode, d),
            byte b => Instruction.Create(instr.OpCode, b),
            sbyte sb => Instruction.Create(instr.OpCode, sb),
            FieldReference fref => Instruction.Create(instr.OpCode, fref),
            MethodReference mref => Instruction.Create(instr.OpCode, mref),
            TypeReference tref => Instruction.Create(instr.OpCode, tref),
            ParameterDefinition pref => Instruction.Create(instr.OpCode, pref),
            VariableDefinition vdef => Instruction.Create(instr.OpCode, vdef),
            Instruction target => Instruction.Create(instr.OpCode, target),
            Instruction[] targets => Instruction.Create(instr.OpCode, targets),
            CallSite cs => Instruction.Create(instr.OpCode, cs),
            _ => throw new NotSupportedException($"Operand type {instr.Operand.GetType()} not supported")
        };
    }
}
