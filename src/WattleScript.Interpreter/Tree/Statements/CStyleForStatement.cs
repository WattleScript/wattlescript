using System.Runtime.Serialization;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
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

        private string localDefName;
        
        public CStyleForStatement(ScriptLoadingContext lcontext, Token forTok) : base(lcontext)
        {
            //Init
            if (lcontext.Lexer.Current.Type != TokenType.SemiColon)
            {
                if (lcontext.Lexer.Current.Type == TokenType.Local)
                {
                    isDefLocal = true;
                    lcontext.Lexer.Next();
                    localDefName = CheckTokenType(lcontext, TokenType.Name).Text;
                    CheckTokenType(lcontext, TokenType.Op_Assignment);
                    initExpression = Expression.Expr(lcontext);
                    CheckTokenType(lcontext, TokenType.SemiColon);
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
            
            lcontext.Source.Refs.Add(refBegin);
            lcontext.Source.Refs.Add(refEnd);
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            lcontext.Scope.PushBlock();
            if (isDefLocal) {
                var lcl = lcontext.Scope.TryDefineLocal(localDefName, out _);
                initAssignee = new SymbolRefExpression(lcontext, lcl);
            }
            (initAssignee as Expression)?.ResolveScope(lcontext);
            initExpression?.ResolveScope(lcontext);
            condition?.ResolveScope(lcontext);
            modify?.ResolveScope(lcontext);
            lcontext.Scope.PushBlock();
            block.ResolveScope(lcontext);
            stackFrame = lcontext.Scope.PopBlock();
            iteratorFrame = lcontext.Scope.PopBlock();
        }

        public override void Compile(FunctionBuilder bc)
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