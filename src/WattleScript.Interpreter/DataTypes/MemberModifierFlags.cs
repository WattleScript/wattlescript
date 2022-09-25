using System;

namespace WattleScript.Interpreter
{
    [Flags]
    internal enum MemberModifierFlags
    {
        None = 0,
        Static = 1
    }
}