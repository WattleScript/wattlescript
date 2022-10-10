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
            items.Add(new WattleMemberInfo(nameToken, expression, modifiers, isFunction));
        }

        public void Add(WattleMemberInfo info)
        {
            if (names.Contains(info.Name))
            {
                throw new SyntaxErrorException(
                    info.Token, 
                    "duplicate declaration of a {0} '{1}'", 
                    info.IsFunction ? "function" : "field",
                    info.Name
                );
            }
            
            names.Add(info.Name);
            items.Add(info);
        }
        
        public void Add(MemberCollection memberCollection)
        {
            foreach (WattleMemberInfo info in memberCollection)
            {
                Add(info);
            }
        }

        public int Count => items.Count;


        public WattleMemberInfo this[int index]
        {
            get => items[index];
            set => items[index] = value;
        }
    }
}