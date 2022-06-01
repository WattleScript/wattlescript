using System;
using System.Collections.Generic;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Tree.Expressions;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree
{
	abstract class Expression : NodeBase
	{
		public Expression(ScriptLoadingContext lcontext)
			: base(lcontext)
		{ }

		public bool LexerCarry { get; set; } // currently used to indicate whether ternary parsing in in progress
		
		public virtual string GetFriendlyDebugName()
		{
			return null;
		}

		public abstract DynValue Eval(ScriptExecutionContext context);

		public abstract void ResolveScope(ScriptLoadingContext lcontext);

		public abstract bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null);

		public void CompilePossibleLiteral(FunctionBuilder bc)
		{
			if (EvalLiteral(out var dv))
			{
				bc.Emit_Literal(dv);
			}
			else Compile(bc);
		}

		public virtual SymbolRef FindDynamic(ScriptExecutionContext context)
		{
			return null;
		}

		internal static List<Expression> ExprListAfterFirstExpr(ScriptLoadingContext lcontext, Expression expr1)
		{
			List<Expression> exps = new List<Expression>();

			exps.Add(expr1);

			while ((lcontext.Lexer.Current.Type == TokenType.Comma))
			{
				lcontext.Lexer.Next();
				exps.Add(Expr(lcontext));
			}

			return exps;
		}
		
		internal static List<Expression> ExprList(ScriptLoadingContext lcontext)
		{
			List<Expression> exps = new List<Expression>();

			while (true)
			{
				exps.Add(Expr(lcontext));

				if (lcontext.Lexer.Current.Type != TokenType.Comma)
					break;

				lcontext.Lexer.Next();
			}

			return exps; 
		}

		internal static Expression Expr(ScriptLoadingContext lcontext, bool isTableInit = false)
		{
			return SubExpr(lcontext, true, false, isTableInit);
		}

		internal static Expression SubExpr(ScriptLoadingContext lcontext, bool isPrimary, bool binaryChainInProgress = false, bool isTableInit = false)
		{
			Expression e;
			Token T = lcontext.Lexer.Current;

			if (T.IsUnaryOperator())
			{
				lcontext.Lexer.Next();
				e = SubExpr(lcontext, false);

				// check for power operator -- it be damned forever and ever for being higher priority than unary ops
				Token unaryOp = T;
				T = lcontext.Lexer.Current;

				if (isPrimary && T.Type == TokenType.Op_Pwr)
				{
					List<Expression> powerChain = new List<Expression>();
					powerChain.Add(e);

					while (isPrimary && T.Type == TokenType.Op_Pwr)
					{
						lcontext.Lexer.Next();
						powerChain.Add(SubExpr(lcontext, false));
						T = lcontext.Lexer.Current;
					}

					e = powerChain[powerChain.Count - 1];

					for (int i = powerChain.Count - 2; i >= 0; i--)
					{
						e = BinaryOperatorExpression.CreatePowerExpression(powerChain[i], e, lcontext);
					}
				}

				e = new UnaryOperatorExpression(lcontext, e, unaryOp);
			}
			else
			{
				e = SimpleExp(lcontext, isTableInit);
			}

			T = lcontext.Lexer.Current;

			if (T.Type == TokenType.Ternary)
			{
				if (!binaryChainInProgress)
				{
					return new TernaryExpression(lcontext, e);	
				}

				e.LexerCarry = true;
				return e;
			}

			if (isPrimary && T.IsBinaryOperator())
			{
				object chain = BinaryOperatorExpression.BeginOperatorChain();

				BinaryOperatorExpression.AddExpressionToChain(chain, e);
				bool forceReturnTernary = false;
				
				while (T.IsBinaryOperator())
				{
					BinaryOperatorExpression.AddOperatorToChain(chain, T);
					lcontext.Lexer.Next();
					Expression right = SubExpr(lcontext, false, true);
					BinaryOperatorExpression.AddExpressionToChain(chain, right);
					T = lcontext.Lexer.Current;
					
					if (right.LexerCarry)
					{
						forceReturnTernary = true;
					}
				}

				e = BinaryOperatorExpression.CommitOperatorChain(chain, lcontext);
				
				if (forceReturnTernary)
				{
					return new TernaryExpression(lcontext, e);	
				}
			}

			return e;
		}

		internal static Expression SimpleExp(ScriptLoadingContext lcontext, bool isTableInit)
		{
			Token t = lcontext.Lexer.Current;

			switch (t.Type)
			{
				case TokenType.Number:
				case TokenType.Number_Hex:
				case TokenType.Number_HexFloat:
				case TokenType.String when lcontext.Syntax != ScriptSyntax.Wattle:
				case TokenType.String_Long:
				case TokenType.Nil:
				case TokenType.True:
				case TokenType.False:
					return new LiteralExpression(lcontext, t);
				case TokenType.VarArgs:
					return new SymbolRefExpression(t, lcontext);
				case TokenType.Function:
					lcontext.Lexer.Next();
					return new FunctionDefinitionExpression(lcontext, isTableInit ? SelfType.Implicit : SelfType.None, false);
				case TokenType.Lambda:
					return new FunctionDefinitionExpression(lcontext, isTableInit ? SelfType.Implicit : SelfType.None, true);
				case TokenType.Brk_Open_Round:
				{
					if (lcontext.Syntax == ScriptSyntax.Lua) return PrimaryExp(lcontext, isTableInit);
					//Scan to see if this is an arrow lambda
					lcontext.Lexer.SavePos();
					lcontext.Lexer.Next(); // skip bracket
					while (lcontext.Lexer.Current.Type != TokenType.Eof &&
					       lcontext.Lexer.Current.Type != TokenType.Brk_Close_Round &&
					       lcontext.Lexer.Current.Type != TokenType.Brk_Open_Round) {
						lcontext.Lexer.Next();
					}
					lcontext.Lexer.Next();
					bool arrowLambda = lcontext.Lexer.Current.Type == TokenType.Arrow || lcontext.Lexer.PeekNext().Type == TokenType.Arrow;
					lcontext.Lexer.RestorePos();
					if (arrowLambda) 					
						return new FunctionDefinitionExpression(lcontext, isTableInit ? SelfType.Implicit : SelfType.None, true);
					else
						return PrimaryExp(lcontext, isTableInit);
				}
				default:
					return PrimaryExp(lcontext, isTableInit);
			}

		}

		/// <summary>
		/// Primaries the exp.
		/// </summary>
		/// <param name="lcontext">The lcontext.</param>
		/// <returns></returns>
		internal static Expression PrimaryExp(ScriptLoadingContext lcontext, bool tableInit)
		{
			if (lcontext.Lexer.PeekNext().Type == TokenType.Arrow && lcontext.Lexer.Current.Type == TokenType.Name)
			{
				return new FunctionDefinitionExpression(lcontext, tableInit ? SelfType.Implicit : SelfType.None, true);
			}

			Expression e = PrefixExp(lcontext);

			while (true)
			{
				Token T = lcontext.Lexer.Current;
				Token thisCallName = null;
				CallKind callKind = CallKind.Normal;
				switch (T.Type)
				{
					case TokenType.Dot:
					case TokenType.DotNil:
					{
						lcontext.Lexer.Next();
						Token name = CheckTokenType(lcontext, TokenType.Name);
						var type = lcontext.Lexer.Current.Type;
						if (lcontext.Syntax == ScriptSyntax.Wattle)
						{
							//Implicit thiscall
							if (type == TokenType.Brk_Open_Round ||
							    type == TokenType.String ||
							    type == TokenType.Brk_Open_Curly ||
							    type == TokenType.Brk_Open_Curly_Shared)
							{
								thisCallName = name;
								callKind = T.Type == TokenType.DotNil
									? CallKind.ImplicitThisSkipNil
									: CallKind.ImplicitThisCall;
								goto case TokenType.Brk_Open_Round;
							}
						}
						var ne = new IndexExpression(e, name, T.Type == TokenType.DotNil, lcontext);
						//Break nil checking chain on next nil check
						if (e is IndexExpression ie && T.Type != TokenType.DotNil) ie.NilChainNext = ne;
						e = ne;
						break;
					}
					case TokenType.BrkOpenSquareNil:
					case TokenType.Brk_Open_Square:
						{
							Token openBrk = lcontext.Lexer.Current;
							lcontext.Lexer.Next(); // skip bracket
							Expression index = Expr(lcontext);
							// support wattle multiple indexers for userdata
							if (lcontext.Lexer.Current.Type == TokenType.Comma)
							{
								var explist = ExprListAfterFirstExpr(lcontext, index);
								index = new ExprListExpression(explist, lcontext);
							}
							CheckMatch(lcontext, openBrk, TokenType.Brk_Close_Square, "]");
							//Regular indexing
							var ne = new IndexExpression(e, index, T.Type == TokenType.BrkOpenSquareNil, lcontext);
							//Break nil checking chain on next nil check
							if (e is IndexExpression ie && T.Type != TokenType.BrkOpenSquareNil)
								ie.NilChainNext = ne;
							e = ne;
							if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Round &&
							    lcontext.Syntax == ScriptSyntax.Wattle)
							{
								//Function call
								var call = new FunctionCallExpression(lcontext, ne, null, CallKind.ImplicitThisCall);
								ne.NilChainNext = call;
								e = call;
							}
							break;
						}
					case TokenType.Colon when lcontext.Syntax != ScriptSyntax.Wattle:
					case TokenType.DoubleColon when lcontext.Syntax == ScriptSyntax.Wattle:
						lcontext.Lexer.Next();
						thisCallName = CheckTokenType(lcontext, TokenType.Name);
						callKind = CallKind.ThisCall;
						goto case TokenType.Brk_Open_Round;
					case TokenType.Brk_Open_Round:
					case TokenType.String:
					case TokenType.String_Long:
					case TokenType.Brk_Open_Curly_Shared:
					case TokenType.Brk_Open_Curly:
					{
						if (T.Type == TokenType.Brk_Open_Curly && 
						    e is AdjustmentExpression && thisCallName == null) {
							return e;
						}
						var ne = new FunctionCallExpression(lcontext, e, thisCallName, callKind);
						if (e is IndexExpression ie && callKind != CallKind.ImplicitThisSkipNil) ie.NilChainNext = ne;
						e = ne;
						break;
					}
					default:
						return e;
				}
			}
		}



		private static Expression PrefixExp(ScriptLoadingContext lcontext)
		{
			Token T = lcontext.Lexer.Current;
			switch (T.Type)
			{
				case TokenType.String when lcontext.Syntax == ScriptSyntax.Wattle:
				case TokenType.String_EndTemplate:
					return new LiteralExpression(lcontext, T);
				case TokenType.String_TemplateFragment:
					return new TemplatedStringExpression(lcontext, T);
				case TokenType.Brk_Open_Round:
					lcontext.Lexer.Next();
					Expression e = Expr(lcontext);
					e = new AdjustmentExpression(lcontext, e);
					CheckMatch(lcontext, T, TokenType.Brk_Close_Round, ")");
					return e;
				case TokenType.Name:
					return new SymbolRefExpression(T, lcontext);
				case TokenType.New:
					return new NewExpression(lcontext);
				case TokenType.Brk_Open_Curly:
				case TokenType.Brk_Open_Curly_Shared:
					return new TableConstructor(lcontext, T.Type == TokenType.Brk_Open_Curly_Shared);
				case TokenType.Brk_Open_Square when lcontext.Syntax != ScriptSyntax.Lua:
					return new TableConstructor(lcontext, false);
				default:
					throw new SyntaxErrorException(T, "unexpected symbol near '{0}'", T.Text)
					{
						IsPrematureStreamTermination = (T.Type == TokenType.Eof)
					};

			}
		}





	}
}
