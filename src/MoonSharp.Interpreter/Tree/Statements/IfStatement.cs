using System.Collections.Generic;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;


namespace MoonSharp.Interpreter.Tree.Statements
{
	class IfStatement : Statement
	{
		private class IfBlock
		{
			public Expression Exp;
			public Statement Block;
			public RuntimeScopeBlock StackFrame;
			public SourceRef Source;
		}

		List<IfBlock> m_Ifs = new List<IfBlock>();
		IfBlock m_Else = null;
		SourceRef m_End;

		public IfStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			bool openedCurly = false;
			
			m_Ifs.Add(CreateIfBlock(lcontext, out openedCurly));
			if (openedCurly) {
				m_End = CheckTokenType(lcontext, TokenType.Brk_Close_Curly).GetSourceRef();
			}
			while (lcontext.Lexer.Current.Type == TokenType.ElseIf) {
				m_Ifs.Add(CreateIfBlock(lcontext, out openedCurly));
				if (openedCurly) {
					m_End = CheckTokenType(lcontext, TokenType.Brk_Close_Curly).GetSourceRef();
				}
			}
			if (lcontext.Lexer.Current.Type == TokenType.Else) {
				m_Else = CreateElseBlock(lcontext, out openedCurly);
				m_End = CheckTokenType(lcontext, openedCurly ? TokenType.Brk_Close_Curly : TokenType.End).GetSourceRef();
			} else if (!openedCurly) {
				m_End = CheckTokenType(lcontext, TokenType.End).GetSourceRef();
			}

			lcontext.Source.Refs.Add(m_End);
		}

		IfBlock CreateIfBlock(ScriptLoadingContext lcontext, out bool curlyOpen)
		{
			Token type = CheckTokenType(lcontext, TokenType.If, TokenType.ElseIf);

			lcontext.Scope.PushBlock();

			var ifblock = new IfBlock();

			ifblock.Exp = Expression.Expr(lcontext);
			var open = CheckTokenType(lcontext, TokenType.Then, TokenType.Brk_Open_Curly);
			curlyOpen = open.Type == TokenType.Brk_Open_Curly;
			ifblock.Source = type.GetSourceRef(open);
			ifblock.Block = new CompositeStatement(lcontext, open.Type == TokenType.Brk_Open_Curly ? BlockEndType.CloseCurly : BlockEndType.Normal);
			ifblock.StackFrame = lcontext.Scope.PopBlock();
			lcontext.Source.Refs.Add(ifblock.Source);


			return ifblock;
		}

		IfBlock CreateElseBlock(ScriptLoadingContext lcontext, out bool openedCurly)
		{
			Token type = CheckTokenType(lcontext, TokenType.Else);

			lcontext.Scope.PushBlock();

			var ifblock = new IfBlock();
			openedCurly = lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly;
			if(openedCurly) lcontext.Lexer.Next();
			ifblock.Block = new CompositeStatement(lcontext, openedCurly ? BlockEndType.CloseCurly : BlockEndType.Normal);
			ifblock.StackFrame = lcontext.Scope.PopBlock();
			ifblock.Source = type.GetSourceRef();
			lcontext.Source.Refs.Add(ifblock.Source);
			return ifblock;
		}


		public override void Compile(Execution.VM.ByteCode bc)
		{
			List<int> endJumps = new List<int>();

			int lastIfJmp = -1;

			foreach (var ifblock in m_Ifs)
			{
				using (bc.EnterSource(ifblock.Source))
				{
					if (lastIfJmp != -1)
						bc.SetNumVal(lastIfJmp, bc.GetJumpPointForNextInstruction());

					ifblock.Exp.CompilePossibleLiteral(bc);
					lastIfJmp = bc.Emit_Jump(OpCode.Jf, -1);
					bc.Emit_Enter(ifblock.StackFrame);
					ifblock.Block.Compile(bc);
				}

				using (bc.EnterSource(m_End))
					bc.Emit_Leave(ifblock.StackFrame);

				endJumps.Add(bc.Emit_Jump(OpCode.Jump, -1));
			}

			bc.SetNumVal(lastIfJmp, bc.GetJumpPointForNextInstruction());

			if (m_Else != null)
			{
				using (bc.EnterSource(m_Else.Source))
				{
					bc.Emit_Enter(m_Else.StackFrame);
					m_Else.Block.Compile(bc);
				}

				using (bc.EnterSource(m_End))
					bc.Emit_Leave(m_Else.StackFrame);
			}

			foreach(var endjmp in endJumps)
				bc.SetNumVal(endjmp, bc.GetJumpPointForNextInstruction());
		}



	}
}
