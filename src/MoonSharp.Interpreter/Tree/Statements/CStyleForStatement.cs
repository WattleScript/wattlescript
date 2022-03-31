using System.Runtime.Serialization;
using MoonSharp.Interpreter.Debugging;
using MoonSharp.Interpreter.Execution;
using MoonSharp.Interpreter.Execution.VM;
using MoonSharp.Interpreter.Tree.Expressions;

namespace MoonSharp.Interpreter.Tree.Statements
{
    class CStyleForStatement : Statement, IBlockStatement
    {
        private Expression initExpression;
        private Expression condition;
        private Statement modify;
        private IVariable initAssignee;
        private Statement block;
        private bool isDefLocal;
        
        SourceRef refBegin;
        SourceRef refEnd;
        RuntimeScopeBlock stackFrame;

        private RuntimeScopeBlock iteratorFrame;
        public SourceRef End => refEnd;
        
        public CStyleForStatement(ScriptLoadingContext lcontext, Token forTok) : base(lcontext)
        {
            lcontext.Scope.PushBlock();
            //Init
            if (lcontext.Lexer.Current.Type != TokenType.SemiColon)
            {
                if (lcontext.Lexer.Current.Type == TokenType.Local)
                {
                    isDefLocal = true;
                    lcontext.Lexer.Next();
                    var lcl = lcontext.Scope.TryDefineLocal(CheckTokenType(lcontext, TokenType.Name).Text);
                    CheckTokenType(lcontext, TokenType.Op_Assignment);
                    initExpression = Expression.Expr(lcontext);
                    CheckTokenType(lcontext, TokenType.SemiColon);
                    initAssignee = new SymbolRefExpression(lcontext, lcl);
                }
                else
                {
                    var exp = Expression.Expr(lcontext);
                    initAssignee = CheckVar(lcontext, exp);
                    CheckTokenType(lcontext, TokenType.Op_Assignment);
                    initExpression = Expression.Expr(lcontext);
                    CheckTokenType(lcontext, TokenType.SemiColon);
                }
            }
            else {
                lcontext.Lexer.Next();
            }
            //Condition
            if (lcontext.Lexer.Current.Type != TokenType.SemiColon) {
                condition = Expression.Expr(lcontext);
                CheckTokenType(lcontext, TokenType.SemiColon);
            }
            else {
                lcontext.Lexer.Next();
                condition = new LiteralExpression(lcontext, DynValue.NewBoolean(true));
            }
            //Modify
            if (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Round)
            {
                modify = Statement.CreateStatement(lcontext, out _);
                if (modify is IBlockStatement)
                {
                    throw new SyntaxErrorException(forTok, "for conditions cannot contain a block");
                }
            }
            else
            {
                modify = new EmptyStatement(lcontext);
            }

            CheckTokenType(lcontext, TokenType.Brk_Close_Round);
            refBegin = forTok.GetSourceRef(lcontext.Lexer.Current);
            lcontext.Scope.PushBlock();
            if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Curly)
            {
                lcontext.Lexer.Next();
                block = new CompositeStatement(lcontext, BlockEndType.CloseCurly);
                refEnd = CheckTokenType(lcontext, TokenType.Brk_Close_Curly).GetSourceRef();
            }
            else
            {
                refEnd = CheckTokenType(lcontext, TokenType.SemiColon).GetSourceRef();
            }
            stackFrame = lcontext.Scope.PopBlock();
            iteratorFrame = lcontext.Scope.PopBlock();
        }

        public override void Compile(ByteCode bc)
        {
            //TODO: There are scope issues which mean we have to copy
            //the defined local to-from the stack a few times
            //- figure out what on earth is going on there?
            
            Loop L = new Loop()
            {
                Scope = stackFrame
            };
            bc.PushSourceRef(refBegin);

            bc.LoopTracker.Loops.Push(L);
            bc.Emit_Enter(iteratorFrame);
            if (initExpression != null) {
                initExpression.Compile(bc);
                initAssignee.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
                bc.Emit_Pop();
            }
            int start = bc.GetJumpPointForNextInstruction();
            condition.Compile(bc);
            if (isDefLocal) {
                ((Expression) initAssignee).Compile(bc);
                bc.Emit_Swap(0, 1);
            }
            var jumpend = bc.Emit_Jump(OpCode.Jf, -1);
            
            bc.Emit_Enter(stackFrame);
            block.Compile(bc);
            int continuePoint = bc.GetJumpPointForNextInstruction();
            if(isDefLocal) bc.Emit_CloseUp((initAssignee as SymbolRefExpression).Symbol);
            modify.Compile(bc);
            bc.Emit_Leave(stackFrame);
            bc.Emit_Jump(OpCode.Jump, start);
            bc.LoopTracker.Loops.Pop();
            bc.PopSourceRef();
            bc.PushSourceRef(refEnd);

            int exitpoint = bc.GetJumpPointForNextInstruction();
            foreach (int i in L.BreakJumps)
                bc.SetNumVal(i, exitpoint);
            
            foreach (int i in L.ContinueJumps)
                bc.SetNumVal(i, continuePoint);

            bc.SetNumVal(jumpend, exitpoint);

            bc.Emit_Leave(iteratorFrame);
            bc.PopSourceRef();
            
        }
    }
}