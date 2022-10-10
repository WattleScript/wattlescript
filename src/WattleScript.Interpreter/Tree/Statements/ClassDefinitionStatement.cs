using System;
using System.Collections.Generic;
using System.Linq;
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
        private SymbolRefExpression mixinLocal;
        private SymbolRefExpression staticThis;
        private string initName;
        
        private SymbolRef classLocalRef;
        private SymbolRef classGlobalRef;
        private SymbolRefExpression mixinRef;
        private SymbolRefExpression varargsRef;
        private SourceRef defSource;
        private string className;
        private string localName;
        private string baseName;
        private Annotation[] annotations;
        private RuntimeScopeBlock classBlock;
        private RuntimeScopeBlock classStaticBlock;
        //
        private GeneratedClosure newClosure;
        private GeneratedClosure initClosure;
        private GeneratedClosure tostringClosure;
        private GeneratedClosure emptyConstructor;
        private bool tostringImpl;
        
        //Class members
        private MemberCollection functions = new MemberCollection();
        private MemberCollection fields = new MemberCollection();
        private FunctionDefinitionExpression constructor;

        private List<string> mixinNames = new List<string>();
        private Dictionary<string, SymbolRefExpression> mixinRefs = new Dictionary<string, SymbolRefExpression>();

        private MemberModifierFlags flags = MemberModifierFlags.None;
        
        public ClassDefinitionStatement(ScriptLoadingContext lcontext) : base(lcontext)
        {
            while (lcontext.Lexer.Current.IsMemberModifier())
            {
                MemberUtilities.AddModifierFlag(ref flags, lcontext.Lexer.Current, WattleMemberType.Class);
                lcontext.Lexer.Next();
            }
            
            lcontext.Lexer.Next();

            var nameToken = CheckTokenType(lcontext, TokenType.Name);
            className = nameToken.Text;
            localName = $"$class:{className}";
            //base class
            if (lcontext.Lexer.Current.Type == TokenType.Colon)
            {
                lcontext.Lexer.Next();
                var baseToken = CheckTokenType(lcontext, TokenType.Name);
                baseName = baseToken.Text;
            }
            //mixins
            if (lcontext.Lexer.Current.Type == TokenType.Name &&
                lcontext.Lexer.Current.Text == "with")
            {
                do
                {
                    lcontext.Lexer.Next();
                    var mName = CheckTokenType(lcontext, TokenType.Name);
                    if (mixinNames.Contains(mName.Text))
                        throw new SyntaxErrorException(mName, "class already uses mixin {0}");
                    mixinNames.Add(mName.Text);
                } while (lcontext.Lexer.Current.Type == TokenType.Comma);
            }
            defSource = nameToken.GetSourceRefUpTo(CheckTokenType(lcontext, TokenType.Brk_Open_Curly));
            annotations = lcontext.FunctionAnnotations.ToArray();
            //new()
            newClosure = new GeneratedClosure($"{className}.new", defSource, FunctionFlags.None, true);
            //__init()
            initClosure = new GeneratedClosure($"{className}.__init", defSource, FunctionFlags.None, false);
            //__tostring()
            tostringClosure = new GeneratedClosure($"{className}.__tostring", defSource, FunctionFlags.TakesSelf, false);
   
            MemberModifierFlags modifierFlags = MemberModifierFlags.None;

            //parse members
            while (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Curly &&
                   lcontext.Lexer.Current.Type != TokenType.Eof)
            {

                ParseAnnotations(lcontext);
                switch (lcontext.Lexer.Current.Type)
                {
                    case TokenType.Comma: //skip extras
                    case TokenType.SemiColon:
                        lcontext.Lexer.Next();
                        break;
                    case TokenType.Public:
                    case TokenType.Static:
                    case TokenType.Private:
                        MemberUtilities.AddModifierFlag(ref modifierFlags, lcontext.Lexer.Current, WattleMemberType.ClassMember);
                        lcontext.Lexer.Next();
                        break;
                    case TokenType.Function:
                    {
                        lcontext.Lexer.Next();
                        var funcName = CheckTokenType(lcontext, TokenType.Name);
                        if (funcName.Text == "tostring") tostringImpl = true;

                        MemberUtilities.CheckReserved(funcName, "class");

                        if (flags.HasFlag(MemberModifierFlags.Static) && !modifierFlags.HasFlag(MemberModifierFlags.Static))
                        {
                            throw new SyntaxErrorException(funcName, "static class '{0}' cannot contain non-static function '{1}'", className, funcName.Text);
                        }

                        functions.Add(funcName,
                            new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false, modifierFlags),
                            modifierFlags, true);
                        modifierFlags = MemberModifierFlags.None;
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
                        if (flags.HasFlag(MemberModifierFlags.Static))
                        {
                            throw new SyntaxErrorException(lcontext.Lexer.Current, "static class '{0}' cannot contain constructor", className);
                        }
                        
                        lcontext.Lexer.Next();
                        constructor = new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false, false, true);
                        modifierFlags = MemberModifierFlags.None;
                        break;
                    }
                    case TokenType.Name:
                    {
                        var T = lcontext.Lexer.Current;
                        lcontext.Lexer.Next();
                        switch (lcontext.Lexer.Current.Type)
                        {
                            case TokenType.Brk_Open_Round:
                                
                                MemberUtilities.CheckReserved(T, "class");

                                if (flags.HasFlag(MemberModifierFlags.Static) && !modifierFlags.HasFlag(MemberModifierFlags.Static))
                                {
                                    throw new SyntaxErrorException(T, "static class '{0}' cannot contain non-static function '{1}'", className, T.Text);
                                }
     
                                functions.Add(T,
                                    new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false, modifierFlags),
                                    modifierFlags, true);
                                break;
                            case TokenType.Op_Assignment:
                                
                                MemberUtilities.CheckReserved(T, "class");

                                if (flags.HasFlag(MemberModifierFlags.Static) && !modifierFlags.HasFlag(MemberModifierFlags.Static))
                                {
                                    throw new SyntaxErrorException(T, "static class '{0}' cannot contain non-static field '{1}'", className, T.Text);
                                }

                                lcontext.Lexer.Next();
                                var exp = Expression.Expr(lcontext, true);
                                fields.Add(T, exp, modifierFlags, false);
                                break;
                            case TokenType.Comma: //no-op
                            case TokenType.SemiColon:
                                break;
                            default:
                                CheckTokenType(lcontext, TokenType.SemiColon); //throws error
                                break;
                        }
                        modifierFlags = MemberModifierFlags.None;
                        break;
                    }
                    default:
                        UnexpectedTokenType(lcontext.Lexer.Current);
                        break;
                }
            }
            
            CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            lcontext.Scope.PushBlock();
            
            //
            if (baseName != null) {
                var baseLocalRef = lcontext.Scope.DefineBaseRef();
                baseLocal = new SymbolRefExpression(lcontext, baseLocalRef);
            }
            else {
                lcontext.Scope.DefineBaseEmpty();
            }
            classLocalRef = lcontext.Scope.DefineLocal(localName);
            classGlobalRef = lcontext.Scope.Find(className);
            classStoreGlobal = new SymbolRefExpression(lcontext, classGlobalRef);
            classStoreLocal = new SymbolRefExpression(lcontext, classLocalRef);
            if (constructor != null) {
                var conName = localName + ".__ctor";
                constructorLocal = new SymbolRefExpression(lcontext, lcontext.Scope.DefineLocal(conName));
                newClosure.AddSymbol(conName);
            }
            else
            {
                emptyConstructor =
                    new GeneratedClosure(localName + ".ctor [BLANK]", defSource, FunctionFlags.None, false);
                emptyConstructor.ResolveScope(lcontext);
            }
            //mixin init array
            if (mixinNames.Count > 0) {
                mixinLocal = new SymbolRefExpression(lcontext, lcontext.Scope.DefineLocal(localName + ".__mixins"));
                initClosure.AddSymbol(localName + ".__mixins");
            }
            //resolve init
            initClosure.AddSymbol(localName);
            initClosure.DefineLocal("table"); //arg 0
            initClosure.DefineLocal("depth"); //arg 1
            initClosure.DefineLocal("this");
            foreach(var fn in fields.Where(x => !x.Flags.HasFlag(MemberModifierFlags.Static)))
                initClosure.AddExpression(fn.Expr);
            if (baseName != null)
            {
                initClosure.AddSymbol("base");
                initClosure.AddSymbol(baseName);
            }
            initClosure.ResolveScope(lcontext, (l2) =>
            {
                foreach (var n in mixinNames) {
                    var exp = new SymbolRefExpression(lcontext, lcontext.Scope.CreateGlobalReference(n));
                    initClosure.AddExpression(exp);
                    mixinRefs[n] = exp;
                }
            });
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
            foreach(var fn in functions.Where(x => !x.Flags.HasFlag(MemberModifierFlags.Static)))
                fn.Expr.ResolveScope(lcontext);
            constructor?.ResolveScope(lcontext);
            //statics
            lcontext.Scope.PushBlock();
            staticThis = new SymbolRefExpression(lcontext, lcontext.Scope.DefineThisArg("this"));
            foreach(var fn in fields.Where(x => x.Flags.HasFlag(MemberModifierFlags.Static)))
                fn.Expr.ResolveScope(lcontext);
            foreach(var fn in functions.Where(x => x.Flags.HasFlag(MemberModifierFlags.Static)))
                fn.Expr.ResolveScope(lcontext);
            classStaticBlock = lcontext.Scope.PopBlock();
            classBlock = lcontext.Scope.PopBlock();
        }

        void CompileInit(FunctionBuilder parent)
        {
            initClosure.Compile(parent, (bc, sym) =>
            {
                bc.Emit_Args(2, false);
                if (mixinNames.Count > 0)
                {
                    var mixinSym = sym[localName + ".__mixins"];
                    mixinSym.Compile(bc);
                    int mixInit = bc.Emit_Jump(OpCode.JtOrPop, -1);
                    sym[localName].Compile(bc);
                    bc.Emit_Copy(0);
                    bc.Emit_Index("__index");
                    bc.Emit_TblInitN(0, 1);
                    mixinSym.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
                    foreach (var n in mixinNames) {
                        //stack:
                        //class
                        //class __index table
                        //mixin init table
                        //mixin ref
                        mixinRefs[n].Compile(bc);
                        bc.Emit_MergeFlags(0, 3);
                        bc.Emit_MixInit(n);
                    }
                    //stack: class, __index, init table
                    bc.Emit_Swap(0, 2);
                    bc.Emit_Pop(2); //remove class and __index, just have init table
                    bc.SetNumVal(mixInit, bc.GetJumpPointForNextInstruction());
                    foreach (var n in mixinNames)
                    {
                        bc.Emit_Copy(0);
                        bc.Emit_Index(n);
                        sym["table"].Compile(bc);
                        bc.Emit_Call(1, "mixin.init");
                        bc.Emit_Pop();
                    }
                    bc.Emit_Pop();
                }
                if (baseName != null)
                {
                    bc.Emit_LoopChk(sym["depth"].Symbol, className);
                    var baseSym = sym["base"];
                    baseSym.Compile(bc);
                    int jp = bc.Emit_Jump(OpCode.JNilChk, -1);
                    int jp2 = bc.Emit_Jump(OpCode.Jump, -1);
                    bc.SetNumVal(jp, bc.GetJumpPointForNextInstruction());
                    //Store to closure
                    sym[baseName].Compile(bc);
                    bc.Emit_BaseChk(baseName);
                    baseSym.ForceWrite = true;
                    baseSym.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
                    //Set __index metatable
                    sym[localName].Compile(bc);
                    bc.Emit_Index("__index");
                    bc.Emit_Swap(0, 1);
                    bc.Emit_SetMetaTab(className);
                    bc.Emit_Pop();
                    //Set Base member, copy private field info + leave on stack
                    baseSym.Compile(bc);
                    sym[localName].Compile(bc);
                    bc.Emit_MergeFlags(1, 0);
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
                sym["this"].CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
                sym[localName].Compile(bc);
                bc.Emit_CopyFlags();
                foreach (var field in fields.Where(x => !x.Flags.HasFlag(MemberModifierFlags.Static)))
                {
                    field.Expr.CompilePossibleLiteral(bc);
                    sym["table"].Compile(bc);
                    bc.Emit_IndexSet(0, 0, field.Name, false, false, true);
                    bc.Emit_Pop();
                }
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
            //mixin names
            if (mixinNames.Count > 0) {
                bc.Emit_Literal(DynValue.Nil);
                mixinLocal.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
                bc.Emit_Pop();
            }
            //class table
            //class name
            bc.Emit_Literal(DynValue.NewString("Name"));
            bc.Emit_Literal(DynValue.NewString(className));
            //build __index table
            bc.Emit_Literal(DynValue.NewString("__index"));
            foreach (var fn in functions.Where(x => !x.Flags.HasFlag(MemberModifierFlags.Static)))
            {
                bc.Emit_Literal(DynValue.NewString(fn.Name));
                ((FunctionDefinitionExpression)fn.Expr).Compile(bc, fn.Name);
            }
            bc.Emit_TblInitN(functions.Count(x => !x.Flags.HasFlag(MemberModifierFlags.Static)) * 2, 1);
            //compile __tostring metamethod
            bc.Emit_Literal(DynValue.NewString("__tostring"));
            CompileToString(bc);
            //compile __init function, closing over class + base
            bc.Emit_Literal(DynValue.NewString("__init"));
            CompileInit(bc);
            initLocal.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            //make ctor function 
            bc.Emit_Literal(DynValue.NewString("__ctor"));
            if (constructor != null) {
                constructor.Compile(bc, className + ".ctor");   
                constructorLocal.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            }
            else {
                emptyConstructor.Compile(bc, (fn, sym) => fn.Emit_Ret(0));
            }
            //make new() function closing over class, ctor and __init
            bc.Emit_Literal(DynValue.NewString("new"));
            CompileNew(bc);
            bc.Emit_TblInitN(12, 1);
            //set metadata and store to local
            foreach(var annot in annotations)
                bc.Emit_Annot(annot);
            // group members by flags
            MemberCollection memberAccumulator = new MemberCollection();
            memberAccumulator.Add(functions);
            memberAccumulator.Add(fields);

            foreach (IGrouping<MemberModifierFlags, WattleMemberInfo> group in memberAccumulator.GroupBy(x => x.Flags))
            {
                int groupCount = 0;
                
                foreach (WattleMemberInfo memberInfo in group)
                {
                    groupCount++;
                    bc.Emit_Literal(DynValue.NewString(memberInfo.Name));
                }
                
                bc.Emit_SetFlags(groupCount, group.Key);
            }

            bc.Emit_TabProps(TableKind.Class, flags, false);
            classStoreLocal.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            //set global to class name
            classStoreGlobal.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            //static block
            bc.Emit_Enter(classStaticBlock);
            staticThis.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            //static functions
            foreach (var fn in functions.Where(x => x.Flags.HasFlag(MemberModifierFlags.Static)))
            {
                ((FunctionDefinitionExpression)fn.Expr).Compile(bc, fn.Name);
                bc.Emit_Load(classGlobalRef);
                bc.Emit_IndexSet(0, 0, fn.Name, isNameIndex: true);
                bc.Emit_Pop();
            }
            //static fields
            foreach (var field in fields.Where(x => x.Flags.HasFlag(MemberModifierFlags.Static)))
            {
                field.Expr.CompilePossibleLiteral(bc);
                bc.Emit_Load(classGlobalRef);
                bc.Emit_IndexSet(0, 0, field.Name, isNameIndex: true);
                bc.Emit_Pop();
            }

            //static block end
            bc.Emit_Leave(classStaticBlock);
            //class block end
            bc.Emit_Pop();
            bc.Emit_Leave(classBlock);

            bc.PopSourceRef();
        }
    }
}