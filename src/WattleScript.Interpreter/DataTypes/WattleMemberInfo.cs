using WattleScript.Interpreter.Tree;

namespace WattleScript.Interpreter
{
    internal class WattleMemberInfo
    {
        public string Name => Token.Text;
        public MemberModifierFlags Flags { get; set; }
        public Expression Expr { get; set; }
        public Token Token { get; set; }
        public bool IsFunction { get; set; }

        public WattleMemberInfo(Token token, Expression expr, MemberModifierFlags flags, bool isFunction)
        {
            Token = token;
            Expr = expr;
            Flags = flags;
            IsFunction = isFunction;
        }
    }   
}