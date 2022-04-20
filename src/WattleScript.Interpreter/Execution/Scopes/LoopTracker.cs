using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Execution
{
	interface ILoop
	{
		void CompileBreak(FunctionBuilder bc);
		void CompileContinue(FunctionBuilder bc);
		bool IsBoundary();
		bool IsSwitch();
	}


	internal class LoopTracker
	{
		public FastStack<ILoop> Loops = new FastStack<ILoop>(32, 16384);
	}
}
