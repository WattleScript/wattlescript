using WattleScript.Interpreter.Execution;

namespace WattleScript.Interpreter.Tree.Statements
{
	class EmptyStatement : Statement
	{
		public EmptyStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			//No-op
		}


		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
		}
	}
}
