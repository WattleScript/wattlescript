using System;
using System.Collections.Generic;
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
        private List<(string name, FunctionDefinitionExpression exp)> functions =
            new List<(string name, FunctionDefinitionExpression exp)>();

        private GeneratedClosure init;
        private Annotation[] annotations;
        private List<(string name, Expression exp)> fields 
            = new List<(string name, Expression exp)>();

        private SourceRef sourceRef;
        private RuntimeScopeBlock scopeBlock;
        
        public MixinDefinitionStatement(ScriptLoadingContext lcontext) : base(lcontext)
        {
            annotations = lcontext.FunctionAnnotations.ToArray();
            lcontext.Lexer.Next();
            var nameToken = CheckTokenType(lcontext, TokenType.Name);
            name = nameToken.Text;
            sourceRef = nameToken.GetSourceRef(CheckTokenType(lcontext, TokenType.Brk_Open_Curly));
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
                    case TokenType.Function:
                    {
                        lcontext.Lexer.Next();
                        var funcName = CheckTokenType(lcontext, TokenType.Name);
                        functions.Add((funcName.Text, new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false)));
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
                        lcontext.Lexer.Next();
                        switch (lcontext.Lexer.Current.Type)
                        {
                            case TokenType.Brk_Open_Round:
                                functions.Add((T.Text, new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false)));
                                break;
                            case TokenType.Op_Assignment:
                                lcontext.Lexer.Next();
                                var exp = Expression.Expr(lcontext, true);
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
                    bc.Emit_Literal(DynValue.NewString(field.name));
                    field.exp.CompilePossibleLiteral(bc);
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
                bc.Emit_Literal(DynValue.NewString(fn.name));
                fn.exp.Compile(bc, () => 0, fn.name);
            }
            bc.Emit_TblInitN(functions.Count * 2, 1);
            bc.Emit_TblInitN(4, 1);
            //set metadata and store global
            foreach(var annot in annotations)
                bc.Emit_Annot(annot);
            bc.Emit_TabProps(TableKind.Mixin, true);
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
                init.AddExpression(f.exp);
            init.ResolveScope(lcontext);
            foreach(var f in functions)
                f.exp.ResolveScope(lcontext);
            scopeBlock = lcontext.Scope.PopBlock();
        }
    }
}