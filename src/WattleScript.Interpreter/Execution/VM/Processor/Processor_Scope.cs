using System;

namespace WattleScript.Interpreter.Execution.VM
{
	sealed partial class Processor
	{
		private CallStackItem Nil = new CallStackItem();
		
		private void ClearBlockData(Instruction I)
		{
			ref var exStack = ref m_ExecutionStack.Peek();
			int from = exStack.BasePointer + I.NumVal;
			int to = exStack.BasePointer + I.NumVal2;

			int length = to - from + 1;
			
			if (to >= 0 && from >= 0 && to >= from)
			{
				if (exStack.OpenClosures != null)
				{
					for (int i = exStack.OpenClosures.Count - 1; i >= 0; i--)
					{
						if (exStack.OpenClosures[i].Index >= from && exStack.OpenClosures[i].Index <= to) {
							exStack.OpenClosures[i].Close();
							exStack.OpenClosures.RemoveAt(i);
						}
					}					
				}

				m_ValueStack.ClearSection(from, length);
			}
		}


		public DynValue GetGenericSymbol(SymbolRef symref)
		{
			switch (symref.i_Type)
			{
				case  SymbolRefType.DefaultEnv:
					return DynValue.NewTable(this.GetScript().Globals);
				case SymbolRefType.Global:
					return GetGlobalSymbol(GetGenericSymbol(symref.i_Env), symref.i_Name);
				case SymbolRefType.Local:
					return m_ValueStack[GetTopNonClrFunction().BasePointer + symref.i_Index];
				case SymbolRefType.Upvalue:
					return GetTopNonClrFunction().ClosureScope[symref.i_Index].Value();
				default:
					throw new InternalErrorException("Unexpected {0} LRef at resolution: {1}", symref.i_Type, symref.i_Name);
			}
		}

		private DynValue GetGlobalSymbol(DynValue dynValue, string name)
		{
			if (dynValue.Type != DataType.Table)
				throw new InvalidOperationException(string.Format("_ENV is not a table but a {0}", dynValue.Type));

			return dynValue.Table.Get(name);
		}

		private void SetGlobalSymbol(DynValue dynValue, string name, DynValue value)
		{
			if (dynValue.Type != DataType.Table)
				throw new InvalidOperationException(string.Format("_ENV is not a table but a {0}", dynValue.Type));

			dynValue.Table.Set(name, value);
		}


		public void AssignGenericSymbol(SymbolRef symref, DynValue value)
		{
			switch (symref.i_Type)
			{
				case SymbolRefType.Global:
					SetGlobalSymbol(GetGenericSymbol(symref.i_Env), symref.i_Name, value);
					break;
				case SymbolRefType.Local:
					{
						ref var stackframe = ref GetTopNonClrFunction();
						m_ValueStack[stackframe.BasePointer + symref.i_Index] = value;
					}
					break;
				case SymbolRefType.Upvalue:
					{
						ref var stackframe = ref GetTopNonClrFunction();
						if(stackframe.ClosureScope[symref.i_Index] == null)
							stackframe.ClosureScope[symref.i_Index] = Upvalue.NewNil();
						
						stackframe.ClosureScope[symref.i_Index].Value() = value;
					}
					break;
				case SymbolRefType.DefaultEnv:
					{
						throw new ArgumentException("Can't AssignGenericSymbol on a DefaultEnv symbol");
					}
				default:
					throw new InternalErrorException("Unexpected {0} LRef at resolution: {1}", symref.i_Type, symref.i_Name);
			}
		}

		ref CallStackItem GetTopNonClrFunction()
		{
			for (int i = 0; i < m_ExecutionStack.Count; i++)
			{
				ref CallStackItem stackframe = ref m_ExecutionStack.Peek(i);
				
				if (stackframe.ClrFunction == null)
					return ref stackframe;
			}

			return ref Nil;
		}


		public SymbolRef FindSymbolByName(string name)
		{
			if (m_ExecutionStack.Count > 0)
			{
				ref CallStackItem stackframe = ref GetTopNonClrFunction();

				if (!stackframe.IsNil)
				{
					if (stackframe.Function.locals != null)
					{
						for (int i = stackframe.Function.locals.Length - 1; i >= 0; i--)
						{
							var l = stackframe.Function.locals[i];

							if (l.i_Name == name /*&& stackframe.LocalScope[i] != null*/) //should a local scope ever not be inited?
								return l;
						}
					}


					var closure = stackframe.ClosureScope;

					if (closure != null)
					{
						for (int i = 0; i < closure.Symbols.Length; i++)
							if (closure.Symbols[i] == name)
								return SymbolRef.Upvalue(name, i);
					}
				}
			}

			if (name != WellKnownSymbols.ENV)
			{
				SymbolRef env = FindSymbolByName(WellKnownSymbols.ENV);
				return SymbolRef.Global(name, env);
			}
			else
			{
				return SymbolRef.DefaultEnv;
			}
		}

	}
}
