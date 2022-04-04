using System.Collections.Generic;
using System.Linq;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Tree.Expressions;


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
				Token t = lcontext.Lexer.Current;
				EndToken = lcontext.Lexer.Current;

				if (t.IsEndOfBlock()) break;
				if (endType == BlockEndType.CloseCurly && t.Type == TokenType.Brk_Close_Curly) break;

				Statement s = CreateStatement(lcontext, out bool forceLast);
				m_Statements.Add(s);
				EndToken = lcontext.Lexer.Current;
				if (forceLast) break;
			}

			// eat away all superfluos ';'s
			while (lcontext.Lexer.Current.Type == TokenType.SemiColon)
				lcontext.Lexer.Next();
			
			if (lcontext.Syntax == ScriptSyntax.CLike)
			{
				List<Statement> reordered = new List<Statement>();
				foreach (Statement statement in m_Statements)
				{
					bool resolved = false;
					
					switch (statement)
					{
						case FunctionDefinitionStatement _:
						{
							reordered.Insert(0, statement);
							resolved = true;
							break;
						}
					}

					if (!resolved)
					{
						reordered.Add(statement);
					}

					m_Statements = reordered;
				}
			}
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
