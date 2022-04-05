﻿using System;
using System.Collections.Generic;
using MoonSharp.Interpreter.DataStructs;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;

using MoonSharp.Interpreter.Tree.Statements;

namespace MoonSharp.Interpreter.Tree.Expressions
{
	class FunctionDefinitionExpression : Expression, IClosureBuilder
	{
		SymbolRef[] m_ParamNames = null;
		Statement m_Statement;
		RuntimeScopeFrame m_StackFrame;
		List<SymbolRef> m_Closure = new List<SymbolRef>();
		bool m_HasVarArgs = false;
		
		int m_ClosureInstruction = -1;
		private ByteCode m_bc = null;
		
		bool m_UsesGlobalEnv;
		SymbolRef m_Env;

		SourceRef m_Begin, m_End;


		public FunctionDefinitionExpression(ScriptLoadingContext lcontext, bool usesGlobalEnv)
			: this(lcontext, false, usesGlobalEnv, false)
		{ }

		public FunctionDefinitionExpression(ScriptLoadingContext lcontext, bool pushSelfParam, bool isLambda)
			: this(lcontext, pushSelfParam, false, isLambda)
		{ }


		private FunctionDefinitionExpression(ScriptLoadingContext lcontext, bool pushSelfParam, bool usesGlobalEnv, bool isLambda)
			: base(lcontext)
		{
			if (m_UsesGlobalEnv = usesGlobalEnv)
				CheckTokenType(lcontext, TokenType.Function);

			// here lexer should be at the '(' or at the '|'
			//Token openRound = CheckTokenType(lcontext, isLambda ? TokenType.Lambda : TokenType.Brk_Open_Round);

			Token openRound;
			List<string> paramnames;
			bool openCurly = false;
			if (isLambda)
			{
				openRound = lcontext.Lexer.Current;
				lcontext.Lexer.Next();
				if (openRound.Type == TokenType.Name)
					paramnames = new List<string>(new[] {openRound.Text});
				else
					paramnames = BuildParamList(lcontext, pushSelfParam, openRound);
			}
			else
			{
				openRound = CheckTokenType(lcontext, TokenType.Brk_Open_Round);
				paramnames = BuildParamList(lcontext, pushSelfParam, openRound);
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

			// create scope
			lcontext.Scope.PushFunction(this, m_HasVarArgs);

			if (m_UsesGlobalEnv)
			{
				m_Env = lcontext.Scope.DefineLocal(WellKnownSymbols.ENV);
			}
			else
			{
				lcontext.Scope.ForceEnvUpValue();
			}

			m_ParamNames = DefineArguments(paramnames, lcontext);

			if(isLambda)
				m_Statement = CreateLambdaBody(lcontext, arrowFunc);
			else
				m_Statement = CreateBody(lcontext, openCurly);

			m_StackFrame = lcontext.Scope.PopFunction();

			lcontext.Source.Refs.Add(m_Begin);
			lcontext.Source.Refs.Add(m_End);

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
				Expression e = Expression.Expr(lcontext);
				Token end = lcontext.Lexer.Current;
				SourceRef sref = start.GetSourceRefUpTo(end);
				Statement s = new ReturnStatement(lcontext, e, sref);
				return s;
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

		private List<string> BuildParamList(ScriptLoadingContext lcontext, bool pushSelfParam, Token openBracketToken)
		{
			TokenType closeToken = openBracketToken.Type == TokenType.Lambda ? TokenType.Lambda : TokenType.Brk_Close_Round;

			List<string> paramnames = new List<string>();

			// method decls with ':' must push an implicit 'self' param
			if (pushSelfParam)
				paramnames.Add(lcontext.Syntax == ScriptSyntax.CLike ? "this" : "self");

			while (lcontext.Lexer.Current.Type != closeToken)
			{
				Token t = lcontext.Lexer.Current;

				if (t.Type == TokenType.Name)
				{
					paramnames.Add(t.Text);
				}
				else if (t.Type == TokenType.VarArgs)
				{
					m_HasVarArgs = true;
					paramnames.Add(WellKnownSymbols.VARARGS);
				}
				else
					UnexpectedTokenType(t);

				lcontext.Lexer.Next();

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

		private SymbolRef[] DefineArguments(List<string> paramnames, ScriptLoadingContext lcontext)
		{
			HashSet<string> names = new HashSet<string>();

			SymbolRef[] ret = new SymbolRef[paramnames.Count];

			for (int i = paramnames.Count - 1; i >= 0; i--)
			{
				if (!names.Add(paramnames[i]))
					paramnames[i] = paramnames[i] + "@" + i.ToString();

				ret[i] = lcontext.Scope.DefineLocal(paramnames[i]);
			}

			return ret;
		}

		public SymbolRef CreateUpvalue(BuildTimeScope scope, SymbolRef symbol)
		{
			for (int i = 0; i < m_Closure.Count; i++)
			{
				if (m_Closure[i].i_Name == symbol.i_Name)
				{
					return SymbolRef.Upvalue(symbol.i_Name, i);
				}
			}

			m_Closure.Add(symbol);

			if (m_ClosureInstruction != -1)
			{
				var i = m_bc.Code[m_ClosureInstruction];
				i.SymbolList = m_Closure.ToArray();
				m_bc.Code[m_ClosureInstruction] = i;
			}

			return SymbolRef.Upvalue(symbol.i_Name, m_Closure.Count - 1);
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			throw new DynamicExpressionException("Dynamic Expressions cannot define new functions.");
		}

		public int CompileBody(ByteCode bc, string friendlyName)
		{
			string funcName = friendlyName ?? ("<" + this.m_Begin.FormatLocation(bc.Script, true) + ">");

			bc.PushSourceRef(m_Begin);

			int I = bc.Emit_Jump(OpCode.Jump, -1);

			int meta = bc.Emit_Meta(funcName, OpCodeMetadataType.FunctionEntrypoint);

			bc.Emit_BeginFn(m_StackFrame);

			bc.LoopTracker.Loops.Push(new LoopBoundary());

			int entryPoint = bc.GetJumpPointForLastInstruction();

			if (m_UsesGlobalEnv)
			{
				bc.Emit_Load(SymbolRef.Upvalue(WellKnownSymbols.ENV, 0));
				bc.Emit_Store(m_Env, 0, 0);
				bc.Emit_Pop();
			}

			if (m_ParamNames.Length > 0)
				bc.Emit_Args(m_ParamNames);

			m_Statement.Compile(bc);

			bc.PopSourceRef();
			bc.PushSourceRef(m_End);

			bc.Emit_Ret(0);

			bc.LoopTracker.Loops.Pop();

			bc.SetNumVal(I, bc.GetJumpPointForNextInstruction());
			bc.SetNumVal(meta, bc.GetJumpPointForLastInstruction() - meta);

			bc.PopSourceRef();

			return entryPoint;
		}

		public int Compile(ByteCode bc, Func<int> afterDecl, string friendlyName)
		{
			using (bc.EnterSource(m_Begin))
			{
				SymbolRef[] symbs = m_Closure
					//.Select((s, idx) => s.CloneLocalAndSetFrame(m_ClosureFrames[idx]))
					.ToArray();

				m_bc = bc;
				m_ClosureInstruction = bc.Emit_Closure(symbs, bc.GetJumpPointForNextInstruction());
				int ops = afterDecl();

				var ins = bc.Code[m_ClosureInstruction];
				ins.NumVal += 2 + ops;
				bc.Code[m_ClosureInstruction] = ins;
			}

			return CompileBody(bc, friendlyName);
		}

		public override bool EvalLiteral(out DynValue dv)
		{
			dv = DynValue.Nil;
			return false;
		}


		public override void Compile(ByteCode bc)
		{
			Compile(bc, () => 0, null);
		}
	}
}
