﻿using System.Collections.Generic;
using System.Reflection.Emit;
using WattleScript.Interpreter.DataStructs;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using OpCode = WattleScript.Interpreter.Execution.VM.OpCode;

namespace WattleScript.Interpreter.Tree.Expressions
{
	enum CallKind
	{
		Normal,
		ThisCall,
		ImplicitThisCall,
		ImplicitThisSkipNil
	}
	class FunctionCallExpression : Expression
	{
		List<Expression> m_Arguments;
		Expression m_Function;
		string m_Name;
		string m_DebugErr;
		private CallKind m_Kind;
		internal SourceRef SourceRef { get; private set; }

		public FunctionCallExpression(ScriptLoadingContext lcontext, Expression function, Token thisCallName, CallKind kind)
			: base(lcontext)
		{
			Token callToken = thisCallName ?? lcontext.Lexer.Current;

			m_Name = thisCallName != null ? thisCallName.Text : null;
			m_DebugErr = function.GetFriendlyDebugName();
			m_Function = function;
			m_Kind = kind;
			
			switch (lcontext.Lexer.Current.Type)
			{
				case TokenType.Brk_Open_Round:
					Token openBrk = lcontext.Lexer.Current;
					lcontext.Lexer.Next();
					Token t = lcontext.Lexer.Current;
					if (t.Type == TokenType.Brk_Close_Round)
					{
						m_Arguments = new List<Expression>();
						SourceRef = callToken.GetSourceRef(t);
						lcontext.Lexer.Next();
					}
					else
					{
						m_Arguments = ExprList(lcontext);
						SourceRef = callToken.GetSourceRef(CheckMatch(lcontext, openBrk, TokenType.Brk_Close_Round, ")"));
					}
					break;
				case TokenType.String:
				case TokenType.String_Long:
					{
						m_Arguments = new List<Expression>();
						Expression le = new LiteralExpression(lcontext, lcontext.Lexer.Current);
						m_Arguments.Add(le);
						SourceRef = callToken.GetSourceRef(lcontext.Lexer.Current);
					}
					break;
				case TokenType.Brk_Open_Curly:
				case TokenType.Brk_Open_Curly_Shared:
					{
						m_Arguments = new List<Expression>();
						m_Arguments.Add(new TableConstructor(lcontext, lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly_Shared));
						SourceRef = callToken.GetSourceRefUpTo(lcontext.Lexer.Current);
					}
					break;
				default:
					throw new SyntaxErrorException(lcontext.Lexer.Current, "function arguments expected")
					{
						IsPrematureStreamTermination = (lcontext.Lexer.Current.Type == TokenType.Eof)
					};
			}
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			m_Function.ResolveScope(lcontext);
			foreach(var arg in m_Arguments)
				arg.ResolveScope(lcontext);
		}

		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			bc.PushSourceRef(SourceRef);
			int argslen = m_Arguments.Count;

			if (m_Kind == CallKind.ImplicitThisCall && m_Name == null)
			{
				((IndexExpression)m_Function).Compile(bc, true);
				bc.Emit_Swap(0, 1);
				++argslen;
			}
			else
			{
				m_Function.Compile(bc);
			}

			int nilCoalesce = -1;
			if (m_Kind == CallKind.ImplicitThisSkipNil)
			{
				nilCoalesce = bc.Emit_Jump(OpCode.JNilChk, -1);
			}

			if (!string.IsNullOrEmpty(m_Name))
			{
				bc.Emit_Copy(0);
				bc.Emit_Index(m_Name, true);
				bc.Emit_Swap(0, 1);
				++argslen;
			}
			
			for (int i = 0; i < m_Arguments.Count; i++)
				m_Arguments[i].CompilePossibleLiteral(bc);

			switch (m_Kind)
			{
				case CallKind.Normal:
					bc.Emit_Call(argslen, m_DebugErr);
					break;
				case CallKind.ThisCall:
					bc.Emit_ThisCall(argslen, m_DebugErr);
					break;
				case CallKind.ImplicitThisCall: 
				case CallKind.ImplicitThisSkipNil:
					bc.Emit_ThisCall(-argslen, m_DebugErr);
					break;
			}

			if (nilCoalesce != -1) {
				bc.SetNumVal(nilCoalesce, bc.GetJumpPointForNextInstruction());
			}
			if (bc.NilChainTargets.Count > 0) {
				bc.SetNumVal(bc.NilChainTargets.Pop(), bc.GetJumpPointForNextInstruction());
			}
			bc.PopSourceRef();
		}

		public override bool EvalLiteral(out DynValue dv)
		{
			dv = DynValue.Nil;
			return false;
		}

		public override DynValue Eval(ScriptExecutionContext context)
		{
			throw new DynamicExpressionException("Dynamic Expressions cannot call functions.");
		}

	}
}
