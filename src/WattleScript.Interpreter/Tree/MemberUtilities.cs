using System.Collections.Generic;

namespace WattleScript.Interpreter.Tree
{
    class MemberUtilities
    {
        private static HashSet<string> reservedFields = new HashSet<string>()
        {
            "__index",
            "__init",
            "__ctor",
            "__tostring",
        };

        public static void CheckReserved(Token name, string buildKind)
        {
            if (reservedFields.Contains(name.Text))
            {
                throw new SyntaxErrorException(name, "member name '{0}' is reserved in '{1}' definition", name.Text, buildKind);
            }
        }
        
        public static void AddModifierFlag(ref MemberModifierFlags source, Token token)
        {
            var flag = token.ToMemberModiferFlag();
            if (source.HasFlag(flag))
            {
                NodeBase.UnexpectedTokenType(token);
            }
            source |= flag;
        }

        public static void CheckModifierCombination(Token nameToken, MemberModifierFlags modifiers)
        {
            if (modifiers.HasFlag(MemberModifierFlags.Private) &&
                modifiers.HasFlag(MemberModifierFlags.Static))
                throw new SyntaxErrorException(nameToken, "members declared static may not be private");
            
            if (modifiers.HasFlag(MemberModifierFlags.Private) &&
                modifiers.HasFlag(MemberModifierFlags.Public))
                throw new SyntaxErrorException(nameToken, "conflicting access modifiers public and private");
        }
    }
}