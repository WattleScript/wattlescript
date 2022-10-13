using System;
using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Expressions
{ 
    class NewExpression : Expression
    {
        internal SourceRef SourceRef;
        
        private SymbolRefExpression classRef;
        private List<Expression> arguments;
        private string className;
        private List<Token> namespaceQualifier = null;
        private bool referencesNamespace => namespaceQualifier != null;
        
        public NewExpression(ScriptLoadingContext lcontext) : base(lcontext)
        {
            lcontext.Lexer.Next(); //lexer at "new" token
            var classTok = CheckTokenType(lcontext, TokenType.Name);

            if (lcontext.Lexer.Current.Type == TokenType.Dot) // possible namespace qualifier
            {
                namespaceQualifier = ParseNamespace(lcontext, true);
                namespaceQualifier.Insert(0, classTok);

                if (namespaceQualifier.Count < 2) // at least ident-dot
                {
                    throw new SyntaxErrorException(namespaceQualifier[namespaceQualifier.Count - 1], $"Unexpected token '{namespaceQualifier[namespaceQualifier.Count - 1].Text}' while parsing namespace in 'new' expresson");
                }
                
                classTok = CheckTokenType(lcontext, TokenType.Name);
            }
            
            className = classTok.Text;
            CheckTokenType(lcontext, TokenType.Brk_Open_Round);
            arguments = lcontext.Lexer.Current.Type == TokenType.Brk_Close_Round ? new List<Expression>() : ExprList(lcontext);
            var end = CheckTokenType(lcontext, TokenType.Brk_Close_Round);
            SourceRef = classTok.GetSourceRef(end);
        }

        public override void Compile(FunctionBuilder bc)
        {
            bc.PushSourceRef(SourceRef);
            classRef.Compile(bc);
            foreach(var a in arguments)
                a.CompilePossibleLiteral(bc);

            if (referencesNamespace)
            {
                // [todo] update indexing for fully qualified access
               // bc.Emit_PrepNmspc(namespaceQualifier);
            }

            bc.Emit_NewCall(arguments.Count, className);
            bc.PopSourceRef();
        }

        public override DynValue Eval(ScriptExecutionContext context)
        {
            //Probably incorrect exception
            throw new InvalidOperationException(); 
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            classRef = new SymbolRefExpression(lcontext, lcontext.Scope.CreateGlobalReference(className));
            foreach(var a in arguments)
                a.ResolveScope(lcontext);
        }

        public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
        {
            dv = DynValue.Nil;
            return false;
        }
    }
}