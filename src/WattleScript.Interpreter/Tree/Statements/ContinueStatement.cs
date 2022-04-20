using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;


namespace WattleScript.Interpreter.Tree.Statements
{
    class ContinueStatement : Statement
    {
        SourceRef m_Ref;

        public ContinueStatement(ScriptLoadingContext lcontext)
            : base(lcontext)
        {
            m_Ref = CheckTokenType(lcontext, TokenType.Continue).GetSourceRef();
            lcontext.Source.Refs.Add(m_Ref);
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            //No-op
        }


        public override void Compile(FunctionBuilder bc)
        {
            using (bc.EnterSource(m_Ref))
            {
                if (bc.LoopTracker.Loops.Count == 0)
                    throw new SyntaxErrorException(this.Script, m_Ref, "<break> at line {0} not inside a loop", m_Ref.FromLine);

                ILoop loop = bc.LoopTracker.Loops.Peek();

                if (loop.IsBoundary() || loop.IsSwitch())
                    throw new SyntaxErrorException(this.Script, m_Ref, "<break> at line {0} not inside a loop", m_Ref.FromLine);

                loop.CompileContinue(bc);
            }
        }
    }
}