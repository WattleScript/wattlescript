using System.Collections;
using System.Collections.Generic;

namespace WattleScript.Interpreter.Tree
{
    internal class MemberCollection : IEnumerable<WattleMemberInfo>
    {
        private List<WattleMemberInfo> items = new List<WattleMemberInfo>();
        private HashSet<string> names = new HashSet<string>();
        
        public IEnumerator<WattleMemberInfo> GetEnumerator()
        {
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) items).GetEnumerator();
        }

        public void Add(Token nameToken, Expression expression, MemberModifierFlags modifiers, bool isFunction)
        {
            if (modifiers.HasFlag(MemberModifierFlags.Private) &&
                modifiers.HasFlag(MemberModifierFlags.Static))
                throw new SyntaxErrorException(nameToken, "members declared static may not be private");
            
            if (names.Contains(nameToken.Text))
            {
                throw new SyntaxErrorException(
                    nameToken, 
                    "duplicate declaration of a {0} '{1}'", 
                    isFunction ? "function" : "field",
                    nameToken.Text
                    );
            }
            names.Add(nameToken.Text);
            items.Add(new WattleMemberInfo(nameToken.Text, expression, modifiers));
        }

        public int Count => items.Count;


        public WattleMemberInfo this[int index]
        {
            get => items[index];
            set => items[index] = value;
        }
    }
}