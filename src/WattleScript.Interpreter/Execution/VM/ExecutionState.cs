using WattleScript.Interpreter.DataStructs;

namespace WattleScript.Interpreter.Execution.VM
{
	internal sealed class ExecutionState
	{
		public FastStack<DynValue> ValueStack = new FastStack<DynValue>(1024, 131072);
		public FastStack<CallStackItem> ExecutionStack = new FastStack<CallStackItem>(1024, 131072);
		public int InstructionPtr = 0;
		public CoroutineState State = CoroutineState.NotStarted;
	}
}
