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
			m_Env = lcontext.Scope.DefineLocal(WellKnownSymbols.ENV);
			m_VarArgs = lcontext.Scope.DefineLocal(WellKnownSymbols.VARARGS);

			m_Block.ResolveScope(lcontext);
			
			m_StackFrame = lcontext.Scope.PopFunction();
		}


		public override void Compile(Execution.VM.ByteCode bc)
		{
			int meta = bc.Emit_Meta("<chunk-root>", OpCodeMetadataType.ChunkEntrypoint);
			if (annotations.Length != 0) {
				bc.Emit_Annot(annotations);
			}
			bc.Emit_BeginFn(m_StackFrame);
			bc.Emit_Args(m_VarArgs);

			bc.Emit_Load(SymbolRef.Upvalue(WellKnownSymbols.ENV, 0));
			bc.Emit_Store(m_Env, 0, 0);
			bc.Emit_Pop();

			m_Block.Compile(bc);
			bc.Emit_Ret(0);

			var ins = bc.Code[meta];
			ins.NumVal = bc.GetJumpPointForLastInstruction() - meta;
			bc.Code[meta] = ins;
		}

		public SymbolRef CreateUpvalue(BuildTimeScope scope, SymbolRef symbol)
		{
			return null;
		}
	}
}
