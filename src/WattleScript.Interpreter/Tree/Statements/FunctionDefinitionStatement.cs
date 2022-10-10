using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;

using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
{
	class FunctionDefinitionStatement : Statement
	{
		internal class FunctionParamRef
		{
			public string Name { get; set; }
			public Expression DefaultValue { get; set; }
			
			public bool IsThis { get; set; }

			public FunctionParamRef(string name)
			{
				Name = name;
			}

			public FunctionParamRef(string name, Expression defaultValue)
			{
				Name = name;
				DefaultValue = defaultValue;
			}
		}
	
		internal class FunctionRef
		{
			public string Name { get; set; }
			public List<FunctionParamRef> Params { get; set; }
		}
		
		SymbolRef m_FuncSymbol;
		SourceRef m_SourceRef;

		bool m_Local = false;
		bool m_IsMethodCallingConvention = false;
		string m_MethodName = null;

		string m_FriendlyName;
		List<string> m_TableAccessors;
		FunctionDefinitionExpression m_FuncDef;

		private string m_FuncDefName;
		private string m_FuncLookupSymbol;
		
		public bool CanHoist => m_MethodName == null || m_Local;

		public FunctionDefinitionStatement(ScriptLoadingContext lcontext, bool local, Token localToken)
			: base(lcontext)
		{
			// here lexer must be at the 'function' keyword
			Token funcKeyword = CheckTokenType(lcontext, TokenType.Function);
			funcKeyword = localToken ?? funcKeyword; // for debugger purposes
			
			m_Local = local;
			Token name = CheckTokenType(lcontext, TokenType.Name);
			SelfType selfType = SelfType.None;
			SourceRef funcKeywordSourceRef;
			
			if (m_Local)
			{
				m_FuncDefName = name.Text;
				m_FriendlyName = string.Format("{0} (local)", name.Text);
				m_SourceRef = funcKeyword.GetSourceRef(name);
				funcKeywordSourceRef = funcKeyword.GetSourceRef();
			}
			else
			{
				string firstName = name.Text;

				m_SourceRef = funcKeyword.GetSourceRef(name);
				funcKeywordSourceRef = funcKeyword.GetSourceRef();
				m_FuncLookupSymbol = firstName;

				m_FriendlyName = firstName;

				if (lcontext.Lexer.Current.Type != TokenType.Brk_Open_Round)
				{
					m_TableAccessors = new List<string>();

					while (lcontext.Lexer.Current.Type != TokenType.Brk_Open_Round)
					{
						Token separator = lcontext.Lexer.Current;

						if (separator.Type != TokenType.Colon && separator.Type != TokenType.Dot)
							UnexpectedTokenType(separator);
						
						lcontext.Lexer.Next();

						Token field = CheckTokenType(lcontext, TokenType.Name);

						m_FriendlyName += separator.Text + field.Text;
						m_SourceRef = funcKeyword.GetSourceRef(field);

						if (separator.Type == TokenType.Colon)
						{
							m_MethodName = field.Text;
							m_IsMethodCallingConvention = true;
							break;
						}
						else
						{
							m_TableAccessors.Add(field.Text);
						}
					}

					if (m_MethodName == null && m_TableAccessors.Count > 0)
					{
						m_MethodName = m_TableAccessors[m_TableAccessors.Count - 1];
						m_TableAccessors.RemoveAt(m_TableAccessors.Count - 1);
					}
				}
			}

			if (m_IsMethodCallingConvention) selfType = SelfType.Explicit;
			else if (m_MethodName != null && lcontext.Syntax == ScriptSyntax.Wattle)
			{
				selfType = SelfType.Implicit;
			}
			
			m_FuncDef = new FunctionDefinitionExpression(lcontext, selfType, false);
			m_FuncDef.m_Begin = funcKeywordSourceRef;
			lcontext.Source.Refs.Add(m_SourceRef);
		}

		public void DefineLocals(ScriptLoadingContext lcontext)
		{
			if (m_FuncDefName != null)
			{
				m_FuncSymbol = lcontext.Scope.TryDefineLocal(m_FuncDefName, out _);
			}
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			if (m_FuncSymbol == null)
			{
				m_FuncSymbol = lcontext.Scope.Find(m_FuncLookupSymbol);
			}	
			m_FuncDef.ResolveScope(lcontext);
		}

		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			using (bc.EnterSource(m_SourceRef))
			{
				if (m_Local)
				{
					bc.Emit_Literal(DynValue.Nil);
					bc.Emit_Store(m_FuncSymbol, 0, 0);
					m_FuncDef.Compile(bc, () => SetFunction(bc, 2), m_FriendlyName);
				}
				else if (m_MethodName == null)
				{
					m_FuncDef.Compile(bc, () => SetFunction(bc, 1), m_FriendlyName);
				}
				else
				{
					m_FuncDef.Compile(bc, () => SetMethod(bc), m_FriendlyName);
				}
			}
		}
		
		private int SetMethod(Execution.VM.FunctionBuilder bc)
		{
			int cnt = 0;

			cnt += bc.Emit_Load(m_FuncSymbol);

			foreach (string str in m_TableAccessors)
			{
				bc.Emit_Index(str, true);
				cnt += 1;
			}

			bc.Emit_IndexSet(0, 0, m_MethodName, true);

			return 1 + cnt;
		}

		private int SetFunction(Execution.VM.FunctionBuilder bc, int numPop)
		{
			int num = bc.Emit_Store(m_FuncSymbol, 0, 0);
			bc.Emit_Pop(numPop);
			return num + 1;
		}

	}
}
