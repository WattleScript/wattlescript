using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;


namespace WattleScript.Interpreter.Tree.Statements
{
	class IfStatement : Statement, IBlockStatement
	{
		private class IfBlock
		{
			public Expression Exp;
			public Statement Block;
			public RuntimeScopeBlock StackFrame;
			public SourceRef Source;
		}

		public SourceRef End => m_End;

		List<IfBlock> m_Ifs = new List<IfBlock>();
		IfBlock m_Else = null;
		SourceRef m_End;

		public IfStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			bool cStyle;
			bool endBlock;
			m_Ifs.Add(CreateIfBlock(lcontext, out cStyle, out endBlock));
			while (!endBlock && lcontext.Lexer.Current.Type == TokenType.ElseIf) {
				m_Ifs.Add(CreateIfBlock(lcontext, out cStyle, out endBlock));
			}
			if (!endBlock && lcontext.Lexer.Current.Type == TokenType.Else)
			{
				m_Else = CreateElseBlock(lcontext, cStyle);
			}
			lcontext.Source.Refs.Add(m_End);
		}

		
		IfBlock CreateIfBlock(ScriptLoadingContext lcontext, out bool cstyle, out bool endBlock)
		{
			Token type = CheckTokenType(lcontext, TokenType.If, TokenType.ElseIf);
			var ifblock = new IfBlock();
			ifblock.Exp = Expression.Expr(lcontext);
			cstyle = false;
			endBlock = false;
			if (lcontext.Syntax == ScriptSyntax.Lua)
			{
				ifblock.Source = type.GetSourceRef(CheckTokenType(lcontext, TokenType.Then));
				ifblock.Block = new CompositeStatement(lcontext, BlockEndType.Normal);
				if (lcontext.Lexer.Current.Type == TokenType.End) {
					m_End = lcontext.Lexer.Current.GetSourceRef();
					lcontext.Lexer.Next();
					endBlock = true;
				}
			}
			else
			{
				Token open = lcontext.Lexer.Current;
				ifblock.Source = type.GetSourceRef(open);
				if (open.Type == TokenType.Brk_Open_Curly) {
					lcontext.Lexer.Next();
					ifblock.Block = new CompositeStatement(lcontext, BlockEndType.CloseCurly);
					m_End = CheckTokenType(lcontext, TokenType.Brk_Close_Curly).GetSourceRef();
					cstyle = true;
				} 
				else if (open.Type == TokenType.Then) {
					lcontext.Lexer.Next();
					ifblock.Block = new CompositeStatement(lcontext, BlockEndType.Normal);
					if (lcontext.Lexer.Current.Type == TokenType.End) {
						m_End = lcontext.Lexer.Current.GetSourceRef();
						lcontext.Lexer.Next();
						endBlock = true;
					}
				}
				else
				{
					ifblock.Source = type.GetSourceRef(lcontext.Lexer.Current);
					ifblock.Block = Statement.CreateStatement(lcontext, out _);
					if (ifblock.Block is IBlockStatement block)
						m_End = block.End;
					else
						m_End = CheckTokenType(lcontext, TokenType.SemiColon).GetSourceRef();
					cstyle = true;
				}
			}
			lcontext.Source.Refs.Add(ifblock.Source);
			return ifblock;
		}
		

		IfBlock CreateElseBlock(ScriptLoadingContext lcontext, bool cstyle)
		{
			Token type = CheckTokenType(lcontext, TokenType.Else);
			var ifblock = new IfBlock();
			if (cstyle)
			{
				Token open = lcontext.Lexer.Current;
				ifblock.Source = type.GetSourceRef(open);
				if (open.Type == TokenType.Brk_Open_Curly) {
					lcontext.Lexer.Next();
					ifblock.Block = new CompositeStatement(lcontext, BlockEndType.CloseCurly);
					m_End = CheckTokenType(lcontext, TokenType.Brk_Close_Curly).GetSourceRef();
				}
				else {
					ifblock.Block = CreateStatement(lcontext, out _);
					if (ifblock.Block is IBlockStatement block)
						m_End = block.End;
					else
						m_End = CheckTokenType(lcontext, TokenType.SemiColon).GetSourceRef();
				}
			}
			else
			{
				ifblock.Source = type.GetSourceRef();
				ifblock.Block = new CompositeStatement(lcontext, BlockEndType.Normal);
				m_End = CheckTokenType(lcontext, TokenType.End).GetSourceRef();
			}
			lcontext.Source.Refs.Add(ifblock.Source);
			return ifblock;
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			foreach (var block in m_Ifs)
			{
				lcontext.Scope.PushBlock();
				block.Exp?.ResolveScope(lcontext);
				block.Block?.ResolveScope(lcontext);
				block.StackFrame = lcontext.Scope.PopBlock();
			}
			if (m_Else != null)
			{
				lcontext.Scope.PushBlock();
				m_Else.Block?.ResolveScope(lcontext);
				m_Else.StackFrame = lcontext.Scope.PopBlock();
			}
		}


		public override void Compile(Execution.VM.FunctionBuilder bc)
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
