using System;
using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.Execution.VM
{
    [Flags]
    internal enum FunctionFlags
    {
        None = 0x0,
        IsChunk = 0x1,
        TakesSelf = 0x2,
        ImplicitThis = 0x4
    }
    
    public class FunctionProto
    {
        //Function Data
        public string Name;
        internal FunctionFlags Flags;
        public SymbolRef[] Locals;
        public SymbolRef[] Upvalues;
        public Annotation[] Annotations;
        public int LocalCount;
        //Constants
        public FunctionProto[] Functions;
        public string[] Strings;
        public double[] Numbers;
        //Source Code
        internal Instruction[] Code;
        public SourceRef[] SourceRefs;
    }
}