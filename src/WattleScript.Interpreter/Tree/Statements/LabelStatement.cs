using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;

namespace WattleScript.Interpreter.Tree.Statements
{
	class LabelStatement : Statement
	{
		public string Label { get; private set; }
		public int Address { get; private set; }
		public SourceRef SourceRef { get; private set; }
		public Token NameToken { get; private set; }

		internal int DefinedVarsCount { get; private set; }
		internal string LastDefinedVarName { get; private set; }

		List<GotoStatement> m_Gotos = new List<GotoStatement>();
		RuntimeScopeBlock m_StackFrame;


		public LabelStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			if (lcontext.Syntax == ScriptSyntax.Wattle)
			{
				NameToken = CheckTokenType(lcontext, TokenType.Name);
				CheckTokenType(lcontext, TokenType.Colon);
			}
			else
			{
				CheckTokenType(lcontext, TokenType.DoubleColon);
				NameToken = CheckTokenType(lcontext, TokenType.Name);
				CheckTokenType(lcontext, TokenType.DoubleColon);
			}

			SourceRef = NameToken.GetSourceRef();
			Label = NameToken.Text;
		}

		public LabelStatement(ScriptLoadingContext lcontext, string label) : base(lcontext)
		{
			Label = label;
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			lcontext.Scope.DefineLabel(this);
		}

		internal void SetDefinedVars(int definedVarsCount, string lastDefinedVarsName)
		{
			DefinedVarsCount = definedVarsCount;
			LastDefinedVarName = lastDefinedVarsName;
		}

		internal void RegisterGoto(GotoStatement gotostat)
		{
			m_Gotos.Add(gotostat);
		}


		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			bc.Emit_Clean(m_StackFrame);

			Address = bc.GetJumpPointForLastInstruction();

			foreach (var gotostat in m_Gotos)
				gotostat.SetAddress(this.Address);
		}

		internal void SetScope(RuntimeScopeBlock runtimeScopeBlock)
		{
			m_StackFrame = runtimeScopeBlock;
		}
	}
}

