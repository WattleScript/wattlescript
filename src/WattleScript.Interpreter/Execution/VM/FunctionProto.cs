using System;
using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.Execution.VM
{
    [Flags]
    enum FunctionFlags
    {
        None = 0x0,
        IsChunk = 0x1,
        TakesSelf = 0x2,
        ImplicitThis = 0x4
    }
    class FunctionProto
    {
        //Function Data
        public string Name;
        public FunctionFlags Flags;
        public SymbolRef[] Locals;
        public SymbolRef[] Upvalues;
        public Annotation[] Annotations;
        public int LocalCount;
        //Constants
        public FunctionProto[] Functions;
        public string[] Strings;
        public double[] Numbers;
        //Source Code
        public Instruction[] Code;
        public SourceRef[] SourceRefs;
    }
}