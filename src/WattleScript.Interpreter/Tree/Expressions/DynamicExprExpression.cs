using System;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution;

namespace WattleScript.Interpreter.Tree.Expressions
{
	class DynamicExprExpression : Expression
	{
		Expression m_Exp;

		public DynamicExprExpression(Expression exp, ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			lcontext.Anonymous = true;
			m_Exp = exp;
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			m_Exp.ResolveScope(lcontext);
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			return m_Exp.Eval(context);
		}

		public override bool EvalLiteral(out DynValue dv)
		{
			throw new InvalidOperationException();
		}

		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			throw new InvalidOperationException();
		}

		public override SymbolRef FindDynamic(ScriptExecutionContext context)
		{
			return m_Exp.FindDynamic(context);
		}
	}
}
