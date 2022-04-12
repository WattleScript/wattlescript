using System.Threading;
using WattleScript.Interpreter.DataStructs;

namespace WattleScript.Interpreter.Execution
{
    /// <summary>
    /// Holds a reference to a local in another function
    /// </summary>
    internal class Upvalue
    {
        public FastStack<DynValue> ParentScope;
        public int Index;
        private DynValue storage;
        
        private static int s = 1;
        private int _refID;
        public int ReferenceID { get => _refID; }
        
        public Upvalue(FastStack<DynValue> parentScope, int index)
        {
            ParentScope = parentScope;
            Index = index;
            _refID = Interlocked.Increment(ref s);
        }

        protected Upvalue()
        {
        }

        public void Close()
        {
            storage = ParentScope[Index];
            ParentScope = null;
        }

        public ref DynValue Value()
        {
            if(ParentScope != null)
                return ref ParentScope[Index];
            return ref storage;
        }

        public static Upvalue NewNil() => new Upvalue();
        public static Upvalue Create(DynValue obj) => new Upvalue() {storage = obj};
    }
}