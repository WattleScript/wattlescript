using System;
using System.Collections.Generic;
using WattleScript.Interpreter.DataStructs;

namespace WattleScript.Interpreter.Execution.VM
{
	sealed partial class Processor
	{
		private static DynValue[] Internal_AdjustTuple(IList<DynValue> values)
		{
			while (true)
			{
				if (values == null || values.Count == 0) 
					return Array.Empty<DynValue>();

				if (values[values.Count - 1].Type == DataType.Tuple)
				{
					int baseLen = values.Count - 1 + values[values.Count - 1].Tuple.Length;
					DynValue[] result = new DynValue[baseLen];

					for (int i = 0; i < values.Count - 1; i++)
					{
						result[i] = values[i].ToScalar();
					}

					for (int i = 0; i < values[values.Count - 1].Tuple.Length; i++)
					{
						result[values.Count + i - 1] = values[values.Count - 1].Tuple[i];
					}

					if (result[result.Length - 1].Type == DataType.Tuple)
					{
						values = result;
					}
					else
						return result;
				}
				else
				{
					DynValue[] result = new DynValue[values.Count];

					for (int i = 0; i < values.Count; i++)
					{
						result[i] = values[i].ToScalar();
					}

					return result;
				}
			}
		}
		
		private int Internal_InvokeUnaryMetaMethod(DynValue op1, string eventName, int instructionPtr)
		{
			DynValue m = DynValue.Nil;

			if (op1.Type == DataType.UserData)
			{
				m = op1.UserData.Descriptor.MetaIndex(m_Script, op1.UserData.Object, eventName);
			}

			if (m.IsNil())
			{
				var op1_MetaTable = GetMetatable(op1);

				if (op1_MetaTable != null)
				{
					DynValue meta1 = op1_MetaTable.RawGet(eventName);
					if (meta1.IsNotNil())
						m = meta1;
				}
			}

			if (m.IsNotNil())
			{
				m_ValueStack.Push(m);
				m_ValueStack.Push(op1);
				return Internal_ExecCall(false, 1, instructionPtr);
			}

			return -1;
		}
		
		private int Internal_InvokeBinaryMetaMethod(DynValue l, DynValue r, string eventName, int instructionPtr, DynValue extraPush = default)
		{
			var m = GetBinaryMetamethod(l, r, eventName);

			if (m.IsNotNil())
			{
				if (extraPush.IsNotNil())
					m_ValueStack.Push(extraPush);

				m_ValueStack.Push(m);
				m_ValueStack.Push(l);
				m_ValueStack.Push(r);
				return Internal_ExecCall(false, 2, instructionPtr);
			}

			return -1;
		}
	}
}
