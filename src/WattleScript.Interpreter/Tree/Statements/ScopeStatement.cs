using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Statements
{
    class ScopeStatement : Statement, IBlockStatement
    {
        Statement m_Block;
        RuntimeScopeBlock m_StackFrame;
        SourceRef m_Start, m_End;

        public SourceRef End => m_End;

        public ScopeStatement(ScriptLoadingContext lcontext)
            : base(lcontext)
        {
            m_Start = CheckTokenType(lcontext, TokenType.Brk_Open_Curly).GetSourceRef();
            m_Block = new CompositeStatement(lcontext, BlockEndType.CloseCurly);
            m_End = CheckTokenType(lcontext, TokenType.Brk_Close_Curly).GetSourceRef();
        }
        
        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            lcontext.Scope.PushBlock();
            m_Block.ResolveScope(lcontext);
            m_StackFrame = lcontext.Scope.PopBlock();
        }

        public override void Compile(FunctionBuilder bc)
        {
            using(bc.EnterSource(m_Start))
                bc.Emit_Enter(m_StackFrame);

            m_Block.Compile(bc);

            using (bc.EnterSource(m_End))
                bc.Emit_Leave(m_StackFrame);
        }
    }
}