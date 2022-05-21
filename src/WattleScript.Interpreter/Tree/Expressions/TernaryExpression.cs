using System;
using System.Collections.Generic;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Expressions
{
    class TernaryExpression : Expression
    {
        private Expression condition;
        private Expression exp1;
        private Expression exp2;
        public TernaryExpression(ScriptLoadingContext lcontext, Expression cond) : base(lcontext)
        {
            this.condition = cond;
            CheckTokenType(lcontext, TokenType.Ternary);
            exp1 = Expr(lcontext);
            CheckTokenType(lcontext, TokenType.Colon);
            exp2 = Expr(lcontext);
        }

        public override void Compile(FunctionBuilder bc)
        {
            if (condition.EvalLiteral(out var evaluated))
            {
                if(evaluated.CastToBool())
                    exp1.CompilePossibleLiteral(bc);
                else
                    exp2.CompilePossibleLiteral(bc);
            }
            else
            {
                condition.Compile(bc);
                int j1 = bc.Emit_Jump(OpCode.Jf, -1);
                exp1.CompilePossibleLiteral(bc);
                int j2 = bc.Emit_Jump(OpCode.Jump, -1);
                bc.SetNumVal(j1, bc.GetJumpPointForNextInstruction()); //JF to here
                exp2.CompilePossibleLiteral(bc);
                bc.SetNumVal(j2, bc.GetJumpPointForNextInstruction()); //JUMP to here
            }
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            condition.ResolveScope(lcontext);
            exp1.ResolveScope(lcontext);
            exp2.ResolveScope(lcontext);
        }

        public override DynValue Eval(ScriptExecutionContext context)
        {
            if (condition.Eval(context).CastToBool())
                return exp1.Eval(context);
            else
                return exp2.Eval(context);
        }

        public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
        {
            if (condition.EvalLiteral(out var cond)) {
                if (cond.CastToBool())
                {
                    return exp1.EvalLiteral(out dv);
                }
                else {
                    return exp2.EvalLiteral(out dv);
                }
            }
            dv = DynValue.Nil;
            return false;
        }
    }
}