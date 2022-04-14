using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.Execution.VM
{
    class FunctionProto
    {
        //Function Data
        public string Name;
        public bool IsChunk;
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