using System;
using System.Collections.Generic;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

using WattleScript.Interpreter.Tree.Statements;

namespace WattleScript.Interpreter.Tree.Expressions
{
	enum SelfType
	{
		None,
		Explicit,
		Implicit
	}
	class FunctionDefinitionExpression : Expression, IClosureBuilder
	{
		SymbolRef[] m_ParamNames = null;
		Statement m_Statement;
		RuntimeScopeFrame m_StackFrame;
		List<SymbolRef> m_Closure = new List<SymbolRef>();
		private Annotation[] m_Annotations;
		bool m_HasVarArgs = false;
		bool m_ImplicitThis = false;
		
		private FunctionBuilder m_bc = null;
		
		bool m_UsesGlobalEnv;
		SymbolRef m_Env;

		internal SourceRef m_Begin, m_End;
		internal MemberModifierFlags flags;
		private ScriptLoadingContext lcontext;
		List<FunctionDefinitionStatement.FunctionParamRef> paramnames;
		private bool m_IsConstructor;

		public FunctionDefinitionExpression(ScriptLoadingContext lcontext, bool usesGlobalEnv)
			: this(lcontext, SelfType.None, usesGlobalEnv, false)
		{ }

		public FunctionDefinitionExpression(ScriptLoadingContext lcontext, SelfType self, bool isLambda)
			: this(lcontext, self, false, isLambda)
		{ }

		public FunctionDefinitionExpression(ScriptLoadingContext lcontext, SelfType self, bool isLambda, MemberModifierFlags flags)
			: this(lcontext, self, false, isLambda)
		{
			this.flags = flags;
		}
		
		public FunctionDefinitionExpression(ScriptLoadingContext lcontext, SelfType self, bool usesGlobalEnv, bool isLambda, bool isConstructor = false)
			: base(lcontext)
		{
			this.lcontext = lcontext;
			this.m_IsConstructor = isConstructor;
			if (m_UsesGlobalEnv = usesGlobalEnv)
				CheckTokenType(lcontext, TokenType.Function);

			m_Annotations = lcontext.FunctionAnnotations.ToArray();
			lcontext.FunctionAnnotations = new List<Annotation>();
	
			// Parse arguments
			// here lexer should be at the '(' or at the '|'
			//Token openRound = CheckTokenType(lcontext, isLambda ? TokenType.Lambda : TokenType.Brk_Open_Round);

			Token openRound;
			bool openCurly = false;
			if (isLambda)
			{
				openRound = lcontext.Lexer.Current;
				lcontext.Lexer.Next();
				if (openRound.Type == TokenType.Name)
					paramnames = new List<FunctionDefinitionStatement.FunctionParamRef>(new FunctionDefinitionStatement.FunctionParamRef[] {new FunctionDefinitionStatement.FunctionParamRef(openRound.Text)});
				else
					paramnames = BuildParamList(lcontext, self, openRound);
			}
			else
			{
				openRound = CheckTokenType(lcontext, TokenType.Brk_Open_Round);
				paramnames = BuildParamList(lcontext, self, openRound);
				if (lcontext.Syntax != ScriptSyntax.Lua && lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly) {
					openCurly = true;
					lcontext.Lexer.Next();
				}
			}
			
			// skip arrow
			bool arrowFunc = false;
			if (lcontext.Lexer.Current.Type == TokenType.Arrow) {
				arrowFunc = true;
				lcontext.Lexer.Next();
			}
			
			// here lexer is at first token of body

			m_Begin = openRound.GetSourceRefUpTo(lcontext.Lexer.Current);
			
			if(isLambda || arrowFunc)
				m_Statement = CreateLambdaBody(lcontext, arrowFunc);
			else
				m_Statement = CreateBody(lcontext, openCurly);


			lcontext.Source.Refs.Add(m_Begin);
			lcontext.Source.Refs.Add(m_End);

		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			resolved = true;
			lcontext.Scope.PushFunction(this, m_IsConstructor);

			m_ParamNames = DefineArguments(paramnames, lcontext);
			
			if (m_UsesGlobalEnv)
			{
				m_Env = lcontext.Scope.DefineLocal(WellKnownSymbols.ENV);
			}
			else
			{
				lcontext.Scope.ForceEnvUpValue();
			}
			
			if(m_HasVarArgs) lcontext.Scope.SetHasVarArgs(); //Moved here
			
			m_Statement.ResolveScope(lcontext);
			
			m_StackFrame = lcontext.Scope.PopFunction();
		}


		private Statement CreateLambdaBody(ScriptLoadingContext lcontext, bool arrowFunc)
		{
			Token start = lcontext.Lexer.Current;
			if (lcontext.Syntax != ScriptSyntax.Lua && start.Type == TokenType.Brk_Open_Curly)
			{
				lcontext.Lexer.Next();
				return CreateBody(lcontext, true);
			}
			else
			{
				Expression e = Expr(lcontext);
				switch (lcontext.Lexer.Current.Type)
				{
					//Lambda body can be a single-value assignment. Returns nil
					case TokenType.Op_Assignment:
					case TokenType.Op_AddEq:
					case TokenType.Op_SubEq:
					case TokenType.Op_MulEq:
					case TokenType.Op_DivEq:
					case TokenType.Op_ModEq:
					case TokenType.Op_PwrEq:
					case TokenType.Op_ConcatEq:
					case TokenType.Op_NilCoalescingAssignment:
					case TokenType.Op_NilCoalescingAssignmentInverse:
						return new AssignmentStatement(lcontext, e, lcontext.Lexer.Current);
					//Lambda body is an expression.
					default:
						Token end = lcontext.Lexer.Current;
						SourceRef sref = start.GetSourceRefUpTo(end);
						Statement s = new ReturnStatement(lcontext, e, sref);
						return s;
				}
			}
		}


		private Statement CreateBody(ScriptLoadingContext lcontext, bool openCurly)
		{
			Statement s = new CompositeStatement(lcontext, openCurly ? BlockEndType.CloseCurly : BlockEndType.Normal);

			if (openCurly) {
				if(lcontext.Lexer.Current.Type != TokenType.Brk_Close_Curly) {
					throw new SyntaxErrorException(lcontext.Lexer.Current, "'}' expected near '{0}'",
						lcontext.Lexer.Current.Text)
					{
						IsPrematureStreamTermination = (lcontext.Lexer.Current.Type == TokenType.Eof)
					};
				}
			}
			else if (lcontext.Lexer.Current.Type != TokenType.End)
			{
				throw new SyntaxErrorException(lcontext.Lexer.Current, "'end' expected near '{0}'",
					lcontext.Lexer.Current.Text)
				{
					IsPrematureStreamTermination = (lcontext.Lexer.Current.Type == TokenType.Eof)
				};
			}
			m_End = lcontext.Lexer.Current.GetSourceRef();

			lcontext.Lexer.Next();
			return s;
		}

		private List<FunctionDefinitionStatement.FunctionParamRef> BuildParamList(ScriptLoadingContext lcontext, SelfType self, Token openBracketToken)
		{
			TokenType closeToken = openBracketToken.Type == TokenType.Lambda ? TokenType.Lambda : TokenType.Brk_Close_Round;

			List<FunctionDefinitionStatement.FunctionParamRef> paramnames = new List<FunctionDefinitionStatement.FunctionParamRef>();

			// method decls with ':' must push an implicit 'self' param
			if (self != SelfType.None)
				paramnames.Add(new FunctionDefinitionStatement.FunctionParamRef(lcontext.Syntax == ScriptSyntax.Wattle ? "this" : "self") { IsThis = true });
			m_ImplicitThis = self == SelfType.Implicit;
			
			bool parsingDefaultParams = false;
			while (lcontext.Lexer.Current.Type != closeToken)
			{
				Token t = lcontext.Lexer.Current;
				bool nextAfterParamDeclr = true;

				if (t.Type == TokenType.Name)
				{
					string paramName = t.Text;
					
					if (lcontext.Lexer.PeekNext().Type == TokenType.Op_Assignment)
					{
						parsingDefaultParams = true;
						lcontext.Lexer.Next();
						lcontext.Lexer.Next();
						Expression defaultVal = Expr(lcontext);
						nextAfterParamDeclr = false;

						paramnames.Add(new FunctionDefinitionStatement.FunctionParamRef(paramName, defaultVal));
					}
					else
					{
						if (parsingDefaultParams)
						{
							throw new SyntaxErrorException(t, "after first parameter with default value a parameter without default value cannot be declared", t.Text)
							{
								IsPrematureStreamTermination = (t.Type == TokenType.Eof)
							};
						}
						
						paramnames.Add(new FunctionDefinitionStatement.FunctionParamRef(paramName));
					}
				}
				else if (t.Type == TokenType.VarArgs)
				{
					m_HasVarArgs = true;
					paramnames.Add(new FunctionDefinitionStatement.FunctionParamRef(WellKnownSymbols.VARARGS));
				}
				else
					UnexpectedTokenType(t);

				if (nextAfterParamDeclr)
				{
					lcontext.Lexer.Next();	
				}

				t = lcontext.Lexer.Current;

				if (t.Type == TokenType.Comma)
				{
					lcontext.Lexer.Next();
				}
				else
				{
					CheckMatch(lcontext, openBracketToken, closeToken, openBracketToken.Type == TokenType.Lambda ? "|" : ")");
					break;
				}
			}

			if (lcontext.Lexer.Current.Type == closeToken)
				lcontext.Lexer.Next();

			return paramnames;
		}

		private SymbolRef[] DefineArguments(List<FunctionDefinitionStatement.FunctionParamRef> paramnames, ScriptLoadingContext lcontext)
		{
			HashSet<string> names = new HashSet<string>();

			SymbolRef[] ret = new SymbolRef[paramnames.Count];

			for (int i = paramnames.Count - 1; i >= 0; i--)
			{
				if (!names.Add(paramnames[i].Name))
					paramnames[i].Name = paramnames[i].Name + "@" + i.ToString();
				paramnames[i].DefaultValue?.ResolveScope(lcontext);
				if(paramnames[i].IsThis)
					ret[i] = lcontext.Scope.DefineThisArg(paramnames[i].Name);
				else
					ret[i] = lcontext.Scope.DefineLocal(paramnames[i].Name);
			}

			return ret;
		}

		public SymbolRef CreateUpvalue(BuildTimeScope scope, SymbolRef symbol)
		{
			if (compile) throw new Exception("Upvalue after resolve");
			
			for (int i = 0; i < m_Closure.Count; i++)
			{
				if (m_Closure[i].i_Name == symbol.i_Name)
				{
					return SymbolRef.Upvalue(symbol.i_Name, i);
				}
			}

			m_Closure.Add(symbol);

			return SymbolRef.Upvalue(symbol.i_Name, m_Closure.Count - 1);
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			throw new DynamicExpressionException("Dynamic Expressions cannot define new functions.");
		}

		private bool resolved = false;
		private bool compile = false;

		public FunctionProto CompileBody(FunctionBuilder parent, Script script, string friendlyName)
		{
			compile = true;
			if (!resolved) throw new InternalErrorException("Function definition scope not resolved");
			
			string funcName = friendlyName ?? ("<" + this.m_Begin.FormatLocation(script, true) + ">");

			var bc = new FunctionBuilder(script);
			bc.PushSourceRef(m_Begin);

			bc.LoopTracker.Loops.Push(new LoopBoundary());

			int entryPoint = 0;

			if (m_UsesGlobalEnv)
			{
				bc.Emit_Load(SymbolRef.Upvalue(WellKnownSymbols.ENV, 0));
				bc.Emit_Store(m_Env, 0, 0);
				bc.Emit_Pop();
			}

			if (m_ParamNames.Length > 0)
			{
				bc.Emit_Args(m_ParamNames.Length, m_HasVarArgs);

				for (int i = 0; i < m_ParamNames.Length; i++)
				{
					FunctionDefinitionStatement.FunctionParamRef fr = paramnames[i];
					SymbolRef sr = m_ParamNames[i];
					
					if (fr.DefaultValue != null)
					{
						var jp = bc.Emit_JLclInit(sr, -1);
						fr.DefaultValue.CompilePossibleLiteral(bc);
						new SymbolRefExpression(lcontext, sr).CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
						bc.Emit_Pop();
						bc.SetNumVal(jp, bc.GetJumpPointForNextInstruction());		
					}
				}
			}
			
			m_Statement.Compile(bc);

			bc.PopSourceRef();
			bc.PushSourceRef(m_End);

			bc.Emit_Ret(0);

			bc.LoopTracker.Loops.Pop();

			bc.PopSourceRef();

			var proto = bc.GetProto(funcName, m_StackFrame);
			proto.annotations = m_Annotations;
			proto.upvalues = m_Closure.ToArray();
			if (m_ParamNames.Length > 0 && (m_ParamNames[0].i_Name == "self" || m_ParamNames[0].i_Name == "this"))
			{
				proto.flags |= FunctionFlags.TakesSelf;
			}
			if (m_ImplicitThis) proto.flags |= FunctionFlags.ImplicitThis;
			
			if(parent != null) parent.Protos.Add(proto);
			
			return proto;
		}
		
		public void Compile(FunctionBuilder bc, string friendlyName)
		{
			CompileBody(bc, bc.Script, friendlyName);			
			bc.Emit_Closure(bc.Protos.Count - 1);
		}

		public void Compile(FunctionBuilder bc, Func<int> afterDecl, string friendlyName)
		{
			CompileBody(bc, bc.Script, friendlyName);			
			bc.Emit_Closure(bc.Protos.Count - 1);
			afterDecl();
		}

		public override bool EvalLiteral(out DynValue dv, IDictionary<string, DynValue> symbols = null)
		{
			dv = DynValue.Nil;
			return false;
		}


		public override void Compile(FunctionBuilder bc)
		{
			Compile(bc, () => 0, null);
		}
	}
}
