using System.Collections.Generic;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Expressions
{
    class TypeExpression : Expression
    {
        public Token NameToken { get; set; }
        
        public TypeExpression(ScriptLoadingContext lcontext) : base(lcontext)
        {
        }

        public override void Compile(FunctionBuilder bc)
        {
            
        }

        public override DynValue Eval(ScriptExecutionContext context)
        {
            return DynValue.Nil;
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            
        }

        public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
        {
            dv = DynValue.Void;
            return true;
        }
    }
}