using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;


namespace MoonSharp.Interpreter.Tree.Statements
{
	class RepeatStatement : Statement, IBlockStatement
	{
		Expression m_Condition;
		Statement m_Block;
		RuntimeScopeBlock m_StackFrame;
		SourceRef m_Repeat, m_Until;

		public SourceRef End => m_Until;

		public RepeatStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			m_Repeat = CheckTokenType(lcontext, TokenType.Repeat).GetSourceRef();

			lcontext.Scope.PushBlock();
			bool openCurly = lcontext.Syntax != ScriptSyntax.Lua &&
			                 lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly;
			if (openCurly) lcontext.Lexer.Next();
			m_Block = new CompositeStatement(lcontext, openCurly ? BlockEndType.CloseCurly : BlockEndType.Normal);
			if (openCurly) CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
			
			Token until = CheckTokenType(lcontext, TokenType.Until);
			m_Condition = Expression.Expr(lcontext);
			m_Until = until.GetSourceRefUpTo(lcontext.Lexer.Current);

			m_StackFrame = lcontext.Scope.PopBlock();
			lcontext.Source.Refs.Add(m_Repeat);
			lcontext.Source.Refs.Add(m_Until);
		}

		public override void Compile(ByteCode bc)
		{
			Loop L = new Loop()
			{
				Scope = m_StackFrame
			};

			bc.PushSourceRef(m_Repeat);

			bc.LoopTracker.Loops.Push(L);

			int start = bc.GetJumpPointForNextInstruction();

			bc.Emit_Enter(m_StackFrame);
			m_Block.Compile(bc);

			bc.PopSourceRef();
			bc.PushSourceRef(m_Until);
			bc.Emit_Debug("..end");
			int continuePoint = bc.GetJumpPointForNextInstruction();
			m_Condition.Compile(bc);
			bc.Emit_Leave(m_StackFrame);
			bc.Emit_Jump(OpCode.Jf, start);

			bc.LoopTracker.Loops.Pop();

			int exitpoint = bc.GetJumpPointForNextInstruction();

			foreach (int i in L.BreakJumps)
				bc.SetNumVal(i, exitpoint);
			foreach (int i in L.ContinueJumps)
				bc.SetNumVal(i, continuePoint);

			bc.PopSourceRef();
		}


	}
}
