using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Tree.Fast_Interface;
using static WattleScript.Interpreter.Tree.NodeBase;

namespace WattleScript.Interpreter.Tree
{
    internal class Using
    {
        private StringBuilder output;
        private Script script;
        private int sourceIndex;
        private bool outputChars = true;
        private ScriptLoadingContext lcontext;
        private bool firstUsingEncountered = false;
        private bool anyNonUsingEncounterd = false;
        private string text;
        private StringBuilder usingIdent = new StringBuilder();
        
        //public string ProcessedSource => output.ToString();
        public Dictionary<string, Module> ResolvedUsings = new Dictionary<string, Module>();


        public Using(Script script, int sourceIndex, string text, Dictionary<string, DefineNode> defines = null)
        {
            this.script = script;
            this.sourceIndex = sourceIndex;
            this.text = text;
            
            output = new StringBuilder();
            lcontext = Loader_Fast.CreateLoadingContext(script, script.GetSourceCode(sourceIndex), text, defines, false, true);
        }

        void PushToOutput(string str)
        {
            output.Append(str);
        }
        
        int ProcessUsingStatement()
        {
            CheckTokenType(lcontext, TokenType.Using);
            int currentLineFrom = lcontext.Lexer.Current.FromLine;
            bool canBeDot = false;
            int charTo = 0;
            Token prev = null;
            usingIdent.Clear();

            while (lcontext.Lexer.PeekNext().Type != TokenType.Eof)
            {
                Token tkn = lcontext.Lexer.Current;

                if (tkn.FromLine != currentLineFrom)
                {
                    break;
                }

                prev = lcontext.Lexer.Current;

                canBeDot = canBeDot switch
                {
                    false when tkn.Type != TokenType.Name => throw new SyntaxErrorException(tkn, $"unexpected token '{tkn.Text}' found in using statement"),
                    true when tkn.Type != TokenType.Dot => throw new SyntaxErrorException(tkn, $"unexpected token '{tkn.Text}' found in using statement"),
                    _ => !canBeDot
                };

                usingIdent.Append(tkn.Text);
                lcontext.Lexer.Next();
            }

            string usingIdentStr = usingIdent.ToString();

            if (ResolvedUsings.ContainsKey(usingIdentStr))
            {
                throw new SyntaxErrorException(prev, $"duplicate using '{usingIdentStr}' found");
            }

            ResolvedUsings.Add(usingIdentStr, script.UsingHandler(usingIdentStr));
            
            return prev?.CharIndexTo ?? 0;
        }
        
        public void Process()
        {
            if (!text.Contains("using")) // heuristic, will trip on comments, variable names, etc. but might be worth it
            {
               // PushToOutput(text);
               // return;
            }
            
            int previousCharTo = 0;
            
            
            while (lcontext.Lexer.PeekNext().Type != TokenType.Eof)
            {
                lcontext.Lexer.Next();
                afterUsingStatement:
                Token tkn = lcontext.Lexer.Current;
                
                
                switch (tkn.Type)
                {
                    case TokenType.Using:
                        firstUsingEncountered = true;
                        previousCharTo = ProcessUsingStatement();
                        goto afterUsingStatement;
                    default:
                        anyNonUsingEncounterd = true;
                        PushToOutput(text.Substring(previousCharTo, tkn.CharIndexTo - previousCharTo));
                        break;
                }

                previousCharTo = tkn.CharIndexTo;
            }

            string str = output.ToString();
            int z = 0;
        }
    }
}