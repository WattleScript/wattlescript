using System.Collections.Generic;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution;


namespace WattleScript.Interpreter.Tree.Expressions
{
	class AdjustmentExpression : Expression 
	{
		private Expression expression;

		public AdjustmentExpression(ScriptLoadingContext lcontext, Expression exp)
			: base(lcontext)
		{
			expression = exp;
		}

		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			expression.Compile(bc);
			//TODO: Add more tests, make sure this opt is 100% correct
			if (expression is FunctionCallExpression)
			{
				bc.Emit_Scalar();
			}
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			return expression.Eval(context).ToScalar();
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			expression.ResolveScope(lcontext);
		}

		public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
		{
			if (expression.EvalLiteral(out dv, symbols))
			{
				dv = dv.ToScalar();
				return true;
			}
			return false;
		}
	}
}
