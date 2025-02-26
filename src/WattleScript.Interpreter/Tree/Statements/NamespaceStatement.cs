using System.Text;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Statements
{
    class NamespaceStatement : Statement
    {
        private CompositeStatement block;
        private RuntimeScopeBlock stackFrame;
        private string namespaceIdentStr;
        
        public NamespaceStatement(ScriptLoadingContext lcontext) : base(lcontext)
        {
            CheckTokenType(lcontext, TokenType.Namespace);
            bool canBeDot = false;
            StringBuilder namespaceIdent = new StringBuilder();

            while (lcontext.Lexer.PeekNext().Type != TokenType.Eof)
            {
                Token tkn = lcontext.Lexer.Current;

                if (!canBeDot && tkn.Type != TokenType.Name)
                {
                    break;
                }

                if (canBeDot && tkn.Type != TokenType.Dot)
                {
                    break;
                }

                canBeDot = !canBeDot;

                namespaceIdent.Append(tkn.Text);
                lcontext.Lexer.Next();
            }

            namespaceIdentStr = namespaceIdent.ToString();
            lcontext.Linker.CurrentNamespace = namespaceIdentStr;

            if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly)
            {
                block = new CompositeStatement(lcontext, BlockEndType.CloseCurly);
            }
            else
            {
                block = new CompositeStatement(lcontext, BlockEndType.Normal);
            }
        }

        public override void Compile(FunctionBuilder bc)
        {
            bc.Emit_Enter(stackFrame);
            block.Compile(bc);
            bc.Emit_Leave(stackFrame);
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            lcontext.Scope.PushBlock(namespaceIdentStr);
            block.ResolveScope(lcontext);
            stackFrame = lcontext.Scope.PopBlock();
        }
    }
}