using System.Collections.Generic;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Execution;

namespace WattleScript.Interpreter.Tree.Expressions
{
	class TableConstructor : Expression 
	{
		bool m_Shared = false;
		List<Expression> m_PositionalValues = new List<Expression>();
		List<KeyValuePair<Expression, Expression>> m_CtorArgs = new List<KeyValuePair<Expression, Expression>>();

		public TableConstructor(ScriptLoadingContext lcontext, bool shared)
			: base(lcontext)
		{
			m_Shared = shared;

			// here lexer is at the '{' (or '[' for c-like), go on
			TokenType closeType = TokenType.Brk_Close_Curly;
			if (lcontext.Syntax != ScriptSyntax.Lua && lcontext.Lexer.Current.Type == TokenType.Brk_Open_Square) {
				closeType = TokenType.Brk_Close_Square;
				lcontext.Lexer.Next();
			}
			else {
				CheckTokenType(lcontext, TokenType.Brk_Open_Curly, TokenType.Brk_Open_Curly_Shared);
			}

			while (lcontext.Lexer.Current.Type != closeType)
			{
				switch (lcontext.Lexer.Current.Type)
				{
					case TokenType.String:
						if (lcontext.Syntax != ScriptSyntax.Lua)
						{
							Token assign = lcontext.Lexer.PeekNext();
							if(assign.Type == TokenType.Colon)
								StructField(lcontext);
							else
								ArrayField(lcontext);
						}
						else ArrayField(lcontext);
						break;
					case TokenType.Name:
						{
							Token assign = lcontext.Lexer.PeekNext();

							if (assign.Type == TokenType.Op_Assignment ||
							    assign.Type == TokenType.Colon && lcontext.Syntax != ScriptSyntax.Lua)
							    StructField(lcontext);
							else
								ArrayField(lcontext);
						}
						break;
					case TokenType.Brk_Open_Square:
						MapField(lcontext);
						break;
					default:
						ArrayField(lcontext);
						break;
				}

				Token curr = lcontext.Lexer.Current;

				if (curr.Type == TokenType.Comma || curr.Type == TokenType.SemiColon)
				{
					lcontext.Lexer.Next();
				}
				else
				{
					break;
				}
			}

			CheckTokenType(lcontext, closeType);
		}

		private void MapField(ScriptLoadingContext lcontext)
		{
			lcontext.Lexer.SavePos();
			lcontext.Lexer.Next(); // skip '['

			Expression key = Expr(lcontext);
			if (lcontext.Syntax != ScriptSyntax.Lua &&
			    lcontext.Lexer.Current.Type == TokenType.Comma) {
				lcontext.Lexer.RestorePos();
				ArrayField(lcontext);
				return;
			}
			CheckTokenType(lcontext, TokenType.Brk_Close_Square);
			if (lcontext.Syntax != ScriptSyntax.Lua &&
			    lcontext.Lexer.Current.Type != TokenType.Op_Assignment &&
			    lcontext.Lexer.Current.Type != TokenType.Colon)
			{
				lcontext.Lexer.RestorePos();
				ArrayField(lcontext);
				return;
			}

			CheckTokenTypeEx(lcontext, TokenType.Op_Assignment, TokenType.Colon);

			Expression value = Expr(lcontext, lcontext.Syntax == ScriptSyntax.Wattle);

			m_CtorArgs.Add(new KeyValuePair<Expression, Expression>(key, value));
		}

		private void StructField(ScriptLoadingContext lcontext)
		{
			Expression key = new LiteralExpression(lcontext, DynValue.NewString(lcontext.Lexer.Current.Text));
			lcontext.Lexer.Next();

			CheckTokenTypeEx(lcontext, TokenType.Op_Assignment, TokenType.Colon);

			Expression value = Expr(lcontext, lcontext.Syntax == ScriptSyntax.Wattle);

			m_CtorArgs.Add(new KeyValuePair<Expression, Expression>(key, value));
		}


		private void ArrayField(ScriptLoadingContext lcontext)
		{
			Expression e = Expr(lcontext, lcontext.Syntax == ScriptSyntax.Wattle);
			m_PositionalValues.Add(e);
		}

		
		/// <summary>
		/// Different to EvalLiteral, as this can't be stored in regular code.
		/// dv is a prime table on success
		/// </summary>
		public bool TryGetLiteral(out DynValue dv)
		{
			dv = DynValue.Nil;
			var tblVal = DynValue.NewPrimeTable();
			var table = tblVal.Table;
			foreach (var kvp in m_CtorArgs)
			{
				DynValue key, value;
				//Key must be literal
				if (!kvp.Key.EvalLiteral(out key)) return false;
				//Value can be prime table or literal
				if (kvp.Value is TableConstructor tbl) 
				{
					if (!tbl.TryGetLiteral(out value))
					{
						return false;
					}
				} 
				else if (!kvp.Value.EvalLiteral(out value))
				{
					return false;
				}
				table.Set(key, value);
			}
			for (int i = 0; i < m_PositionalValues.Count; i++)
			{
				var exp = m_PositionalValues[i];
				DynValue value;
				if (exp is TableConstructor tbl) 
				{
					if (!tbl.TryGetLiteral(out value))
					{
						return false;
					}
				} 
				else if (!exp.EvalLiteral(out value))
				{
					return false;
				}
				table.Set(i + 1, value);
			}
			dv = tblVal;
			return true;
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			foreach (var kvp in m_CtorArgs)
			{
				kvp.Key.ResolveScope(lcontext);
				kvp.Value.ResolveScope(lcontext);
			}
			foreach (var p in m_PositionalValues)
			{
				p.ResolveScope(lcontext);
			}
		}


		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			//Set pair values in groups of 8
			int j = 0;
			bool created = false;
			int type = m_Shared ? 2 : 1;
			foreach (var kvp in m_CtorArgs)
			{
				if (j >= 8)
				{
					bc.Emit_TblInitN(j * 2, created ? 0 : type);
					created = true;
					j = 0;
				}
				kvp.Key.Compile(bc);
				kvp.Value.Compile(bc);
				j++;
			}
			if (j > 0) {
				bc.Emit_TblInitN(j * 2, created ? 0 : type);
				created = true;
			}
			//Set positional values in groups of 16
			j = 0;
			int start = 0;
			for (int i = 0; i < m_PositionalValues.Count; i++ )
			{
				if ((i == m_PositionalValues.Count - 1 && j > 0)|| j >= 16)
				{
					bc.Emit_TblInitI(start, j, created ? 0 : type, false);
					start += j;
					created = true;
					j = 0;
				}
				m_PositionalValues[i].Compile(bc);
				if (i == m_PositionalValues.Count - 1) {
					bc.Emit_TblInitI(start, 1, created ? 0 : type, true);
					created = true;
				}
				j++;
			}
			//
			if (!created) bc.Emit_TblInitN(0, type); //create empty table
		}


		public override DynValue Eval(ScriptExecutionContext context)
		{
			if (!this.m_Shared)
			{
				throw new DynamicExpressionException("Dynamic Expressions cannot define new non-prime tables.");
			}

			DynValue tval = DynValue.NewPrimeTable();
			Table t = tval.Table;

			int idx = 0;
			foreach (Expression e in m_PositionalValues)
			{
				t.Set(++idx, e.Eval(context));
			}

			foreach (KeyValuePair<Expression, Expression> kvp in this.m_CtorArgs)
			{
				t.Set(kvp.Key.Eval(context), kvp.Value.Eval(context));
			}

			return tval;
		}

		public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
		{
			dv = DynValue.Nil;
			return false;
		}
	}
}
