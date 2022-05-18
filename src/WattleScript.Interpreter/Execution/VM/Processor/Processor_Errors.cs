using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.Execution.VM
{
	sealed partial class Processor
	{
		private SourceRef GetCurrentSourceRef(int instructionPtr)
		{
			var code = m_ExecutionStack.Peek().Function?.sourceRefs;
			if (code == null) return null;
			if (instructionPtr >= 0 && instructionPtr < code.Length)
			{
				return code[instructionPtr];
			}
			return null;
		}


		private void FillDebugData(InterpreterException ex, int ip)
		{
			// adjust IP
			if (ip == YIELD_SPECIAL_TRAP)
				ip = m_SavedInstructionPtr;
			else
				ip -= 1;

			ex.InstructionPtr = ip;

			SourceRef sref = GetCurrentSourceRef(ip);

			ex.DecorateMessage(m_Script, sref, ip);

			ex.CallStack = Debugger_GetCallStack(sref);
		}


	}
}
