using System.Collections.Generic;
using System.Linq;

namespace WattleScript.Interpreter
{
    public sealed class WattleShape
    {
        public IReadOnlyDictionary<string, MemberModifierFlags> Members => i_Members;

        internal readonly Dictionary<string, MemberModifierFlags> i_Members = new Dictionary<string, MemberModifierFlags>();

        public bool MemberHasModifier(string memberName, MemberModifierFlags modifier)
        {
            return MemberHasModifier(DynValue.NewString(memberName), modifier);
        }
        
        public bool MemberHasModifier(DynValue memberName, MemberModifierFlags modifier)
        {
            if (memberName.Type != DataType.String) return false;
            return i_Members.TryGetValue(memberName.String, out MemberModifierFlags flags) && flags.HasFlag(modifier);
        }

        internal void Merge(WattleShape parent)
        {
            parent.i_Members.ToList().ForEach(x => i_Members[x.Key] = x.Value);
        }

        internal WattleShape() { }
    }
}