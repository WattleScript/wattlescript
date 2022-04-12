using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Statements
{
	class GotoStatement : Statement
	{
		internal SourceRef SourceRef { get; private set; }
		internal Token GotoToken { get; private set; }
		public string Label { get; private set; }

		internal int DefinedVarsCount { get; private set; }
		internal string LastDefinedVarName { get; private set; }

		private int m_Jump = -1;
		private ByteCode m_bc;
		int m_LabelAddress = -1;

		public GotoStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			GotoToken = CheckTokenType(lcontext, TokenType.Goto);
			Token name = CheckTokenType(lcontext, TokenType.Name);

			SourceRef = GotoToken.GetSourceRef(name);

			Label = name.Text;
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			lcontext.Scope.RegisterGoto(this);
		}

		public override void Compile(ByteCode bc)
		{
			m_Jump = bc.Emit_Jump(OpCode.Jump, m_LabelAddress);
			m_bc = bc;
		}

		internal void SetDefinedVars(int definedVarsCount, string lastDefinedVarsName)
		{
			DefinedVarsCount = definedVarsCount;
			LastDefinedVarName = lastDefinedVarsName;
		}


		internal void SetAddress(int labelAddress)
		{
			m_LabelAddress = labelAddress;

			if (m_Jump != -1)
				m_bc.SetNumVal(m_Jump, labelAddress);
		}

	}
}
