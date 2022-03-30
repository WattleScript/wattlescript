#define EMIT_DEBUG_OPS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using MoonSharp.Interpreter.Debugging;

namespace MoonSharp.Interpreter.Execution.VM
{
	internal class ByteCode : RefIdObject
	{
		public List<Instruction> Code = new List<Instruction>();
		public List<SourceRef> SourceRefs = new List<SourceRef>();
		public Script Script { get; private set; }
		private List<SourceRef> m_SourceRefStack = new List<SourceRef>();
		private SourceRef m_CurrentSourceRef = null;

		internal LoopTracker LoopTracker = new LoopTracker();

		public ByteCode(Script script)
		{
			Script = script;
		}
		
		public IDisposable EnterSource(SourceRef sref)
		{
			return new SourceCodeStackGuard(sref, this);
		}
		
		private class SourceCodeStackGuard : IDisposable
		{
			ByteCode m_Bc;

			public SourceCodeStackGuard(SourceRef sref, ByteCode bc)
			{
				m_Bc = bc;
				m_Bc.PushSourceRef(sref);
			}

			public void Dispose()
			{
				m_Bc.PopSourceRef();
			}
		}
		
		public void PushSourceRef(SourceRef sref)
		{
			m_SourceRefStack.Add(sref);
			m_CurrentSourceRef = sref;
		}

		public void PopSourceRef()
		{
			m_SourceRefStack.RemoveAt(m_SourceRefStack.Count - 1);
			m_CurrentSourceRef = (m_SourceRefStack.Count > 0) ? m_SourceRefStack[m_SourceRefStack.Count - 1] : null;
		}

		public string Dump()
		{
			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < Code.Count; i++)
			{
				if (Code[i].OpCode == OpCode.Debug)
					sb.AppendFormat("    {0}\n", Code[i]);
				else
					sb.AppendFormat("{0:X8}  {1}\n", i, Code[i]);
			}

			return sb.ToString();
		}

		public int GetJumpPointForNextInstruction()
		{
			return Code.Count;
		}
		public int GetJumpPointForLastInstruction()
		{
			return Code.Count - 1;
		}

		private int AppendInstruction(Instruction c)
		{
			Code.Add(c);
			SourceRefs.Add(m_CurrentSourceRef);
			return Code.Count - 1;
		}

		public void SetNumVal(int instruction, int val)
		{
			var ins = Code[instruction];
			ins.NumVal = val;
			Code[instruction] = ins;
		}

		public int Emit_Nop(string comment)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Nop, String = comment });
		}

		public int Emit_Invalid(string type)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Invalid, String = type });
		}

		public int Emit_Pop(int num = 1)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Pop, NumVal = num });
		}

		public void Emit_Call(int argCount, string debugName)
		{
			AppendInstruction(new Instruction() { OpCode = OpCode.Call, NumVal = argCount, String = debugName });
		}

		public void Emit_ThisCall(int argCount, string debugName)
		{
			AppendInstruction(new Instruction() { OpCode = OpCode.ThisCall, NumVal = argCount, String = debugName });
		}

		public int Emit_Literal(DynValue value)
		{
			switch (value.Type)
			{
				case DataType.Nil:
					return AppendInstruction(Instruction.OpCodeNil);
				case DataType.Boolean:
					return AppendInstruction(value.Boolean ? new Instruction() {OpCode = OpCode.PushTrue} : new Instruction() {OpCode = OpCode.PushFalse});
				case DataType.Number:
					return AppendInstruction(new Instruction() {OpCode = OpCode.PushNumber, Number = value.Number});
				case DataType.String:
					return AppendInstruction(new Instruction() {OpCode = OpCode.PushString, String = value.String});
			}
			throw new InvalidOperationException(value.Type.ToString());
		}

		public int Emit_Jump(OpCode jumpOpCode, int idx, int optPar = 0)
		{
			return AppendInstruction(new Instruction() { OpCode = jumpOpCode, NumVal = idx, NumVal2 = optPar });
		}

		public int Emit_MkTuple(int cnt)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.MkTuple, NumVal = cnt });
		}

		public int Emit_Operator(OpCode opcode)
		{
			var i = AppendInstruction(new Instruction() { OpCode = opcode });

			switch (opcode)
			{
				case OpCode.LessEq:
					AppendInstruction(Instruction.OpCodeCNot);
					break;
				case OpCode.Eq:
				case OpCode.Less:
					AppendInstruction(Instruction.OpCodeToBool);
					break;
			}

			return i;
		}


		[Conditional("EMIT_DEBUG_OPS")]
		public void Emit_Debug(string str)
		{
			AppendInstruction(new Instruction() { OpCode = OpCode.Debug, String = str.Substring(0, Math.Min(32, str.Length)) });
		}

		public int Emit_Enter(RuntimeScopeBlock runtimeScopeBlock)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Clean, NumVal = runtimeScopeBlock.From, NumVal2 = runtimeScopeBlock.ToInclusive });
		}

		public int Emit_Leave(RuntimeScopeBlock runtimeScopeBlock)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Clean, NumVal = runtimeScopeBlock.From, NumVal2 = runtimeScopeBlock.To });
		}

		public int Emit_Exit(RuntimeScopeBlock runtimeScopeBlock)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Clean, NumVal = runtimeScopeBlock.From, NumVal2 = runtimeScopeBlock.ToInclusive });
		}

		public int Emit_Clean(RuntimeScopeBlock runtimeScopeBlock)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Clean, NumVal = runtimeScopeBlock.To + 1, NumVal2 = runtimeScopeBlock.ToInclusive });
		}

		public int Emit_Closure(SymbolRef[] symbols, int jmpnum)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Closure, SymbolList = symbols, NumVal = jmpnum });
		}

		public int Emit_Args(params SymbolRef[] symbols)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Args, SymbolList = symbols });
		}

		public int Emit_Ret(int retvals)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Ret, NumVal = retvals });
		}

		public int Emit_Incr(int i)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Incr, NumVal = i });
		}

		public int Emit_NewTable(bool shared)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.NewTable, NumVal = shared ? 1 : 0 });
		}

		public int Emit_IterPrep()
		{
			return AppendInstruction(Instruction.OpCodeIterPrep);
		}

		public int Emit_ExpTuple(int stackOffset)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.ExpTuple, NumVal = stackOffset });
		}

		public int Emit_IterUpd()
		{
			return AppendInstruction(Instruction.OpCodeIterUpd);
		}

		public int Emit_Meta(string funcName, OpCodeMetadataType metaType)
		{
			return AppendInstruction(new Instruction()
			{
				OpCode = OpCode.Meta,
				String = funcName,
				NumVal2 = (int)metaType
			});
		}


		public int Emit_BeginFn(RuntimeScopeFrame stackFrame)
		{
			return AppendInstruction(new Instruction()
			{
				OpCode = OpCode.BeginFn,
				SymbolList = stackFrame.DebugSymbols.ToArray(),
				NumVal = stackFrame.Count,
				NumVal2 = stackFrame.ToFirstBlock,
			});
		}

		public int Emit_Scalar()
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Scalar });
		}

		public int Emit_Load(SymbolRef sym)
		{
			switch (sym.Type)
			{
				case SymbolRefType.Global:
					Emit_Load(sym.i_Env);
					AppendInstruction(new Instruction() { OpCode = OpCode.Index, String = sym.i_Name });
					return 2;
				case SymbolRefType.Local:
					AppendInstruction(new Instruction() { OpCode = OpCode.Local, NumVal = sym.i_Index });
					return 1;
				case SymbolRefType.Upvalue:
					AppendInstruction(new Instruction() { OpCode = OpCode.Upvalue, NumVal = sym.i_Index });
					return 1;
				default:
					throw new InternalErrorException("Unexpected symbol type : {0}", sym);
			}
		}

		public int Emit_Store(SymbolRef sym, int stackofs, int tupleidx)
		{
			switch (sym.Type)
			{
				case SymbolRefType.Global:
					Emit_Load(sym.i_Env);
					AppendInstruction(new Instruction() { OpCode = OpCode.IndexSet, NumVal = stackofs, NumVal2 = tupleidx, String = sym.i_Name });
					return 2;
				case SymbolRefType.Local:
					AppendInstruction(new Instruction() { OpCode = OpCode.StoreLcl, Symbol = sym, NumVal = stackofs, NumVal2 = tupleidx });
					return 1;
				case SymbolRefType.Upvalue:
					AppendInstruction(new Instruction() { OpCode = OpCode.StoreUpv, Symbol = sym, NumVal = stackofs, NumVal2 = tupleidx });
					return 1;
				default:
					throw new InternalErrorException("Unexpected symbol type : {0}", sym);
			}
		}

		public int Emit_TblInitN()
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.TblInitN });
		}

		public int Emit_TblInitI(bool lastpos)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.TblInitI, NumVal = lastpos ? 1 : 0 });
		}

		public int Emit_Index(string index = null, bool isNameIndex = false, bool isExpList = false)
		{
			OpCode o;
			if (isNameIndex) o = OpCode.IndexN;
			else if (isExpList) o = OpCode.IndexL;
			else o = OpCode.Index;

			return AppendInstruction(new Instruction() { OpCode = o, String = index });
		}

		public int Emit_IndexSet(int stackofs, int tupleidx, string index = null, bool isNameIndex = false, bool isExpList = false)
		{
			OpCode o;
			if (isNameIndex) o = OpCode.IndexSetN;
			else if (isExpList) o = OpCode.IndexSetL;
			else o = OpCode.IndexSet;

			return AppendInstruction(new Instruction() { OpCode = o, NumVal = stackofs, NumVal2 = tupleidx, String = index });
		}

		public int Emit_Copy(int numval)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Copy, NumVal = numval });
		}

		public int Emit_Swap(int p1, int p2)
		{
			return AppendInstruction(new Instruction() { OpCode = OpCode.Swap, NumVal = p1, NumVal2 = p2 });
		}

	}
}
