using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Expressions
{
    class TemplatedStringExpression : Expression
    {
        private List<Expression> arguments = new List<Expression>();
        private string formatString;

        static string EscapeForFormat(string s) => s.Replace("{", "{{").Replace("}", "}}");

        public TemplatedStringExpression(ScriptLoadingContext lcontext, Token startToken) : base(lcontext)
        {
            var builder = new StringBuilder();
            builder.Append(EscapeForFormat(startToken.Text));
            lcontext.Lexer.Next();
            int i = 0;
            while (lcontext.Lexer.Current.Type != TokenType.String_EndTemplate) {
                if (lcontext.Lexer.Current.Type == TokenType.Eof) {
                    throw new SyntaxErrorException(lcontext.Lexer.Current, "` expected")
                    {
                        IsPrematureStreamTermination = true
                    };
                }
                if (lcontext.Lexer.Current.Type != TokenType.String_TemplateFragment) {
                    builder.Append("{").Append(i++).Append("}");
                    arguments.Add(Expr(lcontext));
                }
                else {
                    builder.Append(EscapeForFormat(lcontext.Lexer.Current.Text));
                    lcontext.Lexer.Next();
                }
            }
            builder.Append(EscapeForFormat(lcontext.Lexer.Current.Text));
            lcontext.Lexer.Next();
            formatString = builder.ToString();
        }

        public override void Compile(FunctionBuilder bc)
        {
            bc.Emit_Literal(DynValue.NewString(formatString));
            foreach (var exp in arguments) {
                exp.CompilePossibleLiteral(bc);
            }
            bc.Emit_StrFormat(arguments.Count);
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            foreach(var exp in arguments)
                exp.ResolveScope(lcontext);
        }

        public override DynValue Eval(ScriptExecutionContext context)
        {
            return DynValue.NewString(string.Format(formatString, arguments.Select(x =>
            {
                var dyn = x.Eval(context);
                if (dyn.Type == DataType.String) return dyn.String;
                else if (dyn.Type == DataType.Number) return dyn.Number.ToString();
                else if (dyn.Type == DataType.Boolean) {
                    return dyn.Boolean ? "true" : "false";
                }
                else {
                    //TODO: I think this is incorrect
                    throw new DynamicExpressionException("Cannot call __tostring in dynamic expression");
                }
            })));
        }

        public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
        {
            dv = DynValue.Nil;
            return false;
        }
    }
}