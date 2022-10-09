using System.Collections.Generic;
using System.Linq;

namespace WattleScript.Interpreter
{
    public sealed class WattleShape
    {
        internal readonly Dictionary<string, MemberModifierFlags> Members = new Dictionary<string, MemberModifierFlags>();

        public bool IsKeyPrivate(DynValue key)
        {
            if (key.Type != DataType.String) return false;
            if (Members.TryGetValue(key.String, out MemberModifierFlags flags))
            {
                return flags.HasFlag(MemberModifierFlags.Private);
            }

            return false;
        }

        internal void Merge(WattleShape parent)
        {
            parent.Members.ToList().ForEach(x => Members[x.Key] = x.Value);
        }

        internal WattleShape() { }
    }
}