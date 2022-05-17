using System.Collections.Generic;
using System.Linq;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
{
	class ForEachLoopStatement : Statement
	{
		RuntimeScopeBlock m_StackFrame;
		SymbolRef[] m_Names;
		IVariable[] m_NameExps;
		Expression m_RValues;
		Statement m_Block;
		SourceRef m_RefFor, m_RefEnd;
		private ScriptLoadingContext lcontext;

		private List<string> names;

		public ForEachLoopStatement(ScriptLoadingContext lcontext, Token firstNameToken, Token forToken, bool paren)
			: base(lcontext)
		{
			this.lcontext = lcontext;
			//	for namelist in explist do block end | 		

			names = new List<string>();
			names.Add(firstNameToken.Text);

			while (lcontext.Lexer.Current.Type == TokenType.Comma)
			{
				lcontext.Lexer.Next();
				Token name = CheckTokenType(lcontext, TokenType.Name);
				names.Add(name.Text);
			}

			CheckTokenType(lcontext, TokenType.In);

			m_RValues = new ExprListExpression(Expression.ExprList(lcontext), lcontext);
			
			if (paren) CheckTokenType(lcontext, TokenType.Brk_Close_Round);
			
			if (lcontext.Syntax == ScriptSyntax.Lua || lcontext.Lexer.Current.Type == TokenType.Do)
			{
				m_RefFor = forToken.GetSourceRef(CheckTokenType(lcontext, TokenType.Do));
				m_Block = new CompositeStatement(lcontext, BlockEndType.Normal);
				m_RefEnd = CheckTokenType(lcontext, TokenType.End).GetSourceRef();
			}
			else if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly)
			{
				m_RefFor = forToken.GetSourceRef(CheckTokenType(lcontext, TokenType.Brk_Open_Curly));
				m_Block = new CompositeStatement(lcontext, BlockEndType.CloseCurly);
				m_RefEnd = CheckTokenType(lcontext, TokenType.Brk_Close_Curly).GetSourceRef();
			}
			else
			{
				m_RefFor = forToken.GetSourceRef(lcontext.Lexer.Current);
				m_Block = CreateStatement(lcontext, out _);
				if (m_Block is IBlockStatement block)
					m_RefEnd = block.End;
				else
					m_RefEnd = CheckTokenType(lcontext, TokenType.SemiColon).GetSourceRef();
			}

			

			lcontext.Source.Refs.Add(m_RefFor);
			lcontext.Source.Refs.Add(m_RefEnd);
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			lcontext.Scope.PushBlock();
			m_Names = names
				.Select(n => lcontext.Scope.TryDefineLocal(n, out _))
				.ToArray();

			m_NameExps = m_Names
				.Select(s => new SymbolRefExpression(lcontext, s))
				.Cast<IVariable>()
				.ToArray();
			
			m_RValues?.ResolveScope(lcontext);
			m_Block.ResolveScope(lcontext);
			m_StackFrame = lcontext.Scope.PopBlock();
		}


		public override void Compile(FunctionBuilder bc)
		{
			//for var_1, ···, var_n in explist do block end
			bc.PushSourceRef(m_RefFor);

			Loop L = new Loop()
			{
				Scope = m_StackFrame
			};
			
			bc.LoopTracker.Loops.Push(L);

			// scan for range loop, if found compile as JFor
			if (CompileRangeStatement(L, bc))
			{
				return;
			}

			// get iterator tuple
			m_RValues.Compile(bc);

			// prepares iterator tuple - stack : iterator-tuple
			bc.Emit_IterPrep();

			// loop start - stack : iterator-tuple
			int start = bc.GetJumpPointForNextInstruction();
			bc.Emit_Enter(m_StackFrame);

			// expand the tuple - stack : iterator-tuple, f, var, s
			bc.Emit_ExpTuple(0);

			// calls f(s, var) - stack : iterator-tuple, iteration result
			bc.Emit_Call(2, "for..in");

			// perform assignment of iteration result- stack : iterator-tuple, iteration result
			for (int i = 0; i < m_NameExps.Length; i++)
				m_NameExps[i].CompileAssignment(bc, Operator.NotAnOperator, 0, i);

			// pops  - stack : iterator-tuple
			bc.Emit_Pop();

			// repushes the main iterator var - stack : iterator-tuple, main-iterator-var
			bc.Emit_Load(m_Names[0]);

			// updates the iterator tuple - stack : iterator-tuple, main-iterator-var
			bc.Emit_IterUpd();

			// checks head, jumps if nil - stack : iterator-tuple, main-iterator-var
			var endjump = bc.Emit_Jump(OpCode.JNil, -1);

			// executes the stuff - stack : iterator-tuple
			m_Block.Compile(bc);

			bc.PopSourceRef();
			bc.PushSourceRef(m_RefEnd);

			// loop back again - stack : iterator-tuple
			int continuePoint = bc.GetJumpPointForNextInstruction();
			bc.Emit_Leave(m_StackFrame);
			bc.Emit_Jump(OpCode.Jump, start);

			bc.LoopTracker.Loops.Pop();

			int exitpointLoopExit = bc.GetJumpPointForNextInstruction();
			bc.Emit_Leave(m_StackFrame);

			int exitpointBreaks = bc.GetJumpPointForNextInstruction();

			bc.Emit_Pop();

			foreach (int i in L.BreakJumps)
			{
				bc.SetNumVal(i, exitpointBreaks);
			}
			            
			foreach (int i in L.ContinueJumps)
				bc.SetNumVal(i, continuePoint);

			bc.SetNumVal(endjump, exitpointLoopExit);
			

			bc.PopSourceRef();
		}

		private bool CompileRangeStatement(Loop l, FunctionBuilder bc)
		{
			if (!(m_RValues is ExprListExpression listExpr)) return false;
			if (listExpr.Expressions.Count != 1) return false;
			
			Expression expr = listExpr.Expressions[0];

			if (!(expr is BinaryOperatorExpression binaryExpr)) return false;
			if (!binaryExpr.IsRangeCtor()) return false;

			if (binaryExpr.Operator == Operator.RightExclusiveRange || binaryExpr.Operator == Operator.ExclusiveRange) // ..<, >..< -> sub 1
			{
				if (binaryExpr.Exp2.EvalLiteral(out DynValue binExprDv) && binExprDv.TryCastToNumber(out double d))
				{
					bc.Emit_Literal(DynValue.NewNumber((int) d - 1));
				}
				else
				{
					binaryExpr.Exp2.Compile(bc);
					bc.Emit_ToNum(5); //Do conversion here to get correct error message
					bc.Emit_Literal(DynValue.NewNumber(1));
					bc.Emit_Operator(OpCode.Sub);
				}
			}
			else
			{
				if (binaryExpr.Exp2.EvalLiteral(out DynValue binExprDv) && binExprDv.TryCastToNumber(out double d))
				{
					bc.Emit_Literal(DynValue.NewNumber((int)d));
				}
				else
				{
					binaryExpr.Exp2.Compile(bc);
					bc.Emit_ToNum(5);
				}
			}
			
			new LiteralExpression(lcontext, DynValue.NewNumber(1)).Compile(bc); // step

			if (binaryExpr.Operator == Operator.LeftExclusiveRange || binaryExpr.Operator == Operator.ExclusiveRange) // >.., >..< -> add 1
			{
				if (binaryExpr.Exp1.EvalLiteral(out DynValue binExprDv) && binExprDv.TryCastToNumber(out double d))
				{
					bc.Emit_Literal(DynValue.NewNumber((int) d + 1));
				}
				else
				{
					binaryExpr.Exp1.Compile(bc);
					bc.Emit_ToNum(4); //Do conversion here to get correct error message
					bc.Emit_Literal(DynValue.NewNumber(1));
					bc.Emit_Operator(OpCode.Add);
				}
			}
			else
			{
				if (binaryExpr.Exp1.EvalLiteral(out DynValue binExprDv) && binExprDv.TryCastToNumber(out double d))
				{
					bc.Emit_Literal(DynValue.NewNumber((int)d));
				}
				else
				{
					binaryExpr.Exp1.Compile(bc);	
					bc.Emit_ToNum(4);
				}
			}

			int rangeStart = bc.GetJumpPointForNextInstruction();
			int rangeJmpEnd = bc.Emit_Jump(OpCode.JFor, -1);
			bc.Emit_Enter(m_StackFrame);
							
			foreach (IVariable t in m_NameExps)
				t.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);

			m_Block.Compile(bc);
							
			bc.PopSourceRef();
			bc.PushSourceRef(m_RefEnd);
							
			int rangeContinuePoint = bc.GetJumpPointForNextInstruction();
			bc.Emit_Leave(m_StackFrame);
			bc.Emit_Incr(1);
			bc.Emit_Jump(OpCode.Jump, rangeStart);

			bc.LoopTracker.Loops.Pop();

			int exitpoint = bc.GetJumpPointForNextInstruction();

			foreach (int i in l.BreakJumps)
				bc.SetNumVal(i, exitpoint);
			foreach (int i in l.ContinueJumps)
				bc.SetNumVal(i, rangeContinuePoint);
			
			bc.SetNumVal(rangeJmpEnd, exitpoint);
			bc.Emit_Pop(3);

			bc.PopSourceRef();

			return true;
		}
	}
}
