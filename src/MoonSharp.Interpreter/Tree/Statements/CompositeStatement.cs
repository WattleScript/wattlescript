using System.Collections.Generic;

using MoonSharp.Interpreter.Execution;


namespace MoonSharp.Interpreter.Tree.Statements
{
	enum BlockEndType
	{
		Normal,
		CloseCurly,
		SemiColon
	}
	class CompositeStatement : Statement 
	{
		List<Statement> m_Statements = new List<Statement>();

		public CompositeStatement(ScriptLoadingContext lcontext, BlockEndType endType)
			: base(lcontext)
		{
			while (true)
			{
				Token t = lcontext.Lexer.Current;
				if (t.IsEndOfBlock()) break;
				if (endType == BlockEndType.CloseCurly && t.Type == TokenType.Brk_Close_Curly) {
					lcontext.Lexer.Next();
					break;
				}
				if (endType == BlockEndType.SemiColon && t.Type == TokenType.SemiColon) {
					lcontext.Lexer.Next();
					break;
				}
				bool forceLast;
				
				Statement s = Statement.CreateStatement(lcontext, out forceLast);
				m_Statements.Add(s);

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
