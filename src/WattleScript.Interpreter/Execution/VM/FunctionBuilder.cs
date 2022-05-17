#define EMIT_DEBUG_OPS

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.Execution.VM
{
	internal class FunctionBuilder : RefIdObject
	{
		public List<Instruction> Code = new List<Instruction>();
		public List<SourceRef> SourceRefs = new List<SourceRef>();
		public Script Script { get; private set; }
		private List<SourceRef> m_SourceRefStack = new List<SourceRef>();
		private SourceRef m_CurrentSourceRef = null;

		internal LoopTracker LoopTracker = new LoopTracker();

		public Stack<int> NilChainTargets = new Stack<int>();
		
		public List<FunctionProto> Protos = new List<FunctionProto>();

		private List<string> strings = new List<string>();
		private Dictionary<string, int> stringMap = new Dictionary<string, int>();
		private List<double> numbers = new List<double>();

		int NumberArg(double dbl)
		{
			for (int i = 0; i < numbers.Count; i++)
			{
				// ReSharper disable once CompareOfFloatsByEqualityOperator
				if (numbers[i] == dbl)
					return i;
			}
			numbers.Add(dbl);
			return numbers.Count - 1;
		}

		int StringArg(string str)
		{
			if (strings.Count == 0) {
				strings.Add(null);
			}
			if (str == null) return 0;
			if (!stringMap.TryGetValue(str, out int idx))
			{
				idx = strings.Count;
				strings.Add(str);
				stringMap[str] = idx;
			}
			return idx;
		}

		public FunctionBuilder(Script script)
		{
			Script = script;
		}

		public FunctionProto GetProto(string name, RuntimeScopeFrame stackFrame)
		{
			return new FunctionProto()
			{
				Code = Code.ToArray(),
				SourceRefs = SourceRefs.ToArray(),
				Name = name,
				Locals = stackFrame.DebugSymbols.ToArray(),
				LocalCount = stackFrame.Count,
				Functions = Protos.ToArray(),
				Strings = strings.ToArray(),
				Numbers = numbers.ToArray()
			};
		}


		public IDisposable EnterSource(SourceRef sref)
		{
			return new SourceCodeStackGuard(sref, this);
		}


		private class SourceCodeStackGuard : IDisposable
		{
			FunctionBuilder m_Bc;

			public SourceCodeStackGuard(SourceRef sref, FunctionBuilder bc)
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
		public void SetNumValB(int instruction, uint valB)
		{
			var ins = Code[instruction];
			ins.NumValB = valB;
			Code[instruction] = ins;
		}

		public int Emit_Switch(bool nil, bool ctrue, bool cfalse, uint strings, uint numbers)
		{
			var numval = (uint) (strings & 0x1FFFFFFF); //29 bits
			if (nil) numval |= 0x80000000;
			if (ctrue) numval |= 0x40000000;
			if (cfalse) numval |= 0x20000000;
			return AppendInstruction(new Instruction(OpCode.Switch, (int)numval) {NumValB = numbers});
		}

		public int Emit_SwitchTable()
		{
			return AppendInstruction(new Instruction(OpCode.SSpecial));
		}
		
		public int Emit_SwitchTable(string str)
		{
			return AppendInstruction(new Instruction(OpCode.SString, StringArg(str)));
		}

		public int Emit_SwitchTable(double n)
		{
			// ReSharper disable once CompareOfFloatsByEqualityOperator
			if (n == (int) n) {
				return AppendInstruction(new Instruction(OpCode.SInteger, (int) n));
			}
			else {
				return AppendInstruction(new Instruction(OpCode.SNumber, NumberArg(n)));
			}
		}
		
		
		public int Emit_Nop(string comment)
		{
			return AppendInstruction(new Instruction(OpCode.Nop));
		}

		public int Emit_Invalid(string type)
		{
			return AppendInstruction(new Instruction(OpCode.Invalid));
		}

		public int Emit_Pop(int num = 1)
		{
			return AppendInstruction(new Instruction(OpCode.Pop, num));
		}

		public void Emit_Call(int argCount, string debugName)
		{
			AppendInstruction(new Instruction(OpCode.Call, argCount, StringArg(debugName)));
		}

		public void Emit_ThisCall(int argCount, string debugName)
		{
			AppendInstruction(new Instruction(OpCode.ThisCall, argCount, StringArg(debugName)));
		}

		public int Emit_Literal(DynValue value)
		{
			switch (value.Type)
			{
				case DataType.Nil:
					return AppendInstruction(new Instruction(OpCode.PushNil));
				case DataType.Boolean:
					if (value.Boolean)
						return AppendInstruction(new Instruction(OpCode.PushTrue));
					else
						return AppendInstruction(new Instruction(OpCode.PushFalse));
				case DataType.Number:
				{
					// If it's an integer number, keep it in the instruction stream
					// Else constant pool
					// ReSharper disable once CompareOfFloatsByEqualityOperator
					if (value.Number == (int) value.Number) {
						return AppendInstruction(new Instruction(OpCode.PushInt, (int) value.Number));
					}
					else {
						return AppendInstruction(new Instruction(OpCode.PushNumber, NumberArg(value.Number)));
					}
				}
				case DataType.String:
					return AppendInstruction(new Instruction(OpCode.PushString, StringArg(value.String)));
				case DataType.Range:
				{
					if (value.Range.To >= Instruction.NumVal2Min && value.Range.To <= Instruction.NumVal2Max)
					{
						return AppendInstruction(new Instruction(OpCode.NewRange, value.Range.From, value.Range.To, 0));
					}

					AppendInstruction(new Instruction(OpCode.PushInt, value.Range.From));
					AppendInstruction(new Instruction(OpCode.PushInt, value.Range.To));
					return AppendInstruction(new Instruction(OpCode.NewRange, 0, 0, 1));
				}
			}
			throw new InvalidOperationException(value.Type.ToString());
		}

		public int Emit_StrFormat(int argCount)
		{
			return AppendInstruction(new Instruction(OpCode.StrFormat, argCount));
		}

		public int Emit_Jump(OpCode jumpOpCode, int idx, int optPar = 0)
		{
			return AppendInstruction(new Instruction(jumpOpCode, idx, optPar));
		}

		public int Emit_MkTuple(int cnt)
		{
			return AppendInstruction(new Instruction(OpCode.MkTuple, cnt));
		}

		public int Emit_Operator(OpCode opcode, bool invert = false)
		{
			Instruction instr = opcode == OpCode.NewRange ? new Instruction(opcode, 0, 0, 1) : new Instruction(opcode, invert ? 1 : 0);
			int i = AppendInstruction(instr);
			
			switch (opcode)
			{
				case OpCode.LessEq:
					AppendInstruction(new Instruction(OpCode.CNot, invert ? 1 : 0));
					break;
				case OpCode.Eq:
				case OpCode.Less:
					if (invert)
						AppendInstruction(new Instruction(OpCode.Not));
					else
						AppendInstruction(new Instruction(OpCode.ToBool));
					break;
			}

			return i;
		}


		[Conditional("EMIT_DEBUG_OPS")]
		public void Emit_Debug(string str)
		{
			//AppendInstruction(new Instruction() { OpCode = OpCode.Debug, String = str.Substring(0, Math.Min(32, str.Length)) });
		}

		// Skips emitting clean ops that would be interpreted as a nop by the VM
		// Saves us a little bit of space
		void PossibleCleanOp(int from, int to)
		{
			if (to >= from)
			{
				AppendInstruction(new Instruction(OpCode.Clean, from, to));
			}
		}

		public void Emit_Enter(RuntimeScopeBlock runtimeScopeBlock)
		{
			PossibleCleanOp(runtimeScopeBlock.From, runtimeScopeBlock.ToInclusive);
		}

		public void Emit_Leave(RuntimeScopeBlock runtimeScopeBlock)
		{
			PossibleCleanOp(runtimeScopeBlock.From, runtimeScopeBlock.To);
		}

		public void Emit_Exit(RuntimeScopeBlock runtimeScopeBlock)
		{
			PossibleCleanOp(runtimeScopeBlock.From, runtimeScopeBlock.ToInclusive);
		}

		public void Emit_Clean(RuntimeScopeBlock runtimeScopeBlock)
		{
			//This one needs to be a possible nop for Label statements to work correctly
			AppendInstruction(new Instruction(OpCode.Clean, runtimeScopeBlock.To + 1, runtimeScopeBlock.ToInclusive));
		}

		public int Emit_CloseUp(SymbolRef sym)
		{
			if (sym.Type != SymbolRefType.Local)
				throw new InternalErrorException("Can only emit CloseUp for locals");
			return AppendInstruction(new Instruction(OpCode.CloseUp, sym.i_Index));
		}

		public int Emit_Closure(int index)
		{
			return AppendInstruction(new Instruction(OpCode.Closure, index));
		}

		public int Emit_Args(int arglen, bool hasVararg)
		{
			return AppendInstruction(new Instruction(OpCode.Args, arglen, hasVararg ? 1 : 0));
		}

		public int Emit_Ret(int retvals)
		{
			return AppendInstruction(new Instruction(OpCode.Ret, retvals));
		}

		public int Emit_Incr(int i)
		{
			return AppendInstruction(new Instruction(OpCode.Incr, i));
		}

		public int Emit_NewTable(bool shared)
		{
			return AppendInstruction(new Instruction(OpCode.NewTable, shared ? 1 : 0));
		}

		public int Emit_IterPrep()
		{
			return AppendInstruction(new Instruction(OpCode.IterPrep));
		}

		public int Emit_ExpTuple(int stackOffset)
		{
			return AppendInstruction(new Instruction(OpCode.ExpTuple, stackOffset));
		}

		public int Emit_IterUpd()
		{
			return AppendInstruction(new Instruction(OpCode.IterUpd));
		}

		public int Emit_Scalar()
		{
			return AppendInstruction(new Instruction(OpCode.Scalar));
		}

		public int Emit_Load(SymbolRef sym)
		{
			switch (sym.Type)
			{
				case SymbolRefType.Global:
					Emit_Load(sym.i_Env);
					AppendInstruction(new Instruction(OpCode.Index, StringArg(sym.i_Name)));
					return 2;
				case SymbolRefType.Local:
					AppendInstruction(new Instruction(OpCode.Local, sym.i_Index));
					return 1;
				case SymbolRefType.Upvalue:
					AppendInstruction(new Instruction(OpCode.Upvalue, sym.i_Index));
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
					AppendInstruction(new Instruction(OpCode.IndexSet, stackofs, tupleidx, (uint)StringArg(sym.i_Name)));
					return 2;
				case SymbolRefType.Local:
					AppendInstruction(new Instruction(OpCode.StoreLcl, stackofs, tupleidx, (uint) sym.i_Index));
					return 1;
				case SymbolRefType.Upvalue:
					AppendInstruction(new Instruction(OpCode.StoreUpv, stackofs, tupleidx, (uint) sym.i_Index));
					return 1;
				default:
					throw new InternalErrorException("Unexpected symbol type : {0}", sym);
			}
		}

		public int Emit_ToNum(int stage)
		{
			return AppendInstruction(new Instruction(OpCode.ToNum, stage));
		}
		
		public int Emit_TblInitN(int count)
		{
			return AppendInstruction(new Instruction(OpCode.TblInitN, count));
		}

		public int Emit_TblInitI(bool lastpos, int count)
		{
			return AppendInstruction(new Instruction(OpCode.TblInitI, count, lastpos ? 1 : 0));
		}

		public int Emit_Index(string index = null, bool isNameIndex = false, bool isExpList = false, bool isMethodCall = false)
		{
			OpCode o;
			if (isNameIndex) o = OpCode.IndexN;
			else if (isExpList) o = OpCode.IndexL;
			else o = OpCode.Index;
			return AppendInstruction(new Instruction(o, StringArg(index), isMethodCall ? 1 : 0));
		}

		public int Emit_IndexSet(int stackofs, int tupleidx, string index = null, bool isNameIndex = false, bool isExpList = false)
		{
			OpCode o;
			if (isNameIndex) o = OpCode.IndexSetN;
			else if (isExpList) o = OpCode.IndexSetL;
			else o = OpCode.IndexSet;
			return AppendInstruction(new Instruction(o, stackofs, tupleidx, (uint)StringArg(index)));
		}

		public int Emit_Copy(int numval)
		{
			return AppendInstruction(new Instruction(OpCode.Copy, numval));
		}
		
		public int Emit_CopyValue(int numval, int tupleidx)
		{
			return AppendInstruction(new Instruction(OpCode.CopyValue, numval, tupleidx));
		}

		public int Emit_Swap(int p1, int p2)
		{
			return AppendInstruction(new Instruction(OpCode.Swap, p1, p2));
		}

		public int Emit_JLclInit(SymbolRef sym, int target)
		{
			if(sym.Type != SymbolRefType.Local) throw new InternalErrorException("Unexpected symbol type : {0}", sym);
			return AppendInstruction(new Instruction(OpCode.JLclInit, target, sym.Index));
		}
	}
}
