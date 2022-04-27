using System;
using System.Globalization;
using System.Linq;
using System.Text;

namespace WattleScript.Interpreter.Tree
{
	internal static class LexerUtils
	{
		public static double ParseNumber(Token T)
		{
			string txt = T.Text;
			double res;
			if (!double.TryParse(txt, NumberStyles.Float, CultureInfo.InvariantCulture, out res))
				throw new SyntaxErrorException(T, "malformed number near '{0}'", txt);

			return res;
		}

		public static double ParseHexInteger(Token T)
		{
			string txt = T.Text;
			if ((txt.Length < 2) || (txt[0] != '0' && (char.ToUpper(txt[1]) != 'X')))
				throw new InternalErrorException("hex numbers must start with '0x' near '{0}'.", txt);

			ulong res;

			if (!ulong.TryParse(txt.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out res))
				throw new SyntaxErrorException(T, "malformed number near '{0}'", txt);

			return (double)res;
		}

		public static string ReadHexProgressive(string s, ref double d, out int digits)
		{
			digits = 0;

			for (int i = 0; i < s.Length; i++)
			{
				char c = s[i];

				if (LexerUtils.CharIsHexDigit(c))
				{
					int v = LexerUtils.HexDigit2Value(c);
					d *= 16.0;
					d += v;
					++digits;
				}
				else
				{
					return s.Substring(i);
				}
			}

			return string.Empty;
		}

		public static double ParseHexFloat(Token T)
		{
			string s = T.Text;

			try
			{
				if ((s.Length < 2) || (s[0] != '0' && (char.ToUpper(s[1]) != 'X')))
					throw new InternalErrorException("hex float must start with '0x' near '{0}'", s);

				s = s.Substring(2);

				double value = 0.0;
				int dummy, exp = 0;

				s = ReadHexProgressive(s, ref value, out dummy);

				if (s.Length > 0 && s[0] == '.')
				{
					s = s.Substring(1);
					s = ReadHexProgressive(s, ref value, out exp);
				}

				exp *= -4;

				if (s.Length > 0 && char.ToUpper(s[0]) == 'P')
				{
					if (s.Length == 1)
						throw new SyntaxErrorException(T, "invalid hex float format near '{0}'", s);

					s = s.Substring(s[1] == '+' ? 2 : 1);

					int exp1 = int.Parse(s, CultureInfo.InvariantCulture);

					exp += exp1;
				}

				double result = value * Math.Pow(2, exp);
				return result;
			}
			catch (FormatException)
			{
				throw new SyntaxErrorException(T, "malformed number near '{0}'", s);
			}
		}


		public static int HexDigit2Value(char c)
		{
			if (c >= '0' && c <= '9')
				return c - '0';
			else if (c >= 'A' && c <= 'F')
				return 10 + (c - 'A');
			else if (c >= 'a' && c <= 'f')
				return 10 + (c - 'a');
			else
				throw new InternalErrorException("invalid hex digit near '{0}'", c);
		}

		public static bool CharIsDigit(char c)
		{
			return c >= '0' && c <= '9';
		}

		public static bool CharIsHexDigit(char c)
		{
			return CharIsDigit(c) ||
				c == 'a' || c == 'b' || c == 'c' || c == 'd' || c == 'e' || c == 'f' ||
				c == 'A' || c == 'B' || c == 'C' || c == 'D' || c == 'E' || c == 'F';
		}

		public static string AdjustLuaLongString(string str)
		{
			if (str.StartsWith("\r\n"))
				str = str.Substring(2);
			else if (str.StartsWith("\n"))
				str = str.Substring(1);

			return str;
		}

		public static string UnescapeLuaString(Token token, string str)
		{
			if (!str.Contains('\\'))
				return str;

			StringBuilder sb = new StringBuilder();

			bool escape = false;
			bool hex = false;
			int unicode_state = 0;
			string hexprefix = "";
			string val = "";
			bool zmode = false;
			bool parsing_unicode_without_brks = false;

			for (int i = 0; i < str.Length; i++)
			{
				redo:
				char c = str[i];
				if (escape)
				{
					if (val.Length == 0 && !hex && unicode_state == 0)
					{
						switch (c)
						{
							case 'a':
								sb.Append('\a');
								escape = false;
								zmode = false;
								break;
							case '\r':
								break; // this makes \\r\n -> \\n
							case '\n':
								sb.Append('\n');
								escape = false;
								break;
							case 'b':
								sb.Append('\b');
								escape = false;
								break;
							case 'f':
								sb.Append('\f');
								escape = false;
								break;
							case 'n':
								sb.Append('\n');
								escape = false;
								break;
							case 'r':
								sb.Append('\r');
								escape = false;
								break;
							case 't':
								sb.Append('\t');
								escape = false;
								break;
							case 'v':
								sb.Append('\v');
								escape = false;
								break;
							case '\\':
								sb.Append('\\');
								escape = false;
								zmode = false;
								break;
							case '"':
								sb.Append('\"');
								escape = false;
								zmode = false;
								break;
							case '\'':
								sb.Append('\'');
								escape = false;
								zmode = false;
								break;
							case '[':
								sb.Append('[');
								escape = false;
								zmode = false;
								break;
							case ']':
								sb.Append(']');
								escape = false;
								zmode = false;
								break;
							case '{':
								sb.Append('{');
								escape = false;
								zmode = false;
								break;
							case '}':
								sb.Append('}');
								escape = false;
								zmode = false;
								break;
							case '`':
								sb.Append('`');
								escape = false;
								zmode = false;
								break;
							case 'x':
								hex = true;
								break;
							case 'u':
								unicode_state = 1;
								break;
							case 'z':
								zmode = true;
								escape = false;
								break;
							default:
							{
								if (CharIsDigit(c))
								{
									val = val + c;
								}
								else throw new SyntaxErrorException(token, "invalid escape sequence near '\\{0}'", c);

								break;
							}
						}
					}
					else
					{
						void EndSequence()
						{
							try
							{
								if (int.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsedVal))
								{
									sb.Append(ConvertUtf32ToChar(parsedVal));
									unicode_state = 0;
									val = string.Empty;
									escape = false;
									parsing_unicode_without_brks = false;
								}
								else
								{
									throw new SyntaxErrorException(token, $"Parsing unicode sequence failed at index {i}, char '{c}'. {val} could not be parsed to an int.");
								}
							}
							catch (Exception e)
							{
								throw new SyntaxErrorException(token, $"Parsing unicode sequence failed at index {i} '{c}'. Native message: {e.Message}");
							}
						}

						if (unicode_state == 1)
						{
							if (c != '{')
							{
								parsing_unicode_without_brks = true;
								val += c;
							}

							unicode_state = 2;
						}
						else if (unicode_state == 2)
						{
							if (!parsing_unicode_without_brks && c == '}')
							{
								EndSequence();
							}
							else if (val.Length >= 8)
							{
								if (parsing_unicode_without_brks)
								{
									throw new SyntaxErrorException(token, "Unicode code point too large after '\\u' (max 8 chars)");
								}

								throw new SyntaxErrorException(token, "'}' missing, or unicode code point too large after '\\u' (max 8 chars)");
							}
							else if (parsing_unicode_without_brks && !CharIsHexDigit(c))
							{
								EndSequence();
								goto redo;
							}
							else
							{
								val += c;
							}
						}
						else if (hex)
						{
							if (CharIsHexDigit(c))
							{
								val += c;
								if (val.Length == 2)
								{
									if (int.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsedVal))
									{
										sb.Append(ConvertUtf32ToChar(parsedVal));
										zmode = false;
										escape = false;
									}
									else
									{
										throw new SyntaxErrorException(token, $"Parsing hexadecimal number failed. Parsed value: {val}", hexprefix, val, c);
									}
								}
							}
							else
							{
								throw new SyntaxErrorException(token, "hexadecimal digit expected near '\\{0}{1}{2}'", hexprefix, val, c);
							}
						}
						else if (val.Length > 0)
						{
							if (CharIsDigit(c))
							{
								val = val + c;
							}

							if (val.Length == 3 || !CharIsDigit(c))
							{
								if (int.TryParse(val, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedVal))
								{
									if (parsedVal > 255)
										throw new SyntaxErrorException(token, "decimal escape too large near '\\{0}'", val);

									sb.Append(ConvertUtf32ToChar(parsedVal));

									zmode = false;
									escape = false;

									if (!CharIsDigit(c))
										goto redo;	
								}
								else
								{
									throw new SyntaxErrorException(token, $"decimal escape parsing failed. {val} could not be parsed to an int");
								}
							}
						}
					}
				}
				else
				{
					if (c == '\\')
					{
						escape = true;
						hex = false;
						val = "";
					}
					else
					{
						if (!zmode || !char.IsWhiteSpace(c))
						{
							sb.Append(c);
							zmode = false;
						}
					}
				}
			}

			if (parsing_unicode_without_brks)
			{
				if (int.TryParse(val, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int parsedVal))
				{
					sb.Append(ConvertUtf32ToChar(parsedVal));
					unicode_state = 0;
					val = string.Empty;
					escape = false;
					parsing_unicode_without_brks = false;
				}
				else
				{
					throw new SyntaxErrorException(token, $"Parsing unicode sequence failed at index {str.Length - 1}, char '{str[str.Length - 1]}'. {val} could not be parsed to an int.");
				}
			}

			if (escape && !hex && val.Length > 0)
			{
				int i = int.Parse(val, CultureInfo.InvariantCulture);
				sb.Append(ConvertUtf32ToChar(i));
				escape = false;
			}

			if (escape)
			{
				throw new SyntaxErrorException(token, "unfinished string near '\"{0}\"'", sb.ToString());
			}

			return sb.ToString();
		}

		private static string ConvertUtf32ToChar(int i)
		{
			return char.ConvertFromUtf32(i);
		}

	}
}
