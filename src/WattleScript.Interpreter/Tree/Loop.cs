using System.Collections.Generic;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree
{
	internal class Loop : ILoop
	{
		public RuntimeScopeBlock Scope;
		public List<int> BreakJumps = new List<int>();
		public List<int> ContinueJumps = new List<int>();

		public void CompileBreak(ByteCode bc)
		{
			bc.Emit_Exit(Scope);
			BreakJumps.Add(bc.Emit_Jump(OpCode.Jump, -1));
		}

		public void CompileContinue(ByteCode bc)
		{
			ContinueJumps.Add(bc.Emit_Jump(OpCode.Jump, -1));
		}

		public bool IsBoundary()
		{
			return false;
		}
	}

	internal class LoopBoundary : ILoop
	{
		public void CompileBreak(ByteCode bc)
		{
			throw new InternalErrorException("CompileBreak called on LoopBoundary");
		}
		
		public void CompileContinue(ByteCode bc)
		{
			throw new InternalErrorException("CompileBreak called on LoopBoundary");
		}

		public bool IsBoundary()
		{
			return true;
		}
	}

}
