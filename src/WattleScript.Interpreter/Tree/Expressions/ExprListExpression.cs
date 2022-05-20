using System.Collections.Generic;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution;

namespace WattleScript.Interpreter.Tree.Expressions
{
	class ExprListExpression : Expression 
	{
		private List<Expression> expressions;
		internal List<Expression> Expressions => expressions;

		public ExprListExpression(List<Expression> exps, ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			expressions = exps;
		}


		public Expression[] GetExpressions()
		{
			return expressions.ToArray();
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			foreach(var exp in expressions)
				exp.ResolveScope(lcontext);
		}

		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			foreach (var exp in expressions)
				exp.CompilePossibleLiteral(bc);

			if (expressions.Count > 1)
				bc.Emit_MkTuple(expressions.Count);
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			if (expressions.Count >= 1)
				return expressions[0].Eval(context);

			return DynValue.Void;
		}

		public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
		{
			dv = DynValue.Nil;
			return false;
		}
	}
}
