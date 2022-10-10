using System.Collections.Generic;
using System.Linq;

namespace WattleScript.Interpreter
{
    public sealed class WattleMembersInfo
    {
        public IReadOnlyDictionary<string, MemberModifierFlags> Modifiers => i_Modifiers;

        internal readonly Dictionary<string, MemberModifierFlags> i_Modifiers = new Dictionary<string, MemberModifierFlags>();

        public bool MemberHasModifier(string memberName, MemberModifierFlags modifier)
        {
            return MemberHasModifier(DynValue.NewString(memberName), modifier);
        }
        
        public bool MemberHasModifier(DynValue memberName, MemberModifierFlags modifier)
        {
            if (memberName.Type != DataType.String) return false;
            return i_Modifiers.TryGetValue(memberName.String, out MemberModifierFlags flags) && flags.HasFlag(modifier);
        }

        internal void Merge(WattleMembersInfo parent)
        {
            parent.i_Modifiers.ToList().ForEach(x => i_Modifiers[x.Key] = x.Value);
        }

        internal WattleMembersInfo() { }
    }
}