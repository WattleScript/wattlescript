using System.Collections.Generic;
using System.Linq;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree
{
	abstract class NodeBase
	{
		public Script Script { get; private set; }
		protected ScriptLoadingContext LoadingContext { get; private set; }

		public NodeBase(ScriptLoadingContext lcontext)
		{
			Script = lcontext.Script;
		}


		public abstract void Compile(FunctionBuilder bc);


		internal static Token UnexpectedTokenType(Token t)
		{
			throw new SyntaxErrorException(t, "unexpected symbol near '{0}'", t.Text)
			{
				IsPrematureStreamTermination = (t.Type == TokenType.Eof)
			};
		}

		/// <summary>
		/// Parses a sequence of tokens in form of (name->dot->name->..)
		/// </summary>
		/// <param name="lcontext">Current ScriptLoadingContext</param>
		/// <param name="currentTokenShouldBeDot">Whether the first token should be "name" or "dot"</param>
		/// <returns>A list of tokens representing the qualifier</returns>
		protected static List<Token> ParseNamespace(ScriptLoadingContext lcontext, bool currentTokenShouldBeDot)
		{
			List<Token> tokens = new List<Token>();

			while (lcontext.Lexer.PeekNext().Type != TokenType.Eof)
			{
				if (currentTokenShouldBeDot && lcontext.Lexer.PeekNext().Type != TokenType.Name)
				{
					break;
				}

				if (!currentTokenShouldBeDot && lcontext.Lexer.PeekNext().Type != TokenType.Dot)
				{
					break;
				}

				currentTokenShouldBeDot = !currentTokenShouldBeDot;
				tokens.Add(lcontext.Lexer.Current);
				lcontext.Lexer.Next();
			}

			if (tokens.Last().Type == TokenType.Dot)
			{
				tokens.Remove(tokens.Last());
			}
			
			return tokens;
		}

		protected static Token CheckTokenTypeEx(ScriptLoadingContext lcontext, TokenType tokenType1, TokenType tokenType2)
		{
			if (lcontext.Syntax != ScriptSyntax.Lua)
			{
				Token t = lcontext.Lexer.Current;
				if (t.Type != tokenType1 &&
				    t.Type != tokenType2)
					return UnexpectedTokenType(t);
				lcontext.Lexer.Next();
				return t;
			}
			else
				return CheckTokenType(lcontext, tokenType1);
		}

		internal static Token CheckTokenType(ScriptLoadingContext lcontext, TokenType tokenType)
		{
			Token t = lcontext.Lexer.Current;
			if (t.Type != tokenType)
				return UnexpectedTokenType(t);

			lcontext.Lexer.Next();

			return t;
		}



		protected static Token CheckTokenType(ScriptLoadingContext lcontext, TokenType tokenType1, TokenType tokenType2)
		{
			Token t = lcontext.Lexer.Current;
			if (t.Type != tokenType1 && t.Type != tokenType2)
				return UnexpectedTokenType(t);

			lcontext.Lexer.Next();

			return t;
		}
		protected static Token CheckTokenType(ScriptLoadingContext lcontext, TokenType tokenType1, TokenType tokenType2, TokenType tokenType3)
		{
			Token t = lcontext.Lexer.Current;
			if (t.Type != tokenType1 && t.Type != tokenType2 && t.Type != tokenType3)
				return UnexpectedTokenType(t);

			lcontext.Lexer.Next();

			return t;
		}

		protected static void CheckTokenTypeNotNext(ScriptLoadingContext lcontext, TokenType tokenType)
		{
			Token t = lcontext.Lexer.Current;
			if (t.Type != tokenType)
				UnexpectedTokenType(t);
		}

		protected static Token CheckMatch(ScriptLoadingContext lcontext, Token originalToken, TokenType expectedTokenType, string expectedTokenText)
		{
			Token t = lcontext.Lexer.Current;
			if (t.Type != expectedTokenType)
			{
				throw new SyntaxErrorException(lcontext.Lexer.Current,
					"'{0}' expected (to close '{1}' at line {2}) near '{3}'",
					expectedTokenText, originalToken.Text, originalToken.FromLine, t.Text)
										{
											IsPrematureStreamTermination = (t.Type == TokenType.Eof)
										};
			}

			lcontext.Lexer.Next();

			return t;
		}
	}
}
