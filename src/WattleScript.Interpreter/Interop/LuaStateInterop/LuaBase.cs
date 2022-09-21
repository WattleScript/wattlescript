// Disable warnings about XML documentation
#pragma warning disable 1591

using System;
using System.Text;
using lua_Integer = System.Int32;

namespace WattleScript.Interpreter.Interop.LuaStateInterop
{
	/// <summary>
	/// Classes using the classic interface should inherit from this class.
	/// This class defines only static methods and is really meant to be used only
	/// from C# and not other .NET languages. 
	/// 
	/// For easier operation they should also define:
	///		using ptrdiff_t = System.Int32;
	///		using lua_Integer = System.Int32;
	///		using LUA_INTFRM_T = System.Int64;
	///		using UNSIGNED_LUA_INTFRM_T = System.UInt64;
	/// </summary>
	public partial class LuaBase
	{
		protected const string LUA_INTFRMLEN = "l";

		protected static DynValue GetArgument(LuaState L, lua_Integer pos)
		{
			return L.At(pos);
		}

		protected static DynValue ArgAsType(LuaState L, lua_Integer pos, DataType type, bool allowNil = false)
		{
			return GetArgument(L, pos).CheckType(L.FunctionName, type, pos - 1, allowNil ? TypeValidationFlags.AllowNil | TypeValidationFlags.AutoConvert : TypeValidationFlags.AutoConvert);
		}

		protected static string LuaLCheckLString(LuaState L, lua_Integer argNum, out uint l, bool allowNil = false)
		{
			string str = ArgAsType(L, argNum, DataType.String, allowNil).String;
			l = (uint)(str?.Length ?? 0);
			return str;
		}

		protected static void LuaLAddValue(LuaState L, StringBuilder b)
		{
			b.Append(L.Pop().ToPrintString());
		}

		protected static lua_Integer LuaGetTop(LuaState L)
		{
			return L.Count;
		}

		protected static lua_Integer LuaLError(LuaState luaState, string message, params object[] args)
		{
			throw new ScriptRuntimeException(message, args);
		}
		

		protected static void LuaPushLiteral(LuaState L, string literalString)
		{
			L.Push(DynValue.NewString(literalString));
		}

		protected static void LuaLPushResult(LuaState L, StringBuilder b)
		{
			LuaPushLiteral(L, b.ToString());
		}
		
		protected static string LUA_QL(string p)
		{
			return "'" + p + "'";
		}

		protected static void LuaLArgError(LuaState L, lua_Integer arg, string p)
		{
			throw ScriptRuntimeException.BadArgument(arg - 1, L.FunctionName, p);
		}

		protected static double LuaLCheckNumber(LuaState L, lua_Integer pos)
		{
			DynValue v = ArgAsType(L, pos, DataType.Number, false);
			return v.Number;
		}

		protected static void LuaPushValue(LuaState L, lua_Integer arg)
		{
			DynValue v = L.At(arg);
			L.Push(v);
		}
	}
}
