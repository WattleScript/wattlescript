using System;
using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
{
    class ClassDefinitionStatement : Statement
    {
        //Class construction related
        private SymbolRefExpression classStoreGlobal;
        private SymbolRefExpression classStoreLocal;
        private SymbolRefExpression baseLocal;
        private SymbolRefExpression constructorLocal;
        private SymbolRefExpression initLocal;
        private string initName;
        
        private SymbolRef classLocalRef;
        private SymbolRefExpression varargsRef;
        private SourceRef defSource;
        private string className;
        private string localName;
        private string baseName;
        private Annotation[] annotations;
        private RuntimeScopeBlock classBlock;
        //
        private GeneratedClosure newClosure;
        private GeneratedClosure initClosure;
        private GeneratedClosure tostringClosure;
        private bool tostringImpl;

        //Class members
        private List<(string name, FunctionDefinitionExpression exp)> functions =
            new List<(string name, FunctionDefinitionExpression exp)>();
        private List<(string name, Expression exp)> fields 
            = new List<(string name, Expression exp)>();
        private FunctionDefinitionExpression constructor;
        
        public ClassDefinitionStatement(ScriptLoadingContext lcontext) : base(lcontext)
        {
            lcontext.Lexer.Next();
            var nameToken = CheckTokenType(lcontext, TokenType.Name);
            className = nameToken.Text;
            localName = $"$class:{className}";
            if (lcontext.Lexer.Current.Type == TokenType.Colon)
            {
                lcontext.Lexer.Next();
                var baseToken = CheckTokenType(lcontext, TokenType.Name);
                baseName = baseToken.Text;
            }
            defSource = nameToken.GetSourceRefUpTo(CheckTokenType(lcontext, TokenType.Brk_Open_Curly));
            annotations = lcontext.FunctionAnnotations.ToArray();
            //new()
            newClosure = new GeneratedClosure($"{className}.new", defSource, FunctionFlags.None, true);
            //__init()
            initClosure = new GeneratedClosure($"{className}.__init", defSource, FunctionFlags.None, false);
            //__tostring()
            tostringClosure = new GeneratedClosure($"{className}.__tostring", defSource, FunctionFlags.TakesSelf, false);
            //parse members
            while (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Curly &&
                   lcontext.Lexer.Current.Type != TokenType.Eof)
            {
                switch (lcontext.Lexer.Current.Type)
                {
                    case TokenType.Comma: //skip extras
                    case TokenType.SemiColon:
                        lcontext.Lexer.Next();
                        break;
                    case TokenType.Function:
                    {
                        lcontext.Lexer.Next();
                        var funcName = CheckTokenType(lcontext, TokenType.Name);
                        if (funcName.Text == "tostring") tostringImpl = true;
                        functions.Add((funcName.Text, new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false)));
                        break;
                    }
                    case TokenType.Local: //var
                        lcontext.Lexer.Next();
                        if (lcontext.Lexer.Current.Type == TokenType.Name &&
                            lcontext.Lexer.Current.Text != className)
                            goto case TokenType.Name;
                        else
                            throw new SyntaxErrorException(lcontext.Lexer.Current, "expected name");
                    case TokenType.Name when lcontext.Lexer.Current.Text == className:
                    {
                        lcontext.Lexer.Next();
                        constructor = new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false);
                        break;
                    }
                    case TokenType.Name:
                    {
                        var T = lcontext.Lexer.Current;
                        lcontext.Lexer.Next();
                        switch (lcontext.Lexer.Current.Type)
                        {
                            case TokenType.Brk_Open_Round:
                                functions.Add((T.Text, new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false)));
                                break;
                            case TokenType.Op_Assignment:
                                lcontext.Lexer.Next();
                                var exp = Expression.Expr(lcontext, true);
                                initClosure.AddExpression(exp);
                                fields.Add((T.Text, exp));
                                break;
                            case TokenType.Comma: //no-op
                            case TokenType.SemiColon:
                                break;
                            default:
                                CheckTokenType(lcontext, TokenType.SemiColon); //throws error
                                break;
                        }
                        break;
                    }
                }
            }
            
            CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            lcontext.Scope.PushBlock();
            //
            if (baseName != null) {
                var baseLocalRef = lcontext.Scope.DefineLocal(localName + ".Base");
                baseLocal = new SymbolRefExpression(lcontext, baseLocalRef);
            }
            classLocalRef = lcontext.Scope.DefineLocal(localName);
            classStoreGlobal = new SymbolRefExpression(lcontext, lcontext.Scope.Find(className));
            classStoreLocal = new SymbolRefExpression(lcontext, classLocalRef);
            if (constructor != null) {
                var conName = localName + ".__ctor";
                constructorLocal = new SymbolRefExpression(lcontext, lcontext.Scope.DefineLocal(conName));
                newClosure.AddSymbol(conName);
            }
            //resolve init
            initClosure.DefineLocal("table"); //arg 0
            initClosure.DefineLocal("depth"); //arg 1
            if (baseName != null) {
                initClosure.AddSymbol(localName);
                initClosure.AddSymbol(localName + ".Base");
                initClosure.AddSymbol(baseName);
            }
            initClosure.ResolveScope(lcontext);
            initLocal = new SymbolRefExpression(lcontext, lcontext.Scope.DefineLocal(localName + ".__init"));
            //resolve new
            newClosure.DefineLocal(WellKnownSymbols.VARARGS); //arg 0
            newClosure.AddSymbol(localName);
            newClosure.AddSymbol(localName + ".__init");
            newClosure.ResolveScope(lcontext);
            //resolve tostring
            tostringClosure.DefineLocal("this");
            tostringClosure.ResolveScope(lcontext);
            //functions
            foreach(var fn in functions)
                fn.exp.ResolveScope(lcontext);
            constructor?.ResolveScope(lcontext);
            classBlock = lcontext.Scope.PopBlock();
        }

        void CompileInit(FunctionBuilder parent)
        {
            initClosure.Compile(parent, (bc, sym) =>
            {
                bc.Emit_Args(2, false);
                if (baseName != null)
                {
                    bc.Emit_LoopChk(sym["depth"].Symbol, className);
                    var baseSym = sym[localName + ".Base"];
                    baseSym.Compile(bc);
                    int jp = bc.Emit_Jump(OpCode.JNilChk, -1);
                    int jp2 = bc.Emit_Jump(OpCode.Jump, -1);
                    bc.SetNumVal(jp, bc.GetJumpPointForNextInstruction());
                    //Store to closure
                    sym[baseName].Compile(bc);
                    bc.Emit_BaseChk(baseName);
                    baseSym.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
                    //Set __index metatable
                    sym[localName].Compile(bc);
                    bc.Emit_Index("__index");
                    bc.Emit_Swap(0, 1);
                    bc.Emit_SetMetaTab();
                    bc.Emit_Pop();
                    //Set Base member + leave on stack
                    baseSym.Compile(bc);
                    sym[localName].Compile(bc);
                    bc.Emit_IndexSet(0, 0, "Base");
                    //Base resolved, call __init
                    bc.SetNumVal(jp2, bc.GetJumpPointForNextInstruction());
                    bc.Emit_Index("__init");
                    sym["depth"].Compile(bc);
                    bc.Emit_Literal(DynValue.NewNumber(1));
                    bc.Emit_Operator(OpCode.Add);
                    sym["table"].Compile(bc);
                    bc.Emit_Call(2, "base.__init(table)");
                    bc.Emit_Pop();
                }
                sym["table"].Compile(bc);
                foreach (var field in fields)
                {
                    bc.Emit_Literal(DynValue.NewString(field.name));
                    field.exp.CompilePossibleLiteral(bc);
                }
                bc.Emit_TblInitN(fields.Count * 2, 0);
                bc.Emit_Pop();
                bc.Emit_Ret(0);
            });
        }

        void CompileToString(FunctionBuilder parent)
        {
            tostringClosure.Compile(parent, (bc, sym) =>
            {
                bc.Emit_Args(1, false);
                sym["this"].Compile(bc);
                bc.Emit_Copy(0);
                bc.Emit_Index("tostring");
                //tostring may still be present in our base class, only optimise out check
                //if we know for sure it's implemented
                int nilCheck = -1;
                if (!tostringImpl) {
                    nilCheck = bc.Emit_Jump(OpCode.JNilChk, -1);
                }
                bc.Emit_Swap(0, 1);
                bc.Emit_ThisCall(-1, "tostring");
                bc.Emit_Ret(1);
                if (!tostringImpl) {
                    bc.SetNumVal(nilCheck, bc.GetJumpPointForNextInstruction());
                    bc.Emit_Literal(DynValue.NewString($"< {className} >"));
                    bc.Emit_Ret(1);
                }
            });
        }
        
        void CompileNew(FunctionBuilder parent)
        {
            newClosure.Compile(parent, (bc, sym) =>
            {
                bc.Emit_Args(1, true);
                bc.Emit_TblInitN(0, 1);
                sym[localName].Compile(bc);
                bc.Emit_SetMetaTab();
                sym[localName + ".__init"].Compile(bc);
                bc.Emit_Literal(DynValue.NewNumber(1));
                bc.Emit_Copy(2);
                bc.Emit_Call(2, "__init");
                bc.Emit_Pop();
                if (constructor != null)
                {
                    sym[localName + ".__ctor"].Compile(bc);
                    bc.Emit_Copy(1);
                    sym[WellKnownSymbols.VARARGS].Compile(bc);
                    bc.Emit_ThisCall(-2, ".ctor");
                    bc.Emit_Pop();
                }
                bc.Emit_Ret(1);
            });
        }
        
        public override void Compile(FunctionBuilder bc)
        {
            bc.PushSourceRef(defSource);
            bc.Emit_Enter(classBlock);
            //class name
            bc.Emit_Literal(DynValue.NewString("Name"));
            bc.Emit_Literal(DynValue.NewString(className));
            //build __index table
            bc.Emit_Literal(DynValue.NewString("__index"));
            foreach (var fn in functions)
            {
                bc.Emit_Literal(DynValue.NewString(fn.name));
                fn.exp.Compile(bc, () => 0, fn.name);
            }
            bc.Emit_TblInitN(functions.Count * 2, 1);
            //compile __tostring metamethod
            bc.Emit_Literal(DynValue.NewString("__tostring"));
            CompileToString(bc);
            //compile __init function, closing over class + base
            bc.Emit_Literal(DynValue.NewString("__init"));
            CompileInit(bc);
            initLocal.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            //make ctor function 
            if (constructor != null) {
                bc.Emit_Literal(DynValue.NewString("__ctor"));
                constructor.Compile(bc, () => 0, className + ".ctor");   
                constructorLocal.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            }
            //make new() function closing over class, ctor and __init
            bc.Emit_Literal(DynValue.NewString("new"));
            CompileNew(bc);
            bc.Emit_TblInitN(constructor != null ? 12 : 10, 1);
            //set metadata and store to local
            foreach(var annot in annotations)
                bc.Emit_Annot(annot);
            bc.Emit_TabProps(TableKind.Class, false);
            classStoreLocal.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            //set global to class name
            classStoreGlobal.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            bc.Emit_Pop();
            bc.Emit_Leave(classBlock);
            bc.PopSourceRef();
        }
    }
}