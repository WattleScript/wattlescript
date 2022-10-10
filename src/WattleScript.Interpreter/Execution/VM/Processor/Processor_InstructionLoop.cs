using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Interop;
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Execution.VM
{
	sealed partial class Processor
	{
		const int YIELD_SPECIAL_TRAP = -99;
		const int YIELD_SPECIAL_AWAIT = -100;

		internal ulong AutoYieldCounter = 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		string GetString(int i)
		{
			return m_ExecutionStack.Peek().Function.strings[i];
		}

		private DynValue Processing_Loop(int instructionPtr, bool canAwait = false)
		{
			// This is the main loop of the processor, has a weird control flow and needs to be as fast as possible.
			// This sentence is just a convoluted way to say "don't complain about gotos".
			ulong executedInstructions = 0;
			bool canAutoYield = (AutoYieldCounter > 0) && m_CanYield && (State != CoroutineState.Main);

			repeat_execution:

			try
			{
				while (true)
				{
					//TODO: Hoist outside of while loop, update any instruction that
					//can change frame to jump to before loop when changing frame
					ref var currentFrame = ref m_ExecutionStack.Peek();
					
					Instruction i = currentFrame.Function.code[instructionPtr];
					int currentPtr = instructionPtr;
					if (m_Debug.DebuggerAttached != null)
					{
						ListenDebugger(instructionPtr);
					}

					++executedInstructions;

					if (m_Script.Options.InstructionLimit > 0 && executedInstructions > m_Script.Options.InstructionLimit)
					{
						throw new ScriptRuntimeException($"Maximum limit of {m_Script.Options.InstructionLimit} exceeded. Script terminated.");
					}

					if (canAutoYield && executedInstructions > AutoYieldCounter)
					{
						m_SavedInstructionPtr = instructionPtr;
						return DynValue.NewForcedYieldReq();
					}

					++instructionPtr;

					switch (i.OpCode)
					{
						case OpCode.Nop:
						case OpCode.Debug:
							break;
						case OpCode.Pop:
							m_ValueStack.RemoveLast(i.NumVal);
							break;
						case OpCode.Copy:
							m_ValueStack.Push(m_ValueStack.Peek(i.NumVal));
							break;
						case OpCode.CopyValue:
							m_ValueStack.Push(GetStoreValue(i));
							break;
						case OpCode.Swap:
							ExecSwap(i);
							break;
						case OpCode.PushNil:
							m_ValueStack.Push(DynValue.Nil);
							break;
						case OpCode.PushTrue:
							m_ValueStack.Push(DynValue.True);
							break;
						case OpCode.PushFalse:
							m_ValueStack.Push(DynValue.False);
							break;
						case OpCode.PushInt:
							m_ValueStack.Push(DynValue.NewNumber(i.NumVal));
							break;
						case OpCode.PushNumber:
							m_ValueStack.Push(DynValue.NewNumber(currentFrame.Function.numbers[i.NumVal]));
							break;
						case OpCode.PushString:
							m_ValueStack.Push(DynValue.NewString(currentFrame.Function.strings[i.NumVal]));
							break;
						case OpCode.BNot:
							ExecBNot();
							break;
						case OpCode.BAnd:
							ExecBAnd();
							break;
						case OpCode.BOr:
							ExecBOr();
							break;
						case OpCode.BXor:
							ExecBXor();
							break;
						case OpCode.BLShift:
							ExecBlShift();
							break;
						case OpCode.BRShiftA:
							ExecBrShiftA();
							break;
						case OpCode.BRShiftL:
							ExecBrShiftL();
							break;
						case OpCode.Add:
							instructionPtr = ExecAdd(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.AddStr:
							instructionPtr = ExecAddStr(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Concat:
							instructionPtr = ExecConcat(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Neg:
							instructionPtr = ExecNeg(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Sub:
							instructionPtr = ExecSub(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Mul:
							instructionPtr = ExecMul(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Div:
							instructionPtr = ExecDiv(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Mod:
							instructionPtr = ExecMod(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Power:
							instructionPtr = ExecPower(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Eq:
							instructionPtr = ExecEq(instructionPtr, i);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.LessEq:
							instructionPtr = ExecLessEq(instructionPtr, i);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Less:
							instructionPtr = ExecLess(instructionPtr, i);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Len:
							instructionPtr = ExecLen(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Switch:
							instructionPtr = ExecSwitch(currentPtr, i, ref currentFrame);
							break;
						case OpCode.Call:
						case OpCode.ThisCall:
							instructionPtr = Internal_ExecCall(canAwait, i.NumVal, instructionPtr, null, null, i.OpCode == OpCode.ThisCall, GetString(i.NumVal2));
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							if (instructionPtr == YIELD_SPECIAL_AWAIT) goto yield_to_await;
							break;
						case OpCode.NewCall:
							instructionPtr = ExecNewCall(i, instructionPtr, canAwait);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							if (instructionPtr == YIELD_SPECIAL_AWAIT) goto yield_to_await;
							break;
						case OpCode.Scalar:
							m_ValueStack.Push(m_ValueStack.Pop().ToScalar());
							break;
						case OpCode.CloseUp:
						{
							if (currentFrame.OpenClosures == null) break;
							for (int j = currentFrame.OpenClosures.Count - 1; j >= 0; j--) {
								if (currentFrame.OpenClosures[j].Index == currentFrame.BasePointer + i.NumVal)
								{
									currentFrame.OpenClosures[j].Close();
									currentFrame.OpenClosures.RemoveAt(j);
								}
							}
							break;
						}
						case OpCode.Not:
							ExecNot();
							break;
						case OpCode.CNot:
							ExecCNot(i);
							break;
						case OpCode.JfOrPop:
						case OpCode.JtOrPop:
							instructionPtr = ExecShortCircuitingOperator(i, instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.JNil:
							{
								DynValue v = m_ValueStack.Pop().ToScalar();

								if (v.Type == DataType.Nil || v.Type == DataType.Void)
									instructionPtr = i.NumVal;
							}
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.JNilChk:
							{
								if(m_ValueStack.Peek().IsNil())
									instructionPtr = i.NumVal;
							}
							break;
						case OpCode.Jf:
							instructionPtr = JumpBool(i, false, instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Jt:
							instructionPtr = JumpBool(i, true, instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Jump:
							instructionPtr = i.NumVal;
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.MkTuple:
							ExecMkTuple(i);
							break;
						case OpCode.Clean:
							ClearBlockData(i);
							break;
						case OpCode.Closure:
							ExecClosure(i);
							break;
						case OpCode.ToBool:
						{
							ref var top = ref m_ValueStack.Peek();
							top = DynValue.NewBoolean(top.CastToBool());
							break;
						}
						case OpCode.StrFormat:
							ExecStrFormat(i);
							break;
						case OpCode.Args:
							ExecArgs(i);
							break;
						case OpCode.Ret:
							instructionPtr = ExecRet(i, currentPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							if (instructionPtr < 0)
								goto return_to_native_code;
							break;
						case OpCode.Incr:
							ExecIncr(i);
							break;
						case OpCode.JFor:
							instructionPtr = ExecJFor(i, instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.TabProps:
						{
							ref var top = ref m_ValueStack.Peek();
							if (top.Type != DataType.Table) throw new InternalErrorException("v-stack top NOT table");
							top.Table.Kind = (TableKind)i.NumVal2;
							top.Table.ReadOnly = i.NumVal3 != 0;
							top.Table.ModifierFlags = (MemberModifierFlags)i.NumVal;
							break;
						}
						case OpCode.SetMetaTab:
						{
							var top = m_ValueStack.Pop();
							ref var tab = ref m_ValueStack.Peek();
							if (top.Type != DataType.Table) throw new InternalErrorException("v-stack top NOT table");
							if (tab.Type != DataType.Table) throw new InternalErrorException("v-stack tab NOT table");
							
							// (NumVal > 0) -> whether base class is present
							if (i.NumVal > 0 && (top.Table.ModifierFlags.HasFlag(MemberModifierFlags.Static) || top.Table.ModifierFlags.HasFlag(MemberModifierFlags.Sealed)))
							{
								string modifier = top.Table.ModifierFlags.HasFlag(MemberModifierFlags.Static) ? "static" : "sealed";
								throw ScriptRuntimeException.BaseInvalidModifier(modifier, top.Table.Get("Name").String ?? "(null)", currentFrame.Function.strings[i.NumVal]);
							}
							tab.Table.MetaTable = top.Table;
							break;
						}
						case OpCode.LoopChk:
						{
							if (!m_ValueStack[currentFrame.BasePointer + i.NumVal].TryGetNumber(out var n))
								throw new InternalErrorException("LoopChk operand NOT number");
							if (n > 100)
								throw ScriptRuntimeException.CyclicReference(currentFrame.Function.strings[i.NumValB]);
							break;
						}
						case OpCode.BaseChk:
						{
							ref var cls = ref m_ValueStack.Peek();
							if (cls.Type != DataType.Table ||
							    cls.Table.Kind != TableKind.Class) {
								throw ScriptRuntimeException.NotAClass(currentFrame.Function.strings[i.NumVal], cls);
							}
							break;
						}
						case OpCode.MixInit:
							ExecMixInit(i);
							break;
						case OpCode.AnnotI:
							ExecAnnotX(currentFrame.Function.strings[i.NumValB], DynValue.NewNumber(i.NumVal));
							break;
						case OpCode.AnnotB:
							ExecAnnotX(currentFrame.Function.strings[i.NumValB], DynValue.NewBoolean(i.NumVal != 0));
							break;
						case OpCode.AnnotS: 
							ExecAnnotX(currentFrame.Function.strings[i.NumValB], 
								DynValue.NewString(currentFrame.Function.strings[i.NumVal]));
							break;
						case OpCode.AnnotN:
							ExecAnnotX(currentFrame.Function.strings[i.NumValB],
								DynValue.NewNumber(currentFrame.Function.numbers[i.NumVal]));
							break;
						case OpCode.AnnotT:
							if (m_ValueStack.Peek().Type != DataType.Table)
								throw new InternalErrorException("v-stack top NOT table");
							ExecAnnotX(currentFrame.Function.strings[i.NumValB],
								m_ValueStack.Pop());
							break;
						case OpCode.IterPrep:
							ExecIterPrep();
							break;
						case OpCode.IterUpd:
							ExecIterUpd();
							break;
						case OpCode.ExpTuple:
							ExecExpTuple(i);
							break;
						case OpCode.Local:
							m_ValueStack.Push(m_ValueStack[currentFrame.BasePointer + i.NumVal]);
							break;
						case OpCode.Upvalue:
							m_ValueStack.Push(currentFrame.ClosureScope[i.NumVal].Value());
							break;
						case OpCode.StoreUpv:
							ExecStoreUpv(i);
							break;
						case OpCode.StoreLcl:
							ExecStoreLcl(i, ref currentFrame);
							break;
						case OpCode.TblInitN:
							ExecTblInitN(i);
							break;
						case OpCode.TblInitI:
							ExecTblInitI(i);
							break;
						case OpCode.Index:
						case OpCode.IndexN:
						case OpCode.IndexL:
							instructionPtr = ExecIndex(i, instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.IndexSet:
						case OpCode.IndexSetN:
						case OpCode.IndexSetL:
							instructionPtr = ExecIndexSet(i, instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.NilCoalescing:
							instructionPtr = ExecNilCoalescingAssignment( instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.NilCoalescingInverse:
							instructionPtr = ExecNilCoalescingAssignmentInverse(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.JLclInit:
							if(m_ValueStack[m_ExecutionStack.Peek().BasePointer + i.NumVal2].IsNotNil())
								instructionPtr = i.NumVal;
							break;
						case OpCode.NewRange:
							ExecNewRange(i);
							break;
						case OpCode.SetFlags:
							ExecSetFlags(i);
							break;
						case OpCode.CopyFlags:
							ExecCopyFlags(i);
							break;
						case OpCode.MergeFlags:
							ExecMergeFlags(i);
							break;
						case OpCode.ToNum:
						{
							ref var top = ref m_ValueStack.Peek();
							if (top.Type != DataType.Number) 
							{
								if (!top.TryCastToNumber(out var d))
									throw ScriptRuntimeException.ConvertToNumberFailed(i.NumVal, ref top);
								top = DynValue.NewNumber(d);
							}
							break;
						}
						case OpCode.Invalid:
							throw new NotImplementedException($"Invalid opcode {i.OpCode}");
						default:
							throw new NotImplementedException($"Execution for {i.OpCode} not implemented yet!");
					}
				}

				yield_to_calling_coroutine:

				DynValue yieldRequest = m_ValueStack.Pop().ToScalar();

				if (m_CanYield)
					return yieldRequest;
				if (State == CoroutineState.Main)
					throw ScriptRuntimeException.CannotYieldMain();
				throw ScriptRuntimeException.CannotYield();
				
				yield_to_await:
				DynValue awaitRequest = m_ValueStack.Pop().ToScalar();
				return awaitRequest;

			}
			catch (InterpreterException ex)
			{
				FillDebugData(ex, instructionPtr);

				if (!(ex is ScriptRuntimeException e))
				{
					ex.Rethrow();
					throw;
				}

				if (m_Debug.DebuggerAttached != null)
				{
					if (m_Debug.DebuggerAttached.SignalRuntimeException(e))
					{
						if(instructionPtr > 0) ListenDebugger(instructionPtr);
					}
				}

				for (int i = 0; i < m_ExecutionStack.Count; i++)
				{
					var c = m_ExecutionStack.Peek(i);

					if (c.ErrorHandlerBeforeUnwind.IsNotNil())
						e.DecoratedMessage = PerformMessageDecorationBeforeUnwind(c.ErrorHandlerBeforeUnwind, e.DecoratedMessage, GetCurrentSourceRef(instructionPtr));
				}


				while (m_ExecutionStack.Count > 0)
				{
					CallStackItem csi = PopToBasePointer();

					if (csi.ErrorHandler != null)
					{
						instructionPtr = csi.ReturnAddress;

						if (csi.ClrFunction == null)
						{
							var argscnt = (int)(m_ValueStack.Pop().Number);
							m_ValueStack.RemoveLast(argscnt + 1);
						}

						var cbargs = new[] { DynValue.NewString(e.DecoratedMessage) };

						DynValue handled = csi.ErrorHandler.Invoke(new ScriptExecutionContext(this, csi.ErrorHandler, GetCurrentSourceRef(instructionPtr)), cbargs, DynValue.Nil);

						m_ValueStack.Push(handled);

						goto repeat_execution;
					}
					
					if ((csi.Flags & CallStackItemFlags.EntryPoint) != 0)
					{
						e.Rethrow();
						throw;
					}
				}

				e.Rethrow();
				throw;
			}

			return_to_native_code:
			return m_ValueStack.Pop();
		}

		internal string PerformMessageDecorationBeforeUnwind(DynValue messageHandler, string decoratedMessage, SourceRef sourceRef)
		{
			try
			{
				DynValue[] args = { DynValue.NewString(decoratedMessage) };
				DynValue ret;

				if (messageHandler.Type == DataType.Function)
				{
					ret = Call(messageHandler, args);
				}
				else if (messageHandler.Type == DataType.ClrFunction)
				{
					ScriptExecutionContext ctx = new ScriptExecutionContext(this, messageHandler.Callback, sourceRef);
					ret = messageHandler.Callback.Invoke(ctx, args, DynValue.Nil);
				}
				else
				{
					throw new ScriptRuntimeException("error handler not set to a function");
				}

				string newmsg = ret.ToPrintString();
				if (newmsg != null)
					return newmsg;
			}
			catch (ScriptRuntimeException innerEx)
			{
				return innerEx.Message + "\n" + decoratedMessage;
			}

			return decoratedMessage;
		}
		
		private void ExecStoreLcl(Instruction i, ref CallStackItem currentFrame)
		{
			m_ValueStack[currentFrame.BasePointer + (int)i.NumVal3] = GetStoreValue(i);
		}

		private void ExecStoreUpv(Instruction i)
		{
			DynValue value = GetStoreValue(i);
			var stackframe = m_ExecutionStack.Peek();
			stackframe.ClosureScope[(int) i.NumVal3] ??= Upvalue.NewNil();
			stackframe.ClosureScope[(int)i.NumVal3].Value() = value;
		}

		private void ExecSwap(Instruction i)
		{
			DynValue v1 = m_ValueStack.Peek(i.NumVal);
			DynValue v2 = m_ValueStack.Peek(i.NumVal2);

			m_ValueStack.Set(i.NumVal, v2);
			m_ValueStack.Set(i.NumVal2, v1);
		}
		
		private DynValue GetStoreValue(Instruction i)
		{
			int stackofs = i.NumVal;
			int tupleidx = i.NumVal2;

			ref DynValue v = ref m_ValueStack.Peek(stackofs);

			if (v.Type == DataType.Tuple)
			{
				return (tupleidx < v.Tuple.Length) ? v.Tuple[tupleidx] : DynValue.Nil;
			}

			return tupleidx == 0 ? v : DynValue.Nil;
		}

		private void ExecClosure(Instruction i)
		{
			var proto = m_ExecutionStack.Peek().Function.Functions[i.NumVal];
			Closure c = new Closure(m_Script, proto, proto.upvalues,
				proto.upvalues.Select(GetUpvalueSymbol).ToList());
			m_ValueStack.Push(DynValue.NewClosure(c));
		}

		private Upvalue GetUpvalueSymbol(SymbolRef s)
		{
			switch (s.Type)
			{
				case SymbolRefType.Local:
				{
					ref var ex = ref m_ExecutionStack.Peek();
					for (int i = 0; i < ex.OpenClosures?.Count; i++) {
						if (ex.OpenClosures[i].Index == ex.BasePointer + s.i_Index) return ex.OpenClosures[i];
					}
					var upval = new Upvalue(m_ValueStack, ex.BasePointer + s.i_Index);
				
					ex.OpenClosures ??= new List<Upvalue>();
					ex.OpenClosures.Add(upval);
					return upval;
				}
				case SymbolRefType.Upvalue:
					return m_ExecutionStack.Peek().ClosureScope[s.i_Index];
				default:
					throw new Exception("unsupported symbol type");
			}
		}

		private void ExecMkTuple(Instruction i)
		{
			var v = Internal_AdjustTuple(new Slice<DynValue>(m_ValueStack, m_ValueStack.Count - i.NumVal, i.NumVal));
			m_ValueStack.RemoveLast(i.NumVal);
			m_ValueStack.Push(DynValue.NewTuple(v));
		}

		private void ExecIterUpd()
		{
			DynValue v = m_ValueStack.Peek();
			DynValue t = m_ValueStack.Peek(1);
			t.Tuple[2] = v;
		}

		private void ExecExpTuple(Instruction i)
		{
			DynValue t = m_ValueStack.Peek(i.NumVal);

			if (t.Type == DataType.Tuple)
			{
				for (int idx = 0; idx < t.Tuple.Length; idx++)
					m_ValueStack.Push(t.Tuple[idx]);
			}
			else
			{
				m_ValueStack.Push(t);
			}

		}

		private void ExecSetFlags(Instruction i)
		{
			ref DynValue t = ref m_ValueStack.Peek(i.NumVal);
			if (t.Type != DataType.Table)
				throw new InternalErrorException("SETFLAGS called on non-table object");
			t.Table.Members ??= new WattleMembersInfo();
			while (i.NumVal-- > 0) {
				t.Table.Members.i_Modifiers.Add(m_ValueStack.Pop().CastToString(), (MemberModifierFlags)i.NumVal2);
			}
		}

		private void ExecCopyFlags(Instruction i)
		{
			ref DynValue dest = ref m_ValueStack.Peek(1);
			ref DynValue src = ref m_ValueStack.Peek(0);
			if (dest.Type != DataType.Table || src.Type != DataType.Table)
				throw new InternalErrorException("COPYFLAGS called on non-table object");
			dest.Table.Members ??= src.Table.Members;
			m_ValueStack.Pop();
		}

		private void ExecMergeFlags(Instruction i)
		{
			ref DynValue dest = ref m_ValueStack.Peek(i.NumVal2);
			ref DynValue src = ref m_ValueStack.Peek(i.NumVal);
			if (dest.Type != DataType.Table || src.Type != DataType.Table)
				throw new InternalErrorException("MERGEFLAGS called on non-table object");
			if (src.Table.Members != null)
			{
				dest.Table.Members ??= new WattleMembersInfo();
				dest.Table.Members.Merge(src.Table.Members);
			}
		}
		
		private void ExecIterPrep()
		{
			DynValue v = m_ValueStack.Pop();

			if (v.Type != DataType.Tuple)
			{
				v = DynValue.NewTuple(v, DynValue.Nil, DynValue.Nil);
			}

			DynValue f = v.Tuple.Length >= 1 ? v.Tuple[0] : DynValue.Nil;
			DynValue s = v.Tuple.Length >= 2 ? v.Tuple[1] : DynValue.Nil;
			DynValue var = v.Tuple.Length >= 3 ? v.Tuple[2] : DynValue.Nil;

			// WattleScript additions - given f, s, var
			// 1) if f is not a function and has a __iterator metamethod, call __iterator to get the triplet
			// 2) if f is a table with no __call metamethod, use a default table iterator
			if (f.Type != DataType.Function && f.Type != DataType.ClrFunction)
			{
				// Ranges have no metamethods, we can forward this to the top
				if (f.Type == DataType.Range)
				{
					m_ValueStack.Push(EnumerableWrapper.ConvertRange(f.Range));
					return;
				}
				
				DynValue meta = GetMetamethod(f, "__iterator");

				if (!meta.IsNil())
				{
					v = meta.Type != DataType.Tuple ? GetScript().Call(meta, f, s, var) : meta;
					f = v.Tuple.Length >= 1 ? v.Tuple[0] : DynValue.Nil;
					s = v.Tuple.Length >= 2 ? v.Tuple[1] : DynValue.Nil;
					var = v.Tuple.Length >= 3 ? v.Tuple[2] : DynValue.Nil;

					m_ValueStack.Push(DynValue.NewTuple(f, s, var));
					return;
				}
				
				if (f.Type == DataType.Table)
				{
					DynValue callmeta = GetMetamethod(f, "__call");

					if (callmeta.IsNil())
					{
						m_ValueStack.Push(EnumerableWrapper.ConvertTable(f.Table));
						return;
					}
				}
			}

			m_ValueStack.Push(DynValue.NewTuple(f, s, var));
		}

		void ExecAnnotX(string name, DynValue value)
		{
			ref var top = ref m_ValueStack.Peek();
			if (top.Type != DataType.Table) throw new InternalErrorException("v-stack top NOT table");
			var t = top.Table;
			t.Annotations ??= new List<Annotation>();
			t.Annotations.Add(new Annotation(name, value));
		}
		
		private int ExecJFor(Instruction i, int instructionPtr)
		{
			double val = m_ValueStack.Peek().Number;
			double step = m_ValueStack.Peek(1).Number;
			double stop = m_ValueStack.Peek(2).Number;

			bool whileCond = (step > 0) ? val <= stop : val >= stop;

			return !whileCond ? i.NumVal : instructionPtr;
		}

		private void ExecIncr(Instruction i)
		{
			ref DynValue top = ref m_ValueStack.Peek();
			DynValue btm = m_ValueStack.Peek(i.NumVal);

			top = DynValue.NewNumber(top.Number + btm.Number);
		}
		
		private void ExecCNot(Instruction i)
		{
			DynValue v = m_ValueStack.Pop().ToScalar();
			DynValue not = m_ValueStack.Pop().ToScalar();

			if (not.Type != DataType.Boolean)
				throw new InternalErrorException("CNOT had non-bool arg");

			var result = v.CastToBool();
			if (not.CastToBool()) result = !result; //execute CNOT
			if (i.NumVal != 0) result = !result; //merged NOT instruction
			m_ValueStack.Push(DynValue.NewBoolean(result));
		}

		private void ExecNot()
		{
			m_ValueStack.Set(0, DynValue.NewBoolean(!m_ValueStack.Peek().CastToBool()));
		}

		private CallStackItem PopToBasePointer()
		{
			var csi = m_ExecutionStack.Pop();
			if (csi.OpenClosures != null)
				foreach(var closure in csi.OpenClosures) closure.Close();
			if (csi.BasePointer >= 0)
				m_ValueStack.CropAtCount(csi.BasePointer);
			return csi;
		}

		private IList<DynValue> CreateArgsListForFunctionCall(int numargs, int offsFromTop)
		{
			if (numargs <= 0) return Array.Empty<DynValue>();

			DynValue lastParam = m_ValueStack.Peek(offsFromTop);

			if (lastParam.Type == DataType.Tuple && lastParam.Tuple.Length > 1)
			{
				List<DynValue> values = new List<DynValue>();

				for (int idx = 0; idx < numargs - 1; idx++)
					values.Add(m_ValueStack.Peek(numargs - idx - 1 + offsFromTop));

				for (int idx = 0; idx < lastParam.Tuple.Length; idx++)
					values.Add(lastParam.Tuple[idx]);

				return values;
			}

			return new Slice<DynValue>(m_ValueStack, m_ValueStack.Count - numargs - offsFromTop, numargs);
		}
		
		private void ExecArgs(Instruction I)
		{
			var stackframe = m_ExecutionStack.Peek();

			int localCount = stackframe.Function.localCount;
			int numargs = (int)m_ValueStack.Peek(localCount).Number;
			bool implicitThis = false;
			if (numargs < 0)
			{
				numargs = -numargs;
				implicitThis = true;
			}

			// unpacks last tuple arguments to simplify a lot of code down under
			var argsList = CreateArgsListForFunctionCall(numargs, 1 + localCount);

			int offset = 0;
			int startIdx = 0;
			//Skip implicit this passed on stack
			if (implicitThis && (stackframe.Function.flags & FunctionFlags.TakesSelf) != FunctionFlags.TakesSelf) {
				offset = 1;
			}
			//Only fill implicit this argument if method is thiscall.
			if ((stackframe.Function.flags & FunctionFlags.ImplicitThis) == FunctionFlags.ImplicitThis &&
			    (stackframe.Flags & CallStackItemFlags.MethodCall) != CallStackItemFlags.MethodCall)
			{
				m_ValueStack[stackframe.BasePointer + I.NumVal - 1] = DynValue.Nil;
				startIdx = 1; //Fill from 2nd arg (skip self)
				offset = -1; //First argument from list
			}
			
			for (int i = startIdx; i < I.NumVal; i++)
			{
				int argIdx = i + offset;
				if (argIdx >= argsList.Count) {
					//Argument locals are stored in reverse order
					//This is to do with some edge cases in argument naming
					m_ValueStack[stackframe.BasePointer + (I.NumVal - i - 1)] = DynValue.Nil;
				}
				//Make a tuple if the last argument is varargs
				else if (i == I.NumVal - 1 && I.NumVal2 != 0)
				{
					int len = argsList.Count - argIdx;
					DynValue[] varargs = new DynValue[len];

					for (int ii = 0; ii < len; ii++, argIdx++)
					{
						varargs[ii] = argsList[argIdx].ToScalar();
					}
					m_ValueStack[stackframe.BasePointer] = DynValue.NewTuple(Internal_AdjustTuple(varargs));
				}
				else
				{
					m_ValueStack[stackframe.BasePointer + (I.NumVal - i - 1)] = argsList[argIdx].ToScalar();
				}
			}
		}
		
		private int Internal_ExecCall(bool canAwait, int argsCount, int instructionPtr, CallbackFunction handler = null,
			CallbackFunction continuation = null, bool thisCall = false, string debugText = null, DynValue unwindHandler = default)
		{
			bool implicitThis = argsCount < 0;
			if (implicitThis) argsCount = -argsCount;
			
			DynValue fn = m_ValueStack.Peek(argsCount);
			CallStackItemFlags flags = (thisCall ? CallStackItemFlags.MethodCall : CallStackItemFlags.None);

			//Functions fetched from metatables transform implicit
			//thiscall to explicit thiscall.
			if (fn.FromMetatable && implicitThis) {
				implicitThis = false; 
				thisCall = true;
			}
			
			// if TCO threshold reached
			if ((m_ExecutionStack.Count > m_Script.Options.TailCallOptimizationThreshold && m_ExecutionStack.Count > 1)
				|| (m_ValueStack.Count > m_Script.Options.TailCallOptimizationThreshold && m_ValueStack.Count > 1))
			{
				var code = m_ExecutionStack.Peek().Function.code;
				// and the "will-be" return address is valid (we don't want to crash here)
				if (instructionPtr >= 0 && instructionPtr < code.Length)
				{
					Instruction I = code[instructionPtr];

					// and we are followed *exactly* by a RET 1
					if (I.OpCode == OpCode.Ret && I.NumVal == 1)
					{
						ref CallStackItem csi = ref m_ExecutionStack.Peek();

						// if the current stack item has no "odd" things pending and neither has the new coming one..
						if (csi.ClrFunction == null && csi.Continuation == null && csi.ErrorHandler == null
							&& csi.ErrorHandlerBeforeUnwind.IsNil() && continuation == null && unwindHandler.IsNil() && handler == null)
						{
							instructionPtr = PerformTco(argsCount);
							flags |= CallStackItemFlags.TailCall;
						}
					}
				}
			}
			
			switch (fn.Type)
			{
				case DataType.ClrFunction:
				{
					IList<DynValue> args = CreateArgsListForFunctionCall(implicitThis ? (argsCount - 1) : argsCount, 0);
					DynValue thisObj = DynValue.Nil;
					if (implicitThis) thisObj = m_ValueStack[m_ValueStack.Count - argsCount];
					// we expand tuples before callbacks
					// args = DynValue.ExpandArgumentsToList(args);

					// instructionPtr - 1: instructionPtr already points to the next instruction at this moment
					// but we need the current instruction here
					SourceRef sref = GetCurrentSourceRef(instructionPtr - 1);

					m_ExecutionStack.Push(new CallStackItem()
					{
						ClrFunction = fn.Callback,
						ReturnAddress = instructionPtr,
						CallingSourceRef = sref,
						BasePointer = -1,
						ErrorHandler = handler,
						Continuation = continuation,
						ErrorHandlerBeforeUnwind = unwindHandler,
						Flags = flags,
					});
					
					var ret = fn.Callback.Invoke(new ScriptExecutionContext(this, fn.Callback, sref) { CanAwait = canAwait }, args, thisObj, isMethodCall: thisCall && !implicitThis);
					m_ValueStack.RemoveLast(argsCount + 1);
					if (m_Script.Options.AutoAwait && 
					    ret.Type == DataType.UserData &&
					    ret.UserData?.Object is TaskWrapper tw)
					{
						ret = tw.await(
							new ScriptExecutionContext(this, fn.Callback, sref) {CanAwait = canAwait}, null);
					}
					m_ValueStack.Push(ret);

					m_ExecutionStack.Pop();

					return Internal_CheckForTailRequests(canAwait, instructionPtr);
				}
				case DataType.Function:
				{
					m_ValueStack.Push(DynValue.NewNumber(implicitThis ? -argsCount : argsCount));
					m_ExecutionStack.Push(new CallStackItem()
					{
						ReturnAddress = instructionPtr,
						Function = fn.Function.Function,
						CallingSourceRef = GetCurrentSourceRef(instructionPtr - 1), // See right above in GetCurrentSourceRef(instructionPtr - 1)
						ClosureScope = fn.Function.ClosureContext,
						ErrorHandler = handler,
						Continuation = continuation,
						ErrorHandlerBeforeUnwind = unwindHandler,
						Flags = flags,
						//Reserve stack space for locals
						BasePointer = m_ValueStack.Reserve(fn.Function.Function.localCount)
					});
					return 0;
				}
			}

			// fallback to __call metamethod
			var m = GetMetamethod(fn, "__call");

			if (m.IsNotNil())
			{
				DynValue[] tmp = new DynValue[argsCount + 1];
				for (int i = 0; i < argsCount + 1; i++)
					tmp[i] = m_ValueStack.Pop();

				m_ValueStack.Push(m);

				for (int i = argsCount; i >= 0; i--)
					m_ValueStack.Push(tmp[i]);

				return Internal_ExecCall(canAwait, argsCount + 1, instructionPtr, handler, continuation);
			}

			throw ScriptRuntimeException.AttemptToCallNonFunc(fn.Type, debugText);
		}

		private int PerformTco(int argsCount)
		{
			DynValue[] args = new DynValue[argsCount + 1];

			// Remove all cur args and func ptr
			for (int i = 0; i <= argsCount; i++)
				args[i] = m_ValueStack.Pop();

			// perform a fake RET
			CallStackItem csi = PopToBasePointer();
			int retpoint = csi.ReturnAddress;
			var argscnt = (int)(m_ValueStack.Pop().Number);
			if (argscnt < 0) argscnt = -argscnt;
			m_ValueStack.RemoveLast(argscnt + 1);

			// Re-push all cur args and func ptr
			for (int i = argsCount; i >= 0; i--)
				m_ValueStack.Push(args[i]);

			return retpoint;
		}
		
		private int ExecNilCoalescingAssignment(int instructionPtr)
		{
			ref DynValue lhs = ref m_ValueStack.Peek(1);
			if (lhs.IsNil()) 
			{
				m_ValueStack.Set(1, m_ValueStack.Peek());
			}
			
			m_ValueStack.Pop();
			return instructionPtr;
		}
		
		private int ExecNilCoalescingAssignmentInverse(int instructionPtr)
		{
			ref DynValue lhs = ref m_ValueStack.Peek(1);
			if (lhs.IsNotNil()) 
			{
				m_ValueStack.Set(1, m_ValueStack.Peek());
			}
			
			m_ValueStack.Pop();
			return instructionPtr;
		}

		private int ExecRet(Instruction i, int currentPtr)
		{
			CallStackItem csi;
			int retpoint;
			
			switch (i.NumVal)
			{
				case 0:
				{
					csi = PopToBasePointer();
					retpoint = csi.ReturnAddress;
					var argscnt = (int)(m_ValueStack.Pop().Number);
					if (argscnt < 0) argscnt = -argscnt;
					m_ValueStack.RemoveLast(argscnt + 1);
					m_ValueStack.Push(DynValue.Void);
					break;
				}
				case 1:
				{
					var retval = m_ValueStack.Pop();
					csi = PopToBasePointer();
					retpoint = csi.ReturnAddress;
					var argscnt = (int)(m_ValueStack.Pop().Number);
					if (argscnt < 0) argscnt = -argscnt;
					m_ValueStack.RemoveLast(argscnt + 1);
					m_ValueStack.Push(retval);
					retpoint = Internal_CheckForTailRequests(false, retpoint);
					break;
				}
				default:
					throw new InternalErrorException("RET supports only 0 and 1 ret val scenarios");
			}

			if (csi.Continuation != null)
				m_ValueStack.Push(csi.Continuation.Invoke(new ScriptExecutionContext(this, csi.Continuation, csi.Function.sourceRefs[currentPtr]),
					new[] { m_ValueStack.Pop() }, DynValue.Nil));

			return retpoint;
		}
		
		private int Internal_CheckForTailRequests(bool canAwait, int instructionPtr)
		{
			DynValue tail = m_ValueStack.Peek();

			switch (tail.Type)
			{
				case DataType.TailCallRequest:
				{
					m_ValueStack.Pop(); // discard tail call request

					TailCallData tcd = tail.TailCallData;

					m_ValueStack.Push(tcd.Function);

					for (int ii = 0; ii < tcd.Args.Length; ii++)
						m_ValueStack.Push(tcd.Args[ii]);

					return Internal_ExecCall(canAwait, tcd.Args.Length, instructionPtr, tcd.ErrorHandler, tcd.Continuation, false, null, tcd.ErrorHandlerBeforeUnwind);
				}
				case DataType.YieldRequest:
					m_SavedInstructionPtr = instructionPtr;
					return YIELD_SPECIAL_TRAP;
				case DataType.AwaitRequest when !canAwait:
					throw new ScriptRuntimeException(
						"Await Request happened when it shouldn't have. Internal state corruption?");
				case DataType.AwaitRequest:
					m_SavedInstructionPtr = instructionPtr;
					return YIELD_SPECIAL_AWAIT;
				default:
					return instructionPtr;
			}
		}
		
		private int JumpBool(Instruction i, bool expectedValueForJump, int instructionPtr)
		{
			DynValue op = m_ValueStack.Pop();

			return op.CastToBool() == expectedValueForJump ? i.NumVal : instructionPtr;
		}

		private int ExecShortCircuitingOperator(Instruction i, int instructionPtr)
		{
			bool expectedValToShortCircuit = i.OpCode == OpCode.JtOrPop;

			DynValue op = m_ValueStack.Peek();

			if (op.CastToBool() == expectedValToShortCircuit)
			{
				return i.NumVal;
			}

			m_ValueStack.Pop();
			return instructionPtr;
		}
		
		private void ExecBNot()
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var ln))
			{
				m_ValueStack.Set(0, DynValue.NewNumber(~(int)ln)); 
			}
			else
			{
				var l = m_ValueStack.Pop().ToScalar();
				throw ScriptRuntimeException.ArithmeticOnNonNumber(l);
			}
		}

		private int ExecSwitch(int currentPtr, Instruction i, ref CallStackItem cframe)
		{
			//Decode
			//First 3 table entries are present with bit flags
			int nil = -1, ctrue = -1, cfalse = -1;
			int strOff = 1;
			uint strCount = (uint)i.NumVal;
			if ((strCount & 0x80000000) != 0)
				nil = strOff++;
			if ((strCount & 0x40000000) != 0)
				ctrue = strOff++;
			if ((strCount & 0x20000000) != 0)
				cfalse = strOff++;
			//clear flags to get count of string entries
			strCount &= 0x1FFFFFFF;
			//get number entries
			int numOff = (int) (strOff + strCount);
			uint numCount = i.NumValB;
			//default comes immediately after switch case
			int defaultPtr = (int)(currentPtr + numOff + numCount);
			int GetJump(FunctionProto p, int offset) => (int) (currentPtr + (p.code[currentPtr + offset].NumValB));
			var value = m_ValueStack.Pop().ToScalar();
			switch (value.Type)
			{
				case DataType.Nil:
					return nil != -1 ? GetJump(cframe.Function, nil) : defaultPtr;
				case DataType.Boolean:
					if (value.Boolean) {
						return ctrue != -1 ? GetJump(cframe.Function, ctrue) : defaultPtr;
					}
					else {
						return cfalse != -1 ? GetJump(cframe.Function, cfalse) : defaultPtr;
					}
				case DataType.Number:
					var d = value.Number;
					for (int j = 0; j < numCount; j++)
					{
						var ins = cframe.Function.code[currentPtr + numOff + j];
						if (ins.OpCode == OpCode.SInteger && ins.NumVal == d) {
							return (int) (currentPtr + ins.NumValB);
						}
						if (ins.OpCode == OpCode.SNumber && d == cframe.Function.numbers[ins.NumVal]) {
							return (int) (currentPtr + ins.NumValB);
						}
					}
					return defaultPtr;
				case DataType.String:
					var s = value.String;
					for (int j = 0; j < strCount; j++)
					{
						var ins = cframe.Function.code[currentPtr + strOff + j];
						if (s == cframe.Function.strings[ins.NumVal]) {
							return (int) (currentPtr + ins.NumValB);
						}
					}
					return defaultPtr;
				default:
					return defaultPtr;
			}
		}

		private void ExecBAnd()
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber((int)ln & (int)rn)); 
			}
			else
			{
				var r = m_ValueStack.Pop().ToScalar();
				var l = m_ValueStack.Pop().ToScalar();
				throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
			}
		}
		
		private void ExecBOr()
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber((int)ln | (int)rn)); 
			}
			else
			{
				var r = m_ValueStack.Pop().ToScalar();
				var l = m_ValueStack.Pop().ToScalar();
				throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
			}
		}
		
		private void ExecBXor()
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber((int)ln ^ (int)rn)); 
			}
			else
			{
				var r = m_ValueStack.Pop().ToScalar();
				var l = m_ValueStack.Pop().ToScalar();
				throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
			}
		}
		
		private void ExecBlShift()
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber((int)ln << (int)rn)); 
			}
			else
			{
				var r = m_ValueStack.Pop().ToScalar();
				var l = m_ValueStack.Pop().ToScalar();
				throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
			}
		}
		
		private void ExecBrShiftA()
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber((int)ln >> (int)rn)); 
			}
			else
			{
				var r = m_ValueStack.Pop().ToScalar();
				var l = m_ValueStack.Pop().ToScalar();
				throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
			}
		}
		
		private void ExecBrShiftL()
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber((int)(
					(uint)ln >> (int)rn
				))); 
			}
			else
			{
				var r = m_ValueStack.Pop().ToScalar();
				var l = m_ValueStack.Pop().ToScalar();
				throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
			}
		}
		

		private int ExecAdd(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(ln + rn));
				return instructionPtr;
			}

			return ExecBinaryOp(instructionPtr, "__add");
		}

		bool ToConcatString(ref DynValue v, out string s, ref int metamethodCounter)
		{
			if (v.IsNil()) {
				s = null;
				return false;
			}
			
			switch (v.Type)
			{
				case DataType.String:
					s = v.String;
					return true;
				case DataType.Boolean:
					s = v.Boolean ? "true" : "false";
					return true;
				case DataType.Number:
					s = v.Number.ToString(CultureInfo.InvariantCulture);
					return true;
			}

			var m = GetMetamethod(v, "__tostring");
			if (!m.IsNil())
			{
				if (metamethodCounter++ > 10) {
					s = null;
					return false;
				}
				var retval = Call(m, new[] {v});
				return ToConcatString(ref retval, out s, ref metamethodCounter);
			}

			s = null;
			return false;
		}

		private void ExecStrFormat(Instruction i)
		{
			string[] formatValues = new string[i.NumVal];
			if (i.NumVal > 0)
			{
				for (int j = 0; j < i.NumVal; j++)
				{
					var off = (i.NumVal - j - 1);
					int mCount = 0;
					if (!ToConcatString(ref m_ValueStack.Peek(off), out formatValues[j], ref mCount))
					{
						formatValues[j] = m_ValueStack.Peek(off).ToPrintString();
					}
				}
			}
			m_ValueStack.RemoveLast(i.NumVal);
			// ReSharper disable once CoVariantArrayConversion
			m_ValueStack.Set(0, DynValue.NewString(string.Format(m_ValueStack.Peek().String, formatValues)));
		}
		
		private int ExecBinaryOp(int instructionPtr, string metaMethodName)
		{
			var r = m_ValueStack.Pop().ToScalar();
			var l = m_ValueStack.Pop().ToScalar();
			int ip = Internal_InvokeBinaryMetaMethod(l, r, metaMethodName, instructionPtr);
			if (ip >= 0) return ip;
			throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
		}
		
		private int ExecAddStr(int instructionPtr)
		{
			ref DynValue dvA = ref m_ValueStack.Peek();
			ref DynValue dvB = ref m_ValueStack.Peek(1);
			DataType dvAType = dvA.Type;
			DataType dvBType = dvB.Type;

			if (dvA.TryGetNumber(out double rn) && dvB.TryGetNumber(out double ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(ln + rn));
				return instructionPtr;
			}

			if (dvAType == DataType.String || dvBType == DataType.String)
			{
				int c1 = 0, c2 = 0;
				if (!ToConcatString(ref m_ValueStack.Peek(), out var rhs, ref c1) ||
				    !ToConcatString(ref m_ValueStack.Peek(1), out var lhs, ref c2))
				{
					var l = m_ValueStack.Pop().ToScalar();
					var r = m_ValueStack.Pop().ToScalar();
					throw ScriptRuntimeException.ConcatOnNonString(l, r);
				}
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewString(lhs + rhs));
				return instructionPtr;
			}

			return ExecBinaryOp(instructionPtr, "__add");
		}

		private int ExecSub(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(ln - rn));
				return instructionPtr;
			}
			
			return ExecBinaryOp(instructionPtr, "__sub");
		}


		private int ExecMul(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(ln * rn));
				return instructionPtr;
			}
			
			return ExecBinaryOp(instructionPtr, "__mul");
		}

		private int ExecMod(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				var mod = ln - Math.Floor(ln / rn) * rn;
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(mod));
				return instructionPtr;
			}
			
			return ExecBinaryOp(instructionPtr, "__mod");
		}

		private int ExecDiv(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(ln / rn));
				return instructionPtr;
			}
			
			return ExecBinaryOp(instructionPtr, "__div");
		}
		private int ExecPower(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && 
			    m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(Math.Pow(ln,rn)));
				return instructionPtr;
			}
			
			return ExecBinaryOp(instructionPtr, "__pow");
		}

		private int ExecNeg(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn))
			{
				m_ValueStack.Set(0, DynValue.NewNumber(-rn));
				return instructionPtr;
			}

			DynValue r = m_ValueStack.Pop().ToScalar();
			int ip = Internal_InvokeUnaryMetaMethod(r, "__unm", instructionPtr);
			if (ip >= 0) return ip;
			throw ScriptRuntimeException.ArithmeticOnNonNumber(r);
		}


		
		private int ExecEq(int instructionPtr, Instruction i)
		{
			DynValue r = m_ValueStack.Pop().ToScalar();
			DynValue l = m_ValueStack.Pop().ToScalar();

			bool invert = i.NumVal != 0; //elide NOT
			
			// if they are userdatas, attempt meta
			if (l.Type == DataType.UserData || r.Type == DataType.UserData)
			{
				int ip = Internal_InvokeBinaryMetaMethod(l, r, "__eq", instructionPtr);
				if (ip >= 0) return ip;
			}

			// then if types are different, ret false
			if (r.Type != l.Type)
			{
				if ((l.Type == DataType.Nil && r.Type == DataType.Void) || (l.Type == DataType.Void && r.Type == DataType.Nil))
					m_ValueStack.Push(invert ? DynValue.False: DynValue.True);
				else
					m_ValueStack.Push(invert ? DynValue.True : DynValue.False);

				return instructionPtr + 1; //Skip TOBOOL/NOT
			}

			// then attempt metatables for tables
			if ((l.Type == DataType.Table) && (GetMetatable(l) != null) && (GetMetatable(l) == GetMetatable(r)))
			{
				int ip = Internal_InvokeBinaryMetaMethod(l, r, "__eq", instructionPtr);
				if (ip >= 0) return ip;
			}

			// else perform standard comparison
			bool result = r.Equals(l);
			if (invert) result = !result;
			m_ValueStack.Push(DynValue.NewBoolean(result));
			return instructionPtr + 1; //Skip TOBOOL/NOT
		}

		private int ExecLess(int instructionPtr, Instruction i)
		{
			DynValue r = m_ValueStack.Pop().ToScalar();
			DynValue l = m_ValueStack.Pop().ToScalar();
			bool invert = i.NumVal != 0;
			if (l.Type == DataType.Number && r.Type == DataType.Number)
			{
				var res = l.Number < r.Number;
				if (invert) res = !res;
				m_ValueStack.Push(DynValue.NewBoolean(res));
			}
			else if (l.Type == DataType.String && r.Type == DataType.String)
			{
				var res = string.Compare(l.String, r.String, StringComparison.Ordinal) < 0;
				if (invert) res = !res;
				m_ValueStack.Push(DynValue.NewBoolean(res));
			}
			else
			{
				int ip = Internal_InvokeBinaryMetaMethod(l, r, "__lt", instructionPtr);
				if (ip < 0)
					throw ScriptRuntimeException.CompareInvalidType(l, r);
				return ip;
			}

			return instructionPtr + 1; //Skip TOBOOL/NOT
		}
		
		private int ExecLessEq(int instructionPtr, Instruction i)
		{
			DynValue r = m_ValueStack.Pop().ToScalar();
			DynValue l = m_ValueStack.Pop().ToScalar();
			bool invert = i.NumVal != 0;

			switch (l.Type)
			{
				case DataType.Number when r.Type == DataType.Number:
				{
					var res = l.Number <= r.Number;
					if (invert) res = !res;
					m_ValueStack.Push(DynValue.NewBoolean(res));
					return instructionPtr + 1; //skip CNOT
				}
				case DataType.String when r.Type == DataType.String:
				{
					var res = string.Compare(l.String, r.String, StringComparison.Ordinal) <= 0;
					if (invert) res = !res;
					m_ValueStack.Push(DynValue.NewBoolean(res));
					return instructionPtr + 1; //skip CNOT
				}
				default:
				{
					int ip = Internal_InvokeBinaryMetaMethod(l, r, "__le", instructionPtr, DynValue.False);
					if (ip < 0)
					{
						ip = Internal_InvokeBinaryMetaMethod(r, l, "__lt", instructionPtr, DynValue.True);

						if (ip < 0)
							throw ScriptRuntimeException.CompareInvalidType(l, r);
						return ip;
					}
				
					return ip;
				}
			}
		}

		private int ExecLen(int instructionPtr)
		{
			DynValue r = m_ValueStack.Pop().ToScalar();

			if (r.Type == DataType.String)
				m_ValueStack.Push(DynValue.NewNumber(r.String.Length));
			else
			{
				int ip = Internal_InvokeUnaryMetaMethod(r, "__len", instructionPtr);
				if (ip >= 0)
					return ip;
				if (r.Type == DataType.Table)
					m_ValueStack.Push(DynValue.NewNumber(r.Table.Length));
				else throw ScriptRuntimeException.LenOnInvalidType(r);
			}

			return instructionPtr;
		}
		
		private int ExecConcat(int instructionPtr)
		{
			DynValue r = m_ValueStack.Pop().ToScalar();
			DynValue l = m_ValueStack.Pop().ToScalar();

			string rs = r.CastToString();
			string ls = l.CastToString();

			if (rs != null && ls != null)
			{
				m_ValueStack.Push(DynValue.NewString(ls + rs));
				return instructionPtr;
			}
			
			int ip = Internal_InvokeBinaryMetaMethod(l, r, "__concat", instructionPtr);
			if (ip >= 0) return ip;
			throw ScriptRuntimeException.ConcatOnNonString(l, r);
		}
		
		private void ExecTblInitI(Instruction i)
		{
			// stack: tbl - val,val,val
			bool lastPos = (i.NumVal3 & 0x80) == 0x80;
			DynValue tbl = (i.NumVal3 & 0x7F) switch
			{
				0 => m_ValueStack.Peek(i.NumVal2),
				1 => DynValue.NewTable(m_Script),
				2 => DynValue.NewPrimeTable(),
				_ => throw new InternalErrorException("TblInitI NumVal3 invalid")
			};
			if (tbl.Type != DataType.Table)
				throw new InternalErrorException("Unexpected type in table ctor : {0}", tbl);
			var index = i.NumVal + m_Script.Options.IndexTablesFrom;
			for (int j = i.NumVal2 - 1; j >= 0; j--)
			{
				ref var val = ref m_ValueStack.Peek(j);
				if (lastPos && val.Type == DataType.Tuple) {
					foreach(var v in val.Tuple)
						tbl.Table.Set(index++, v);
				}
				else
				{
					tbl.Table.Set(index++, val.ToScalar());
				}
			}
			m_ValueStack.RemoveLast(i.NumVal2);
			if ((i.NumVal3 & 0x7F) > 0) m_ValueStack.Push(tbl);
		}

		private int ExecNewCall(Instruction i, int instructionPtr, bool canAwait)
		{
			ref var cls = ref m_ValueStack.Peek(i.NumVal);
			if (cls.Type != DataType.Table ||
			    cls.Table.Kind != TableKind.Class)
				throw ScriptRuntimeException.NotAClass(GetString((int) i.NumValB), cls);
			if (cls.Table.ModifierFlags.HasFlag(MemberModifierFlags.Static))
				throw ScriptRuntimeException.NewCallIncompatibleModifier(MemberModifierFlags.Static.ToString().ToLowerInvariant(), GetString((int) i.NumValB), cls);
			cls = cls.Table.Get("new");
			return Internal_ExecCall(canAwait, i.NumVal, instructionPtr);
		}

		private void ExecMixInit(Instruction i)
		{
			ref var cls_index = ref m_ValueStack.Peek(2);
			ref var mixin_tab = ref m_ValueStack.Peek(1);
			var mix = m_ValueStack.Pop();
			if (mix.Type != DataType.Table ||
			    mix.Table.Kind != TableKind.Mixin)
				throw ScriptRuntimeException.NotAMixin(GetString((int) i.NumVal), mix);
			//copy functions into table
			var funcs = mix.Table.Get("functions").Table;
			foreach (var f in funcs.Pairs) {
				if (cls_index.Table.Get(f.Key).IsNil()) {
					cls_index.Table.Set(f.Key, f.Value);
				}
			}
			//copy init() into mixin table
			mixin_tab.Table.Set(GetString((int) i.NumVal), mix.Table.Get("init"));
		}

		private void ExecNewRange(Instruction i)
		{
			if (i.NumVal3 == 1) // from v-stack
			{
				DynValue toDv = m_ValueStack.Pop();
				DynValue fromDv = m_ValueStack.Pop();

				int? fromInt = fromDv.CastToInt();
				int? toInt = toDv.CastToInt();

				if (fromInt == null)
				{
					throw ScriptRuntimeException.NewRangeBadValue("from", fromDv.Type.ToLuaTypeString());
				}
			
				if (toInt == null)
				{
					throw ScriptRuntimeException.NewRangeBadValue("to", toDv.Type.ToLuaTypeString());
				}
				
				m_ValueStack.Push(DynValue.NewRange(new Range(m_Script, fromInt.Value, toInt.Value)));	
			}
			else // from numVal, numVal2
			{
				m_ValueStack.Push(DynValue.NewRange(new Range(m_Script, i.NumVal, i.NumVal2)));	
			}
		}
		
		private void ExecTblInitN(Instruction i)
		{
			// stack: tbl - key - val
			DynValue tbl = i.NumVal2 switch
			{
				0 => m_ValueStack.Peek(i.NumVal),
				1 => DynValue.NewTable(m_Script),
				2 => DynValue.NewPrimeTable(),
				_ => throw new InternalErrorException("TblInitN NumVal2 invalid")
			};			
			if (tbl.Type != DataType.Table)
				throw new InternalErrorException("Unexpected type in table ctor : {0}", tbl);
			if (i.NumVal % 2 != 0)
				throw new InternalErrorException("Incorrect count in TblInitN: {0}", i.NumVal);
			for (int j = i.NumVal - 1; j >= 0; j -= 2) {
				tbl.Table.Set(m_ValueStack.Peek(j), m_ValueStack.Peek(j - 1).ToScalar());
			}
			m_ValueStack.RemoveLast(i.NumVal);
			
			if (i.NumVal2 > 0) m_ValueStack.Push(tbl);
		}

		private int ExecIndexSet(Instruction i, int instructionPtr)
		{
			int nestedMetaOps = 100; // sanity check, to avoid potential infinite loop here

			// stack: vals.. - base - index
			bool isNameIndex = i.OpCode == OpCode.IndexSetN;
			bool isMultiIndex = (i.OpCode == OpCode.IndexSetL);
			// extract privateAccess bool from spare bit in numVal
			bool accessPrivate = (i.NumVal & 0x40000000) != 0;
			i.NumVal &= 0x3FFFFFFF;
			string i_str = GetString((int)i.NumVal3);
			DynValue originalIdx = i_str != null ? DynValue.NewString(i_str) : m_ValueStack.Pop();
			DynValue idx = originalIdx.ToScalar();
			DynValue obj = m_ValueStack.Pop().ToScalar();
			DynValue value = GetStoreValue(i);
			
			while (nestedMetaOps > 0)
			{
				--nestedMetaOps;

				DynValue h;
				switch (obj.Type)
				{
					case DataType.Table:
					{
						if (obj.Table.ReadOnly) throw ScriptRuntimeException.TableIsReadonly();

						if (!isMultiIndex)
						{
							//Private fields - error on write
							if (!accessPrivate && obj.Table.Members != null &&
							    obj.Table.Members.MemberHasModifier(idx, MemberModifierFlags.Private))
							{
								throw new ScriptRuntimeException($"cannot write to private key '{idx.ToPrintString()}'");
							}
							//Don't do check for __newindex if there is no metatable to begin with
							if (obj.Table.MetaTable == null || !obj.Table.Get(idx).IsNil())
							{
								obj.Table.Set(idx, value);
								return instructionPtr;
							}
						}

						h = GetMetamethodRaw(obj, "__newindex");

						if (h.IsNil())
						{
							if (isMultiIndex) throw new ScriptRuntimeException("cannot multi-index a table. userdata expected");

							obj.Table.Set(idx, value);
							return instructionPtr;
						}

						break;
					}
					case DataType.UserData:
					{
						UserData ud = obj.UserData;

						if (!ud.Descriptor.SetIndex(GetScript(), ud.Object, originalIdx, value, isNameIndex))
						{
							throw ScriptRuntimeException.UserDataMissingField(ud.Descriptor.Name, idx.String);
						}

						return instructionPtr;
					}
					case DataType.Range:
					{
						int? valInt = value.CastToInt();

						if (valInt == null)
						{
							throw ScriptRuntimeException.ExistingRangeBadValueAssigned(obj.Range, value.Type.ToLuaTypeString());
						}
						
						switch (idx.String)
						{
							case "to":
								obj.Range.To = value.CastToInt() ?? 0;
								return instructionPtr;
							case "from":
								obj.Range.From = value.CastToInt() ?? 0;
								return instructionPtr;
						}

						throw ScriptRuntimeException.ExistingRangeBadPropertyAssigned(idx.String);
					}
					default:
					{
						h = GetMetamethodRaw(obj, "__newindex");

						if (h.IsNil())
							throw ScriptRuntimeException.IndexType(obj);
						break;
					}
				}

				if (h.Type == DataType.Function || h.Type == DataType.ClrFunction)
				{
					if (isMultiIndex) throw new ScriptRuntimeException("cannot multi-index through metamethods. userdata expected");
					m_ValueStack.Pop(); // burn extra value ?

					m_ValueStack.Push(h);
					m_ValueStack.Push(obj);
					m_ValueStack.Push(idx);
					m_ValueStack.Push(value);
					return Internal_ExecCall(false, 3, instructionPtr);
				}

				obj = h;
			}
			throw ScriptRuntimeException.LoopInNewIndex();
		}

		private int ExecIndex(Instruction i, int instructionPtr)
		{
			int nestedMetaOps = 100; // sanity check, to avoid potential infinite loop here

			// stack: base - index
			bool isNameIndex = i.OpCode == OpCode.IndexN;
			bool isMultiIndex = i.OpCode == OpCode.IndexL;
			bool accessPrivate = i.NumVal3 > 0;

			string i_str = GetString(i.NumVal);
			DynValue originalIdx = i_str != null ? DynValue.NewString(i_str) : m_ValueStack.Pop();
			DynValue idx = originalIdx.ToScalar();
			DynValue obj = m_ValueStack.Pop().ToScalar();

			bool setFromMT = false; 
			
			while (nestedMetaOps > 0)
			{
				--nestedMetaOps;

				DynValue h;
				switch (obj.Type)
				{
					case DataType.Table:
					{
						if (!isMultiIndex)
						{
							//Don't return private fields
							if (!accessPrivate && obj.Table.Members != null &&
							    obj.Table.Members.MemberHasModifier(idx, MemberModifierFlags.Private))
							{
								m_ValueStack.Push(DynValue.Nil);
								return instructionPtr;
							}
								
							var v = obj.Table.Get(idx);

							if (!v.IsNil())
							{
								if (setFromMT) v.FromMetatable = true;
								m_ValueStack.Push(v);
								return instructionPtr;
							}
						}

						h = GetMetamethodRaw(obj, "__index");

						if (h.IsNil())
						{
							if (isMultiIndex) throw new ScriptRuntimeException("cannot multi-index a table. userdata expected");

							//Check prototype for method call syntax
							if (i.NumVal2 != 0)
							{
								var tb = m_Script.GetTablePrototype();
								if (tb != null)
								{
									var v = tb.Get(idx);
									v.FromMetatable = true;
									m_ValueStack.Push(v);
								}
								else
								{
									//No prototype, nil
									m_ValueStack.Push(DynValue.Nil);
								}
							}
							else
							{
								//Not a wattle method call, don't check prototype
								m_ValueStack.Push(DynValue.Nil);
							}
							return instructionPtr;
						}

						break;
					}
					case DataType.UserData:
					{
						UserData ud = obj.UserData;

						var v = ud.Descriptor.Index(GetScript(), ud.Object, originalIdx, isNameIndex);

						if (v.IsVoid())
						{
							throw ScriptRuntimeException.UserDataMissingField(ud.Descriptor.Name, idx.String);
						}

						m_ValueStack.Push(v);
						return instructionPtr;
					}
					case DataType.Range:
					{
						if (idx.String == "to")
						{
							m_ValueStack.Push(DynValue.NewNumber(obj.Range.To));
							return instructionPtr;
						}
						
						if (idx.String == "from")
						{
							m_ValueStack.Push(DynValue.NewNumber(obj.Range.From));
							return instructionPtr;
						}
						
						goto default;
					}
					default:
					{
						h = GetMetamethodRaw(obj, "__index");

						if (h.IsNil())
							throw ScriptRuntimeException.IndexType(obj);
						break;
					}
				}

				if (h.Type == DataType.Function || h.Type == DataType.ClrFunction)
				{
					if (isMultiIndex) throw new ScriptRuntimeException("cannot multi-index through metamethods. userdata expected");
					m_ValueStack.Push(h);
					m_ValueStack.Push(obj);
					m_ValueStack.Push(idx);
					return Internal_ExecCall(false, 2, instructionPtr);
				}

				obj = h;
				setFromMT = i.NumVal2 != 0;
			}

			throw ScriptRuntimeException.LoopInIndex();
		}
	}
}
