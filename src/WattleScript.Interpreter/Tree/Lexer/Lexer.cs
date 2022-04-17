﻿using System;
using System.Collections.Generic;
using System.Text;

namespace WattleScript.Interpreter.Tree
{
	class Lexer
	{
		private Token m_Current;
		private string m_Code;
		private int m_PrevLineTo;
		private int m_PrevColTo = 1;
		private int m_Cursor;
		private int m_Line = 1;
		private int m_Col;
		private int m_SourceId;
		private bool m_AutoSkipComments;
		private ScriptSyntax m_Syntax;
		private HashSet<string> m_Directives;

		public Lexer(int sourceID, string scriptContent, bool autoSkipComments, ScriptSyntax syntax, HashSet<string> directives)
		{
			m_Code = scriptContent;
			m_SourceId = sourceID;

			// remove unicode BOM if any
			if (m_Code.Length > 0 && m_Code[0] == 0xFEFF)
				m_Code = m_Code.Substring(1);

			m_AutoSkipComments = autoSkipComments;
			m_Syntax = syntax;
			m_Directives = directives;
		}

		public Token Current
		{
			get
			{
				if (m_Current == null)
					Next();

				return m_Current;
			}
		}

		private Token FetchNewToken()
		{
			while (true)
			{
				Token T = ReadToken();

				//System.Diagnostics.Debug.WriteLine("LEXER : " + T.ToString());

				if ((T.Type != TokenType.Comment && T.Type != TokenType.HashBang) || (!m_AutoSkipComments))
					return T;
			}
		}

		public void Next()
		{
			m_Current = FetchNewToken();
		}

		private List<int> templateStringState = new List<int>();

		void PushTemplateString()
		{
			templateStringState.Add(0);
		}

		bool InTemplateString() => templateStringState.Count > 0;

		void TemplateStringAddBracket()
		{
			if (InTemplateString()) {
				templateStringState[templateStringState.Count - 1]++;
			}
		}

		bool ReturnToTemplateString()
		{
			var c = templateStringState[templateStringState.Count - 1];
			if (c == 0) return true;
			templateStringState[templateStringState.Count - 1]--;
			return false;
		}

		void PopTemplateString()
		{
			templateStringState.RemoveAt(templateStringState.Count - 1);
		}
		

		struct Snapshot
		{
			public int Cursor;
			public Token Current;
			public int Line;
			public int Col;
			public int[] TemplateStringState;
		}
		

		private Snapshot s;
		
		

		public void SavePos()
		{
			s = new Snapshot() {
				Cursor = m_Cursor,
				Current = m_Current,
				Line = m_Line,
				Col = m_Col,
				TemplateStringState = templateStringState.ToArray(),
			};
		}

		public void RestorePos()
		{
			m_Cursor = s.Cursor;
			m_Current = s.Current;
			m_Line = s.Line;
			m_Col = s.Col;
			templateStringState = new List<int>(s.TemplateStringState);
		}

		public Token PeekNext()
		{
			int snapshot = m_Cursor;
			Token current = m_Current;
			int line = m_Line;
			int col = m_Col;
			//Save the template string state
			int stateC = templateStringState.Count;
			int lastC = 0;
			if (templateStringState.Count > 0) {
				lastC = templateStringState[templateStringState.Count - 1];
			}

			Next();
			Token t = Current;

			m_Cursor = snapshot;
			m_Current = current;
			m_Line = line;
			m_Col = col;
			//Restore the template string state
			if (templateStringState.Count < stateC) 
			{
				templateStringState.Add(lastC);
			} 
			else if (templateStringState.Count > stateC)
			{
				templateStringState.RemoveAt(templateStringState.Count - 1);
			} 
			else if (stateC != 0)
			{
				templateStringState[templateStringState.Count - 1] = lastC;
			}
			return t;
		}


		private void CursorNext()
		{
			if (CursorNotEof())
			{
				if (CursorChar() == '\n')
				{
					m_Col = 0;
					m_Line += 1;
				}
				else
				{
					m_Col += 1;
				}

				m_Cursor += 1;
			}
		}

		private char CursorChar()
		{
			if (m_Cursor < m_Code.Length)
				return m_Code[m_Cursor];
			else
				return '\0'; //  sentinel
		}

		private char CursorCharNext()
		{
			CursorNext();
			return CursorChar();
		}

		private bool CursorMatches(string pattern)
		{
			for (int i = 0; i < pattern.Length; i++)
			{
				int j = m_Cursor + i;

				if (j >= m_Code.Length)
					return false;
				if (m_Code[j] != pattern[i])
					return false;
			}
			return true;
		}

		private bool CursorNotEof()
		{
			return m_Cursor < m_Code.Length;
		}

		private bool IsWhiteSpace(char c)
		{
			return char.IsWhiteSpace(c);
		}

		private void SkipWhiteSpace()
		{
			for (; CursorNotEof() && IsWhiteSpace(CursorChar()); CursorNext())
			{
			}
		}


		private Token ReadToken()
		{
			SkipWhiteSpace();

			int fromLine = m_Line;
			int fromCol = m_Col;

			if (!CursorNotEof())
				return CreateToken(TokenType.Eof, fromLine, fromCol, "<eof>");

			char c = CursorChar();

			switch (c)
			{
				case '@' when m_Syntax == ScriptSyntax.WattleScript:
					return PotentiallyDoubleCharOperator('@', TokenType.FunctionAnnotation, TokenType.ChunkAnnotation,
						fromLine, fromCol);
				case '|':
					if (m_Syntax == ScriptSyntax.WattleScript)
					{
						var next = CursorCharNext();
						if (next == '=')
						{
							CursorCharNext();
							return CreateToken(TokenType.Op_OrEq, fromLine, fromCol, "|=");
						}
						else if (next == '|')
						{
							CursorCharNext();
							return CreateToken(TokenType.Or, fromLine, fromCol, "||");
						}
						return CreateToken(TokenType.Op_Or, fromLine, fromCol, "|");
					}
					else if (m_Syntax == ScriptSyntax.WattleScript) {
						return PotentiallyDoubleCharOperator('|', TokenType.Lambda, TokenType.Or, fromLine, fromCol);
					}
					else
					{
						CursorCharNext();
						return CreateToken(TokenType.Lambda, fromLine, fromCol, "|");
					}
				case ';':
					CursorCharNext();
					return CreateToken(TokenType.SemiColon, fromLine, fromCol, ";");
				case '&' when m_Syntax == ScriptSyntax.WattleScript:
				{
					var next = CursorCharNext();
					if (next == '&') {
						CursorCharNext();
						return CreateToken(TokenType.And, fromLine, fromCol, "&&");
					}
					else if (m_Syntax == ScriptSyntax.WattleScript && next == '=') {
						CursorCharNext();
						return CreateToken(TokenType.Op_AndEq, fromLine, fromCol, "&=");
					}
					else if (m_Syntax == ScriptSyntax.WattleScript) {
						return CreateToken(TokenType.Op_And, fromLine, fromCol, "&");
					}
					else {
						throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", CursorChar());
					}
				}
				case '=':
				{
					if (m_Syntax == ScriptSyntax.WattleScript)
					{
						char next = CursorCharNext();
						if (next == '=') {
							CursorCharNext();
							return CreateToken(TokenType.Op_Equal, fromLine, fromCol, "==");
						} else if (next == '>') {
							CursorCharNext();
							return CreateToken(TokenType.Arrow, fromLine, fromCol, "=>");
						}
						else
						{
							return CreateToken(TokenType.Op_Assignment, fromLine, fromCol, "=");
						}
					}
					else
					{
						return PotentiallyDoubleCharOperator('=', TokenType.Op_Assignment, TokenType.Op_Equal, fromLine,
							fromCol);
					}
				}
				case '<' when m_Syntax == ScriptSyntax.WattleScript:
				{
					char next = CursorCharNext();
					if (next == '<')
					{
						return PotentiallyDoubleCharOperator('=', TokenType.Op_LShift, TokenType.Op_LShiftEq, fromLine, fromCol);
					}  
					if (next == '=')
					{
						CursorCharNext();
						return CreateToken(TokenType.Op_LessThanEqual, fromLine, fromCol, "<=");
					}
					return CreateToken(TokenType.Op_LessThan, fromLine, fromCol, "<");
				}
				case '>' when m_Syntax == ScriptSyntax.WattleScript:
				{
					char next = CursorCharNext();
					if (next == '>')
					{
						next = CursorCharNext();
						if (next == '>') {
							//>>>, >>>= logical shift (zero)
							return PotentiallyDoubleCharOperator('=', 
								TokenType.Op_RShiftLogical, TokenType.Op_RShiftLogicalEq, 
								fromLine, fromCol
								);
						}
						//>>, >>= - arithmetic shift (sign bit)
						return PotentiallyDoubleCharOperator('=', 
							TokenType.Op_RShiftArithmetic, TokenType.Op_RShiftArithmeticEq, 
							fromLine, fromCol
						);
					}
					else if (next == '=')
					{
						CursorCharNext();
						return CreateToken(TokenType.Op_GreaterThanEqual, fromLine, fromCol, ">=");
					}
					return CreateToken(TokenType.Op_GreaterThan, fromLine, fromCol, ">");
				}
				case '<' when m_Syntax != ScriptSyntax.WattleScript:
					return PotentiallyDoubleCharOperator('=', TokenType.Op_LessThan, TokenType.Op_LessThanEqual, fromLine, fromCol);
				case '>' when m_Syntax != ScriptSyntax.WattleScript:
					return PotentiallyDoubleCharOperator('=', TokenType.Op_GreaterThan, TokenType.Op_GreaterThanEqual, fromLine, fromCol);
				case '!' when m_Syntax == ScriptSyntax.WattleScript:
					return PotentiallyDoubleCharOperator('=', TokenType.Not, TokenType.Op_NotEqual, fromLine, fromCol);
				case '~' when m_Syntax == ScriptSyntax.WattleScript:
					return CreateSingleCharToken(TokenType.Op_Not, fromLine, fromCol);
				case '!' when m_Syntax != ScriptSyntax.WattleScript:
				case '~' when m_Syntax != ScriptSyntax.WattleScript:
					if (CursorCharNext() != '=')
						throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", c);
					CursorCharNext();
					return CreateToken(TokenType.Op_NotEqual, fromLine, fromCol, "~=");
				case '.':
					{
						char next = CursorCharNext();
						if (next == '.')
						{
							if (m_Syntax == ScriptSyntax.WattleScript)
							{
								next = CursorCharNext();
								if (next == '.') {
									CursorCharNext();
									return CreateToken(TokenType.VarArgs, fromLine, fromCol, "...");
								} else if (next == '=') {
									CursorCharNext();
									return CreateToken(TokenType.Op_ConcatEq, fromLine, fromCol, "..=");
								}
								else {
									return CreateToken(TokenType.Op_Concat, fromLine, fromCol, "..");
								}
							}
							else {
								return PotentiallyDoubleCharOperator('.', TokenType.Op_Concat, TokenType.VarArgs,
									fromLine,
									fromCol);
							}
						}
						else if (LexerUtils.CharIsDigit(next))
							return ReadNumberToken(fromLine, fromCol, true);
						else
							return CreateToken(TokenType.Dot, fromLine, fromCol, ".");
					}
				case '+':
				{
					if (m_Syntax == ScriptSyntax.WattleScript)
					{
						char next = CursorCharNext();
						if (m_Syntax == ScriptSyntax.WattleScript && next == '+')
						{
							CursorCharNext();
							return CreateToken(TokenType.Op_Inc, fromLine, fromCol, "++");
						}
						else if (next == '=')
						{
							CursorCharNext();
							return CreateToken(TokenType.Op_AddEq, fromLine, fromCol, "+=");
						}
						else
						{
							return CreateToken(TokenType.Op_Add, fromLine, fromCol, "+");
						}
					}
					else
					{
						return CreateSingleCharToken(TokenType.Op_Add, fromLine, fromCol);
					}
				}
				case '-':
					{
						char next = CursorCharNext();
						if (next == '-')
						{
							if (m_Syntax == ScriptSyntax.WattleScript)
							{
								CursorCharNext();
								return CreateToken(TokenType.Op_Dec, fromLine, fromCol, "--");
							}
							else
								return ReadComment(fromLine, fromCol);

						}
						else if (m_Syntax == ScriptSyntax.WattleScript && next == '=')
						{
							CursorCharNext();
							return CreateToken(TokenType.Op_SubEq, fromLine, fromCol, "-=");
						}
						else
						{
							return CreateToken(TokenType.Op_MinusOrSub, fromLine, fromCol, "-");
						}
					}
				case '*':
					if (m_Syntax == ScriptSyntax.WattleScript)
					{
						char next = CursorCharNext();
						if (next == '=')
						{
							CursorCharNext();
							return CreateToken(TokenType.Op_MulEq, fromLine, fromCol, "*=");
						}
						else if (next == '*')
						{
							return PotentiallyDoubleCharOperator('=', TokenType.Op_Pwr, TokenType.Op_PwrEq, fromLine,
								fromCol);
						}
						else
						{
							return CreateToken(TokenType.Op_Mul, fromLine, fromCol, "*");
						}
					} else {
						return CreateSingleCharToken(TokenType.Op_Mul, fromLine, fromCol);
					}
				case '/':
					if (m_Syntax == ScriptSyntax.WattleScript)
					{
						char next = CursorCharNext();
						if (next == '/') return ReadComment(fromLine, fromCol);
						else if (next == '*')
						{
							CursorCharNext();
							return ReadCMultilineComment(fromLine, fromCol);
						}
						else if (next == '=')
						{
							CursorCharNext();
							return CreateToken(TokenType.Op_DivEq, fromLine, fromCol, "/=");
						}
						else {
							return CreateToken(TokenType.Op_Div, fromLine, fromCol, "/");
						}
					}
					return CreateSingleCharToken(TokenType.Op_Div, fromLine, fromCol);
				case '%':
					if (m_Syntax == ScriptSyntax.WattleScript)
						return PotentiallyDoubleCharOperator('=', TokenType.Op_Mod, TokenType.Op_ModEq, fromLine,
							fromCol);
					return CreateSingleCharToken(TokenType.Op_Mod, fromLine, fromCol);
				case '^':
					if (m_Syntax == ScriptSyntax.WattleScript)
					{
						return PotentiallyDoubleCharOperator('=', TokenType.Op_Xor, TokenType.Op_XorEq, fromLine,
							fromCol);
					}
					return CreateSingleCharToken(TokenType.Op_Pwr, fromLine, fromCol);
				case '$':
					return PotentiallyDoubleCharOperator('{', TokenType.Op_Dollar, TokenType.Brk_Open_Curly_Shared, fromLine, fromCol);
				case '#':
					if (m_Cursor == 0 && m_Code.Length > 1 && m_Code[1] == '!')
						return ReadHashBang(fromLine, fromCol);

					return CreateSingleCharToken(TokenType.Op_Len, fromLine, fromCol);
				case '[':
					{
						char next = CursorCharNext();
						if (m_Syntax == ScriptSyntax.Lua && (next == '=' || next == '['))
						{
							string str = ReadLongString(fromLine, fromCol, null, "string");
							return CreateToken(TokenType.String_Long, fromLine, fromCol, str);
						}
						return CreateToken(TokenType.Brk_Open_Square, fromLine, fromCol, "[");
					}
				case ']':
					return CreateSingleCharToken(TokenType.Brk_Close_Square, fromLine, fromCol);
				case '(':
					return CreateSingleCharToken(TokenType.Brk_Open_Round, fromLine, fromCol);
				case ')':
					return CreateSingleCharToken(TokenType.Brk_Close_Round, fromLine, fromCol);
				case '{':
					TemplateStringAddBracket();
					return CreateSingleCharToken(TokenType.Brk_Open_Curly, fromLine, fromCol);
				case '}' when InTemplateString(): {
					if (ReturnToTemplateString()) {
						return ReadTemplateString(fromLine, fromCol, false);
					}
					else {
						return CreateSingleCharToken(TokenType.Brk_Close_Curly, fromLine, fromCol);
					}
				}
				case '}' when !InTemplateString():
					return CreateSingleCharToken(TokenType.Brk_Close_Curly, fromLine, fromCol);
				case ',':
					return CreateSingleCharToken(TokenType.Comma, fromLine, fromCol);
				case '?' when m_Syntax == ScriptSyntax.WattleScript:
				{
					char next = CursorCharNext();

					if (next == '?')
					{
						char next2 = CursorCharNext();
						if (next2 == '=')
						{
							CursorCharNext();
							return CreateToken(TokenType.Op_NilCoalescingAssignment, fromLine, fromCol, "??=");
						}

						return CreateToken(TokenType.Op_NilCoalesce, fromLine, fromCol, "??");
					}
					
					if (next == '!')
					{
						char next2 = CursorCharNext();
						if (next2 == '=')
						{
							CursorCharNext();
							return CreateToken(TokenType.Op_NilCoalescingAssignmentInverse, fromLine, fromCol, "?!=");
						}
						
						return CreateToken(TokenType.Op_NilCoalesceInverse, fromLine, fromCol, "?!");
					}
					if (next == '.')
					{
						CursorCharNext();
						return CreateToken(TokenType.DotNil, fromLine, fromCol, "?.");
					}
					if (next == '[')
					{
						CursorCharNext();
						return CreateToken(TokenType.BrkOpenSquareNil, fromLine, fromCol, "?[");
					}
					return CreateToken(TokenType.Ternary, fromLine, fromCol, "?");
				}
				case ':':
					return PotentiallyDoubleCharOperator(':', TokenType.Colon, TokenType.DoubleColon, fromLine, fromCol);
				case '`':
				{
					char next = CursorCharNext();
					if (next == '`')
					{
						PushTemplateString();
						return ReadTemplateString(fromLine, fromCol, true);
					}
					throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", CursorChar())
					{
						IsPrematureStreamTermination = true
					};
				}
				case '"':
				case '\'':
					return ReadSimpleStringToken(fromLine, fromCol);
				case '\0':
					throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", CursorChar())
					{
						IsPrematureStreamTermination = true
					};
				default:
					{
						if (char.IsLetter(c) || c == '_')
						{
							string name = ReadNameToken();
							return CreateNameToken(name, fromLine, fromCol);
						}
						else if (LexerUtils.CharIsDigit(c))
						{
							return ReadNumberToken(fromLine, fromCol, false);
						}
					}

					throw new SyntaxErrorException(CreateToken(TokenType.Invalid, fromLine, fromCol), "unexpected symbol near '{0}'", CursorChar());
			}
		}


		Token ReadTemplateString(int fromLine, int fromCol, bool isStart)
		{
			StringBuilder text = new StringBuilder(32);
			
			for (char c = CursorCharNext(); CursorNotEof(); c = CursorCharNext())
			{
				redo_Loop:

				if (c == '\\')
				{
					text.Append(c);
					c = CursorCharNext();
					text.Append(c);

					if (c == '\r')
					{
						c = CursorCharNext();
						if (c == '\n')
							text.Append(c);
						else
							goto redo_Loop;
					}
					else if (c == 'z')
					{
						c = CursorCharNext();

						if (char.IsWhiteSpace(c))
							SkipWhiteSpace();

						c = CursorChar();

						goto redo_Loop;
					}
				}
				else if (c == '{')
				{
					CursorCharNext();
					Token t = CreateToken(TokenType.String_TemplateFragment, fromLine, fromCol);
					t.Text = LexerUtils.UnescapeLuaString(t, text.ToString());
					return t;
				}
				else if (c == '`' && CursorMatches("``"))
				{
					CursorCharNext();
					CursorCharNext();
					PopTemplateString();
					Token t = CreateToken(isStart ? TokenType.String_Long : TokenType.String_EndTemplate, fromLine, fromCol);
					t.Text = LexerUtils.UnescapeLuaString(t, text.ToString());
					return t;
				}
				else
				{
					text.Append(c);
				}
			}

			throw new SyntaxErrorException(
				CreateToken(TokenType.Invalid, fromLine, fromCol),
				"unfinished string near '{0}'", text.ToString()) { IsPrematureStreamTermination = true };
		}
		

		private string ReadLongString(int fromLine, int fromCol, string startpattern, string subtypeforerrors)
		{
			// here we are at the first '=' or second '['
			StringBuilder text = new StringBuilder(1024);
			string end_pattern = "]";

			if (startpattern == null)
			{
				for (char c = CursorChar(); ; c = CursorCharNext())
				{
					if (c == '\0' || !CursorNotEof())
					{
						throw new SyntaxErrorException(
							CreateToken(TokenType.Invalid, fromLine, fromCol),
							"unfinished long {0} near '<eof>'", subtypeforerrors) { IsPrematureStreamTermination = true };
					}
					else if (c == '=')
					{
						end_pattern += "=";
					}
					else if (c == '[')
					{
						end_pattern += "]";
						break;
					}
					else
					{
						throw new SyntaxErrorException(
							CreateToken(TokenType.Invalid, fromLine, fromCol),
							"invalid long {0} delimiter near '{1}'", subtypeforerrors, c) { IsPrematureStreamTermination = true };
					}
				}
			}
			else
			{
				end_pattern = startpattern.Replace('[', ']');
			}


			for (char c = CursorCharNext(); ; c = CursorCharNext())
			{
				if (c == '\r') // XXI century and we still debate on how a newline is made. throw new DeveloperExtremelyAngryException.
					continue;

				if (c == '\0' || !CursorNotEof())
				{
					throw new SyntaxErrorException(
							CreateToken(TokenType.Invalid, fromLine, fromCol),
							"unfinished long {0} near '{1}'", subtypeforerrors, text.ToString()) { IsPrematureStreamTermination = true };
				}
				else if (c == ']' && CursorMatches(end_pattern))
				{
					for (int i = 0; i < end_pattern.Length; i++)
						CursorCharNext();

					return LexerUtils.AdjustLuaLongString(text.ToString());
				}
				else
				{
					text.Append(c);
				}
			}
		}

		private Token ReadNumberToken(int fromLine, int fromCol, bool leadingDot)
		{
			StringBuilder text = new StringBuilder(32);

			//INT : Digit+
			//HEX : '0' [xX] HexDigit+
			//FLOAT : Digit+ '.' Digit* ExponentPart?
			//		| '.' Digit+ ExponentPart?
			//		| Digit+ ExponentPart
			//HEX_FLOAT : '0' [xX] HexDigit+ '.' HexDigit* HexExponentPart?
			//			| '0' [xX] '.' HexDigit+ HexExponentPart?
			//			| '0' [xX] HexDigit+ HexExponentPart
			//
			// ExponentPart : [eE] [+-]? Digit+
			// HexExponentPart : [pP] [+-]? Digit+

			bool isHex = false;
			bool dotAdded = false;
			bool exponentPart = false;
			bool exponentSignAllowed = false;

			if (leadingDot)
			{
				text.Append("0.");
			}
			else if (CursorChar() == '0')
			{
				text.Append(CursorChar());
				char secondChar = CursorCharNext();

				if (secondChar == 'x' || secondChar == 'X')
				{
					isHex = true;
					text.Append(CursorChar());
					CursorCharNext();
				}
			}

			for (char c = CursorChar(); CursorNotEof(); c = CursorCharNext())
			{
				if (exponentSignAllowed && (c == '+' || c == '-'))
				{
					exponentSignAllowed = false;
					text.Append(c);
				}
				else if (LexerUtils.CharIsDigit(c))
				{
					text.Append(c);
				}
				else if (c == '.' && !dotAdded)
				{
					if (CursorMatches("..")) break;
					dotAdded = true;
					text.Append(c);
				}
				else if (LexerUtils.CharIsHexDigit(c) && isHex && !exponentPart)
				{
					text.Append(c);
				}
				else if (c == 'e' || c == 'E' || (isHex && (c == 'p' || c == 'P')))
				{
					text.Append(c);
					exponentPart = true;
					exponentSignAllowed = true;
					dotAdded = true;
				}
				else
				{
					break;
				}
			}

			TokenType numberType = TokenType.Number;

			if (isHex && (dotAdded || exponentPart))
				numberType = TokenType.Number_HexFloat;
			else if (isHex)
				numberType = TokenType.Number_Hex;

			string tokenStr = text.ToString();
			return CreateToken(numberType, fromLine, fromCol, tokenStr);
		}

		private Token CreateSingleCharToken(TokenType tokenType, int fromLine, int fromCol)
		{
			char c = CursorChar();
			CursorCharNext();
			return CreateToken(tokenType, fromLine, fromCol, c.ToString());
		}

		private Token ReadHashBang(int fromLine, int fromCol)
		{
			StringBuilder text = new StringBuilder(32);

			for (char c = CursorChar(); CursorNotEof(); c = CursorCharNext())
			{
				if (c == '\n')
				{
					CursorCharNext();
					return CreateToken(TokenType.HashBang, fromLine, fromCol, text.ToString());
				}
				else if (c != '\r')
				{
					text.Append(c);
				}
			}

			return CreateToken(TokenType.HashBang, fromLine, fromCol, text.ToString());
		}

		private Token ReadCMultilineComment(int fromLine, int fromCol)
		{
			StringBuilder text = new StringBuilder(32);
			
			for (char c = CursorChar(); ; c = CursorCharNext())
			{
				if (c == '\r') continue;
				if (c == '\0' || !CursorNotEof())
				{
					throw new SyntaxErrorException(
						CreateToken(TokenType.Invalid, fromLine, fromCol),
						"unfinished multiline comment near '{0}'", text.ToString()) { IsPrematureStreamTermination = true };
				}
				else if (c == '*' && CursorMatches("*/"))
				{
					CursorCharNext();
					CursorCharNext();
					return CreateToken(TokenType.Comment, fromLine, fromCol, text.ToString());
				}
				else
				{
					text.Append(c);
				}
			}
		}


		private Token ReadComment(int fromLine, int fromCol)
		{
			StringBuilder text = new StringBuilder(32);

			bool extraneousFound = false;

			for (char c = CursorCharNext(); CursorNotEof(); c = CursorCharNext())
			{
				if (c == '[' && !extraneousFound && text.Length > 0)
				{
					text.Append('[');
					//CursorCharNext();
					string comment = ReadLongString(fromLine, fromCol, text.ToString(), "comment");
					return CreateToken(TokenType.Comment, fromLine, fromCol, comment);
				}
				else if (c == '\n')
				{
					CursorCharNext();
					return CreateToken(TokenType.Comment, fromLine, fromCol, text.ToString());
				}
				else if (c != '\r')
				{
					if (c != '[' && c != '=')
						extraneousFound = true;

					text.Append(c);
				}
			}

			return CreateToken(TokenType.Comment, fromLine, fromCol, text.ToString());
		}

		private Token ReadSimpleStringToken(int fromLine, int fromCol)
		{
			StringBuilder text = new StringBuilder(32);
			char separator = CursorChar();

			for (char c = CursorCharNext(); CursorNotEof(); c = CursorCharNext())
			{
			redo_Loop:

				if (c == '\\')
				{
					text.Append(c);
					c = CursorCharNext();
					text.Append(c);

					if (c == '\r')
					{
						c = CursorCharNext();
						if (c == '\n')
							text.Append(c);
						else
							goto redo_Loop;
					}
					else if (c == 'z')
					{
						c = CursorCharNext();

						if (char.IsWhiteSpace(c))
							SkipWhiteSpace();

						c = CursorChar();

						goto redo_Loop;
					}
				}
				else if (c == '\n' || c == '\r')
				{
					throw new SyntaxErrorException(
						CreateToken(TokenType.Invalid, fromLine, fromCol),
						"unfinished string near '{0}'", text.ToString());
				}
				else if (c == separator)
				{
					CursorCharNext();
					Token t = CreateToken(TokenType.String, fromLine, fromCol);
					t.Text = LexerUtils.UnescapeLuaString(t, text.ToString());
					return t;
				}
				else
				{
					text.Append(c);
				}
			}

			throw new SyntaxErrorException(
				CreateToken(TokenType.Invalid, fromLine, fromCol),
				"unfinished string near '{0}'", text.ToString()) { IsPrematureStreamTermination = true };
		}


		private Token PotentiallyDoubleCharOperator(char expectedSecondChar, TokenType singleCharToken, TokenType doubleCharToken, int fromLine, int fromCol)
		{
			string op = CursorChar().ToString();

			CursorCharNext();

			if (CursorChar() == expectedSecondChar)
			{
				CursorCharNext();
				return CreateToken(doubleCharToken, fromLine, fromCol, op + expectedSecondChar);
			}
			else
				return CreateToken(singleCharToken, fromLine, fromCol, op);
		}



		private Token CreateNameToken(string name, int fromLine, int fromCol)
		{
			TokenType? reservedType = Token.GetReservedTokenType(name, m_Syntax);

			if (reservedType.HasValue)
			{
				return CreateToken(reservedType.Value, fromLine, fromCol, name);
			}
			else if (m_Directives != null && m_Directives.Contains(name))
			{
				return ReadDirective(name, fromLine, fromCol);
			}
			else
			{
				return CreateToken(TokenType.Name, fromLine, fromCol, name);
			}
		}
		
		Token ReadDirective(string name, int fromLine, int fromCol)
		{
			//Skip space characters
			for (; CursorNotEof() && CursorChar() != '\n' && IsWhiteSpace(CursorChar()); CursorNext()) ;
			bool ValidCursorChar()
			{
				if (!CursorNotEof()) return false; //return on eof
				var c = CursorChar();
				return char.IsLetterOrDigit(c) || c == '.' || c == '_';
			}
			//Blank directive
			if (!ValidCursorChar())
			{
				return CreateToken(TokenType.Directive, fromLine, fromCol, name);
			}
			//Directive with value
			//Directive values can contain letters, digits, underscores, or periods.
			//They may not contain whitespace or other punctuation/control characters
			var builder = new StringBuilder();
			builder.Append(name).Append(" ");
			while (ValidCursorChar())
			{
				builder.Append(CursorChar());
				CursorCharNext();
			}
			return CreateToken(TokenType.Directive, fromLine, fromCol, builder.ToString());
		}


		private Token CreateToken(TokenType tokenType, int fromLine, int fromCol, string text = null)
		{
			Token t = new Token(tokenType, m_SourceId, fromLine, fromCol, m_Line, m_Col, m_PrevLineTo, m_PrevColTo)
			{
				Text = text
			};
			m_PrevLineTo = m_Line;
			m_PrevColTo = m_Col;
			return t;
		}

		private string ReadNameToken()
		{
			StringBuilder name = new StringBuilder(32);

			for (char c = CursorChar(); CursorNotEof(); c = CursorCharNext())
			{
				if (char.IsLetterOrDigit(c) || c == '_')
					name.Append(c);
				else
					break;
			}

			return name.ToString();
		}




	}
}