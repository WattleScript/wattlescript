﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using WattleScript.Interpreter.Interop.Converters;
using WattleScript.Interpreter.Interop;

namespace WattleScript.Interpreter.Serialization
{
	public static class ObjectValueConverter
	{
		public static DynValue SerializeObjectToDynValue(Script script, object o, DynValue valueForNulls = default)
		{
			if (o == null)
				return valueForNulls;

			DynValue v = ClrToScriptConversions.TryObjectToTrivialDynValue(script, o);

			if (v.IsNotNil())
				return v;

			if (o is Enum)
				return DynValue.NewNumber(NumericConversions.TypeToDouble(Enum.GetUnderlyingType(o.GetType()), o));

			Table t = new Table(script);

			System.Collections.IEnumerable ienum = o as System.Collections.IEnumerable;

			if (ienum != null)
			{
				foreach (object obj in ienum)
				{
					t.Append(SerializeObjectToDynValue(script, obj, valueForNulls));
				}
			}
			else
			{
				Type type = o.GetType();

				foreach (PropertyInfo pi in type.GetAllProperties())
				{
					var getter = pi.GetGetMethod(true);
					var isStatic = getter.IsStatic;
					var obj = getter.Invoke(isStatic ? null : o, null); // convoluted workaround for --full-aot Mono execution

					t.Set(pi.Name, SerializeObjectToDynValue(script, obj, valueForNulls));
				}
			}

			return DynValue.NewTable(t);
		}
	}
}
