using System;
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
		public bool Switch = false;

		public void CompileBreak(FunctionBuilder bc)
		{
			bc.Emit_Exit(Scope);
			BreakJumps.Add(bc.Emit_Jump(OpCode.Jump, -1));
		}

		public void CompileContinue(FunctionBuilder bc)
		{
			ContinueJumps.Add(bc.Emit_Jump(OpCode.Jump, -1));
		}

		public bool IsBoundary()
		{
			return false;
		}
		
		public bool IsSwitch() => Switch;
	}

	internal class LoopBoundary : ILoop
	{
		public void CompileBreak(FunctionBuilder bc)
		{
			throw new InternalErrorException("CompileBreak called on LoopBoundary");
		}
		
		public void CompileContinue(FunctionBuilder bc)
		{
			throw new InternalErrorException("CompileBreak called on LoopBoundary");
		}

		public bool IsBoundary()
		{
			return true;
		}

		public bool IsSwitch() => false;
	}

}
