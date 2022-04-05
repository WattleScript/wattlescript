using System;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Tree.Expressions;
using MoonSharp.Interpreter.Tree.Statements;

namespace MoonSharp.Interpreter.Tree
{
	abstract class Statement : NodeBase
	{
		public Statement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{ }
		
		protected IVariable CheckVar(ScriptLoadingContext lcontext, Expression firstExpression)
		{
			IVariable v = firstExpression as IVariable;

			if (v == null)
				throw new SyntaxErrorException(lcontext.Lexer.Current, "unexpected symbol near '{0}' - not a l-value", lcontext.Lexer.Current);

			return v;
		}


		protected static Statement CreateStatement(ScriptLoadingContext lcontext, out bool forceLast)
		{
			Token tkn = lcontext.Lexer.Current;

			forceLast = false;

			switch (tkn.Type)
			{
				case TokenType.DoubleColon when lcontext.Syntax != ScriptSyntax.CLike:
					return new LabelStatement(lcontext);
				case TokenType.Goto:
					return new GotoStatement(lcontext);
				case TokenType.SemiColon:
					lcontext.Lexer.Next();
					return new EmptyStatement(lcontext);
				case TokenType.If:
					return new IfStatement(lcontext);
				case TokenType.While:
					return new WhileStatement(lcontext);
				case TokenType.Do:
					return new DoBlockStatement(lcontext);
				case TokenType.For:
					return DispatchForLoopStatement(lcontext);
				case TokenType.Repeat:
					return new RepeatStatement(lcontext);
				case TokenType.Function:
					return new FunctionDefinitionStatement(lcontext, false, null);
				case TokenType.Local:
					Token localToken = lcontext.Lexer.Current;
					lcontext.Lexer.Next();
					if (lcontext.Lexer.Current.Type == TokenType.Function)
						return new FunctionDefinitionStatement(lcontext, true, localToken);
					else
						return new AssignmentStatement(lcontext, localToken);
				case TokenType.Return:
					forceLast = true;
					return new ReturnStatement(lcontext);
				case TokenType.Break:
					return new BreakStatement(lcontext);
				case TokenType.Continue:
					return new ContinueStatement(lcontext);
				case TokenType.Using when lcontext.Syntax == ScriptSyntax.CLike:
					return new UsingStatement(lcontext);
				default:
				{
						//Check for labels in CLike mode
						lcontext.Lexer.SavePos();
						Token l = lcontext.Lexer.Current;
						if (lcontext.Syntax == ScriptSyntax.CLike && l.Type == TokenType.Name)
						{
							lcontext.Lexer.Next();
							if (lcontext.Lexer.Current.Type == TokenType.Colon) {
								lcontext.Lexer.RestorePos();
								return new LabelStatement(lcontext);
							}
						}
						lcontext.Lexer.RestorePos();
						//Regular expression
						Expression exp = Expression.PrimaryExp(lcontext);
						FunctionCallExpression fnexp = exp as FunctionCallExpression;
						if (fnexp != null)
							return new FunctionCallStatement(lcontext, fnexp);
						else
							return new AssignmentStatement(lcontext, exp, l);
				}
			}
		}

		static bool CheckRangeFor(ScriptLoadingContext lcontext)
		{
			if (lcontext.Syntax == ScriptSyntax.Lua) return false;
			try
			{
				lcontext.Lexer.SavePos();
				if (lcontext.Lexer.Current.Type == TokenType.In)
					lcontext.Lexer.Next();
				else return false;
				if (lcontext.Lexer.Current.Type == TokenType.Number)
					lcontext.Lexer.Next();
				else return false;
				if (lcontext.Lexer.Current.Type == TokenType.Op_Concat)
					lcontext.Lexer.Next();
				else return false;
				return lcontext.Lexer.Current.Type == TokenType.Number;
			}
			finally
			{
				lcontext.Lexer.RestorePos();
			}
		}
		
		private static Statement DispatchForLoopStatement(ScriptLoadingContext lcontext)
		{
			//	for Name ‘=’ exp ‘,’ exp [‘,’ exp] do block end | 
			//	for namelist in explist do block end | 		

			Token forTkn = CheckTokenType(lcontext, TokenType.For);
			//skip opening paren
			bool paren = false;
			if (lcontext.Syntax != ScriptSyntax.Lua && lcontext.Lexer.Current.Type == TokenType.Brk_Open_Round) {
				paren = true;
				lcontext.Lexer.Next();
			}
			//Dispatch
			lcontext.Lexer.SavePos();
			
			if (paren && lcontext.Lexer.Current.Type == TokenType.SemiColon)
			{
				return new CStyleForStatement(lcontext, forTkn);
			}  
			if (paren && lcontext.Lexer.Current.Type == TokenType.Local)
			{
				return new CStyleForStatement(lcontext, forTkn);
			}
			
			Token name = CheckTokenType(lcontext, TokenType.Name);
			switch (lcontext.Lexer.Current.Type)
			{
				case TokenType.Dot when paren:
				case TokenType.Brk_Open_Round when paren:
					lcontext.Lexer.RestorePos();
					return new CStyleForStatement(lcontext, forTkn);
				case TokenType.Op_Assignment when paren:
					lcontext.Lexer.Next();
					Expression.Expr(lcontext);
					if (lcontext.Lexer.Current.Type == TokenType.SemiColon)
					{
						lcontext.Lexer.RestorePos();
						return new CStyleForStatement(lcontext, forTkn);
					}
					lcontext.Lexer.RestorePos();
					lcontext.Lexer.Next();
					return new ForLoopStatement(lcontext, name, forTkn, true);
				case TokenType.Op_Assignment when !paren:
					return new ForLoopStatement(lcontext, name, forTkn, false);
				default:
				{
					if (CheckRangeFor(lcontext))
						return new ForRangeStatement(lcontext, name, forTkn, paren);
					return new ForEachLoopStatement(lcontext, name, forTkn, paren);
				}
			}
		}
	}



}
