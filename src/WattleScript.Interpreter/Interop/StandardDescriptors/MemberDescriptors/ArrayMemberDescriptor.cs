using System;
using WattleScript.Interpreter.Interop.BasicDescriptors;
using WattleScript.Interpreter.Interop.Converters;

namespace WattleScript.Interpreter.Interop
{
	/// <summary>
	/// Member descriptor for indexer of array types
	/// </summary>
	public class ArrayMemberDescriptor : ObjectCallbackMemberDescriptor 
	{
		bool m_IsSetter;

		/// <summary>
		/// Initializes a new instance of the <see cref="ArrayMemberDescriptor"/> class.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="isSetter">if set to <c>true</c> is a setter indexer.</param>
		/// <param name="indexerParams">The indexer parameters.</param>
		public ArrayMemberDescriptor(string name, bool isSetter, ParameterDescriptor[] indexerParams)
			: base(
			name,
			isSetter ? (Func<object, ScriptExecutionContext, CallbackArguments, object>)ArrayIndexerSet : (Func<object, ScriptExecutionContext, CallbackArguments, object>)ArrayIndexerGet,
			indexerParams)
		{
			m_IsSetter = isSetter;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ArrayMemberDescriptor"/> class.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="isSetter">if set to <c>true</c> [is setter].</param>
		public ArrayMemberDescriptor(string name, bool isSetter)
			: base(
			name,
			isSetter ? (Func<object, ScriptExecutionContext, CallbackArguments, object>)ArrayIndexerSet : (Func<object, ScriptExecutionContext, CallbackArguments, object>)ArrayIndexerGet)
		{
			m_IsSetter = isSetter;
		}


		private static int[] BuildArrayIndices(CallbackArguments args, int count)
		{
			int[] indices = new int[count];

			for (int i = 0; i < count; i++)
				indices[i] = args.AsInt(i, "userdata_array_indexer");

			return indices;
		}

		private static object ArrayIndexerSet(object arrayObj, ScriptExecutionContext ctx, CallbackArguments args)
		{
			Array array = (Array)arrayObj;
			int[] indices = BuildArrayIndices(args, args.Count - 1);
			DynValue value = args[args.Count - 1];

			Type elemType = array.GetType().GetElementType();

			object objValue = ScriptToClrConversions.DynValueToObjectOfType(value, elemType, null, false);

			array.SetValue(objValue, indices);

			return DynValue.Void;
		}


		private static object ArrayIndexerGet(object arrayObj, ScriptExecutionContext ctx, CallbackArguments args)
		{
			Array array = (Array)arrayObj;
			int[] indices = BuildArrayIndices(args, args.Count);

			return array.GetValue(indices);
		}

	}
}
