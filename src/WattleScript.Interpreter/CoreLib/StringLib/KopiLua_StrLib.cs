// Disable warnings about XML documentation
#pragma warning disable 1591

//
// This part taken from KopiLua - https://github.com/NLua/KopiLua
//
// =========================================================================================================
//
// Kopi Lua License
// ----------------
// MIT License for KopiLua
// Copyright (c) 2012 LoDC
// Permission is hereby granted, free of charge, to any person obtaining a copy of this software and
// associated documentation files (the "Software"), to deal in the Software without restriction,
// including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so,
// subject to the following conditions:
// The above copyright notice and this permission notice shall be included in all copies or substantial
// portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT
// LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE
// SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
// ===============================================================================
// Lua License
// -----------
// Lua is licensed under the terms of the MIT license reproduced below.
// This means that Lua is free software and can be used for both academic
// and commercial purposes at absolutely no cost.
// For details and rationale, see http://www.lua.org/license.html .
// ===============================================================================
// Copyright (C) 1994-2008 Lua.org, PUC-Rio.
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.


using System.Text;
using WattleScript.Interpreter.Interop.LuaStateInterop;
using lua_Integer = System.Int32;
using LUA_INTFRM_T = System.Int64;
using ptrdiff_t = System.Int32;
using UNSIGNED_LUA_INTFRM_T = System.UInt64;

namespace WattleScript.Interpreter.CoreLib.StringLib
{
	internal class KopiLua_StringLib : LuaBase
	{
		private const char L_ESC = '%';

		/* valid flags in a format specification */
		public const string FLAGS = "-+ #0";
		
		private static void AppendQuoted(LuaState L, StringBuilder b, int arg)
		{
			uint l;
			CharPtr s = LuaLCheckLString(L, arg, out l);
			b.Append('"');
			while ((l--) != 0)
			{
				switch (s[0])
				{
					case '"':
					case '\\':
					case '\n':
					{
						b.Append('\\');
						b.Append(s[0]);
						break;
					}
					case '\r':
					{
						b.Append("\\r");
						break;
					}
					default:
						{
							if (s[0] < (char)16)
							{
								bool isfollowedbynum = false;

								if (l >= 1)
								{
									if (char.IsNumber(s[1]))
										isfollowedbynum = true;
								}

								if (isfollowedbynum)
									b.Append(string.Format("\\{0:000}", (int)s[0]));
								else
									b.Append(string.Format("\\{0}", (int)s[0]));
							}
							else
							{
								b.Append(s[0]);
							}
							break;
						}
				}
				s = s.next();
			}
			b.Append('"');
		}

		private static CharPtr scanformat(LuaState L, CharPtr strfrmt, out string form)
		{
			form = null;
			CharPtr p = strfrmt;
			while (p[0] != '\0' && FLAGS.IndexOf(p[0]) != -1) p = p.next();  /* skip flags */
			if ((uint)(p - strfrmt) >= (FLAGS.Length + 1))
				LuaLError(L, "invalid format (repeated flags)");
			if (char.IsDigit(p[0])) p = p.next();  /* skip width */
			if (char.IsDigit(p[0])) p = p.next();  /* (2 digits at most) */
			if (p[0] == '.')
			{
				p = p.next();
				if (char.IsDigit(p[0])) p = p.next();  /* skip precision */
				if (char.IsDigit(p[0])) p = p.next();  /* (2 digits at most) */
			}
			if (char.IsDigit(p[0]))
				LuaLError(L, "invalid format (width or precision too long)");
			form = "%" + strfrmt.ToString(p - strfrmt + 1);
			return p;
		}


		private static string addintlen(string form)
		{
			return form.Substring(0, form.Length - 1) + LUA_INTFRMLEN + form[form.Length - 1];
		}


		public static int str_format(LuaState L)
		{
			int top = LuaGetTop(L);
			int arg = 1;
			uint sfl;
			CharPtr strfrmt = LuaLCheckLString(L, arg, out sfl);
			CharPtr strfrmt_end = strfrmt + sfl;
			StringBuilder b = new StringBuilder();
			while (strfrmt < strfrmt_end)
			{
				if (strfrmt[0] != L_ESC)
				{
					b.Append(strfrmt[0]);
					strfrmt = strfrmt.next();
				}
				else if (strfrmt[1] == L_ESC)
				{
					b.Append(strfrmt[0]);
					strfrmt = strfrmt + 2;
				}
				else
				{ /* format item */
					strfrmt = strfrmt.next();
					string form;
					string buffer;
					if (++arg > top)
						LuaLArgError(L, arg, "no value");
					strfrmt = scanformat(L, strfrmt, out form);
					char ch = strfrmt[0];
					strfrmt = strfrmt.next();
					switch (ch)
					{
						case 'c':
							{
								buffer = Tools.sprintf(form, (int)LuaLCheckNumber(L, arg));
								break;
							}
						case 'd':
						case 'i':
							{
								form = addintlen(form);
								buffer = Tools.sprintf(form, (LUA_INTFRM_T) LuaLCheckNumber(L, arg));
								break;
							}
						case 'o':
						case 'u':
						case 'x':
						case 'X':
							{
								form = addintlen(form);
								buffer = Tools.sprintf(form, (LUA_INTFRM_T) LuaLCheckNumber(L, arg));
								break;
							}
						case 'e':
						case 'E':
						case 'f':
						case 'g':
						case 'G':
							{
								buffer = Tools.sprintf( form, (double)LuaLCheckNumber(L, arg));
								break;
							}
						case 'q':
							{
								AppendQuoted(L, b, arg);
								continue;  /* skip the 'addsize' at the end */
							}
						case 's':
							{
								uint l;
								CharPtr s = LuaLCheckLString(L, arg, out l, true);
								if ((form.IndexOf('.') == -1) && l >= 100)
								{
									/* no precision and string is too long to be formatted;
									   keep original string */
									LuaPushValue(L, arg);
									LuaLAddValue(L, b);
									continue;  /* skip the `addsize' at the end */
								}
								else
								{
									buffer = Tools.sprintf(form, s);
									break;
								}
							}
						default:
							{  /* also treat cases `pnLlh' */
								return LuaLError(L, "invalid option " + LUA_QL("%" + ch) + " to " +
													 LUA_QL("format"), strfrmt[-1]);
							}
					}
					if (!string.IsNullOrEmpty(buffer)) b.Append(buffer);
				}
			}
			LuaLPushResult(L, b);
			return 1;
		}
		
	}
}
