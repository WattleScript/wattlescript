using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;

using MoonSharp.Interpreter.Tree.Expressions;

namespace MoonSharp.Interpreter.Tree.Statements
{
	class ForLoopStatement : Statement, IBlockStatement
	{
		//for' NAME '=' exp ',' exp (',' exp)? 'do' block 'end'
		RuntimeScopeBlock m_StackFrame;
		Statement m_InnerBlock;
		SymbolRef m_VarName;
		Expression m_Start, m_End, m_Step;
		SourceRef m_RefFor, m_RefEnd;
		private Token nameToken;

		public SourceRef End => m_RefEnd;
		
		public ForLoopStatement(ScriptLoadingContext lcontext, Token nameToken, Token forToken, bool paren)
			: base(lcontext)
		{
			//	for Name ‘=’ exp ‘,’ exp [‘,’ exp] do block end | 

			// lexer already at the '=' ! [due to dispatching vs for-each]
			CheckTokenType(lcontext, TokenType.Op_Assignment);

			m_Start = Expression.Expr(lcontext);
			CheckTokenType(lcontext, TokenType.Comma);
			m_End = Expression.Expr(lcontext);

			if (lcontext.Lexer.Current.Type == TokenType.Comma)
			{
				lcontext.Lexer.Next();
				m_Step = Expression.Expr(lcontext);
			}
			else
			{
				m_Step = new LiteralExpression(lcontext, DynValue.NewNumber(1));
			}

			this.nameToken = nameToken;
			if (paren) CheckTokenType(lcontext, TokenType.Brk_Close_Round);

			if (lcontext.Syntax == ScriptSyntax.Lua || lcontext.Lexer.Current.Type == TokenType.Do)
			{
				m_RefFor = forToken.GetSourceRef(CheckTokenType(lcontext, TokenType.Do));
				m_InnerBlock = new CompositeStatement(lcontext, BlockEndType.Normal);
				m_RefEnd = CheckTokenType(lcontext, TokenType.End).GetSourceRef();
			}
			else if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly)
			{
				m_RefFor = forToken.GetSourceRef(CheckTokenType(lcontext, TokenType.Brk_Open_Curly));
				m_InnerBlock = new CompositeStatement(lcontext, BlockEndType.CloseCurly);
				m_RefEnd = CheckTokenType(lcontext, TokenType.Brk_Close_Curly).GetSourceRef();
			}
			else
			{
				m_RefFor = forToken.GetSourceRef(lcontext.Lexer.Current);
				m_InnerBlock = CreateStatement(lcontext, out _);
				if (m_InnerBlock is IBlockStatement block)
					m_RefEnd = block.End;
				else
					m_RefEnd = CheckTokenType(lcontext, TokenType.SemiColon).GetSourceRef();
			}

			lcontext.Source.Refs.Add(m_RefFor);
			lcontext.Source.Refs.Add(m_RefEnd);
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			m_Start.ResolveScope(lcontext);
			m_End.ResolveScope(lcontext);
			m_Step.ResolveScope(lcontext);
			lcontext.Scope.PushBlock();
			m_VarName = lcontext.Scope.DefineLocal(nameToken.Text);
			m_InnerBlock.ResolveScope(lcontext);
			m_StackFrame = lcontext.Scope.PopBlock();
		}


		public override void Compile(ByteCode bc)
		{
			bc.PushSourceRef(m_RefFor);

			Loop L = new Loop()
			{
				Scope = m_StackFrame
			};

			bc.LoopTracker.Loops.Push(L);

			m_End.CompilePossibleLiteral(bc);
			m_Step.Compile(bc);
			m_Start.CompilePossibleLiteral(bc);

			int start = bc.GetJumpPointForNextInstruction();
			var jumpend = bc.Emit_Jump(OpCode.JFor, -1);
			bc.Emit_Enter(m_StackFrame);

			bc.Emit_Store(m_VarName, 0, 0);

			m_InnerBlock.Compile(bc);

			bc.PopSourceRef();
			bc.PushSourceRef(m_RefEnd);

			bc.Emit_Debug("..end");

			int continuePoint = bc.GetJumpPointForNextInstruction();
			bc.Emit_Leave(m_StackFrame);
			bc.Emit_Incr(1);
			bc.Emit_Jump(OpCode.Jump, start);

			bc.LoopTracker.Loops.Pop();

			int exitpoint = bc.GetJumpPointForNextInstruction();

			foreach (int i in L.BreakJumps)
				bc.SetNumVal(i, exitpoint);
			foreach (int i in L.ContinueJumps)
				bc.SetNumVal(i, continuePoint);
			
			bc.SetNumVal(jumpend, exitpoint);
			bc.Emit_Pop(3);

			bc.PopSourceRef();
		}

	}
}
