using System.Collections.Generic;

namespace WattleScript.Interpreter
{
    public sealed class PrivateKeyInfo
    {
        internal HashSet<string> Fields = new HashSet<string>();

        public bool IsKeyPrivate(DynValue key)
        {
            if (key.Type != DataType.String) return false;
            return Fields.Contains(key.String);
        }

        internal void Merge(PrivateKeyInfo parent)
        {
            Fields.UnionWith(parent.Fields);
        }

        internal PrivateKeyInfo() { }
    }
}