using System;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using WattleScript.Interpreter.Interop.RegistrationPolicies;

namespace WattleScript.Interpreter.Interop.Converters
{
	internal static class ClrToScriptConversions
	{
		/// <summary>
		/// Tries to convert a CLR object to a WattleScript value, using "trivial" logic.
		/// Skips on custom conversions, etc.
		/// Does NOT throw on failure.
		/// </summary>
		internal static DynValue TryObjectToTrivialDynValue(Script script, object obj)
		{
			if (obj == null)
				return DynValue.Nil;

			if (obj is DynValue)
				return (DynValue)obj;

			Type t = obj.GetType();

			if (obj is bool)
				return DynValue.NewBoolean((bool)obj);

			if (obj is string || obj is StringBuilder || obj is char)
				return DynValue.NewString(obj.ToString());

			if (NumericConversions.NumericTypes.Contains(t))
				return DynValue.NewNumber(NumericConversions.TypeToDouble(t, obj));

			if (obj is Table)
				return DynValue.NewTable((Table)obj);

			return DynValue.Nil;
		}


		/// <summary>
		/// Tries to convert a CLR object to a WattleScript value, using "simple" logic.
		/// Does NOT throw on failure.
		/// </summary>
		internal static DynValue TryObjectToSimpleDynValue(Script script, object obj)
		{
			switch (obj)
			{
				case null:
					return DynValue.Nil;
				case DynValue value:
					return value;
			}

			Func<Script, object, DynValue> converter = Script.GlobalOptions.CustomConverters.GetClrToScriptCustomConversion(obj.GetType());
			if (converter != null)
			{
				DynValue v = converter(script, obj);
				if (v.IsNotNil())
					return v;
			}

			Type t = obj.GetType();

			switch (obj)
			{
				case bool b:
					return DynValue.NewBoolean(b);
				case string _:
				case StringBuilder _:
				case char _:
					return DynValue.NewString(obj.ToString());
				case Closure closure:
					return DynValue.NewClosure(closure);
			}

			if (NumericConversions.NumericTypes.Contains(t))
				return DynValue.NewNumber(NumericConversions.TypeToDouble(t, obj));

			switch (obj)
			{
				case Table table:
					return DynValue.NewTable(table);
				case CallbackFunction function:
					return DynValue.NewCallback(function);
				case Delegate del:
				{
					MethodInfo mi = del.Method;
					
					if (CallbackFunction.CheckCallbackSignature(mi, false))
						return DynValue.NewCallback((Func<ScriptExecutionContext, CallbackArguments, DynValue>)del);
					break;
				}
				case Range range:
					return DynValue.NewRange(range);
			}

			return DynValue.Nil;
		}


		/// <summary>
		/// Tries to convert a CLR object to a WattleScript value, using more in-depth analysis
		/// </summary>
		internal static DynValue ObjectToDynValue(Script script, object obj)
		{
			switch (obj)
			{
				case null:
					return DynValue.Nil;
				case DynValue dyn:
					return dyn;
				case Task task:
					return ObjectToDynValue(script, new TaskWrapper(task));
			}

			DynValue v = TryObjectToSimpleDynValue(script, obj);

			if (v.IsNotNil()) return v;

			v = UserData.Create(obj);
			if (v.IsNotNil()) return v;

			switch (obj)
			{
				case Type type:
					v = UserData.CreateStatic(type);
					break;
				// unregistered enums go as integers
				case Enum _:
					return DynValue.NewNumber(NumericConversions.TypeToDouble(Enum.GetUnderlyingType(obj.GetType()), obj));
			}

			if (v.IsNotNil()) return v;

			switch (obj)
			{
				case Delegate del:
					return DynValue.NewCallback(CallbackFunction.FromDelegate(script, del));
				case MethodInfo {IsStatic: true} mi:
					return DynValue.NewCallback(CallbackFunction.FromMethodInfo(script, mi));
				case IList list:
				{
					Table t = TableConversions.ConvertIListToTable(script, list);
					return DynValue.NewTable(t);
				}
				case IDictionary dictionary:
				{
					Table t = TableConversions.ConvertIDictionaryToTable(script, dictionary);
					return DynValue.NewTable(t);
				}
			}

			DynValue enumerator = EnumerationToDynValue(script, obj);
			if (enumerator.IsNotNil()) return enumerator;
			
			throw ScriptRuntimeException.ConvertObjectFailed(obj);
		}

		/// <summary>
		/// Converts an IEnumerable or IEnumerator to a DynValue
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="obj">The object.</param>
		/// <returns></returns>
		public static DynValue EnumerationToDynValue(Script script, object obj)
		{
			if (obj is System.Collections.IEnumerable)
			{
				var enumer = (System.Collections.IEnumerable)obj;
				return EnumerableWrapper.ConvertIterator(script, enumer.GetEnumerator());
			}

			if (obj is System.Collections.IEnumerator)
			{
				var enumer = (System.Collections.IEnumerator)obj;
				return EnumerableWrapper.ConvertIterator(script, enumer);
			}

			return DynValue.Nil;
		}



	}
}
