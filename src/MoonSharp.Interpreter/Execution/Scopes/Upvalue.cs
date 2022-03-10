using System.Threading;

namespace MoonSharp.Interpreter.Execution
{
    /// <summary>
    /// Holds a reference to a local in another function
    /// </summary>
    internal class Upvalue
    {
        public DynValue[] ParentScope;
        public int Index;

        private static int s = 1;
        private int _refID;
        public int ReferenceID { get => _refID; }
        
        public Upvalue(DynValue[] parentScope, int index)
        {
            ParentScope = parentScope;
            Index = index;
            _refID = Interlocked.Increment(ref s);
        }

        public ref DynValue Value() => ref ParentScope[Index];

        public static Upvalue NewNil() => new Upvalue(new DynValue[1], 0);
        public static Upvalue Create(DynValue obj) => new Upvalue(new DynValue[] {obj}, 0);
    }
}