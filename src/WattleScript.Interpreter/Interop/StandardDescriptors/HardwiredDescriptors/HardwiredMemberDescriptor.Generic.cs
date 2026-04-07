using System;
using WattleScript.Interpreter.Interop.BasicDescriptors;
using WattleScript.Interpreter.Interop.Converters;

namespace WattleScript.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors
{
    public abstract class HardwiredMemberDescriptor<T> : IMemberDescriptor
    {
        protected HardwiredMemberDescriptor(string name, bool isStatic, MemberDescriptorAccess access)
        {
            IsStatic = isStatic;
            Name = name;
            MemberAccess = access;
        }

        public bool IsStatic { get; private set; }

        public string Name { get; private set; }

        public MemberDescriptorAccess MemberAccess { get; private set; }
        
        public DynValue GetValue(Script script, object obj)
        {
            this.CheckAccess(MemberDescriptorAccess.CanRead, obj);
            T result = GetValueImpl(script, obj);
            return ClrToScriptConversions.ObjectToDynValue(script, result);
        }

        public void SetValue(Script script, object obj, DynValue value)
        {
            this.CheckAccess(MemberDescriptorAccess.CanWrite, obj);
            T v = ScriptToClrConversions.DynValueToObject<T>(value, default, false);
            SetValueImpl(script, obj, v);
        }
        
        protected virtual T GetValueImpl(Script script, object obj)
        {
            throw new InvalidOperationException("GetValue on write-only hardwired descriptor " + Name);
        }

        protected virtual void SetValueImpl(Script script, object obj, T value)
        {
            throw new InvalidOperationException("SetValue on read-only hardwired descriptor " + Name);
        }
    }
}