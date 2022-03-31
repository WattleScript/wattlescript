using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;

namespace MoonSharp.Interpreter.Tree.Statements
{
	class DoBlockStatement : Statement, IBlockStatement
	{
		Statement m_Block;
		RuntimeScopeBlock m_StackFrame;
		SourceRef m_Do, m_End;
		Expression m_Condition;

		public SourceRef End => m_End;

		public DoBlockStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			lcontext.Scope.PushBlock();

			m_Do = CheckTokenType(lcontext, TokenType.Do).GetSourceRef();


			if (lcontext.Syntax != ScriptSyntax.Lua && 
			    lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly)
			{
				lcontext.Lexer.Next();
				m_Block = new CompositeStatement(lcontext, BlockEndType.CloseCurly);
				CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
				Token cond = CheckTokenType(lcontext, TokenType.While);
				m_Condition = Expression.Expr(lcontext);
				m_End = cond.GetSourceRefUpTo(lcontext.Lexer.Current);
			}
			else
			{
				m_Block = new CompositeStatement(lcontext, BlockEndType.Normal);
				m_End = CheckTokenType(lcontext, TokenType.End).GetSourceRef();
			}
			m_StackFrame = lcontext.Scope.PopBlock();
			lcontext.Source.Refs.Add(m_Do);
			lcontext.Source.Refs.Add(m_End);

		}



		public override void Compile(Execution.VM.ByteCode bc)
		{
			if(m_Condition != null) CompileLoop(bc);
			else CompileScopeBlock(bc);
		}

		void CompileLoop(Execution.VM.ByteCode bc)
		{
			Loop L = new Loop()
			{
				Scope = m_StackFrame
			};

			bc.PushSourceRef(m_Do);

			bc.LoopTracker.Loops.Push(L);

			int start = bc.GetJumpPointForNextInstruction();

			bc.Emit_Enter(m_StackFrame);
			m_Block.Compile(bc);

			bc.PopSourceRef();
			bc.PushSourceRef(m_End);
			bc.Emit_Debug("..end");

			m_Condition.Compile(bc);
			bc.Emit_Leave(m_StackFrame);
			bc.Emit_Jump(OpCode.Jt, start);

			bc.LoopTracker.Loops.Pop();

			int exitpoint = bc.GetJumpPointForNextInstruction();

			foreach (int i in L.BreakJumps)
				bc.SetNumVal(i, exitpoint);

			bc.PopSourceRef();
		}
		
		void CompileScopeBlock(Execution.VM.ByteCode bc)
		{
			using(bc.EnterSource(m_Do))
				bc.Emit_Enter(m_StackFrame);

			m_Block.Compile(bc);

			using (bc.EnterSource(m_End))
				bc.Emit_Leave(m_StackFrame);
		}

	}
}
