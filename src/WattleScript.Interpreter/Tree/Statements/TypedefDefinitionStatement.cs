using System;
using System.Collections.Generic;
using WattleScript.Interpreter.Debugging;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Execution.VM;
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree.Statements
{
    class TypedefDefinitionStatement : Statement
    {
        private string name;
        private SourceRef sourceRef;
        private RuntimeScopeBlock scopeBlock;
        
        private List<(string name, FunctionDefinitionExpression exp)> functions = new List<(string name, FunctionDefinitionExpression exp)>();
        private List<(string name, Expression exp)> fields = new List<(string name, Expression exp)>();
        
        public TypedefDefinitionStatement(ScriptLoadingContext lcontext) : base(lcontext)
        {
            lcontext.Lexer.Next();
            var nameToken = CheckTokenType(lcontext, TokenType.Name);
            name = nameToken.Text;
            sourceRef = nameToken.GetSourceRef(CheckTokenType(lcontext, TokenType.Brk_Open_Curly));

            void ParseFunctionMember(bool hasName)
            {
                lcontext.Lexer.Next();
                string fnName = "[anonymous]";
                
                if (hasName)
                {
                    Token funcName = CheckTokenType(lcontext, TokenType.Name);
                    fnName = funcName.Text;
                }
               
                functions.Add((fnName, new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false, false, false, false)));
            }
            
            // body
            while (lcontext.Lexer.Current.Type != TokenType.Brk_Close_Curly && lcontext.Lexer.Current.Type != TokenType.Eof)
            {
                switch (lcontext.Lexer.Current.Type)
                {
                    case TokenType.Comma: //skip extras
                    case TokenType.SemiColon:
                        lcontext.Lexer.Next();
                        break;
                    case TokenType.Function:
                    {
                         ParseFunctionMember(true);
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
                                functions.Add((T.Text, new FunctionDefinitionExpression(lcontext, SelfType.Implicit, false, false, false, false)));
                                break;
                            case TokenType.Op_Assignment:
                                lcontext.Lexer.Next();
                                var exp = Expression.Expr(lcontext, true);
                                fields.Add((T.Text, exp));
                                break;
                            case TokenType.Comma: //no-op
                            case TokenType.SemiColon:
                                break;
                            case TokenType.Colon:
                            {
                                if (lcontext.Lexer.PeekNext().Type == TokenType.Function)
                                {
                                    lcontext.Lexer.Next();
                                    ParseFunctionMember(false);
                                }
                                else
                                {
                                    AssignmentStatement.ParseType(lcontext);    
                                }
                                
                                break;
                            }
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
           
            CheckTokenType(lcontext, TokenType.Brk_Close_Curly);
        }

        public override void Compile(FunctionBuilder bc)
        {
           
        }

        public override void ResolveScope(ScriptLoadingContext lcontext)
        {
           
        }
    }
}