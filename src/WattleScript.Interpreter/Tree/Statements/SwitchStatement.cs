using System.Collections.Generic;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Interpreter.Tree.Statements
{
    class SwitchStatement : Statement
    {
        private Expression switchCase;
        private List<CaseBlock> caseBlocks = new List<CaseBlock>();
        private RuntimeScopeBlock stackFrame;
        
        public SwitchStatement(ScriptLoadingContext lcontext) : base(lcontext)
        {
            lcontext.Lexer.Next();
            bool hasRound = false;
            if (lcontext.Lexer.Current.Type == TokenType.Brk_Open_Round) {
                hasRound = true;
                lcontext.Lexer.Next();
            }
            switchCase = Expression.Expr(lcontext);
            if (hasRound) CheckTokenType(lcontext, TokenType.Brk_Close_Round);
            
            HashSet<DynValue> usedCases = new HashSet<DynValue>();
            bool hasDefault = false;
            
            CheckTokenType(lcontext, TokenType.Brk_Open_Curly);
            while (true) {
                if (lcontext.Lexer.Current.Type == TokenType.Case ||
                    (lcontext.Lexer.Current.Type == TokenType.Name &&
                     lcontext.Lexer.Current.Text == "default")) {
                   AddCaseBlock(lcontext, usedCases, ref hasDefault);
                } else if (lcontext.Lexer.Current.Type == TokenType.SemiColon) {
                    lcontext.Lexer.Next();
                }
                else {
                    break;
                }
            }
            CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
        }

        class CaseBlock
        {
            public bool IsDefault;
            public List<DynValue> Cases = new List<DynValue>();
            public Statement Block;
            public int Pointer;
            public int EndJump;
            public List<LabelStatement> Labels = new List<LabelStatement>();

            public void RegisterLabels(ScriptLoadingContext lcontext)
            {
                if(IsDefault)
                    Labels.Add(new LabelStatement(lcontext, "default"));
                foreach (var c in Cases)
                {
                    Labels.Add(new LabelStatement(lcontext, "case " + c.ToDebugPrintString()));
                }
                foreach(var v in Labels)
                    v.ResolveScope(lcontext);
            }
            public void Compile(FunctionBuilder bc, int offset)
            {
                foreach(var l in Labels)
                    l.Compile(bc);
                Pointer = (bc.GetJumpPointForNextInstruction() - offset);
                Block.Compile(bc);
                EndJump = bc.Emit_Jump(OpCode.Jump, -1);
            }
        }
        
        void AddCaseBlock(ScriptLoadingContext lcontext, HashSet<DynValue> usedCases, ref bool hasDefault)
        {
            CaseBlock block = new CaseBlock();
            while (lcontext.Lexer.Current.Type == TokenType.Case ||
                   (lcontext.Lexer.Current.Type == TokenType.Name &&
                    lcontext.Lexer.Current.Text == "default"))
            {
                var T = lcontext.Lexer.Current;
                if (T.Text == "default")
                {
                    if (hasDefault)
                        throw new SyntaxErrorException(lcontext.Lexer.Current, "default case already present");
                    block.IsDefault = true;
                    hasDefault = true;
                    lcontext.Lexer.Next();
                }
                else
                {
                    lcontext.Lexer.Next();
                    var exp = Expression.Expr(lcontext);
                    if (!exp.EvalLiteral(out var value))
                    {
                        throw new SyntaxErrorException(T, "switch case must be constant value");
                    }
                    if (usedCases.Contains(value))
                        throw new SyntaxErrorException(lcontext.Lexer.Current, "switch already contains case");
                    usedCases.Add(value);
                    block.Cases.Add(value);
                }
                CheckTokenType(lcontext, TokenType.Colon);
            }

            block.Block = new CompositeStatement(lcontext, BlockEndType.Switch);
            caseBlocks.Add(block);
        }
        

        public override void Compile(FunctionBuilder bc)
        {
            //Use the existing code for breaking out of loops to
            //break out of switch cases
            Loop L = new Loop()
            {
                Scope = stackFrame,
                Switch = true
            };
            
            CaseBlock defaultCase = null;
            CaseBlock nilCase = null;
            CaseBlock trueCase = null;
            CaseBlock falseCase = null;
            List<(string str, CaseBlock cs)> stringBlocks = new List<(string str, CaseBlock cs)>();
            List<(double n, CaseBlock cs)> numberBlocks = new List<(double n, CaseBlock cs)>();
            foreach (var c in caseBlocks)
            {
                if (c.IsDefault) defaultCase = c;
                if (c.Cases.Contains(DynValue.Nil)) nilCase = c;
                if (c.Cases.Contains(DynValue.True)) trueCase = c;
                if (c.Cases.Contains(DynValue.False)) falseCase = c;
                foreach (var cs in c.Cases) {
                    if(cs.Type == DataType.Number)
                        numberBlocks.Add((cs.Number, c));
                    if(cs.Type == DataType.String)
                        stringBlocks.Add((cs.String, c));
                }
            }
            switchCase.CompilePossibleLiteral(bc);
            int offset = bc.Emit_Switch(nilCase != null, trueCase != null, falseCase != null, (uint)stringBlocks.Count,
                (uint)numberBlocks.Count);
            int table = bc.GetJumpPointForNextInstruction();
            if (nilCase != null) bc.Emit_SwitchTable();
            if (trueCase != null) bc.Emit_SwitchTable();
            if (falseCase != null) bc.Emit_SwitchTable();
            foreach (var x in stringBlocks) {
                bc.Emit_SwitchTable(x.str);
            }
            foreach (var x in numberBlocks) {
                bc.Emit_SwitchTable(x.n);
            }
            bc.LoopTracker.Loops.Push(L);
            //Default Case
            defaultCase?.Compile(bc, offset);
            int defaultFinish = -1;
            if (defaultCase == null) defaultFinish = bc.Emit_Jump(OpCode.Jump, -1);
            //All other blocks
            foreach (var c in caseBlocks)
            {
                if(c != defaultCase) c.Compile(bc, offset);
            }

            bc.LoopTracker.Loops.Pop();
            //Set all jump pointers
            int j = table;
            if(nilCase != null) bc.SetNumValB(j++, (uint)nilCase.Pointer);
            if(trueCase != null) bc.SetNumValB(j++, (uint)trueCase.Pointer);
            if(falseCase != null) bc.SetNumValB(j++, (uint)falseCase.Pointer);
            foreach (var x in stringBlocks) {
                bc.SetNumValB(j++, (uint)x.cs.Pointer);
            }
            foreach (var x in numberBlocks) {
                bc.SetNumValB(j++, (uint)x.cs.Pointer);
            }
            //Set all breaks
            int exitpoint = bc.GetJumpPointForNextInstruction();
            foreach (int i in L.BreakJumps)
                bc.SetNumVal(i, exitpoint);
            foreach(var c in caseBlocks)
                bc.SetNumVal(c.EndJump, exitpoint);
            if(defaultFinish != -1)
                bc.SetNumVal(defaultFinish, exitpoint);
            //Finish
            bc.Emit_Leave(stackFrame);
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            lcontext.Scope.PushBlock();
            switchCase.ResolveScope(lcontext);
            foreach (var c in caseBlocks)
            {
                c.RegisterLabels(lcontext);
                c.Block.ResolveScope(lcontext);
            }

            stackFrame = lcontext.Scope.PopBlock();
        }
    }
}