using System;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
{
	class NewCallStatement : Statement
	{
		NewExpression newExpression;

		public NewCallStatement(ScriptLoadingContext lcontext, NewExpression newExpression) : base(lcontext)
		{
			this.newExpression = newExpression;
			lcontext.Source.Refs.Add(newExpression.SourceRef);
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			newExpression.ResolveScope(lcontext);
		}

		public override void Compile(FunctionBuilder bc)
		{
			using (bc.EnterSource(newExpression.SourceRef))
			{
				newExpression.Compile(bc);
				bc.Emit_Pop();
				bc.SourceRefs[bc.SourceRefs.Count - 1] = null; //Remove breakpoint stop
			}
		}
	}
}
