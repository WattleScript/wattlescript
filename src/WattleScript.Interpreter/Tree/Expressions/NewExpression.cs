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
        
        public NewExpression(ScriptLoadingContext lcontext) : base(lcontext)
        {
            lcontext.Lexer.Next(); //lexer at "new" token
            var classTok = CheckTokenType(lcontext, TokenType.Name);
            className = classTok.Text;
            CheckTokenType(lcontext, TokenType.Brk_Open_Round);
            if (lcontext.Lexer.Current.Type == TokenType.Brk_Close_Round)
            {
                arguments = new List<Expression>();
            }
            else
            {
                arguments = ExprList(lcontext);
            }
            var end = CheckTokenType(lcontext, TokenType.Brk_Close_Round);
            SourceRef = classTok.GetSourceRef(end);
        }

        public override void Compile(FunctionBuilder bc)
        {
            bc.PushSourceRef(SourceRef);
            classRef.Compile(bc);
            foreach(var a in arguments)
                a.CompilePossibleLiteral(bc);
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