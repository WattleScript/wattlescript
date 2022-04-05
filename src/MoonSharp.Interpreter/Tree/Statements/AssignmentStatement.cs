﻿using System;
using System.Collections.Generic;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;

using MoonSharp.Interpreter.Tree.Expressions;

namespace MoonSharp.Interpreter.Tree.Statements
{
	class AssignmentStatement : Statement
	{
		List<IVariable> m_LValues = new List<IVariable>();
		List<Expression> m_RValues;
		SourceRef m_Ref;

		public Operator AssignmentOp = Operator.NotAnOperator;

		private bool isIncDec;


		public AssignmentStatement(ScriptLoadingContext lcontext, Token startToken)
			: base(lcontext)
		{
			List<string> names = new List<string>();

			Token first = startToken;

			while (true)
			{
				Token name = CheckTokenType(lcontext, TokenType.Name);
				names.Add(name.Text);

				if (lcontext.Lexer.Current.Type != TokenType.Comma)
					break;

				lcontext.Lexer.Next();
			}

			if (lcontext.Lexer.Current.Type == TokenType.Op_Assignment)
			{
				CheckTokenType(lcontext, TokenType.Op_Assignment);
				m_RValues = Expression.ExprList(lcontext);
			}
			else if (lcontext.Syntax == ScriptSyntax.CLike && lcontext.Lexer.Current.Type == TokenType.Op_NilCoalescingAssignment)
			{
				CheckTokenType(lcontext, TokenType.Op_NilCoalescingAssignment);
				AssignmentOp = Operator.NilCoalescing;
				m_RValues = Expression.ExprList(lcontext);
			}
			else if (lcontext.Syntax == ScriptSyntax.CLike && lcontext.Lexer.Current.Type == TokenType.Op_NilCoalesceInverse)
			{
				CheckTokenType(lcontext, TokenType.Op_NilCoalesceInverse);
				AssignmentOp = Operator.NilCoalescingInverse;
				m_RValues = Expression.ExprList(lcontext);
			}
			else
			{
				if (names.Count > 0)
					m_RValues = new List<Expression>(new Expression[] { new LiteralExpression(lcontext, DynValue.Nil) });
			}

			foreach (string name in names)
			{
				var localVar = lcontext.Scope.TryDefineLocal(name);
				var symbol = new SymbolRefExpression(lcontext, localVar);
				m_LValues.Add(symbol);
			}

			Token last = lcontext.Lexer.Current;
			m_Ref = first.GetSourceRefUpTo(last);
			lcontext.Source.Refs.Add(m_Ref);

		}


		public AssignmentStatement(ScriptLoadingContext lcontext, Expression firstExpression, Token first)
			: base(lcontext)
		{
			m_LValues.Add(CheckVar(lcontext, firstExpression));

			while (lcontext.Lexer.Current.Type == TokenType.Comma)
			{
				lcontext.Lexer.Next();
				Expression e = Expression.PrimaryExp(lcontext);
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
							if (lcontext.Syntax == ScriptSyntax.CLike)
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


		public override void Compile(Execution.VM.ByteCode bc)
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
