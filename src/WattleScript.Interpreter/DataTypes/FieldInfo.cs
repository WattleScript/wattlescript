using WattleScript.Interpreter.Tree;

namespace WattleScript.Interpreter
{
    internal class MemberFieldInfo
    {
        public string Name { get; set; }
        public MemberModifierFlags Flags { get; set; }
        public Expression Expr { get; set; }

        public MemberFieldInfo(string name, Expression expr, MemberModifierFlags flags)
        {
            Name = name;
            Expr = expr;
            Flags = flags;
        }
    }   
}