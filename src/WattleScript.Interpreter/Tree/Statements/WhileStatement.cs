using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;


namespace WattleScript.Interpreter.Tree.Statements
{
	class WhileStatement : Statement, IBlockStatement
	{
		Expression m_Condition;
		Statement m_Block;
		RuntimeScopeBlock m_StackFrame;
		SourceRef m_Start, m_End;
		
		public SourceRef End => m_End;

		
		public WhileStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			Token whileTk = CheckTokenType(lcontext, TokenType.While);

			m_Condition = Expression.Expr(lcontext);

			m_Start = whileTk.GetSourceRefUpTo(lcontext.Lexer.Current);

			
			
			if (lcontext.Syntax != ScriptSyntax.Lua &&
			    lcontext.Lexer.Current.Type != TokenType.Do &&
			    lcontext.Lexer.Current.Type != TokenType.Brk_Open_Curly)
			{
				m_Block = CreateStatement(lcontext, out _);
				if (m_Block is IBlockStatement block)
					m_End = block.End;
				else
					m_End = CheckTokenType(lcontext, TokenType.SemiColon).GetSourceRef();
			}
			else
			{
				var tk = CheckTokenTypeEx(lcontext, TokenType.Do, TokenType.Brk_Open_Curly);
				m_Block = new CompositeStatement(lcontext,
					tk.Type == TokenType.Brk_Open_Curly ? BlockEndType.CloseCurly : BlockEndType.Normal);
				m_End = CheckTokenTypeEx(lcontext, TokenType.End, TokenType.Brk_Close_Curly).GetSourceRef();
			}


			lcontext.Source.Refs.Add(m_Start);
			lcontext.Source.Refs.Add(m_End);
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			lcontext.Scope.PushBlock();
			m_Condition.ResolveScope(lcontext);
			m_Block.ResolveScope(lcontext);
			m_StackFrame = lcontext.Scope.PopBlock();
		}


		public override void Compile(FunctionBuilder bc)
		{
			Loop L = new Loop()
			{
				Scope = m_StackFrame
			};


			bc.LoopTracker.Loops.Push(L);

			bc.PushSourceRef(m_Start);

			int start = bc.GetJumpPointForNextInstruction();

			m_Condition.Compile(bc);
			var jumpend = bc.Emit_Jump(OpCode.Jf, -1);

			bc.Emit_Enter(m_StackFrame);

			m_Block.Compile(bc);

			bc.PopSourceRef();
			bc.Emit_Debug("..end");
			bc.PushSourceRef(m_End);
	
			int continuePoint = bc.GetJumpPointForNextInstruction();
			bc.Emit_Leave(m_StackFrame);
			bc.Emit_Jump(OpCode.Jump, start);
			
			bc.LoopTracker.Loops.Pop();

			int exitpoint = bc.GetJumpPointForNextInstruction();

			foreach (int i in L.BreakJumps)
				bc.SetNumVal(i, exitpoint);
			foreach (int i in L.ContinueJumps)
				bc.SetNumVal(i, continuePoint);
			
			bc.SetNumVal(jumpend, exitpoint);

			bc.PopSourceRef();
		}

	}
}
