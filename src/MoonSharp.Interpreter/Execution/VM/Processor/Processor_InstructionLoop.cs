using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MoonSharp.Interpreter.DataStructs;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Interop;

namespace MoonSharp.Interpreter.Execution.VM
{
	sealed partial class Processor
	{
		const int YIELD_SPECIAL_TRAP = -99;
		const int YIELD_SPECIAL_AWAIT = -100;

		internal long AutoYieldCounter = 0;

		private DynValue Processing_Loop(int instructionPtr, bool canAwait = false)
		{
			// This is the main loop of the processor, has a weird control flow and needs to be as fast as possible.
			// This sentence is just a convoluted way to say "don't complain about gotos".
			long executedInstructions = 0;
			bool canAutoYield = AutoYieldCounter > 0 && m_CanYield && State != CoroutineState.Main;

			repeat_execution:

			try
			{
				while (true)
				{
					Instruction i = m_RootChunk.Code[instructionPtr];
					int currentPtr = instructionPtr;
					if (m_Debug.DebuggerAttached != null)
					{
						ListenDebugger(i, instructionPtr);
					}

					++executedInstructions;

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
						case OpCode.Meta:
							break;
						case OpCode.Pop:
							m_ValueStack.RemoveLast(i.NumVal);
							break;
						case OpCode.Copy:
							m_ValueStack.Push(m_ValueStack.Peek(i.NumVal));
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
						case OpCode.PushNumber:
							m_ValueStack.Push(DynValue.NewNumber(i.Number));
							break;
						case OpCode.PushString:
							m_ValueStack.Push(DynValue.NewString(i.String));
							break;
						case OpCode.Add:
							instructionPtr = ExecAdd(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Concat:
							instructionPtr = ExecConcat(i, instructionPtr);
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
							instructionPtr = ExecEq(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.LessEq:
							instructionPtr = ExecLessEq(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Less:
							instructionPtr = ExecLess(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Len:
							instructionPtr = ExecLen(instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Call:
						case OpCode.ThisCall:
							instructionPtr = Internal_ExecCall(canAwait, i.NumVal, instructionPtr, null, null, i.OpCode == OpCode.ThisCall, i.String);
							switch (instructionPtr)
							{
								case YIELD_SPECIAL_TRAP:
									goto yield_to_calling_coroutine;
								case YIELD_SPECIAL_AWAIT:
									goto yield_to_await;
							}

							break;
						case OpCode.Scalar:
							m_ValueStack.Push(m_ValueStack.Pop().ToScalar());
							break;
						case OpCode.Not:
							ExecNot(i);
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
						case OpCode.Jf:
							instructionPtr = JumpBool(i, false, instructionPtr);
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.Jump:
							instructionPtr = i.NumVal;
							if (instructionPtr == YIELD_SPECIAL_TRAP) goto yield_to_calling_coroutine;
							break;
						case OpCode.MkTuple:
							ExecMkTuple(i.NumVal);
							break;
						case OpCode.Clean:
							ClearBlockData(i);
							break;
						case OpCode.Closure:
							ExecClosure(i);
							break;
						case OpCode.BeginFn:
							ExecBeginFn(i);
							break;
						case OpCode.ToBool:
						{
							ref var top = ref m_ValueStack.Peek();
							top = DynValue.NewBoolean(top.CastToBool());
							break;
						}
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
						case OpCode.NewTable:
							m_ValueStack.Push(i.NumVal == 0 ? DynValue.NewTable(m_Script) : DynValue.NewPrimeTable());
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
							var scope = m_ExecutionStack.Peek().BasePointer;
							m_ValueStack.Push(m_ValueStack[scope + i.NumVal]);
							break;
						case OpCode.Upvalue:
						{
							var cs = m_ExecutionStack.Peek().ClosureScope;
							m_ValueStack.Push(cs[i.NumVal].Value());
							break;
						}
						case OpCode.StoreUpv:
							ExecStoreUpv(i);
							break;
						case OpCode.StoreLcl:
							ExecStoreLcl(i);
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
						case OpCode.Invalid:
							throw new NotImplementedException($"Invalid opcode : {i.String}");
						default:
							throw new NotImplementedException($"Execution for {i.OpCode} not implented yet!");
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

				if (!(ex is ScriptRuntimeException exception))
				{
					ex.Rethrow();
					throw;
				}

				if (m_Debug.DebuggerAttached != null)
				{
					if (m_Debug.DebuggerAttached.SignalRuntimeException(exception))
					{
						if (instructionPtr >= 0 && instructionPtr < m_RootChunk.Code.Count)
						{
							ListenDebugger(m_RootChunk.Code[instructionPtr], instructionPtr);
						}
					}
				}

				for (int i = 0; i < m_ExecutionStack.Count; i++)
				{
					var c = m_ExecutionStack.Peek(i);

					if (c.ErrorHandlerBeforeUnwind.IsNotNil())
						exception.DecoratedMessage = PerformMessageDecorationBeforeUnwind(c.ErrorHandlerBeforeUnwind, exception.DecoratedMessage, GetCurrentSourceRef(instructionPtr));
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

						var cbargs = new DynValue[] { DynValue.NewString(exception.DecoratedMessage) };

						DynValue handled = csi.ErrorHandler.Invoke(new ScriptExecutionContext(this, csi.ErrorHandler, GetCurrentSourceRef(instructionPtr)), cbargs);

						m_ValueStack.Push(handled);

						goto repeat_execution;
					}
					
					if ((csi.Flags & CallStackItemFlags.EntryPoint) != 0)
					{
						exception.Rethrow();
						throw;
					}
				}

				exception.Rethrow();
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

				switch (messageHandler.Type)
				{
					case DataType.Function:
						ret = Call(messageHandler, args);
						break;
					case DataType.ClrFunction:
					{
						ScriptExecutionContext ctx = new ScriptExecutionContext(this, messageHandler.Callback, sourceRef);
						ret = messageHandler.Callback.Invoke(ctx, args);
						break;
					}
					default:
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
		
		private void AssignLocal(SymbolRef symref, DynValue value)
		{
			var stackframe = m_ExecutionStack.Peek();
			m_ValueStack[stackframe.LocalBase + symref.i_Index] = value;
		}

		private void ExecStoreLcl(Instruction i)
		{
			DynValue value = GetStoreValue(i);
			SymbolRef symref = i.Symbol;

			AssignLocal(symref, value);
		}

		private void ExecStoreUpv(Instruction i)
		{
			DynValue value = GetStoreValue(i);
			SymbolRef symref = i.Symbol;

			var stackframe = m_ExecutionStack.Peek();
			
			stackframe.ClosureScope[symref.i_Index] ??= Upvalue.NewNil();
			stackframe.ClosureScope[symref.i_Index].Value() = value;
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

			DynValue v = m_ValueStack.Peek(stackofs);

			if (v.Type == DataType.Tuple)
			{
				return tupleidx < v.Tuple.Length ? v.Tuple[tupleidx] : DynValue.Nil;
			}

			return tupleidx == 0 ? v : DynValue.Nil;
		}

		private void ExecClosure(Instruction i)
		{
			Closure c = new Closure(m_Script, i.NumVal, i.SymbolList, i.SymbolList.Select(GetUpvalueSymbol).ToList());
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
						if (ex.OpenClosures[i].Index == ex.LocalBase + s.i_Index) return ex.OpenClosures[i];
					}
					var upval = new Upvalue(m_ValueStack, ex.LocalBase + s.i_Index);

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

		private void ExecMkTuple(int instrNumVal)
		{
			Slice<DynValue> slice = new Slice<DynValue>(m_ValueStack, m_ValueStack.Count - instrNumVal, instrNumVal, false);
			DynValue[] v = Internal_AdjustTuple(slice);
			m_ValueStack.RemoveLast(instrNumVal);
			m_ValueStack.Push(DynValue.NewTuple(v));
		}

		private void ExecIterUpd()
		{
			DynValue v = m_ValueStack.Peek(0);
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

			// MoonSharp additions - given f, s, var
			// 1) if f is not a function and has a __iterator metamethod, call __iterator to get the triplet
			// 2) if f is a table with no __call metamethod, use a default table iterator

			if (f.Type != DataType.Function && f.Type != DataType.ClrFunction)
			{
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
		
		private int ExecJFor(Instruction i, int instructionPtr)
		{
			double val = m_ValueStack.Peek(0).AssertNumber(1);
			double step = m_ValueStack.Peek(1).AssertNumber(2);
			double stop = m_ValueStack.Peek(2).AssertNumber(3);

			bool whileCond = (step > 0) ? val <= stop : val >= stop;

			return !whileCond ? i.NumVal : instructionPtr;
		}
		
		private void ExecIncr(Instruction i)
		{
			ref DynValue top = ref m_ValueStack.Peek(0);
			DynValue btm = m_ValueStack.Peek(i.NumVal);

			top = DynValue.NewNumber(top.Number + btm.Number);
		}
		
		private void ExecCNot(Instruction i)
		{
			DynValue v = m_ValueStack.Pop().ToScalar();
			DynValue not = m_ValueStack.Pop().ToScalar();

			if (not.Type != DataType.Boolean)
				throw new InternalErrorException("CNOT had non-bool arg");

			m_ValueStack.Push(not.CastToBool() ? DynValue.NewBoolean(!(v.CastToBool())) : DynValue.NewBoolean(v.CastToBool()));
		}

		private void ExecNot(Instruction i)
		{
			DynValue v = m_ValueStack.Pop().ToScalar();
			m_ValueStack.Push(DynValue.NewBoolean(!(v.CastToBool())));
		}

		private void ExecBeginFn(Instruction i)
		{
			ref CallStackItem cur = ref m_ExecutionStack.Peek();

			cur.Debug_Symbols = i.SymbolList;
			cur.LocalCount = i.NumVal;
			cur.LocalBase = m_ValueStack.Reserve(i.NumVal);

			ClearBlockData(i);
		}

		private CallStackItem PopToBasePointer()
		{
			var csi = m_ExecutionStack.Pop();
			if (csi.OpenClosures != null)
			{
				foreach(var closure in csi.OpenClosures) closure.Close();
			}
			if (csi.BasePointer >= 0)
				m_ValueStack.CropAtCount(csi.BasePointer);
			return csi;
		}

		private int PopExecStackAndCheckVStack(int vstackguard)
		{
			var xs = m_ExecutionStack.Pop();
			if (vstackguard != xs.BasePointer)
				throw new InternalErrorException("StackGuard violation");

			return xs.ReturnAddress;
		}

		private IList<DynValue> CreateArgsListForFunctionCall(int numargs, int offsFromTop)
		{
			if (numargs == 0) return Array.Empty<DynValue>();

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

			return new Slice<DynValue>(m_ValueStack, m_ValueStack.Count - numargs - offsFromTop, numargs, false);
		}
		
		private void ExecArgs(Instruction I)
		{
			int localCount = m_ExecutionStack.Peek().LocalCount;
			int numargs = (int)m_ValueStack.Peek(localCount).Number;
			// unpacks last tuple arguments to simplify a lot of code down under
			var argsList = CreateArgsListForFunctionCall(numargs, 1 + localCount);

			for (int i = 0; i < I.SymbolList.Length; i++)
			{
				if (i >= argsList.Count)
				{
					AssignLocal(I.SymbolList[i], DynValue.Nil);
				}
				else if ((i == I.SymbolList.Length - 1) && (I.SymbolList[i].i_Name == WellKnownSymbols.VARARGS))
				{
					int len = argsList.Count - i;
					DynValue[] varargs = new DynValue[len];

					for (int ii = 0; ii < len; ii++, i++)
					{
						varargs[ii] = argsList[i].ToScalar();
					}

					AssignLocal(I.SymbolList[I.SymbolList.Length - 1], DynValue.NewTuple(Internal_AdjustTuple(varargs)));
				}
				else
				{
					AssignLocal(I.SymbolList[i], argsList[i].ToScalar());
				}
			}
		}

		private int Internal_ExecCall(bool canAwait, int argsCount, int instructionPtr, CallbackFunction handler = null, CallbackFunction continuation = null, bool thisCall = false, string debugText = null, DynValue unwindHandler = default)
		{
			while (true)
			{
				DynValue fn = m_ValueStack.Peek(argsCount);
				CallStackItemFlags flags = (thisCall ? CallStackItemFlags.MethodCall : CallStackItemFlags.None);

				// if TCO threshold reached
				if ((m_ExecutionStack.Count > m_Script.Options.TailCallOptimizationThreshold && m_ExecutionStack.Count > 1) || (m_ValueStack.Count > m_Script.Options.TailCallOptimizationThreshold && m_ValueStack.Count > 1))
				{
					// and the "will-be" return address is valid (we don't want to crash here)
					if (instructionPtr >= 0 && instructionPtr < m_RootChunk.Code.Count)
					{
						Instruction I = m_RootChunk.Code[instructionPtr];

						// and we are followed *exactly* by a RET 1
						if (I.OpCode == OpCode.Ret && I.NumVal == 1)
						{
							ref CallStackItem csi = ref m_ExecutionStack.Peek();

							// if the current stack item has no "odd" things pending and neither has the new coming one..
							if (csi.ClrFunction == null && csi.Continuation == null && csi.ErrorHandler == null && csi.ErrorHandlerBeforeUnwind.IsNil() && continuation == null && unwindHandler.IsNil() && handler == null)
							{
								instructionPtr = PerformTCO(instructionPtr, argsCount);
								flags |= CallStackItemFlags.TailCall;
							}
						}
					}
				}

				switch (fn.Type)
				{
					case DataType.ClrFunction:
					{
						//IList<DynValue> args = new Slice<DynValue>(m_ValueStack, m_ValueStack.Count - argsCount, argsCount, false);
						IList<DynValue> args = CreateArgsListForFunctionCall(argsCount, 0);
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

						var ret = fn.Callback.Invoke(new ScriptExecutionContext(this, fn.Callback, sref) {CanAwait = canAwait}, args, isMethodCall: thisCall);
						m_ValueStack.RemoveLast(argsCount + 1);

						if (m_Script.Options.AutoAwait && ret.Type == DataType.UserData && ret.UserData?.Object is TaskWrapper tw)
						{
							ret = tw.await(new ScriptExecutionContext(this, fn.Callback, sref) {CanAwait = canAwait}, null);
						}

						m_ValueStack.Push(ret);
						m_ExecutionStack.Pop();

						return Internal_CheckForTailRequests(canAwait, instructionPtr);
					}
					case DataType.Function:
						m_ValueStack.Push(DynValue.NewNumber(argsCount));
						m_ExecutionStack.Push(new CallStackItem()
						{
							BasePointer = m_ValueStack.Count,
							ReturnAddress = instructionPtr,
							Debug_EntryPoint = fn.Function.EntryPointByteCodeLocation,
							CallingSourceRef = GetCurrentSourceRef(instructionPtr - 1), // See right above in GetCurrentSourceRef(instructionPtr - 1)
							ClosureScope = fn.Function.ClosureContext,
							ErrorHandler = handler,
							Continuation = continuation,
							ErrorHandlerBeforeUnwind = unwindHandler,
							Flags = flags,
						});
						return fn.Function.EntryPointByteCodeLocation;
				}

				// fallback to __call metamethod
				var m = GetMetamethod(fn, "__call");

				if (!m.IsNotNil())
				{
					throw ScriptRuntimeException.AttemptToCallNonFunc(fn.Type, debugText);
				}
				
				DynValue[] tmp = new DynValue[argsCount + 1];
				for (int i = 0; i < argsCount + 1; i++) tmp[i] = m_ValueStack.Pop();

				m_ValueStack.Push(m);

				for (int i = argsCount; i >= 0; i--) m_ValueStack.Push(tmp[i]);

				argsCount += 1;
				thisCall = false;
				debugText = null;
				unwindHandler = default;
			}
		}

		private int PerformTCO(int instructionPtr, int argsCount)
		{
			DynValue[] args = new DynValue[argsCount + 1];

			// Remove all cur args and func ptr
			for (int i = 0; i <= argsCount; i++)
				args[i] = m_ValueStack.Pop();

			// perform a fake RET
			CallStackItem csi = PopToBasePointer();
			int retpoint = csi.ReturnAddress;
			var argscnt = m_ValueStack.Pop().Int;
			m_ValueStack.RemoveLast(argscnt + 1);

			// Re-push all cur args and func ptr
			for (int i = argsCount; i >= 0; i--)
				m_ValueStack.Push(args[i]);

			return retpoint;
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
					var argscnt = m_ValueStack.Pop().Int;
					m_ValueStack.RemoveLast(argscnt + 1);
					m_ValueStack.Push(DynValue.Void);
					break;
				}
				case 1:
				{
					var retval = m_ValueStack.Pop();
					csi = PopToBasePointer();
					retpoint = csi.ReturnAddress;
					var argscnt = m_ValueStack.Pop().Int;
					m_ValueStack.RemoveLast(argscnt + 1);
					m_ValueStack.Push(retval);
					retpoint = Internal_CheckForTailRequests(false, retpoint);
					break;
				}
				default:
					throw new InternalErrorException("RET supports only 0 and 1 ret val scenarios");
			}

			if (csi.Continuation != null)
				m_ValueStack.Push(csi.Continuation.Invoke(new ScriptExecutionContext(this, csi.Continuation, m_RootChunk.SourceRefs[currentPtr]),
					new DynValue[] { m_ValueStack.Pop() }));

			return retpoint;
		}
		
		private int Internal_CheckForTailRequests(bool canAwait, int instructionPtr)
		{
			DynValue tail = m_ValueStack.Peek(0);

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
			DynValue op = m_ValueStack.Pop().ToScalar();
			return op.CastToBool() == expectedValueForJump ? i.NumVal : instructionPtr;
		}

		private int ExecShortCircuitingOperator(Instruction i, int instructionPtr)
		{
			bool expectedValToShortCircuit = i.OpCode == OpCode.JtOrPop;
			DynValue op = m_ValueStack.Peek().ToScalar();

			if (op.CastToBool() == expectedValToShortCircuit)
			{
				return i.NumVal;
			}

			m_ValueStack.Pop();
			return instructionPtr;
		}
		
		private int ExecAdd(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(ln + rn));
				return instructionPtr;
			}

			var r = m_ValueStack.Pop().ToScalar();
			var l = m_ValueStack.Pop().ToScalar();
			int ip = Internal_InvokeBinaryMetaMethod(l, r, "__add", instructionPtr);
			if (ip >= 0) return ip;
			
			throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
		}

		private int ExecSub(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(ln - rn));
				return instructionPtr;
			}

			var r = m_ValueStack.Pop().ToScalar();
			var l = m_ValueStack.Pop().ToScalar();
			int ip = Internal_InvokeBinaryMetaMethod(l, r, "__sub", instructionPtr);
			if (ip >= 0) return ip;
			
			throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
		}
		
		private int ExecMul(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(ln * rn));
				return instructionPtr;
			}

			var r = m_ValueStack.Pop().ToScalar();
			var l = m_ValueStack.Pop().ToScalar();
			int ip = Internal_InvokeBinaryMetaMethod(l, r, "__mul", instructionPtr);
			if (ip >= 0) return ip;
			
			throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
		}

		private int ExecMod(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				var mod = ln - Math.Floor(ln / rn) * rn;
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(mod));
				return instructionPtr;
			}

			var r = m_ValueStack.Pop().ToScalar();
			var l = m_ValueStack.Pop().ToScalar();
			int ip = Internal_InvokeBinaryMetaMethod(l, r, "__mod", instructionPtr);
			if (ip >= 0) return ip;
			
			throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
		}

		private int ExecDiv(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(ln / rn));
				return instructionPtr;
			}

			var r = m_ValueStack.Pop().ToScalar();
			var l = m_ValueStack.Pop().ToScalar();
			int ip = Internal_InvokeBinaryMetaMethod(l, r, "__div", instructionPtr);
			if (ip >= 0) return ip;
			
			throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
		}
		
		private int ExecPower(int instructionPtr)
		{
			if (m_ValueStack.Peek().TryCastToNumber(out var rn) && m_ValueStack.Peek(1).TryCastToNumber(out var ln))
			{
				m_ValueStack.Pop();
				m_ValueStack.Set(0, DynValue.NewNumber(Math.Pow(ln,rn)));
				return instructionPtr;
			}

			var r = m_ValueStack.Pop().ToScalar();
			var l = m_ValueStack.Pop().ToScalar();
			int ip = Internal_InvokeBinaryMetaMethod(l, r, "__pow", instructionPtr);
			if (ip >= 0) return ip;
			
			throw ScriptRuntimeException.ArithmeticOnNonNumber(l, r);
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

		private int ExecEq(int instructionPtr)
		{
			DynValue r = m_ValueStack.Pop().ToScalar();
			DynValue l = m_ValueStack.Pop().ToScalar();

			// then if they are userdatas, attempt meta
			if (l.Type == DataType.UserData || r.Type == DataType.UserData)
			{
				int ip = Internal_InvokeBinaryMetaMethod(l, r, "__eq", instructionPtr);
				if (ip >= 0) return ip;
			}

			// then if types are different, ret false
			if (r.Type != l.Type)
			{
				if ((l.Type == DataType.Nil && r.Type == DataType.Void) || (l.Type == DataType.Void && r.Type == DataType.Nil))
					m_ValueStack.Push(DynValue.True);
				else
					m_ValueStack.Push(DynValue.False);

				return instructionPtr;
			}

			// then attempt metatables for tables
			if (l.Type == DataType.Table && GetMetatable(l) != null && GetMetatable(l) == GetMetatable(r))
			{
				int ip = Internal_InvokeBinaryMetaMethod(l, r, "__eq", instructionPtr);
				if (ip >= 0) return ip;
			}

			// else perform standard comparison
			m_ValueStack.Push(DynValue.NewBoolean(r.Equals(l)));
			return instructionPtr;
		}

		private int ExecLess(int instructionPtr)
		{
			DynValue r = m_ValueStack.Pop().ToScalar();
			DynValue l = m_ValueStack.Pop().ToScalar();

			switch (l.Type)
			{
				case DataType.Number when r.Type == DataType.Number:
					m_ValueStack.Push(DynValue.NewBoolean(l.Number < r.Number));
					break;
				case DataType.String when r.Type == DataType.String:
					m_ValueStack.Push(DynValue.NewBoolean(string.Compare(l.String, r.String, StringComparison.Ordinal) < 0));
					break;
				default:
				{
					int ip = Internal_InvokeBinaryMetaMethod(l, r, "__lt", instructionPtr);
					if (ip < 0)
						throw ScriptRuntimeException.CompareInvalidType(l, r);
				
					return ip;
				}
			}

			return instructionPtr;
		}

		private int ExecLessEq(int instructionPtr)
		{
			DynValue r = m_ValueStack.Pop().ToScalar();
			DynValue l = m_ValueStack.Pop().ToScalar();

			switch (l.Type)
			{
				case DataType.Number when r.Type == DataType.Number:
					m_ValueStack.Push(DynValue.False);
					m_ValueStack.Push(DynValue.NewBoolean(l.Number <= r.Number));
					break;
				case DataType.String when r.Type == DataType.String:
					m_ValueStack.Push(DynValue.False);
					m_ValueStack.Push(DynValue.NewBoolean(String.Compare(l.String, r.String, StringComparison.Ordinal) <= 0));
					break;
				default:
				{
					int ip = Internal_InvokeBinaryMetaMethod(l, r, "__le", instructionPtr, DynValue.False);
					if (ip >= 0)
					{
						return ip;
					}
					
					ip = Internal_InvokeBinaryMetaMethod(r, l, "__lt", instructionPtr, DynValue.True);

					if (ip < 0)
						throw ScriptRuntimeException.CompareInvalidType(l, r);
					
					return ip;

				}
			}

			return instructionPtr;
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
		
		private int ExecConcat(Instruction i, int instructionPtr)
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
			// stack: tbl - val
			DynValue val = m_ValueStack.Pop();
			DynValue tbl = m_ValueStack.Peek();

			if (tbl.Type != DataType.Table)
				throw new InternalErrorException("Unexpected type in table ctor : {0}", tbl);

			tbl.Table.InitNextArrayKeys(val, i.NumVal != 0);
		}

		private void ExecTblInitN(Instruction i)
		{
			// stack: tbl - key - val
			DynValue val = m_ValueStack.Pop();
			DynValue key = m_ValueStack.Pop();
			DynValue tbl = m_ValueStack.Peek();

			if (tbl.Type != DataType.Table)
				throw new InternalErrorException("Unexpected type in table ctor : {0}", tbl);

			tbl.Table.Set(key, val.ToScalar());
		}

		private int ExecIndexSet(Instruction i, int instructionPtr)
		{
			int nestedMetaOps = 100; // sanity check, to avoid potential infinite loop here

			// stack: vals.. - base - index
			bool isNameIndex = i.OpCode == OpCode.IndexSetN;
			bool isMultiIndex = (i.OpCode == OpCode.IndexSetL);

			DynValue originalIdx = i.String != null ? DynValue.NewString(i.String) : m_ValueStack.Pop();
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
						if (!isMultiIndex)
						{
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
			bool isMultiIndex = (i.OpCode == OpCode.IndexL);

			DynValue originalIdx = i.String != null ? DynValue.NewString(i.String) : m_ValueStack.Pop();
			DynValue idx = originalIdx.ToScalar();
			DynValue obj = m_ValueStack.Pop().ToScalar();

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
							DynValue v = obj.Table.Get(idx);

							if (!v.IsNil())
							{
								m_ValueStack.Push(v);
								return instructionPtr;
							}
						}

						h = GetMetamethodRaw(obj, "__index");

						if (h.IsNil())
						{
							if (isMultiIndex) throw new ScriptRuntimeException("cannot multi-index a table. userdata expected");

							m_ValueStack.Push(DynValue.Nil);
							return instructionPtr;
						}

						break;
					}
					case DataType.UserData:
					{
						UserData ud = obj.UserData;
						DynValue v = ud.Descriptor.Index(GetScript(), ud.Object, originalIdx, isNameIndex);

						if (v.IsVoid())
						{
							throw ScriptRuntimeException.UserDataMissingField(ud.Descriptor.Name, idx.String);
						}

						m_ValueStack.Push(v);
						return instructionPtr;
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
			}

			throw ScriptRuntimeException.LoopInIndex();
		}
	}
}
