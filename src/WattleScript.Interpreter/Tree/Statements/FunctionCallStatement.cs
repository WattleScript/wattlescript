using System;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
{
	class FunctionCallStatement : Statement
	{
		FunctionCallExpression m_FunctionCallExpression;

		public FunctionCallStatement(ScriptLoadingContext lcontext, FunctionCallExpression functionCallExpression)
			: base(lcontext)
		{
			m_FunctionCallExpression = functionCallExpression;
			lcontext.Source.Refs.Add(m_FunctionCallExpression.SourceRef);
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			m_FunctionCallExpression.ResolveScope(lcontext);
		}


		public override void Compile(FunctionBuilder bc)
		{
			using (bc.EnterSource(m_FunctionCallExpression.SourceRef))
			{
				m_FunctionCallExpression.Compile(bc);
				bc.Emit_Pop();
				bc.SourceRefs[bc.SourceRefs.Count - 1] = null; //Remove breakpoint stop
			}
		}

		
	}
}
