using System;
using System.Collections.Generic;
using System.Linq;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
{
    class MixinDefinitionStatement : Statement
    {
        private SymbolRefExpression storeValue;
        private string name;
        private GeneratedClosure init;
        private Annotation[] annotations;
        
        private MemberCollection functions = new MemberCollection();
        private MemberCollection fields = new MemberCollection();

        private SourceRef sourceRef;
        private RuntimeScopeBlock scopeBlock;
        
        public MixinDefinitionStatement(ScriptLoadingContext lcontext) : base(lcontext)
        {
            annotations = lcontext.FunctionAnnotations.ToArray();
            lcontext.Lexer.Next();
            var nameToken = CheckTokenType(lcontext, TokenType.Name);
            name = nameToken.Text;
            sourceRef = nameToken.GetSourceRef(CheckTokenType(lcontext, TokenType.Brk_Open_Curly));
            
            MemberModifierFlags modifierFlags = MemberModifierFlags.None;
            //Body
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
                    case TokenType.Private:
                        MemberUtilities.AddModifierFlag(ref modifierFlags, lcontext.Lexer.Current, WattleMemberType.MixinMember);
                        lcontext.Lexer.Next();
                        break;
                    case TokenType.Function:
                    {
                        lcontext.Lexer.Next();
                        var funcName = CheckTokenType(lcontext, TokenType.Name);
                        MemberUtilities.CheckReserved(funcName, "mixin");
                        functions.Add(funcName, new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false), modifierFlags, true);
                        modifierFlags = MemberModifierFlags.None;
                        break;
                    }
                    case TokenType.Local: //var
                        lcontext.Lexer.Next();
                        if (lcontext.Lexer.Current.Type == TokenType.Name)
                            goto case TokenType.Name;
                        else
                            throw new SyntaxErrorException(lcontext.Lexer.Current, "expected name");
                    case TokenType.Name:
                    {
                        var T = lcontext.Lexer.Current;
                        MemberUtilities.CheckReserved(T, "mixin");
                        lcontext.Lexer.Next();
                        switch (lcontext.Lexer.Current.Type)
                        {
                            case TokenType.Brk_Open_Round:
                                functions.Add(T, new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false), modifierFlags, true);
                                break;
                            case TokenType.Op_Assignment:
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
            init = new GeneratedClosure(name + ".init(table)", sourceRef, FunctionFlags.None, false);
            CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
        }

        void CompileInit(FunctionBuilder parent)
        {
            init.Compile(parent, (bc, sym) =>
            {
                bc.Emit_Args(1, false);
                sym["table"].Compile(bc);
                foreach (var field in fields)
                {
                    bc.Emit_Literal(DynValue.NewString(field.Name));
                    field.Expr.CompilePossibleLiteral(bc);
                }
                bc.Emit_TblInitN(fields.Count * 2, 0);
                bc.Emit_Ret(0);
            });
        }
        
        public override void Compile(FunctionBuilder bc)
        {
            bc.PushSourceRef(sourceRef);
            bc.Emit_Enter(scopeBlock);
            bc.Emit_Literal(DynValue.NewString("init"));
            CompileInit(bc);
            bc.Emit_Literal(DynValue.NewString("functions"));
            foreach (var fn in functions)
            {
                bc.Emit_Literal(DynValue.NewString(fn.Name));
                ((FunctionDefinitionExpression) fn.Expr).Compile(bc, fn.Name);
            }
            bc.Emit_TblInitN(functions.Count * 2, 1);
            bc.Emit_TblInitN(4, 1);
            //set metadata and store global
            foreach(var annot in annotations)
                bc.Emit_Annot(annot);
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
            
            bc.Emit_TabProps(TableKind.Mixin, MemberModifierFlags.None, true);
            storeValue.CompileAssignment(bc, Operator.NotAnOperator, 0, 0);
            bc.Emit_Leave(scopeBlock);
            bc.PopSourceRef();
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
            lcontext.Scope.PushBlock();
            lcontext.Scope.DefineBaseEmpty();
            storeValue = new SymbolRefExpression(lcontext, lcontext.Scope.CreateGlobalReference(name));
            init.DefineLocal("table");
            foreach(var f in fields)
                init.AddExpression(f.Expr);
            init.ResolveScope(lcontext);
            foreach(var f in functions)
                f.Expr.ResolveScope(lcontext);
            scopeBlock = lcontext.Scope.PopBlock();
        }
    }
}