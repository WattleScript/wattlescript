using System;
using System.Collections.Generic;
using System.Linq;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;

using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
{
	class AssignmentStatement : Statement
	{
		List<IVariable> m_LValues;
		List<string> localNames;
		List<Expression> m_RValues;
		SourceRef m_Ref;

		public Operator AssignmentOp = Operator.NotAnOperator;

		private bool isIncDec;


		public AssignmentStatement(ScriptLoadingContext lcontext, Token startToken)
			: base(lcontext)
		{
			localNames = new List<string>();

			Token first = startToken;

			while (true)
			{
				Token name = CheckTokenType(lcontext, TokenType.Name);
				localNames.Add(name.Text);

				if (lcontext.Lexer.Current.Type == TokenType.Colon)
				{
					ParseType(lcontext);
				}
				
				if (lcontext.Lexer.Current.Type != TokenType.Comma)
					break;

				lcontext.Lexer.Next();
			}

			if (lcontext.Lexer.Current.Type == TokenType.Op_Assignment)
			{
				CheckTokenType(lcontext, TokenType.Op_Assignment);
				m_RValues = Expression.ExprList(lcontext);
			}
			else if (lcontext.Syntax == ScriptSyntax.Wattle && lcontext.Lexer.Current.Type == TokenType.Op_NilCoalescingAssignment)
			{
				CheckTokenType(lcontext, TokenType.Op_NilCoalescingAssignment);
				AssignmentOp = Operator.NilCoalescing;
				m_RValues = Expression.ExprList(lcontext);
			}
			else if (lcontext.Syntax == ScriptSyntax.Wattle && lcontext.Lexer.Current.Type == TokenType.Op_NilCoalesceInverse)
			{
				CheckTokenType(lcontext, TokenType.Op_NilCoalesceInverse);
				AssignmentOp = Operator.NilCoalescingInverse;
				m_RValues = Expression.ExprList(lcontext);
			}
			else
			{
				if (localNames.Count > 0)
					m_RValues = new List<Expression>(new Expression[] { new LiteralExpression(lcontext, DynValue.Nil) });
			}

			

			Token last = lcontext.Lexer.Current;
			m_Ref = first.GetSourceRefUpTo(last);
			lcontext.Source.Refs.Add(m_Ref);

		}

		private Dictionary<string, SymbolRef> oldScope;
		public void DefineLocals(ScriptLoadingContext lcontext)
		{
			if (localNames != null)
			{
				m_LValues = new List<IVariable>();
				oldScope = new Dictionary<string, SymbolRef>();
				foreach (string name in localNames)
				{
					var localVar = lcontext.Scope.TryDefineLocal(name, out var oldLocal);
					oldScope.Add(name, oldLocal);
					var symbol = new SymbolRefExpression(lcontext, localVar);
					m_LValues.Add(symbol);
				}
			}
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			foreach(var l in m_LValues)((Expression)l).ResolveScope(lcontext);
			if (localNames != null) 
				// local definitions can't reference themselves.
				// To support re-ordering, construct a temporary scope without 
				// the LValues defined yet. This allows for "local print = print;"
				// as well as "local item = 'a'; local item = item .. 'b'"
			{
				lcontext.Scope.TemporaryScope(oldScope);
			}
			if(m_RValues != null)
				foreach(var r in m_RValues) r.ResolveScope(lcontext);
			if(localNames != null) lcontext.Scope.ResetTemporaryScope();
		}

		public static void ParseType(ScriptLoadingContext lcontext, bool currentTokenIsColon = true)
		{
			void TypeBegin() // ":", TypeExpr
			{
				if (currentTokenIsColon)
				{
					CheckTokenType(lcontext, TokenType.Colon);	
				}

				if (lcontext.Lexer.Current.Type == TokenType.Function)
				{
					lcontext.Lexer.Next();
					FunctionDefinitionExpression fnDef = new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false, false, false, false);
					return;
				}
				
				TypeExpr();
			}
			
			void TypeExpr() // LiteralExpr, [GenericTypeExpr], ["?" | "!"];
			{
				CheckTokenType(lcontext, TokenType.Name); // type name

				if (lcontext.Lexer.Current.Type == TokenType.Op_LessThan)
				{
					GenericTypeExpr();
				}

				if (lcontext.Lexer.Current.Type == TokenType.Ternary) // ?
				{
					CheckTokenType(lcontext, TokenType.Ternary);
				}
				else if (lcontext.Lexer.Current.Type == TokenType.Not) // !
				{
					CheckTokenType(lcontext, TokenType.Not);
				}
			}

			void GenericTypeExpr() // "<", {TypeExpr, [","]}, ">";
			{
				CheckTokenType(lcontext, TokenType.Op_LessThan);

				while (lcontext.Lexer.Current.Type == TokenType.Name)
				{
					TypeExpr();

					if (lcontext.Lexer.Current.Type != TokenType.Op_GreaterThan)
					{
						var type = lcontext.Lexer.Current.Type;
						CheckTokenType(lcontext, TokenType.Comma);
					}
				}
				
				CheckTokenType(lcontext, TokenType.Op_GreaterThan);
			}

			// parser is at :
			// ":", TypeExpr
			TypeBegin();
		}

		public AssignmentStatement(ScriptLoadingContext lcontext, Expression firstExpression, Token first)
			: base(lcontext)
		{
			m_LValues = new List<IVariable>();
			m_LValues.Add(CheckVar(lcontext, firstExpression));

			while (lcontext.Lexer.Current.Type == TokenType.Comma)
			{
				lcontext.Lexer.Next();
				Expression e = Expression.PrimaryExp(lcontext, false);
				m_LValues.Add(CheckVar(lcontext, e));
			}

			isIncDec = true;
			foreach (var v in m_LValues) {
				if (!v.IsAssignment)
				{
					isIncDec = false;
					break;
				}
			}

			if (!isIncDec)
			{
				if (lcontext.Syntax != ScriptSyntax.Lua) {
					switch (lcontext.Lexer.Current.Type) {
						case TokenType.Op_AddEq:
							if (lcontext.Syntax == ScriptSyntax.Wattle)
								AssignmentOp = Operator.AddConcat;
							else
								AssignmentOp = Operator.Add;
							lcontext.Lexer.Next();
							break;
						case TokenType.Op_SubEq:
							AssignmentOp = Operator.Sub;
							lcontext.Lexer.Next();
							break;
						case TokenType.Op_MulEq:
							AssignmentOp = Operator.Mul;
							lcontext.Lexer.Next();
							break;
						case TokenType.Op_DivEq:
							AssignmentOp = Operator.Div;
							lcontext.Lexer.Next();
							break;
						case TokenType.Op_ModEq:
							AssignmentOp = Operator.Mod;
							lcontext.Lexer.Next();
							break;
						case TokenType.Op_PwrEq:
							AssignmentOp = Operator.Power;
							lcontext.Lexer.Next();
							break;
						case TokenType.Op_ConcatEq:
							AssignmentOp = Operator.StrConcat;
							lcontext.Lexer.Next();
							break;
						case TokenType.Op_NilCoalescingAssignment:
							AssignmentOp = Operator.NilCoalescing;
							lcontext.Lexer.Next();
							break;
						case TokenType.Op_NilCoalescingAssignmentInverse:
							AssignmentOp = Operator.NilCoalescingInverse;
							lcontext.Lexer.Next();
							break;
						case TokenType.Colon:
							ParseType(lcontext);
							CheckTokenType(lcontext, TokenType.Op_Assignment);
							break;
						case TokenType.Op_GreaterThan:
						{
							if (lcontext.Lexer.PeekNext().Type == TokenType.Op_GreaterThanEqual) // >>=
							{
								lcontext.Lexer.Next();
								AssignmentOp = Operator.BitRShiftA;
								lcontext.Lexer.Next();
								break;
							}
							
							if (lcontext.Lexer.PeekNext().Type == TokenType.Op_GreaterThan) // >>>=
							{
								lcontext.Lexer.Next();
								if (lcontext.Lexer.PeekNext().Type == TokenType.Op_GreaterThanEqual)
								{
									lcontext.Lexer.Next();
									AssignmentOp = Operator.BitRShiftL;
									lcontext.Lexer.Next();
									break;
								}
							}
							
							CheckTokenType(lcontext, TokenType.Op_Assignment); // invalid token combination, throw
							break;
						}
						case TokenType.Op_LessThan:
						{
							if (lcontext.Lexer.PeekNext().Type == TokenType.Op_LessThanEqual) // <<=
							{
								lcontext.Lexer.Next();
								AssignmentOp = Operator.BitLShiftA;
								lcontext.Lexer.Next();
								break;
							}
							
							if (lcontext.Lexer.PeekNext().Type == TokenType.Op_LessThan) // <<<=
							{
								lcontext.Lexer.Next();
								if (lcontext.Lexer.PeekNext().Type == TokenType.Op_LessThanEqual)
								{
									lcontext.Lexer.Next();
									AssignmentOp = Operator.BitLShiftL;
									lcontext.Lexer.Next();
									break;
								}
							}

							CheckTokenType(lcontext, TokenType.Op_Assignment); // invalid token combination, throw
							break;
						}
						default:
							CheckTokenType(lcontext, TokenType.Op_Assignment);
							break;
					}
				}
				else {
					CheckTokenType(lcontext, TokenType.Op_Assignment);
				}

				m_RValues = Expression.ExprList(lcontext);
			} 
			
			Token last = lcontext.Lexer.Current;
			m_Ref = first.GetSourceRefUpTo(last);
			lcontext.Source.Refs.Add(m_Ref);
		}


		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			using (bc.EnterSource(m_Ref))
			{
				if (isIncDec)
				{
					foreach (var v in m_LValues)
					{
						(v as Expression).Compile(bc);
						bc.Emit_Pop();
					}
				}
				else
				{
					foreach (var exp in m_RValues)
					{
						exp.CompilePossibleLiteral(bc);
					}

					for (int i = 0; i < m_LValues.Count; i++)
						m_LValues[i].CompileAssignment(bc, AssignmentOp,
							Math.Max(m_RValues.Count - 1 - i, 0), // index of r-value
							i - Math.Min(i, m_RValues.Count - 1)); // index in last tuple

					bc.Emit_Pop(m_RValues.Count);
				}
			}
		}

	}
}
