using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WattleScript.Interpreter.Serialization.Json
{
	/// <summary>
	/// UserData representing a null value in a table converted from Json
	/// </summary>
	public sealed class JsonNull
	{
		public static bool isNull() { return true; }

		[WattleScriptHidden]
		public static bool IsJsonNull(DynValue v)
		{
			return v.Type == DataType.UserData &&
				v.UserData.Descriptor != null &&
				v.UserData.Descriptor.Type == typeof(JsonNull);
		}

		[WattleScriptHidden]
		public static DynValue Create()
		{
			return UserData.CreateStatic<JsonNull>();
		}
	}
}
