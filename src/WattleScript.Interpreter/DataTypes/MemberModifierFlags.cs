using System;
using System.ComponentModel;

namespace WattleScript.Interpreter
{
    [Flags]
    public enum MemberModifierFlags
    {
        None = 0,
        [Description("static")]
        Static = 1
    }
}