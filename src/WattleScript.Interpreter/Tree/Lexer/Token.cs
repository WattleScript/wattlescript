using System;

namespace WattleScript.Interpreter.Tree
{
	class Token
	{
		public readonly int SourceId;
		public readonly int FromCol, ToCol, FromLine, ToLine, PrevCol, PrevLine;
		public readonly TokenType Type;

		public string Text { get; set; }
		internal string CtxInfo;

		public Token(TokenType type, int sourceId, int fromLine, int fromCol, int toLine, int toCol, int prevLine, int prevCol)
		{
			Type = type;

			SourceId = sourceId;
			FromLine = fromLine;
			FromCol = fromCol;
			ToCol = toCol;
			ToLine = toLine;
			PrevCol = prevCol;
			PrevLine = prevLine;
		}
		
		public Token(TokenType type, int sourceId, int fromLine, int fromCol, int toLine, int toCol, int prevLine, int prevCol, string ctxInfo)
		{
			Type = type;

			SourceId = sourceId;
			FromLine = fromLine;
			FromCol = fromCol;
			ToCol = toCol;
			ToLine = toLine;
			PrevCol = prevCol;
			PrevLine = prevLine;
			CtxInfo = ctxInfo;
		}


		public override string ToString()
		{
			string tokenTypeString = (Type.ToString() + "                                                      ").Substring(0, 16);

			string location = string.Format("{0}:{1}-{2}:{3}", FromLine, FromCol, ToLine, ToCol);

			location = (location + "                                                      ").Substring(0, 10);

			return string.Format("{0}  - {1} - '{2}'", tokenTypeString, location, this.Text ?? "");
		}

		public static TokenType? GetReservedTokenType(string reservedWord, ScriptSyntax syntax)
		{
			if (syntax == ScriptSyntax.WattleScript)
			{
				switch (reservedWord)
				{
					case "let":
					case "var":
						return TokenType.Local;
					case "of":
						return TokenType.In;
					case "continue":
						return TokenType.Continue;
					case "null":
						return TokenType.Nil;
					case "switch":
						return TokenType.Switch;
					case "case":
						return TokenType.Case;
				}
			}

			return reservedWord switch
			{
				"and" => TokenType.And,
				"break" => TokenType.Break,
				"do" => TokenType.Do,
				"else" => TokenType.Else,
				"elseif" => TokenType.ElseIf,
				"end" => TokenType.End,
				"false" => TokenType.False,
				"for" => TokenType.For,
				"function" => TokenType.Function,
				"goto" => TokenType.Goto,
				"if" => TokenType.If,
				"in" => TokenType.In,
				"local" => TokenType.Local,
				"nil" => TokenType.Nil,
				"not" => TokenType.Not,
				"or" => TokenType.Or,
				"repeat" => TokenType.Repeat,
				"return" => TokenType.Return,
				"then" => TokenType.Then,
				"true" => TokenType.True,
				"until" => TokenType.Until,
				"while" => TokenType.While,
				_ => null
			};
		}

		public double GetNumberValue()
		{
			if (this.Type == TokenType.Number)
				return LexerUtils.ParseNumber(this);
			else if (this.Type == TokenType.Number_Hex)
				return LexerUtils.ParseHexInteger(this);
			else if (this.Type == TokenType.Number_HexFloat)
				return LexerUtils.ParseHexFloat(this);
			else
				throw new NotSupportedException("GetNumberValue is supported only on numeric tokens");
		}


		public bool IsEndOfBlock()
		{
			switch (Type)
			{
				case TokenType.Else:
				case TokenType.ElseIf:
				case TokenType.End:
				case TokenType.Until:
				case TokenType.Eof:
					return true;
				default:
					return false;
			}
		}

		public bool IsUnaryOperator()
		{
			return Type == TokenType.Op_MinusOrSub || Type == TokenType.Not || Type == TokenType.Op_Len ||
			       Type == TokenType.Op_Inc || Type == TokenType.Op_Dec || Type == TokenType.Op_Not;
		}

		public bool IsBinaryOperator()
		{
			switch (Type)
			{
				case TokenType.And:
				case TokenType.Or:
				case TokenType.Op_Equal:
				case TokenType.Op_LessThan:
				case TokenType.Op_LessThanEqual:
				case TokenType.Op_GreaterThanEqual:
				case TokenType.Op_GreaterThan:
				case TokenType.Op_NotEqual:
				case TokenType.Op_Concat:
				case TokenType.Op_Pwr:
				case TokenType.Op_Mod:
				case TokenType.Op_Div:
				case TokenType.Op_Mul:
				case TokenType.Op_MinusOrSub:
				case TokenType.Op_Add:
				case TokenType.Op_NilCoalesce:
				case TokenType.Op_NilCoalesceInverse:
				case TokenType.Op_Or:
				case TokenType.Op_And: 
				case TokenType.Op_Xor:
				case TokenType.Op_LShift:
				case TokenType.Op_RShiftArithmetic:
				case TokenType.Op_RShiftLogical:
					return true;
				default:
					return false;
			}
		}


		internal Debugging.SourceRef GetSourceRef(bool isStepStop = true)
		{
			return new Debugging.SourceRef(this.SourceId, this.FromCol, this.ToCol, this.FromLine, this.ToLine, isStepStop);
		}

		internal Debugging.SourceRef GetSourceRef(Token to, bool isStepStop = true)
		{
			return new Debugging.SourceRef(this.SourceId, this.FromCol, to.ToCol, this.FromLine, to.ToLine, isStepStop);
		}

		internal Debugging.SourceRef GetSourceRefUpTo(Token to, bool isStepStop = true)
		{
			return new Debugging.SourceRef(this.SourceId, this.FromCol, to.PrevCol, this.FromLine, to.PrevLine, isStepStop);
		}
	}
}
