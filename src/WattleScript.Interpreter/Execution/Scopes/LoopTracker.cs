using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Execution
{
	interface ILoop
	{
		void CompileBreak(ByteCode bc);
		void CompileContinue(ByteCode bc);
		bool IsBoundary();
	}


	internal class LoopTracker
	{
		public FastStack<ILoop> Loops = new FastStack<ILoop>(32, 16384);
	}
}
