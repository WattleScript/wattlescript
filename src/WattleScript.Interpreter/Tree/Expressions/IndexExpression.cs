using System;
using System.Collections.Generic;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Expressions
{
	class IndexExpression : Expression, IVariable
	{
		Expression m_BaseExp;
		Expression m_IndexExp;
		Expression m_ThisExp;
		string m_Name;
		private bool inc;
		private bool dec;
		private bool nilCheck;
		private bool isLength = false;

		public bool IsAssignment => inc || dec;

		public bool NilCheck => nilCheck;
		public Expression NilChainNext { get; set; }

		public IndexExpression(Expression baseExp, Expression indexExp, bool nilCheck, ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			m_BaseExp = baseExp;
			m_IndexExp = indexExp;
			this.nilCheck = nilCheck;
			//inc/dec expr
			if (lcontext.Lexer.Current.Type == TokenType.Op_Inc)
			{
				inc = true;
				lcontext.Lexer.Next();
			} 
			else if (lcontext.Lexer.Current.Type == TokenType.Op_Dec)
			{
				dec = true;
				lcontext.Lexer.Next();
			}
		}

		public IndexExpression(Expression baseExp, Token nameToken, bool nilCheck, ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			m_BaseExp = baseExp;
			m_Name = nameToken.Text;
			this.nilCheck = nilCheck;
			//
			if (lcontext.Syntax == ScriptSyntax.Wattle && m_Name.Equals("length")) {
				isLength = true;
			}
			//inc/dec expr
			if (lcontext.Lexer.Current.Type == TokenType.Op_Inc)
			{
				if (isLength)
					throw new SyntaxErrorException(lcontext.Lexer.Current, "Cannot assign to readonly property .length");
				inc = true;
				lcontext.Lexer.Next();
			} 
			else if (lcontext.Lexer.Current.Type == TokenType.Op_Dec)
			{
				if (isLength)
					throw new SyntaxErrorException(lcontext.Lexer.Current, "Cannot assign to readonly property .length");
				dec = true;
				lcontext.Lexer.Next();
			}
		}


		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			m_BaseExp.ResolveScope(lcontext);
			m_IndexExp?.ResolveScope(lcontext);
			if (m_BaseExp is SymbolRefExpression se && (se.Symbol?.IsBaseClass ?? false))
			{
				m_ThisExp = new SymbolRefExpression(lcontext, lcontext.Scope.Find("this"));
			}
		}


		public override void Compile(FunctionBuilder bc)
		{
			Compile(bc, false);
		}
		public void Compile(FunctionBuilder bc, bool duplicate, bool isMethodCall = false)
		{
			bool accessPrivate = (m_BaseExp is SymbolRefExpression sr && sr.Symbol.IsThisArgument);
			
			if (duplicate && m_ThisExp != null)
			{
				m_ThisExp.Compile(bc);
				m_BaseExp.Compile(bc);
				bc.Emit_Index("__index");
			} 
			else if (duplicate) 
			{
				m_BaseExp.Compile(bc);
				bc.Emit_Copy(0);
			}
			else {
				m_BaseExp.Compile(bc);
			}
			if (isLength) {
				if (nilCheck) {
					bc.NilChainTargets.Push(bc.Emit_Jump(OpCode.JNilChk, -1));
				}
				bc.Emit_Operator(OpCode.Len);
				if (bc.NilChainTargets.Count > 0 && NilChainNext == null)
				{
					bc.SetNumVal(bc.NilChainTargets.Pop(), bc.GetJumpPointForNextInstruction());
				}
				return;
			}
			if (nilCheck)
			{
				bc.NilChainTargets.Push(bc.Emit_Jump(OpCode.JNilChk, -1));
			}
			if (m_Name != null)
			{
				bc.Emit_Index(m_Name, true, isMethodCall: isMethodCall, accessPrivate: accessPrivate);
			}
			else if (m_IndexExp is LiteralExpression lit && lit.Value.Type == DataType.String)
			{
				bc.Emit_Index(lit.Value.String, isMethodCall: isMethodCall, accessPrivate: accessPrivate);
			}
			else
			{
				m_IndexExp.Compile(bc);
				bc.Emit_Index(isExpList: (m_IndexExp is ExprListExpression), isMethodCall: isMethodCall, accessPrivate: accessPrivate);
			}
			if (inc)
			{
				bc.Emit_Copy(0);
				bc.Emit_Literal(DynValue.NewNumber(1.0));
				bc.Emit_Operator(OpCode.Add);
				CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
				bc.Emit_Pop();
			} 
			else if (dec)
			{
				bc.Emit_Copy(0);
				bc.Emit_Literal(DynValue.NewNumber(1.0));
				bc.Emit_Operator(OpCode.Sub);
				CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
				bc.Emit_Pop();
			}
			if (bc.NilChainTargets.Count > 0 && NilChainNext == null)
			{
				bc.SetNumVal(bc.NilChainTargets.Pop(), bc.GetJumpPointForNextInstruction());
			}
		}

		public void CompileAssignment(FunctionBuilder bc, Operator op, int stackofs, int tupleidx)
		{
			bool accessPrivate = (m_BaseExp is SymbolRefExpression sr && sr.Symbol.IsThisArgument);

			if (isLength)
			{ 
				throw new SyntaxErrorException(null, "Cannot assign to readonly property .length");
			}
			if (op != Operator.NotAnOperator)
			{
				Compile(bc); //left
				bc.Emit_CopyValue(stackofs + 1, tupleidx); //right
				bc.Emit_Operator(BinaryOperatorExpression.OperatorToOpCode(op));
				stackofs = 0;
				tupleidx = 0;
			}
			m_BaseExp.Compile(bc);

			if (m_Name != null)
			{
				bc.Emit_IndexSet(stackofs, tupleidx, m_Name, isNameIndex: true, accessPrivate: accessPrivate);
			}
			else if (m_IndexExp is LiteralExpression lit && lit.Value.Type == DataType.String)
			{
				bc.Emit_IndexSet(stackofs, tupleidx, lit.Value.String, accessPrivate: accessPrivate);
			}
			else
			{
				m_IndexExp.Compile(bc);
				bc.Emit_IndexSet(stackofs, tupleidx, isExpList: (m_IndexExp is ExprListExpression), accessPrivate: accessPrivate);
			}

			if (op != Operator.NotAnOperator) bc.Emit_Pop();
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			DynValue b = m_BaseExp.Eval(context).ToScalar();
			DynValue i = m_IndexExp != null ? m_IndexExp.Eval(context).ToScalar() : DynValue.NewString(m_Name);

			if (b.Type != DataType.Table) throw new DynamicExpressionException("Attempt to index non-table.");
			else if (i.IsNilOrNan()) throw new DynamicExpressionException("Attempt to index with nil or nan key.");
			return b.Table.Get(i);
		}

		public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
		{
			dv = DynValue.Nil;
			return false;
		}
	}
}
