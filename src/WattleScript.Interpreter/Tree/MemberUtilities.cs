using System.Collections.Generic;
using System.Linq;

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

        private static readonly (MemberModifierFlags a, MemberModifierFlags? b, WattleMemberType appliesTo, string msg)[] flagConflicts =
        {
            (MemberModifierFlags.Private, MemberModifierFlags.Static, WattleMemberType.ClassMember, "members declared static may not be private"),
            (MemberModifierFlags.Public, MemberModifierFlags.Private, WattleMemberType.Any, null),
            (MemberModifierFlags.Sealed, MemberModifierFlags.Static, WattleMemberType.Any, null)
        };

        public static void CheckReserved(Token name, string buildKind)
        {
            if (reservedFields.Contains(name.Text))
            {
                throw new SyntaxErrorException(name, "member name '{0}' is reserved in '{1}' definition", name.Text, buildKind);
            }
        }
        
        public static void AddModifierFlag(ref MemberModifierFlags source, Token token, WattleMemberType memberType)
        {
            var flag = token.ToMemberModiferFlag();
            if (source.HasFlag(flag))
            {
                throw new SyntaxErrorException(token, "duplicate modifier '{0}'", token.Text);
            }
            source |= flag;
            
            foreach (var combo in flagConflicts)
            {
                if (combo.appliesTo.HasFlag(memberType) && source.HasFlag(combo.a) && (combo.b == null || source.HasFlag(combo.b)))
                {
                    if (combo.msg != null)
                        throw new SyntaxErrorException(token, combo.msg);
                    
                    if (combo.b != null)
                        throw new SyntaxErrorException(token,
                            "conflicting modifiers '{0}' and '{1}'",
                            combo.a.ToString().ToLowerInvariant(),
                            combo.b.ToString().ToLowerInvariant());
                    
                    throw new SyntaxErrorException(token, $"{memberType.ToString().ToLowerInvariant()} cannot be declared with modifier '{combo.a.ToString()}'");
                }
            }
        }
    }
}