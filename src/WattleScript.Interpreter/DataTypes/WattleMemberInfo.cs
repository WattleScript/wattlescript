using WattleScript.Interpreter.Tree;

namespace WattleScript.Interpreter
{
    internal class WattleMemberInfo
    {
        public string Name { get; set; }
        public MemberModifierFlags Flags { get; set; }
        public Expression Expr { get; set; }

        public WattleMemberInfo(string name, Expression expr, MemberModifierFlags flags)
        {
            Name = name;
            Expr = expr;
            Flags = flags;
        }
    }   
}