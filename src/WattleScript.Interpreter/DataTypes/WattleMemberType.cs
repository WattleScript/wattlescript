using System;

namespace WattleScript.Interpreter
{
    [Flags]
    internal enum WattleMemberType
    {
        Enum = 0,
        Class = 1 << 0,
        Mixin = 1 << 1,
        ClassMember = 1 << 2,
        MixinMember = 1 << 3,
        EnumMember = 1 << 4,
        Any = Enum | Class | Mixin | ClassMember | MixinMember | EnumMember
    }
}