using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Interop;

namespace WattleScript.Interpreter.Execution.VM
{
	sealed partial class Processor
	{
		FastStack<DynValue> m_ValueStack = new FastStack<DynValue>(1024, 131072);
		FastStack<CallStackItem> m_ExecutionStack = new FastStack<CallStackItem>(128, 131072);
		List<Processor> m_CoroutinesStack;

		Table m_GlobalTable;
		Script m_Script;
		Processor m_Parent;
		CoroutineState m_State;
		bool m_CanYield = true;
		int m_SavedInstructionPtr = -1;
		DebugContext m_Debug;

		public Processor(Script script, Table globalContext)
		{
			m_CoroutinesStack = new List<Processor>();

			m_Debug = new DebugContext();
			m_GlobalTable = globalContext;
			m_Script = script;
			m_State = CoroutineState.Main;
			DynValue.NewCoroutine(new Coroutine(this)); // creates an associated coroutine for the main processor
		}

		private Processor(Processor parentProcessor)
		{
			m_Debug = parentProcessor.m_Debug;
			m_GlobalTable = parentProcessor.m_GlobalTable;
			m_Script = parentProcessor.m_Script;
			m_Parent = parentProcessor;
			m_State = CoroutineState.NotStarted;
		}


		public DynValue ThisCall(DynValue function, DynValue[] args)
		{
			return Call_Internal(function, args, true);
		}
		
		public DynValue Call(DynValue function, DynValue[] args)
		{
			return Call_Internal(function, args, false);
		}

		private DynValue Call_Internal(DynValue function, DynValue[] args, bool thisCall)
		{
			List<Processor> coroutinesStack = m_Parent != null ? m_Parent.m_CoroutinesStack : this.m_CoroutinesStack;

			if (coroutinesStack.Count > 0 && coroutinesStack[coroutinesStack.Count - 1] != this)
				return coroutinesStack[coroutinesStack.Count - 1].Call_Internal(function, args, thisCall);

			EnterProcessor();

			try
			{
				var stopwatch = this.m_Script.PerformanceStats.StartStopwatch(Diagnostics.PerformanceCounter.Execution);

				m_CanYield = false;

				try
				{
					var flags = CallStackItemFlags.CallEntryPoint;
					if (thisCall) flags |= CallStackItemFlags.MethodCall;
					int entrypoint = PushClrToScriptStackFrame(flags, function, args);
					return Processing_Loop(entrypoint);
				}
				finally
				{
					m_CanYield = true;

					if (stopwatch != null)
						stopwatch.Dispose();
				}
			}
			finally
			{
				LeaveProcessor();
			}
		}

		public async Task<DynValue> ThisCallAsync(DynValue function, DynValue[] args)
		{
			return await CallAsync_Internal(function, args, true);
		}

		public async Task<DynValue> CallAsync(DynValue function, DynValue[] args)
		{
			return await CallAsync_Internal(function, args, false);
		}
		
		private async Task<DynValue> CallAsync_Internal(DynValue function, DynValue[] args, bool thisCall)
		{
			List<Processor> coroutinesStack = m_Parent != null ? m_Parent.m_CoroutinesStack : this.m_CoroutinesStack;

			if (coroutinesStack.Count > 0 && coroutinesStack[coroutinesStack.Count - 1] != this)
				return await coroutinesStack[coroutinesStack.Count - 1].CallAsync_Internal(function, args, thisCall);

			EnterProcessor();

			try
			{
				var stopwatch = this.m_Script.PerformanceStats.StartStopwatch(Diagnostics.PerformanceCounter.Execution);

				m_CanYield = false;

				try
				{
					var flags = CallStackItemFlags.CallEntryPoint;
					if (thisCall) flags |= CallStackItemFlags.MethodCall;
					m_SavedInstructionPtr  = PushClrToScriptStackFrame(flags, function, args);
					DynValue retval;
					while ((retval = Processing_Loop(m_SavedInstructionPtr, true)).Type == DataType.AwaitRequest)
					{
						await retval.Task;
						m_ValueStack.Push(TaskWrapper.TaskResultToDynValue(m_Script, retval.Task));
					}
					return retval;
				}
				finally
				{
					m_CanYield = true;

					if (stopwatch != null)
						stopwatch.Dispose();
				}
			}
			finally
			{
				LeaveProcessor();
			}
		}
		
		

		// pushes all what's required to perform a clr-to-script function call. function can be null if it's already
		// at vstack top.
		private int PushClrToScriptStackFrame(CallStackItemFlags flags, DynValue function, DynValue[] args)
		{
			if (function.IsNil()) 
				function = m_ValueStack.Peek();
			else
				m_ValueStack.Push(function);  // func val

			args = Internal_AdjustTuple(args);

			for (int i = 0; i < args.Length; i++)
				m_ValueStack.Push(args[i]);

			m_ValueStack.Push(DynValue.NewNumber(args.Length));  // func args count

			m_ExecutionStack.Push(new CallStackItem()
			{
				BasePointer = m_ValueStack.Count,
				Function = function.Function.Function,
				ReturnAddress = -1,
				ClosureScope = function.Function.ClosureContext,
				CallingSourceRef = SourceRef.GetClrLocation(),
				Flags = flags
			});

			m_ValueStack.Reserve(function.Function.Function.localCount);

			return 0;
		}


		int m_OwningThreadID = -1;
		int m_ExecutionNesting = 0;

		private void LeaveProcessor()
		{
			m_ExecutionNesting -= 1;
			m_OwningThreadID = -1;

			if (m_Parent != null)
			{
				m_Parent.m_CoroutinesStack.RemoveAt(m_Parent.m_CoroutinesStack.Count - 1);
			}

			if (m_ExecutionNesting == 0 && m_Debug != null && m_Debug.DebuggerEnabled 
				&& m_Debug.DebuggerAttached != null)
			{
				m_Debug.DebuggerAttached.SignalExecutionEnded();
			}
		}

		int GetThreadId()
		{
			return Thread.CurrentThread.ManagedThreadId;
		}

		private void EnterProcessor()
		{
			int threadID = GetThreadId();

			if (m_OwningThreadID >= 0 && m_OwningThreadID != threadID && m_Script.Options.CheckThreadAccess)
			{
				string msg = string.Format("Cannot enter the same WattleScript processor from two different threads : {0} and {1}", m_OwningThreadID, threadID);
				throw new InvalidOperationException(msg);
			}

			m_OwningThreadID = threadID;

			m_ExecutionNesting += 1;

			if (m_Parent != null)
			{
				m_Parent.m_CoroutinesStack.Add(this);
			}
		}

		internal SourceRef GetCoroutineSuspendedLocation()
		{
			return GetCurrentSourceRef(m_SavedInstructionPtr);
		}
	}
}
