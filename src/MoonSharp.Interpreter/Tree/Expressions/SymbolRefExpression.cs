using System;
using MoonSharp.Interpreter.DataStructs;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;

namespace MoonSharp.Interpreter.Tree.Expressions
{
	class SymbolRefExpression : Expression, IVariable
	{
		SymbolRef m_Ref;
		string m_VarName;
		private Token T;
		
		private bool inc = false;
		private bool dec = false;

		public bool IsAssignment => inc || dec;
		public SymbolRef Symbol => m_Ref;

		public SymbolRefExpression(Token T, ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			m_VarName = T.Text;
			this.T = T;

			lcontext.Lexer.Next();
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

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			if (m_Ref == null) {
				if (T.Type == TokenType.VarArgs)
				{
					m_Ref = lcontext.Scope.Find(WellKnownSymbols.VARARGS);

					if (!lcontext.Scope.CurrentFunctionHasVarArgs())
						throw new SyntaxErrorException(T, "cannot use '...' outside a vararg function");

					if (lcontext.IsDynamicExpression)
						throw new DynamicExpressionException("cannot use '...' in a dynamic expression.");
				}
				else
				{
					if (!lcontext.IsDynamicExpression)
						m_Ref = lcontext.Scope.Find(m_VarName);
				}
			}
		}

		public SymbolRefExpression(ScriptLoadingContext lcontext, SymbolRef refr)
			: base(lcontext)
		{
			m_Ref = refr;

			if (lcontext.IsDynamicExpression)
			{
				throw new DynamicExpressionException("Unsupported symbol reference expression detected.");
			}
		}

		public override void Compile(Execution.VM.ByteCode bc)
		{
			bc.Emit_Load(m_Ref);
			if (inc)
			{
				bc.Emit_Copy(0); //do copy before returning number
				bc.Emit_Literal(DynValue.NewNumber(1.0));
				bc.Emit_Operator(OpCode.Add);
				bc.Emit_Store(m_Ref, 0, 0);
				bc.Emit_Pop();
			} 
			else if (dec)
			{
				bc.Emit_Copy(0); //do copy before returning number
				bc.Emit_Literal(DynValue.NewNumber(1.0));
				bc.Emit_Operator(OpCode.Sub);
				bc.Emit_Store(m_Ref, 0, 0);
				bc.Emit_Pop();
			}
		}


		public void CompileAssignment(Execution.VM.ByteCode bc, Operator op, int stackofs, int tupleidx)
		{
			if (op != Operator.NotAnOperator)
			{				
				bc.Emit_Load(m_Ref); //left
				bc.Emit_CopyValue(stackofs + 1, tupleidx); //right
				bc.Emit_Operator(BinaryOperatorExpression.OperatorToOpCode(op));
				bc.Emit_Store(m_Ref, 0, 0);
				bc.Emit_Pop();
			}
			else {
				bc.Emit_Store(m_Ref, stackofs, tupleidx);
			}
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			return context.EvaluateSymbolByName(m_VarName);
		}

		public override bool EvalLiteral(out DynValue dv)
		{
			dv = DynValue.Nil;
			return false;
		}

		public override SymbolRef FindDynamic(ScriptExecutionContext context)
		{
			return context.FindSymbolByName(m_VarName);
		}
	}
}
