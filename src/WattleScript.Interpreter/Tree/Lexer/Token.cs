using System;
using WattleScript.Interpreter.Tree.Statements;

namespace WattleScript.Interpreter.Tree
{
	class Token
	{
		public readonly int SourceId;
		public readonly int FromCol, ToCol, FromLine, ToLine, PrevCol, PrevLine, CharIndexTo, CharIndexFrom;
		public readonly TokenType Type;
		public string Text { get; set; }

		public Token(TokenType type, int sourceId, int fromLine, int fromCol, int toLine, int toCol, int prevLine, int prevCol, int charIndexFrom, int charIndexTo, string text)
		{
			Type = type;
			SourceId = sourceId;
			FromLine = fromLine;
			FromCol = fromCol;
			ToCol = toCol;
			ToLine = toLine;
			PrevCol = prevCol;
			PrevLine = prevLine;
			CharIndexTo = charIndexTo;
			CharIndexFrom = charIndexFrom;
			Text = text;
		}

		public override string ToString()
		{
			string tokenTypeString = (Type + "                                                      ").Substring(0, 16);

			string location = $"{FromLine}:{FromCol}-{ToLine}:{ToCol}";

			location = (location + "                                                      ").Substring(0, 10);

			return $"{tokenTypeString}  - {location} - '{Text ?? ""}'";
		}

		public static TokenType? GetReservedTokenType(string reservedWord, ScriptSyntax syntax)
		{
			if (syntax == ScriptSyntax.Wattle)
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
					case "class":
						return TokenType.Class;
					case "enum":
						return TokenType.Enum;
					case "new":
						return TokenType.New;
					case "mixin":
						return TokenType.Mixin;
					case "static":
						return TokenType.Static;
					case "private":
						return TokenType.Private;
					case "public":
						return TokenType.Public;
					case "sealed":
						return TokenType.Sealed;
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
			return Type switch
			{
				TokenType.Number => LexerUtils.ParseNumber(this),
				TokenType.Number_Hex => LexerUtils.ParseHexInteger(this),
				TokenType.Number_HexFloat => LexerUtils.ParseHexFloat(this),
				_ => throw new NotSupportedException("GetNumberValue is supported only on numeric tokens")
			};
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

		public bool IsMemberModifier()
		{
			return Type == TokenType.Static || Type == TokenType.Private || Type == TokenType.Public || Type == TokenType.Sealed;
		}

		public MemberModifierFlags ToMemberModiferFlag()
		{
			return Type switch
			{
				TokenType.Static => MemberModifierFlags.Static,
				TokenType.Private => MemberModifierFlags.Private,
				TokenType.Public => MemberModifierFlags.Public,
				TokenType.Sealed => MemberModifierFlags.Sealed,
				_ => throw new InvalidCastException("Token is not modifier flag")
			};
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
				case TokenType.Op_InclusiveRange:
				case TokenType.Op_LeftExclusiveRange:
				case TokenType.Op_RightExclusiveRange:
				case TokenType.Op_ExclusiveRange:
					return true;
				default:
					return false;
			}
		}


		internal Debugging.SourceRef GetSourceRef(bool isStepStop = true)
		{
			return new Debugging.SourceRef(SourceId, FromCol, ToCol, FromLine, ToLine, isStepStop, CharIndexFrom, CharIndexTo);
		}

		internal Debugging.SourceRef GetSourceRef(Token to, bool isStepStop = true)
		{
			return new Debugging.SourceRef(SourceId, FromCol, to.ToCol, FromLine, to.ToLine, isStepStop, CharIndexFrom, CharIndexTo);
		}

		internal Debugging.SourceRef GetSourceRefUpTo(Token to, bool isStepStop = true)
		{
			return new Debugging.SourceRef(SourceId, FromCol, to.PrevCol, FromLine, to.PrevLine, isStepStop, CharIndexFrom, CharIndexTo);
		}
	}
}
