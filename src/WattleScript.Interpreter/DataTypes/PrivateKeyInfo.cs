using System.Collections.Generic;
using System.Linq;

namespace WattleScript.Interpreter
{
    public sealed class WattleFieldsInfo
    {
        internal Dictionary<string, MemberModifierFlags> Fields = new Dictionary<string, MemberModifierFlags>();

        public bool IsKeyPrivate(DynValue key)
        {
            if (key.Type != DataType.String) return false;
            if (Fields.TryGetValue(key.String, out MemberModifierFlags flags))
            {
                return flags.HasFlag(MemberModifierFlags.Private);
            }

            return false;
        }

        internal void Merge(WattleFieldsInfo parent)
        {
            parent.Fields.ToList().ForEach(x => Fields[x.Key] = x.Value);
        }

        internal WattleFieldsInfo() { }
    }
}