using System;
using System.Collections.Generic;
using System.Linq;
using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.Execution.VM
{
	// This part is practically written procedural style - it looks more like C than C#.
	// This is intentional so to avoid this-calls and virtual-calls as much as possible.
	// Same reason for the "sealed" declaration.
	sealed partial class Processor
	{

		internal void AttachDebugger(IDebugger debugger)
		{
			m_Debug.DebuggerAttached = debugger;
			m_Debug.LineBasedBreakPoints = (debugger.GetDebuggerCaps() & DebuggerCaps.HasLineBasedBreakpoints) != 0;
			debugger.SetDebugService(new DebugService(m_Script, this));
		}

		internal bool DebuggerEnabled
		{
			get { return m_Debug.DebuggerEnabled; }
			set { m_Debug.DebuggerEnabled = value; }
		}


		private void ListenDebugger(int instructionPtr)
		{
			bool isOnDifferentRef = false;
			var instr_SourceCodeRef = m_ExecutionStack.Peek().Function.sourceRefs[instructionPtr];
			
			if (instr_SourceCodeRef != null && m_Debug.LastHlRef != null)
			{
				if (m_Debug.LineBasedBreakPoints)
				{
					isOnDifferentRef = instr_SourceCodeRef.SourceIdx != m_Debug.LastHlRef.SourceIdx ||
						instr_SourceCodeRef.FromLine != m_Debug.LastHlRef.FromLine;
				}
				else
				{
					isOnDifferentRef = instr_SourceCodeRef != m_Debug.LastHlRef;
				}
			}
			else if (m_Debug.LastHlRef == null)
			{
				isOnDifferentRef = instr_SourceCodeRef != null;
			}


			if (m_Debug.DebuggerAttached.IsPauseRequested() ||
				(instr_SourceCodeRef != null && instr_SourceCodeRef.Breakpoint && isOnDifferentRef))
			{
				m_Debug.DebuggerCurrentAction = DebuggerAction.ActionType.None;
				m_Debug.DebuggerCurrentActionTarget = -1;
			}

			switch (m_Debug.DebuggerCurrentAction)
			{
				case DebuggerAction.ActionType.Run:
					if (m_Debug.LineBasedBreakPoints)
						m_Debug.LastHlRef = instr_SourceCodeRef;
					return;
				case DebuggerAction.ActionType.ByteCodeStepOver:
					if (m_Debug.DebuggerCurrentActionTarget != instructionPtr) return;
					break;
				case DebuggerAction.ActionType.ByteCodeStepOut:
				case DebuggerAction.ActionType.StepOut:
					if (m_ExecutionStack.Count >= m_Debug.ExStackDepthAtStep) return;
					break;
				case DebuggerAction.ActionType.StepIn:
					if ((m_ExecutionStack.Count >= m_Debug.ExStackDepthAtStep) && (instr_SourceCodeRef == null || instr_SourceCodeRef == m_Debug.LastHlRef)) return;
					break;
				case DebuggerAction.ActionType.StepOver:
					if (instr_SourceCodeRef == null || instr_SourceCodeRef == m_Debug.LastHlRef || m_ExecutionStack.Count > m_Debug.ExStackDepthAtStep) return;
					break;
			}


			RefreshDebugger(false, instructionPtr);

			while (true)
			{
				var action = m_Debug.DebuggerAttached.GetAction(instructionPtr, instr_SourceCodeRef);

				switch (action.Action)
				{
					case DebuggerAction.ActionType.StepIn:
					case DebuggerAction.ActionType.StepOver:
					case DebuggerAction.ActionType.StepOut:
					case DebuggerAction.ActionType.ByteCodeStepOut:
						m_Debug.DebuggerCurrentAction = action.Action;
						m_Debug.LastHlRef = instr_SourceCodeRef;
						m_Debug.ExStackDepthAtStep = m_ExecutionStack.Count;
						return;
					case DebuggerAction.ActionType.ByteCodeStepIn:
						m_Debug.DebuggerCurrentAction = DebuggerAction.ActionType.ByteCodeStepIn;
						m_Debug.DebuggerCurrentActionTarget = -1;
						return;
					case DebuggerAction.ActionType.ByteCodeStepOver:
						m_Debug.DebuggerCurrentAction = DebuggerAction.ActionType.ByteCodeStepOver;
						m_Debug.DebuggerCurrentActionTarget = instructionPtr + 1;
						return;
					case DebuggerAction.ActionType.Run:
						m_Debug.DebuggerCurrentAction = DebuggerAction.ActionType.Run;
						m_Debug.LastHlRef = instr_SourceCodeRef;
						m_Debug.DebuggerCurrentActionTarget = -1;
						return;
					case DebuggerAction.ActionType.ToggleBreakpoint:
						ToggleBreakPoint(action, null);
						RefreshDebugger(true, instructionPtr);
						break;
					case DebuggerAction.ActionType.ResetBreakpoints:
						ResetBreakPoints(action);
						RefreshDebugger(true, instructionPtr);
						break;
					case DebuggerAction.ActionType.SetBreakpoint:
						ToggleBreakPoint(action, true);
						RefreshDebugger(true, instructionPtr);
						break;
					case DebuggerAction.ActionType.ClearBreakpoint:
						ToggleBreakPoint(action, false);
						RefreshDebugger(true, instructionPtr);
						break;
					case DebuggerAction.ActionType.Refresh:
						RefreshDebugger(false, instructionPtr);
						break;
					case DebuggerAction.ActionType.HardRefresh:
						RefreshDebugger(true, instructionPtr);
						break;
					case DebuggerAction.ActionType.None:
					default:
						break;
				}
			}
		}

		private void ResetBreakPoints(DebuggerAction action)
		{
			SourceCode src = m_Script.GetSourceCode(action.SourceID);
			ResetBreakPoints(src, new HashSet<int>(action.Lines));
		}

		internal HashSet<int> ResetBreakPoints(SourceCode src, HashSet<int> lines)
		{
			HashSet<int> result = new HashSet<int>();

			foreach (SourceRef srf in src.Refs)
			{
				if (srf.CannotBreakpoint)
					continue;

				srf.Breakpoint = lines.Contains(srf.FromLine);

				if (srf.Breakpoint)
					result.Add(srf.FromLine);
			}

			return result;
		}

		private bool ToggleBreakPoint(DebuggerAction action, bool? state)
		{
			SourceCode src = m_Script.GetSourceCode(action.SourceID);

			bool found = false;
			foreach (SourceRef srf in src.Refs)
			{
				if (srf.CannotBreakpoint)
					continue;

				if (srf.IncludesLocation(action.SourceID, action.SourceLine, action.SourceCol))
				{
					found = true;

					//System.Diagnostics.Debug.WriteLine(string.Format("BRK: found {0} for {1} on contains", srf, srf.Type));

					if (state == null)
						srf.Breakpoint = !srf.Breakpoint;
					else
						srf.Breakpoint = state.Value;

					if (srf.Breakpoint)
					{
						m_Debug.BreakPoints.Add(srf);
					}
					else
					{
						m_Debug.BreakPoints.Remove(srf);
					}
				}
			}

			if (!found)
			{
				int minDistance = int.MaxValue;
				SourceRef nearest = null;

				foreach (SourceRef srf in src.Refs)
				{
					if (srf.CannotBreakpoint)
						continue;

					int dist = srf.GetLocationDistance(action.SourceID, action.SourceLine, action.SourceCol);

					if (dist < minDistance)
					{
						minDistance = dist;
						nearest = srf;
					}
				}

				if (nearest != null)
				{
					//System.Diagnostics.Debug.WriteLine(string.Format("BRK: found {0} for {1} on distance {2}", nearest, nearest.Type, minDistance));

					if (state == null)
						nearest.Breakpoint = !nearest.Breakpoint;
					else
						nearest.Breakpoint = state.Value;

					if (nearest.Breakpoint)
					{
						m_Debug.BreakPoints.Add(nearest);
					}
					else
					{
						m_Debug.BreakPoints.Remove(nearest);
					}

					return true;
				}
				else
					return false;
			}
			else
				return true;
		}

		private void RefreshDebugger(bool hard, int instructionPtr)
		{
			SourceRef sref = GetCurrentSourceRef(instructionPtr);
			ScriptExecutionContext context = new ScriptExecutionContext(this, null, sref);

			List<DynamicExpression> watchList = m_Debug.DebuggerAttached.GetWatchItems();
			List<WatchItem> callStack = Debugger_GetCallStack(sref);
			List<WatchItem> watches = Debugger_RefreshWatches(context, watchList);
			List<WatchItem> vstack = Debugger_RefreshVStack();
			List<WatchItem> locals = Debugger_RefreshLocals(context);
			List<WatchItem> threads = Debugger_RefreshThreads(context);

			m_Debug.DebuggerAttached.Update(WatchType.CallStack, callStack);
			m_Debug.DebuggerAttached.Update(WatchType.Watches, watches);
			m_Debug.DebuggerAttached.Update(WatchType.VStack, vstack);
			m_Debug.DebuggerAttached.Update(WatchType.Locals, locals);
			m_Debug.DebuggerAttached.Update(WatchType.Threads, threads);

			if (hard)
				m_Debug.DebuggerAttached.RefreshBreakpoints(m_Debug.BreakPoints);
		}

		private List<WatchItem> Debugger_RefreshThreads(ScriptExecutionContext context)
		{
			List<Processor> coroutinesStack = m_Parent != null ? m_Parent.m_CoroutinesStack : this.m_CoroutinesStack;

			return coroutinesStack.Select(c => new WatchItem()
			{
				Address = c.AssociatedCoroutine.ReferenceID,
				Name = "coroutine #" + c.AssociatedCoroutine.ReferenceID.ToString()
			}).ToList();
		}

		private List<WatchItem> Debugger_RefreshVStack()
		{
			List<WatchItem> lwi = new List<WatchItem>();
			for (int i = 0; i < Math.Min(32, m_ValueStack.Count); i++)
			{
				lwi.Add(new WatchItem()
				{
					Address = i,
					Value = m_ValueStack.Peek(i)
				});
			}

			return lwi;
		}

		private List<WatchItem> Debugger_RefreshWatches(ScriptExecutionContext context, List<DynamicExpression> watchList)
		{
			return watchList.Select(w => Debugger_RefreshWatch(context, w)).ToList();
		}

		private List<WatchItem> Debugger_RefreshLocals(ScriptExecutionContext context)
		{
			List<WatchItem> locals = new List<WatchItem>();
			ref var top = ref m_ExecutionStack.Peek();

			if (!top.IsNil && top.Function.locals != null && top.Function.localCount != 0)
			{
				int len = Math.Min(top.Function.locals.Length, top.Function.localCount);

				for (int i = 0; i < len; i++)
				{
					locals.Add(new WatchItem()
					{
						IsError = false,
						LValue = top.Function.locals[i],
						Value = m_ValueStack[top.BasePointer + i],
						Name = top.Function.locals[i].i_Name
					});
				}
			}

			return locals;
		}

		private WatchItem Debugger_RefreshWatch(ScriptExecutionContext context, DynamicExpression dynExpr)
		{
			try
			{
				SymbolRef L = dynExpr.FindSymbol(context);
				DynValue v = dynExpr.Evaluate(context);

				return new WatchItem()
				{
					IsError = dynExpr.IsConstant(),
					LValue = L,
					Value = v,
					Name = dynExpr.ExpressionCode
				};
			}
			catch (Exception ex)
			{
				return new WatchItem()
				{
					IsError = true,
					Value = DynValue.NewString(ex.Message),
					Name = dynExpr.ExpressionCode
				};
			}
		}

		internal List<WatchItem> Debugger_GetCallStack(SourceRef startingRef)
		{
			List<WatchItem> wis = new List<WatchItem>();

			for (int i = 0; i < m_ExecutionStack.Count; i++)
			{
				var c = m_ExecutionStack.Peek(i);

				//var I = m_RootChunk.Code[c.Debug_EntryPoint];

				string callname = c.Function?.Name;

				if (c.ClrFunction != null)
				{
					wis.Add(new WatchItem()
					{
						Address = -1,
						BasePtr = -1,
						RetAddress = c.ReturnAddress,
						Location = startingRef,
						Name = c.ClrFunction.Name
					});
				}
				else
				{
					wis.Add(new WatchItem()
					{
						Address = -1, //TODO: Make this work
						BasePtr = c.BasePointer,
						RetAddress = c.ReturnAddress,
						Name = callname,
						Location = startingRef,
					});
				}

				startingRef = c.CallingSourceRef;

				if (c.Continuation != null)
				{
					wis.Add(new WatchItem()
					{
						Name = c.Continuation.Name,
						Location = SourceRef.GetClrLocation()
					});
				}


			}

			return wis;
		}
	}
}
