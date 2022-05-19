using System;
using System.Collections.Generic;

using WattleScript.Interpreter.Execution;


namespace WattleScript.Interpreter.Tree.Statements
{
	enum BlockEndType
	{
		Normal,
		CloseCurly,
		Switch
	}
	class CompositeStatement : Statement 
	{
		List<Statement> m_Statements = new List<Statement>();

		public CompositeStatement(ScriptLoadingContext lcontext, BlockEndType endType)
			: base(lcontext)
		{
			while (true)
			{
				try
				{
					ParseAnnotations(lcontext);
					Token t = lcontext.Lexer.Current;
					if (t.IsEndOfBlock()) break;
					if (endType == BlockEndType.CloseCurly && t.Type == TokenType.Brk_Close_Curly) break;
					if (endType == BlockEndType.Switch) {
						if (t.Type == TokenType.Brk_Close_Curly) break;
						if (t.Type == TokenType.Case) break;
						if (t.Type == TokenType.Name && t.Text == "default") break;
					}
					Statement s = CreateStatement(lcontext, out bool forceLast);
					m_Statements.Add(s);
					if (forceLast) break;
				}
				catch (InterpreterException e)
				{
					if (lcontext.Script.Options.ParserErrorMode == ScriptOptions.ParserErrorModes.Report)
					{
						Token token = null;
						if (e is SyntaxErrorException se)
						{
							token = se.Token;
						}
						
						lcontext.Script.i_ParserMessages.Add(new Script.ScriptParserMessage(token, e.Message));
						Synchronize(lcontext);
						
						if (lcontext.Lexer.PeekNext().Type == TokenType.Eof)
						{
							lcontext.Lexer.Next();
							break;
						}
					}
					else
					{
						throw;
					}
				}
			}

			// eat away all superfluos ';'s
			while (lcontext.Lexer.Current.Type == TokenType.SemiColon)
				lcontext.Lexer.Next();
		}
		
		private void Synchronize(ScriptLoadingContext lcontext)
		{
			while (lcontext.Lexer.PeekNext().Type != TokenType.Eof)
			{
				lcontext.Lexer.Next();
				Token tkn = lcontext.Lexer.Current;

				switch (tkn.Type)
				{
					case TokenType.ChunkAnnotation:
					case TokenType.Local:
					case TokenType.Until:
					case TokenType.Break:
					case TokenType.Continue:
					case TokenType.While:
					case TokenType.For:
					case TokenType.ElseIf:
					case TokenType.Else:
					case TokenType.Function:
					case TokenType.Goto:
					case TokenType.Directive:
					case TokenType.Do:
					case TokenType.If:
					{
						goto endSynchronize;
					}
				}
			}

			endSynchronize: ;
		}


		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			if (lcontext.Syntax == ScriptSyntax.Wattle)
			{
				//Perform declaration hoisting.
				//Define all locals upfront, then bring function definitions up
				List<Statement> reordered = new List<Statement>(m_Statements.Count);
				foreach (var s in m_Statements)
				{
					if (s is AssignmentStatement a)
					{
						a.DefineLocals(lcontext);
						reordered.Add(a);
					}
					else if (s is FunctionDefinitionStatement fd)
					{
						fd.DefineLocals(lcontext);
						if(fd.CanHoist)
							reordered.Insert(0, fd);
						else
							reordered.Add(fd);
					}
					else
					{
						reordered.Add(s);
					}
				}
				m_Statements = reordered;
				foreach(var s in m_Statements) s.ResolveScope(lcontext);

			}
			else
			{
				foreach (var s in m_Statements)
				{
					if(s is AssignmentStatement a) a.DefineLocals(lcontext);
					if(s is FunctionDefinitionStatement fd) fd.DefineLocals(lcontext);
					s.ResolveScope(lcontext);
				}

			}
			
		}


		public override void Compile(Execution.VM.FunctionBuilder bc)
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
