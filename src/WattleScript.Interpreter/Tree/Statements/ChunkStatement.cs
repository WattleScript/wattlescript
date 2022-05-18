using System;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Statements
{
	class ChunkStatement : Statement, IClosureBuilder
	{
		Statement m_Block;
		RuntimeScopeFrame m_StackFrame;
		SymbolRef m_Env;
		SymbolRef m_VarArgs;
		private Annotation[] annotations;

		public ChunkStatement(ScriptLoadingContext lcontext)
			: base(lcontext)
		{
			m_Block = new CompositeStatement(lcontext, BlockEndType.Normal);

			if (lcontext.Lexer.Current.Type != TokenType.Eof)
				throw new SyntaxErrorException(lcontext.Lexer.Current, "<eof> expected near '{0}'", lcontext.Lexer.Current.Text);
			
			annotations = lcontext.ChunkAnnotations.ToArray();
		}

		public override void ResolveScope(ScriptLoadingContext lcontext)
		{
			lcontext.Scope.PushFunction(this);
			lcontext.Scope.SetHasVarArgs();
			m_VarArgs = lcontext.Scope.DefineLocal(WellKnownSymbols.VARARGS);
			m_Env = lcontext.Scope.DefineLocal(WellKnownSymbols.ENV);

			m_Block.ResolveScope(lcontext);
			
			m_StackFrame = lcontext.Scope.PopFunction();
		}

		public FunctionProto CompileFunction(Script script)
		{
			var bc = new FunctionBuilder(script);
			bc.Emit_Args(1, true);
			bc.Emit_Load(SymbolRef.Upvalue(WellKnownSymbols.ENV, 0));
			bc.Emit_Store(m_Env, 0, 0);
			bc.Emit_Pop();
			m_Block.Compile(bc);
			bc.Emit_Ret(0);
			
			var proto = bc.GetProto("<chunk-root>", m_StackFrame);
			proto.annotations = annotations;
			proto.upvalues = new SymbolRef[] {SymbolRef.Upvalue(WellKnownSymbols.ENV, 0)};
			proto.flags = FunctionFlags.IsChunk;
			return proto;
		}


		public override void Compile(Execution.VM.FunctionBuilder bc)
		{
			throw new InvalidOperationException();
		}

		public SymbolRef CreateUpvalue(BuildTimeScope scope, SymbolRef symbol)
		{
			return null;
		}
	}
}
