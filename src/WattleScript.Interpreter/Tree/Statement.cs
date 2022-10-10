using System;
using System.Collections.Generic;
using System.Text;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Tree.Expressions;
using WattleScript.Interpreter.Tree.Statements;

namespace WattleScript.Interpreter.Tree
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

		public abstract void ResolveScope(ScriptLoadingContext lcontext);

		static void ProcessDirective(ScriptLoadingContext lcontext)
		{
			var str = lcontext.Lexer.Current.Text;
			var firstSpace = str.IndexOf(' ');
			string name = str;
			string value = "";
			if (firstSpace != -1)
			{
				name = str.Substring(0, firstSpace);
				value = str.Substring(firstSpace + 1).Trim();
			}
			var check = lcontext.Script.Options.AnnotationPolicy.OnChunkAnnotation(name, DynValue.NewString(value));
			if (check == AnnotationAction.Allow)
			{
				lcontext.ChunkAnnotations.Add(new Annotation(name, DynValue.NewString(value)));
			}
			if (check == AnnotationAction.Error)
			{
				throw new SyntaxErrorException(lcontext.Lexer.Current, "invalid directive '{0}'", name);
			}
			lcontext.Lexer.Next();
		}

		static Annotation ParseAnnotation(ScriptLoadingContext lcontext)
		{
			lcontext.Lexer.Next(); //Skip annotation marker
			var nameToken = CheckTokenType(lcontext, TokenType.Name); //name
			DynValue value = DynValue.Nil;
			
			DynValue NextPart(bool next)
			{
				DynValue _value = DynValue.Nil;
				if (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Round)
				{
					if (next)
					{
						lcontext.Lexer.Next();	
					}
					
					var exprToken = lcontext.Lexer.Current;
					var expr = Expression.Expr(lcontext);
					if (expr is TableConstructor tbl)
					{
						if(!tbl.TryGetLiteral(out _value))
							throw new SyntaxErrorException(exprToken, "annotation value must be literal or prime table");
					}
					else if (!expr.EvalLiteral(out _value))
					{
						throw new SyntaxErrorException(exprToken, "annotation value must be literal or prime table");
					}
				}

				return _value;
			}
			
			if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Round)
			{
				value = NextPart(true);
				
				if (lcontext.Script.Options.AnnotationPolicy.AnnotationParsingPolicy == AnnotationValueParsingPolicy.StringOrTable || value.Type == DataType.Table)
				{
					CheckTokenType(lcontext, TokenType.Brk_Close_Round);	
					return new Annotation(nameToken.Text, value);
				}

				DynValue table = DynValue.NewTable(lcontext.Script);
				table.Table.Append(value);

				while (true)
				{
					if (lcontext.Lexer.Current.Type == TokenType.Brk_Close_Round)
					{
						break;
					}
					
					CheckTokenType(lcontext, TokenType.Comma);	
					value = NextPart(false);
					table.Table.Append(value);
				}

				CheckTokenType(lcontext, TokenType.Brk_Close_Round);
				return new Annotation(nameToken.Text, table);
			}
			
			return new Annotation(nameToken.Text, value);
		}

		static void ProcessChunkAnnotation(ScriptLoadingContext lcontext)
		{
			var tkn = lcontext.Lexer.Current;
			var ant = ParseAnnotation(lcontext);
			var check = lcontext.Script.Options.AnnotationPolicy.OnChunkAnnotation(ant.Name, ant.Value);
			if (check == AnnotationAction.Allow)
			{
				lcontext.ChunkAnnotations.Add(ant);
			}
			if (check == AnnotationAction.Error)
			{
				throw new SyntaxErrorException(tkn, "invalid chunk annotation");
			}
		}
		
		static void ProcessFunctionAnnotation(ScriptLoadingContext lcontext)
		{
			var tkn = lcontext.Lexer.Current;
			var ant = ParseAnnotation(lcontext);
			var check = lcontext.Script.Options.AnnotationPolicy.OnFunctionAnnotation(ant.Name, ant.Value);
			if (check == AnnotationAction.Allow)
			{
				lcontext.FunctionAnnotations.Add(ant);
			}
			if (check == AnnotationAction.Error)
			{
				throw new SyntaxErrorException(tkn, "invalid function annotation");
			}
		}

		protected static void ParseAnnotations(ScriptLoadingContext lcontext)
		{
			//Process Annotations
			while (true)
			{
				switch (lcontext.Lexer.Current.Type)
				{
					case TokenType.Directive:
						ProcessDirective(lcontext);
						continue;
					case TokenType.ChunkAnnotation:
						ProcessChunkAnnotation(lcontext);
						continue;
					case TokenType.FunctionAnnotation:
						ProcessFunctionAnnotation(lcontext);
						continue;
				}
				break;
			}
		}

		private const string ANNOTATION_ERROR = @"annotations may only be applied to function, class, mixin or enum declarations";
		private const string UNEXPECTED_MODIFIER_ERROR = "'{0}' modifier{1} can be only applied to class or mixin";
		private const string DUPLICATE_MODIFIER_ERROR = "duplicate modifier '{0}' encountered";
		static bool AnnotationsAllowed(TokenType type)
		{
			switch (type)
			{
				case TokenType.Function:
				case TokenType.Enum:
				case TokenType.Class:
				case TokenType.Mixin:
					return true;
			}
			return false;
		}

		protected static Statement CreateStatement(ScriptLoadingContext lcontext, out bool forceLast)
		{
			Token tkn = lcontext.Lexer.Current;

			forceLast = false;

			if (lcontext.FunctionAnnotations.Count != 0)
			{
				if (tkn.Type == TokenType.Local)
				{
					if (lcontext.Lexer.PeekNext().Type != TokenType.Function)
					{
						throw new SyntaxErrorException(tkn, ANNOTATION_ERROR);
					}
				} 
				else if (!AnnotationsAllowed(tkn.Type))
				{
					throw new SyntaxErrorException(tkn, ANNOTATION_ERROR);
				}
			}
			
			switch (tkn.Type)
			{
				case TokenType.DoubleColon when lcontext.Syntax == ScriptSyntax.Lua:
					return new LabelStatement(lcontext);
				case TokenType.Brk_Open_Curly when lcontext.Syntax == ScriptSyntax.Wattle:
					return new ScopeStatement(lcontext);
				case TokenType.Goto:
					return new GotoStatement(lcontext);
				case TokenType.Switch:
					return new SwitchStatement(lcontext);
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
					forceLast = (lcontext.Syntax != ScriptSyntax.Lua); //TODO: can we not force this in lua too?
					return new ReturnStatement(lcontext);
				case TokenType.Break:
					return new BreakStatement(lcontext);
				case TokenType.Continue:
					return new ContinueStatement(lcontext);
				case TokenType.Enum:
					return new EnumDefinitionStatement(lcontext);
				case TokenType.Class:
					return new ClassDefinitionStatement(lcontext);
				case TokenType.Mixin:
					return new MixinDefinitionStatement(lcontext);
				// all member modifier keywords 
				case TokenType.Static:
				case TokenType.Sealed:
					lcontext.Lexer.SavePos();
					HashSet<string> flags = new HashSet<string>();

					while (lcontext.Lexer.Current.IsMemberModifier())
					{
						if (!flags.Contains(lcontext.Lexer.Current.Text))
						{
							flags.Add(lcontext.Lexer.Current.Text);
						}
						else
						{
							throw new SyntaxErrorException(tkn, string.Format(DUPLICATE_MODIFIER_ERROR, lcontext.Lexer.Current.Text));
						}

						lcontext.Lexer.Next();
					}
					
					Token lastToken = lcontext.Lexer.Current; 
					lcontext.Lexer.RestorePos();

					if (lastToken.Type == TokenType.Class)
					{
						return new ClassDefinitionStatement(lcontext);
					}
					// [todo] mixins
				
					throw new SyntaxErrorException(lastToken, string.Format(UNEXPECTED_MODIFIER_ERROR, string.Join(" ", flags), flags.Count > 1 ? "s" : "")); 
				default:
				{
						//Check for labels in CLike mode
						lcontext.Lexer.SavePos();
						Token l = lcontext.Lexer.Current;
						if (lcontext.Syntax == ScriptSyntax.Wattle && l.Type == TokenType.Name)
						{
							lcontext.Lexer.Next();
							if (lcontext.Lexer.Current.Type == TokenType.Colon) {
								lcontext.Lexer.RestorePos();
								return new LabelStatement(lcontext);
							}
						}
						lcontext.Lexer.RestorePos();
						//Regular expression
						Expression exp = Expression.PrimaryExp(lcontext, false);
						if (exp is FunctionCallExpression fnexp)
							return new FunctionCallStatement(lcontext, fnexp);
						if (exp is NewExpression ne)
							return new NewCallStatement(lcontext, ne);
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
					return new ForEachLoopStatement(lcontext, name, forTkn, paren);
				}
			}
		}
	}



}
