// Disable warnings about XML documentation
#pragma warning disable 1591

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.CoreLib
{
	/// <summary>
	/// Class implementing basic Wattle & Lua functions (print, type, tostring, etc) as a WattleScript module.
	/// </summary>
	[WattleScriptModule]
	public class BasicModule
	{
		//type (v)
		//----------------------------------------------------------------------------------------------------------------
		//Returns the type of its only argument, coded as a string. The possible results of this function are "nil" 
		//(a string, not the value nil), "number", "string", "boolean", "table", "function", "thread", and "userdata". 
		[WattleScriptModuleMethod]
		public static DynValue type(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			if (args.Count < 1) throw ScriptRuntimeException.BadArgumentValueExpected(0, "type");

			DynValue v = args[0];
			return DynValue.NewString(v.Type.ToLuaTypeString());
		}



		//assert (v [, message])
		//----------------------------------------------------------------------------------------------------------------
		//Issues an error when the value of its argument v is false (i.e., nil or false); 
		//otherwise, returns all its arguments. message is an error message; when absent, it defaults to "assertion failed!" 
		[WattleScriptModuleMethod]
		public static DynValue assert(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue v = args[0];
			DynValue message = args[1];

			if (!v.CastToBool())
			{
				if (message.IsNil())
					throw new ScriptRuntimeException("assertion failed!"); // { DoNotDecorateMessage = true };
				else
					throw new ScriptRuntimeException(message.ToPrintString()); // { DoNotDecorateMessage = true };
			}

			return DynValue.NewTupleNested(args.GetArray());
		}

		// collectgarbage  ([opt [, arg]])
		// ----------------------------------------------------------------------------------------------------------------
		// This function is a stub. Lua scripts cannot force a .NET GC
		[WattleScriptModuleMethod]
		public static DynValue collectgarbage(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue opt = args[0];

			string mode = opt.CastToString();

			/*if (mode == null || mode == "collect" || mode == "restart")
			{
				GC.Collect();
			}*/

			return DynValue.Nil;
		}

		// error (message [, level])
		// ----------------------------------------------------------------------------------------------------------------
		// Terminates the last protected function called and returns message as the error message. Function error never returns.
		// Usually, error adds some information about the error position at the beginning of the message. 
		// The level argument specifies how to get the error position. 
		// With level 1 (the default), the error position is where the error function was called. 
		// Level 2 points the error to where the function that called error was called; and so on. 
		// Passing a level 0 avoids the addition of error position information to the message. 
		[WattleScriptModuleMethod]
		public static DynValue error(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue message = args.AsType(0, "error", DataType.String, false);
            		DynValue level = args.AsType(1, "error", DataType.Number, true);

            		Coroutine cor = executionContext.GetCallingCoroutine();

            		WatchItem[] stacktrace = cor.GetStackTrace(0, executionContext.CallingLocation);

            		var e = new ScriptRuntimeException(message.String);

            		if (level.IsNil())
            		{
                		level = DynValue.NewNumber(1); // Default
            		}

            		if (level.Number > 0 && level.Number < stacktrace.Length)
            		{
                    		// Lua allows levels up to max. value of a double, while this has to be cast to int
                    		// Probably never will be a problem, just leaving this note here
                    		WatchItem wi = stacktrace[(int)level.Number];

                    		e.DecorateMessage(executionContext.GetScript(), wi.Location);
            		}
            		else
            		{
                		e.DoNotDecorateMessage = true;
            		}

            		throw e;
		}


		// tostring (v)
		// ----------------------------------------------------------------------------------------------------------------
		// Receives a value of any type and converts it to a string in a reasonable format. (For complete control of how 
		// numbers are converted, use string.format.)
		// 
		// If the metatable of v has a "__tostring" field, then tostring calls the corresponding value with v as argument, 
		// and uses the result of the call as its result. 
		[WattleScriptModuleMethod]
		public static DynValue tostring(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			if (args.Count < 1) throw ScriptRuntimeException.BadArgumentValueExpected(0, "tostring");

			DynValue v = args[0];
			DynValue tail = executionContext.GetMetamethodTailCall(v, "__tostring", v);
			
			if (tail.IsNil())
				return DynValue.NewString(v.ToPrintString());

			tail.TailCallData.Continuation = new CallbackFunction(__tostring_continuation, "__tostring");

			return tail;
		}

		private static DynValue __tostring_continuation(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue b = args[0].ToScalar();

			if (b.IsNil())
				return b;

			if (b.Type != DataType.String)
				throw new ScriptRuntimeException("'tostring' must return a string");


			return b;
		}

		// select (index, ...)
		// -----------------------------------------------------------------------------
		// If index is a number, returns all arguments after argument number index; a negative number indexes from 
		// the end (-1 is the last argument). Otherwise, index must be the string "#", and select returns the total
		// number of extra arguments it received. 
		[WattleScriptModuleMethod]
		public static DynValue select(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			if (args[0].Type == DataType.String && args[0].String == "#")
			{
				if (args[args.Count - 1].Type == DataType.Tuple)
				{
					return DynValue.NewNumber(args.Count - 1 + args[args.Count - 1].Tuple.Length);
				}
				else
				{
					return DynValue.NewNumber(args.Count - 1);
				}
			}

			DynValue v_num = args.AsType(0, "select", DataType.Number, false);
			int num = (int)v_num.Number;

			List<DynValue> values = new List<DynValue>();

			if (num > 0)
			{
				for (int i = num; i < args.Count; i++)
					values.Add(args[i]);
			}
			else if (num < 0)
			{
				num = args.Count + num;

				if (num < 1)
					throw ScriptRuntimeException.BadArgumentIndexOutOfRange("select", 0);

				for (int i = num; i < args.Count; i++)
					values.Add(args[i]);
			}
			else
			{
				throw ScriptRuntimeException.BadArgumentIndexOutOfRange("select", 0);
			}

			return DynValue.NewTupleNested(values.ToArray());
		}




		// tonumber (e [, base])
		// ----------------------------------------------------------------------------------------------------------------
		// When called with no base, tonumber tries to convert its argument to a number. If the argument is already 
		// a number or a string convertible to a number (see §3.4.2), then tonumber returns this number; otherwise, 
		// it returns nil.
		//
		// When called with base, then e should be a string to be interpreted as an integer numeral in that base. 
		// The base may be any integer between 2 and 36, inclusive. In bases above 10, the letter 'A' (in either 
		// upper or lower case) represents 10, 'B' represents 11, and so forth, with 'Z' representing 35. If the 
		// string e is not a valid numeral in the given base, the function returns nil. 
		[WattleScriptModuleMethod]
		public static DynValue tonumber(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			if (args.Count < 1) throw ScriptRuntimeException.BadArgumentValueExpected(0, "tonumber");

			DynValue e = args[0];
			DynValue b = args.AsType(1, "tonumber", DataType.Number, true);

			if (b.IsNil())
			{
				if (e.Type == DataType.Number)
					return e;

				if (e.Type != DataType.String)
					return DynValue.Nil;

				double? res = e.CastToNumber();
				if (res != null) return DynValue.NewNumber(res.Value);
				return DynValue.Nil;
			}
			else
			{
				DynValue ee;

				if (args[0].Type != DataType.Number)
					ee = args.AsType(0, "tonumber", DataType.String, false);
				else
					ee = DynValue.NewString(args[0].Number.ToString(CultureInfo.InvariantCulture)); ;
				if (ee.String.Length < 0) return DynValue.Nil;
				int bb = (int)b.Number;

			    double uiv = 0;
			    var trimmed = ee.String.Trim();
			    if (trimmed.Length == 0) return DynValue.Nil;
			    bool negate = false;
			    int startIdx = 0;
			    if (trimmed[0] == '-') {
				    negate = true;
				    startIdx = 1;
			    }
			    if (trimmed[0] == '+') {
				    startIdx = 1;
			    }
				if (bb <= 36 && bb > 1)
			    {
			        for (int ij = startIdx; ij < trimmed.Length; ij++)
			        {
				        char ch = trimmed[ij];
				        int value;
				        if (ch >= 97) value = ch - 87;
				        else if (ch >= 65) value = ch - 55;
				        else value = ch - 48;
				        if (value < 0 || value >= bb)
			            {
				            return DynValue.Nil;
			            }
			            uiv = uiv * bb + value;
			        }
                }
			    else
			    {
                    throw new ScriptRuntimeException("bad argument #2 to 'tonumber' (base out of range)");
                }

                return DynValue.NewNumber(negate ? -uiv : uiv);
			}
		}

		[WattleScriptModuleMethod]
		public static DynValue print(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			StringBuilder sb = new StringBuilder();

			for (int i = 0; i < args.Count; i++)
			{
				if (args[i].IsVoid())
					break;

				if (i != 0)
					sb.Append('\t');

				sb.Append(args.AsStringUsingMeta(executionContext, i, "print"));
			}

			executionContext.GetScript().Options.DebugPrint(sb.ToString());

			return DynValue.Nil;
		}
	}
}
