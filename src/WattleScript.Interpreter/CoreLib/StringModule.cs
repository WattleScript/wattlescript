// Disable warnings about XML documentation
#pragma warning disable 1591

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using WattleScript.Interpreter.CoreLib.StringLib;

namespace WattleScript.Interpreter.CoreLib
{
	/// <summary>
	/// Class implementing string Wattle & Lua functions 
	/// </summary>
	[WattleScriptModule(Namespace = "string")]
	public class StringModule
	{
		public const string BASE64_DUMP_HEADER = "WattleScript_dump_b64::";

		public static void WattleScriptInit(Table globalTable, Table stringTable)
		{
			Table stringMetatable = new Table(globalTable.OwnerScript);
			stringMetatable.Set("__index", DynValue.NewTable(stringTable));
			globalTable.OwnerScript.SetTypeMetatable(DataType.String, stringMetatable);
		}


		[WattleScriptModuleMethod]
		public static DynValue dump(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue fn = args.AsType(0, "dump", DataType.Function, false);

			try
			{
				byte[] bytes;
				using (MemoryStream ms = new MemoryStream())
				{
					executionContext.GetScript().Dump(fn, ms);
					ms.Seek(0, SeekOrigin.Begin);
					bytes = ms.ToArray();
				}
				string base64 = Convert.ToBase64String(bytes);
				return DynValue.NewString(BASE64_DUMP_HEADER + base64);
			}
			catch (Exception ex)
			{
				throw new ScriptRuntimeException(ex.Message);
			}
		}


		[WattleScriptModuleMethod]
		public static DynValue @char(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			StringBuilder sb = new StringBuilder(args.Count);

			for (int i = 0; i < args.Count; i++)
			{
				DynValue v = args[i];
				double d = 0d;

				if (v.Type == DataType.String)
				{
					double? nd = v.CastToNumber();
					if (nd == null)
						args.AsType(i, "char", DataType.Number, false);
					else
						d = nd.Value;
				}
				else
				{
					args.AsType(i, "char", DataType.Number, false);
					d = v.Number;
				}

				sb.Append((char)(d));
			}

			return DynValue.NewString(sb.ToString());
		}


		[WattleScriptModuleMethod]
		public static DynValue @byte(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue vs = args.AsType(0, "byte", DataType.String, false);
			DynValue vi = args.AsType(1, "byte", DataType.Number, true);
			DynValue vj = args.AsType(2, "byte", DataType.Number, true);

			return PerformByteLike(vs, vi, vj,
				i => Unicode2Ascii(i));
		}

		[WattleScriptModuleMethod]
		public static DynValue unicode(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue vs = args.AsType(0, "unicode", DataType.String, false);
			DynValue vi = args.AsType(1, "unicode", DataType.Number, true);
			DynValue vj = args.AsType(2, "unicode", DataType.Number, true);

			return PerformByteLike(vs, vi, vj, i => i);
		}

		private static int Unicode2Ascii(int i)
		{
			if (i >= 0 && i < 255)
				return i;

			return (int)'?';
		}

		private static DynValue PerformByteLike(DynValue vs, DynValue vi, DynValue vj, Func<int, int> filter)
		{
            StringRange range = StringRange.FromLuaRange(vi, vj, null);
            string s = range.ApplyToString(vs.String);

            int length = s.Length;
			DynValue[] rets = new DynValue[length];

            for (int i = 0; i < length; ++i)
            {
                rets[i] = DynValue.NewNumber(filter((int)s[i]));
            }

			return DynValue.NewTuple(rets);
		}


		private static int? AdjustIndex(string s, DynValue vi, int defval)
		{
			if (vi.IsNil())
				return defval;

			int i = (int)Math.Round(vi.Number, 0);

			if (i == 0)
				return null;

			if (i > 0)
				return i - 1;

			return s.Length + i;
		}


		[WattleScriptModuleMethod]
		public static DynValue len(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue vs = args.AsType(0, "len", DataType.String, false);
			return DynValue.NewNumber(vs.String.Length);
		}

		//Skips named groups, needed for balanced parsing
		static Group[] GetGroups(Regex r, Match m)
		{
			var names = r.GetGroupNames();
			return names.Where(x => char.IsDigit(x[0])).Select(x => m.Groups[x]).ToArray();
		}

		private static DynValue IndexOrCapture(int i, Capture capture, bool[] indexReturn)
			=> indexReturn != null && i < indexReturn.Length && indexReturn[i]
				? DynValue.NewNumber (capture.Index + 1)
				: DynValue.NewString(capture.Value);

		[WattleScriptModuleMethod]
		public static DynValue match(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			string arg_s = args.AsType(0, "match", DataType.String, false).String;
			string arg_pattern = args.AsType(1, "match", DataType.String, false).String;
			int arg_index = AdjustIndex(arg_s, args.AsType(2, "find", DataType.Number, true), 0) ?? 0;

			if (string.IsNullOrEmpty(arg_s)) return DynValue.Nil;
			if(string.IsNullOrEmpty(arg_pattern)) return DynValue.Nil;
			if (arg_index >= arg_s.Length) return DynValue.Nil;
			
			var r = PatternRegex.PatternToRegex(arg_pattern, out bool[] captureInfo);
			var m = r.Match(arg_s, arg_index);

			
			
			if (m.Success)
			{
				var groups = GetGroups(r, m);
				if (groups.Length > 1)
				{
					var result = new DynValue[groups.Length - 1];
					for (var i = 1; i < groups.Length; i++)
						result[i - 1] = IndexOrCapture(i - 1, groups[i], captureInfo);
					return DynValue.NewTuple(result);
				}
				else
				{
					var result = new DynValue[m.Captures.Count];
					for (var i = 0; i < m.Captures.Count; i++)
						result[i] = IndexOrCapture(i, m.Captures[i], captureInfo);
					return DynValue.NewTuple(result);
				}
			}
			return DynValue.Nil;
		}

		static IEnumerable<DynValue> MatchEnumerator(Regex r, string str)
		{
			foreach (Match match in r.Matches(str))
			{
				var g = GetGroups(r, match);
				if (g.Length == 1) yield return DynValue.NewString(match.Value);
				else
				{
					var result = new DynValue[g.Length - 1];
					for (var i = 1; i < g.Length; i++)
						result[i - 1] = DynValue.NewString(g[i].Value);
					yield return DynValue.NewTuple(result);
				}
			}
		}
		

		[WattleScriptModuleMethod]
		public static DynValue gmatch(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			string arg_s = args.AsType(0, "gsub", DataType.String, false).String;
			string arg_pattern = args.AsType(1, "gsub", DataType.String, false).String;
			var r = PatternRegex.PatternToRegex(arg_pattern, out _);

			return DynValue.FromObject(executionContext.GetScript(), MatchEnumerator(r, arg_s));
		}

		static (string str, int index)[] ParseGSubString(string input)
		{
			List<(string str, int index)> items = new List<(string str, int index)>();
			int i = 0;
			while (i < input.Length)
			{
				int next = input.IndexOf('%', i);
				if (next == -1) {
					items.Add((input.Substring(i), -1));
					break;
				}
				if (next == input.Length - 1) {
					throw new ScriptRuntimeException("invalid use of '%' in replacement string");
				}
				if (next != i) {
					items.Add((input.Substring(i, next - i), -1));	
				}
				if(input[next + 1] == '%')
					items.Add(("%", -1));
				else if (input[next + 1] >= '0' && input[next + 1] <= '9') {
					items.Add((null, (int)(input[next + 1] - '0')));
				} else {
					throw new ScriptRuntimeException("invalid use of '%' in replacement string");
				}
				i = next + 2;
			}
			return items.ToArray();
		}
		
		[WattleScriptModuleMethod]
		public static DynValue gsub(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			string arg_s = args.AsType(0, "gsub", DataType.String, false).String;
			string arg_pattern = args.AsType(1, "gsub", DataType.String, false).String;
			var arg_replacement = args[2];
			int arg_maxreplace = args.AsType(3, "gsub", DataType.Number, true).CastToInt() ?? int.MaxValue;

			var r = PatternRegex.PatternToRegex(arg_pattern, out _);
			switch (arg_replacement.Type)
			{
				case DataType.Number:
				{
					int count = 0;
					var str = arg_replacement.ToPrintString();
					var result = r.Replace(arg_s, m => { 
						count++;
						return str;
					}, arg_maxreplace);
					return DynValue.NewTuple(DynValue.NewString(result), DynValue.NewNumber(count));
				}
				case DataType.String:
					if (arg_replacement.String.IndexOf('%') == -1)
					{
						int count = 0;
						var result = r.Replace(arg_s, m => { 
							count++;
							return arg_replacement.String;
						}, arg_maxreplace);
						return DynValue.NewTuple(DynValue.NewString(result), DynValue.NewNumber(count));
					}
					else
					{
						var instructions = ParseGSubString(arg_replacement.String);
						int count = 0;
						var result = r.Replace(arg_s, m =>
						{
							count++;
							var builder = new StringBuilder();
							var g = GetGroups(r, m);
							foreach (var i in instructions)
							{
								if (i.index == -1) builder.Append(i.str);
								else if (i.index == 0) builder.Append(m.Value);
								else if (i.index < g.Length) {
									builder.Append(g[i.index].Value);
								} else {
									throw new ScriptRuntimeException($"invalid capture index %{i.index}");
								}
							}
							return builder.ToString();
						}, arg_maxreplace);
						return DynValue.NewTuple(DynValue.NewString(result), DynValue.NewNumber(count));
					}
				case DataType.Function:
				case DataType.ClrFunction:
				{
					int count = 0;
					var result = r.Replace(arg_s, m =>
					{
						count++;
						var g = GetGroups(r, m);
						var args = new DynValue[g.Length - 1];
						for(var i = 1; i < g.Length; i++)
							args[i - 1] = DynValue.NewString(g[i].Value);
						return executionContext.Call(arg_replacement, args).ToScalar().ToPrintString();
					}, arg_maxreplace);
					return DynValue.NewTuple(DynValue.NewString(result), DynValue.NewNumber(count));
				}
				case DataType.Table:
				{
					int count = 0;
					var result = r.Replace(arg_s, m =>
					{
						count++;
						var g = GetGroups(r, m);
						var val = arg_replacement.Table.Get(g.Length < 2 ? m.Value : g[1].Value);
						if (val.IsNil()) return m.Value;
						if (val.Type == DataType.Number) return val.ToPrintString();
						else if (val.Type == DataType.String) return val.String;
						else
						{
							throw new ScriptRuntimeException(
								$"invalid replacement value (a {val.Type.ToErrorTypeString()})");
						}
					}, arg_maxreplace);
					return DynValue.NewTuple(DynValue.NewString(result), DynValue.NewNumber(count));
				}
				default:
				{
					throw ScriptRuntimeException.BadArgument(2, "gsub", "string/function/table expected");
				}
			}
		}

		[WattleScriptModuleMethod]
		public static DynValue find(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			string arg_s = args.AsType(0, "find", DataType.String, false).String;
			string arg_pattern = args.AsType(1, "find", DataType.String, false).String;
			int arg_index = AdjustIndex(arg_s, args.AsType(2, "find", DataType.Number, true), 0) ?? 0;
			bool plain = args[3].CastToBool();
			
			if (string.IsNullOrEmpty(arg_s)) return DynValue.Nil;
			if(string.IsNullOrEmpty(arg_pattern)) return DynValue.Nil;
			if (arg_index >= arg_s.Length) return DynValue.Nil;

			if (plain)
			{
				var idx = arg_s.IndexOf(arg_pattern, arg_index);
				if (idx != -1)
					return DynValue.NewTuple(
						DynValue.NewNumber(idx + 1), DynValue.NewNumber(idx + arg_pattern.Length));
			}
			else
			{
				var r = PatternRegex.PatternToRegex(arg_pattern, out _);
				var match = r.Match(arg_s, arg_index);
				if (match.Success)
				{
					var g = GetGroups(r, match);
					var result = new DynValue[g.Length + 1];
					result[0] = DynValue.NewNumber(match.Index + 1);
					result[1] = DynValue.NewNumber(match.Index + match.Length);
					for (var i = 1; i < g.Length; i++)
						result[i + 1] = DynValue.NewString(g[i].Value);
					return DynValue.NewTuple(result);
				}
			}
			return DynValue.Nil;
		}


        [WattleScriptModuleMethod]
        public static DynValue lower(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue arg_s = args.AsType(0, "lower", DataType.String, false);
            return DynValue.NewString(arg_s.String.ToLower());
        }

        [WattleScriptModuleMethod]
        public static DynValue upper(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue arg_s = args.AsType(0, "upper", DataType.String, false);
            return DynValue.NewString(arg_s.String.ToUpper());
        }

        [WattleScriptModuleMethod]
        public static DynValue rep(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue arg_s = args.AsType(0, "rep", DataType.String, false);
            DynValue arg_n = args.AsType(1, "rep", DataType.Number, false);
			DynValue arg_sep = args.AsType(2, "rep", DataType.String, true);

            if (String.IsNullOrEmpty(arg_s.String) || (arg_n.Number < 1))
            {
                return DynValue.NewString("");
            }

			string sep = (arg_sep.IsNotNil()) ? arg_sep.String : null;

            int count = (int)arg_n.Number;
            StringBuilder result = new StringBuilder(arg_s.String.Length * count);

            for (int i = 0; i < count; ++i)
            {
				if (i != 0 && sep != null)
					result.Append(sep);

                result.Append(arg_s.String);
            }

            return DynValue.NewString(result.ToString());
        }

		[WattleScriptModuleMethod]
		public static DynValue format(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue arg_s = args.AsType(0, "format", DataType.String, false);
			return DynValue.NewString(KopiLua.sprintf(arg_s.String, args, 1));
		}



        [WattleScriptModuleMethod]
        public static DynValue reverse(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue arg_s = args.AsType(0, "reverse", DataType.String, false);

            if (String.IsNullOrEmpty(arg_s.String))
            {
                return DynValue.NewString("");
            }

            char[] elements = arg_s.String.ToCharArray();
            Array.Reverse(elements);

            return DynValue.NewString(new String(elements));
        }

        [WattleScriptModuleMethod]
        public static DynValue sub(ScriptExecutionContext executionContext, CallbackArguments args)
        {
            DynValue arg_s = args.AsType(0, "sub", DataType.String, false);
			DynValue arg_i = args.AsType(1, "sub", DataType.Number, true);
            DynValue arg_j = args.AsType(2, "sub", DataType.Number, true);

			StringRange range = StringRange.FromLuaRange(arg_i, arg_j, -1);
            string s = range.ApplyToString(arg_s.String);

            return DynValue.NewString(s);
        }

		[WattleScriptModuleMethod]
		public static DynValue startsWith(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue arg_s1 = args.AsType(0, "startsWith", DataType.String, true);
			DynValue arg_s2 = args.AsType(1, "startsWith", DataType.String, true);

			if (arg_s1.IsNil() || arg_s2.IsNil())
				return DynValue.False;

			return DynValue.NewBoolean(arg_s1.String.StartsWith(arg_s2.String));
		}

		[WattleScriptModuleMethod]
		public static DynValue endsWith(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue arg_s1 = args.AsType(0, "endsWith", DataType.String, true);
			DynValue arg_s2 = args.AsType(1, "endsWith", DataType.String, true);

			if (arg_s1.IsNil() || arg_s2.IsNil())
				return DynValue.False;

			return DynValue.NewBoolean(arg_s1.String.EndsWith(arg_s2.String));
		}

		[WattleScriptModuleMethod]
		public static DynValue contains(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			DynValue arg_s1 = args.AsType(0, "contains", DataType.String, true);
			DynValue arg_s2 = args.AsType(1, "contains", DataType.String, true);

			if (arg_s1.IsNil() || arg_s2.IsNil())
				return DynValue.False;

			return DynValue.NewBoolean(arg_s1.String.Contains(arg_s2.String));
		}
	}
}
