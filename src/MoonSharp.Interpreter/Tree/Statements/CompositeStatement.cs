using System.Collections.Generic;

using MoonSharp.Interpreter.Execution;


namespace MoonSharp.Interpreter.Tree.Statements
{
	enum BlockEndType
	{
		Normal,
		CloseCurly
	}
	class CompositeStatement : Statement 
	{
		List<Statement> m_Statements = new List<Statement>();

		public Token EndToken;

		public CompositeStatement(ScriptLoadingContext lcontext, BlockEndType endType)
			: base(lcontext)
		{
			while (true)
			{
				ParseAnnotations(lcontext);
				Token t = lcontext.Lexer.Current;
				EndToken = lcontext.Lexer.Current;
				if (t.IsEndOfBlock()) break;
				if (endType == BlockEndType.CloseCurly && t.Type == TokenType.Brk_Close_Curly) break;
				bool forceLast;
				
				Statement s = Statement.CreateStatement(lcontext, out forceLast);
				m_Statements.Add(s);
				EndToken = lcontext.Lexer.Current;
				if (forceLast) break;
			}

			// eat away all superfluos ';'s
			while (lcontext.Lexer.Current.Type == TokenType.SemiColon)
				lcontext.Lexer.Next();
		}


		public override void Compile(Execution.VM.ByteCode bc)
		{
			if (m_Statements != null)
			{
				foreach (Statement s in m_Statements)
				{
					s.Compile(bc);
				}
			}
		}
	}
}
