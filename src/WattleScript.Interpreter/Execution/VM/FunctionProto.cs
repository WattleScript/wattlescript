using System;
using System.Collections.Generic;
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
        /// <summary>
        /// Declared annotations on this function
        /// </summary>
        public IReadOnlyList<Annotation> Annotations => annotations;
        /// <summary>
        /// Name of this function
        /// </summary>
        public string Name => name;
        /// <summary>
        /// Functions declared in scope of this function
        /// </summary>
        public IReadOnlyList<FunctionProto> Functions => functions;
        
        // Function Data
        internal string name;
        internal FunctionFlags flags;
        internal SymbolRef[] locals;
        internal SymbolRef[] upvalues;
        internal Annotation[] annotations;
        internal int localCount;
        // Constants
        internal FunctionProto[] functions;
        internal string[] strings;
        internal double[] numbers;
        // Source Code
        internal Instruction[] code;
        internal SourceRef[] sourceRefs;
        
        public string GetSourceFragment(string fullSourceCode)
        {
            return sourceRefs.GetSourceFragment(fullSourceCode);
        }

        public IReadOnlyList<SourceRef> SourceRefs => sourceRefs;
    }
}