using System;

namespace WattleScript.Interpreter
{
    [Flags]
    public enum MemberModifierFlags
    {
        None = 0,
        Static = 1 << 0,
        Private = 1 << 1,
        Public = 1 << 2,
        Sealed = 1 << 3
    }
}