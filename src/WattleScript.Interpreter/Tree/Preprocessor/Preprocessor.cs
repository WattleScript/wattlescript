using System;
using System.Collections.Generic;
using System.Text;
using WattleScript.Interpreter.DataStructs;

namespace WattleScript.Interpreter.Tree
{
    /*
     * Implementation Note:
     * 
     * We never set IsPrematureStreamTermination to true on exceptions,
     * this is because the preprocessor is not expected to function
     * under a REPL
     */
    public class Preprocessor
    {
        private StringBuilder output;
        private TextCursor cursor;
        private Script script;
        private int sourceIndex;
        
        private Dictionary<string, PreprocessorDefine> defines = new Dictionary<string, PreprocessorDefine>();

        public Preprocessor(Script script, int sourceIndex, string text)
        {
            this.script = script;
            this.sourceIndex = sourceIndex;
            cursor = new TextCursor(text);
            // remove unicode BOM if any
            if (text.Length > 0 && text[0] == 0xFEFF)
                cursor.Input = text.Substring(1);
            output = new StringBuilder();
            
            defines.Add("LANGVER", new PreprocessorDefine("LANGVER", 1.0));
        }

        //preprocessor execution state
        
        private bool outputChars = true;
        private int regionCount = 0;
        private int prevLine = 1;
        private int prevCol = 1;
        struct IfBlockState
        {
            public bool ConditionTriggered;
            public bool ElseTriggered;
            public bool OutputChars;
        }

        private FastStack<IfBlockState> ifs = new FastStack<IfBlockState>(8,255);
        
        
        //Running

        void ProcessIf(PreprocessorExpression expression)
        {
            if (ifs.Count > 0 && !ifs.Peek().OutputChars)
            {
                //Short-circuit if
                ifs.Push(new IfBlockState()
                {
                    ConditionTriggered = true,
                    OutputChars = false
                });
            }
            else
            {
                var block = new IfBlockState();
                block.OutputChars = 
                block.ConditionTriggered =
                outputChars = expression.GetBoolean(defines);
                ifs.Push(block);
            }
        }

        bool ProcessElif(PreprocessorExpression expression)
        {
            if (ifs.Count <= 0) return false;
            ref var block = ref ifs.Peek();
            if (block.ElseTriggered) return false;
            if (block.ConditionTriggered) //already happened, don't exec
            {
                block.OutputChars = outputChars = false;
                return true; //
            }
            block.OutputChars = 
            block.ConditionTriggered = 
            outputChars = expression.GetBoolean(defines);
            return true;
        }

        bool ProcessElse()
        {
            if (ifs.Count <= 0) return false;
            ref var block = ref ifs.Peek();
            if (block.ElseTriggered) return false;
            block.ElseTriggered = true;
            block.OutputChars = !block.ConditionTriggered;
            outputChars = block.OutputChars;
            return true;
        }

        bool ProcessEndIf()
        {
            if (ifs.Count <= 0) return false;
            ifs.Pop();
            if (ifs.Count > 0)
                outputChars = ifs.Peek().OutputChars;
            else
                outputChars = true;
            return true;
        }

        void CheckTokenType(Token t, TokenType type)
        {
            if (t.Type != type)
            {
                //Premature stream termination not possible with preprocessor statements
                var display = t.Type == TokenType.Eof ? "\\n" : t.Text;
                throw new SyntaxErrorException(t, "unexpected symbol near '{0}'", display);
            }
        }
        
        void ProcessDirective()
        {
            //Read up to end of line
            var currentLine = cursor.Line;
            int startCol = cursor.Column;
            var builder = new StringBuilder();
            while (cursor.NotEof() && cursor.Char() != '\n')
            {
                if (cursor.Char() == '/' && 
                    (cursor.PeekNext() == '/' || cursor.PeekNext() == '*')) {
                    //Stop reading directive at comment
                    break;
                }
                builder.Append(cursor.Char());
                cursor.Next();
            }
            //Parse Line
            var lexer = new DirectiveLexer(builder.ToString(), sourceIndex, currentLine, startCol);
            var nameToken = lexer.Current;
            lexer.Next();
            if (nameToken.Type != TokenType.Name && 
                nameToken.Type != TokenType.If &&
                nameToken.Type != TokenType.Else) 
                throw new SyntaxErrorException(nameToken, "unexpected symbol '{0}'", nameToken.Text);
            //
            switch (nameToken.Text.ToLowerInvariant())
            {
                case "define":
                {
                    if (!outputChars) break; //We're not executing now
                    var definedName = lexer.Next();
                    CheckTokenType(nameToken, TokenType.Name);
                    var definedValue = lexer.Next();
                    if (definedValue.Type == TokenType.Op_Equal)
                    {
                        definedValue = lexer.Current;
                        lexer.Next();
                    }
                    //Support values wrapped in () for convenience sake
                    if (definedValue.Type == TokenType.Brk_Open_Round)
                    {
                        definedValue = lexer.Next();
                        if (definedValue.Type != TokenType.Brk_Close_Round)
                        {
                            var close = lexer.Next();
                            CheckTokenType(close, TokenType.Brk_Close_Round);
                        } 
                        else
                        {
                            //() just resolves to a null token value
                            definedValue = lexer.EofToken;
                        }
                    }
                    //
                    switch (definedValue.Type)
                    {
                        case TokenType.Eof:
                            defines[definedName.Text] = new PreprocessorDefine(definedName.Text);
                            break;
                        case TokenType.True:
                            defines[definedName.Text] = new PreprocessorDefine(definedName.Text, true);
                            break;
                        case TokenType.False:
                            defines[definedName.Text] = new PreprocessorDefine(definedName.Text, false);
                            break;
                        case TokenType.Number:
                        case TokenType.Number_Hex:
                        case TokenType.Number_HexFloat:
                            break;
                    }
                    lexer.CheckEndOfLine();
                    //For now do null
                    break;
                }
                case "undef":
                {
                    if (!outputChars) break; //We're not executing now
                    var definedName = lexer.Next();
                    CheckTokenType(definedName, TokenType.Name);
                    defines.Remove(definedName.Text);
                    lexer.CheckEndOfLine();
                    break;
                }
                case "if":
                {
                    var exp = PreprocessorExpression.Create(lexer);
                    lexer.CheckEndOfLine();
                    ProcessIf(exp);
                    break;
                }
                case "else":
                {
                    var tok = lexer.Next();
                    if (tok.Type == TokenType.If) goto case "elif";
                    CheckTokenType(tok, TokenType.Eof);                        
                    if (!ProcessElse())
                    {
                        throw new SyntaxErrorException(nameToken, "unexpected #else");
                    }
                    break;
                }
                case "elif":
                {
                    var exp = PreprocessorExpression.Create(lexer);
                    lexer.CheckEndOfLine();
                    if (!ProcessElif(exp)) {
                        throw new SyntaxErrorException(nameToken, $"unexpected #{nameToken.Text}");
                    }
                    break;
                }
                case "endif":
                    lexer.CheckEndOfLine();
                    if (!ProcessEndIf()) {
                        throw new SyntaxErrorException(nameToken, "unexpected #endif");
                    }
                    break;
                case "line": //Pass through to lexer
                    output.Append("#");
                    output.Append(builder.ToString());
                    break;
                case "region":
                    regionCount++;
                    break;
                case "endregion":
                    if(regionCount <= 0)
                        throw new SyntaxErrorException(nameToken, "unexpected #endregion");
                    lexer.CheckEndOfLine();
                    regionCount--;
                    break;
                case "error":
                {
                    var msg = lexer.Next();
                    var message = string.IsNullOrWhiteSpace(msg.Text) ? "#error" : "#error: " + msg.Text;
                    throw new SyntaxErrorException(nameToken, message);
                }
                default:
                    throw new SyntaxErrorException(nameToken, "unexpected preprocessor directive '{0}'", nameToken.Text);
            }
            //put line in output to keep sourcerefs accurate
            //regardless if we are currently outputting source
            if (cursor.Char() == '\n') output.AppendLine();
            cursor.Next();
        }
        
        
        //Avoid parsing through string literals
        //Only template string are multiline, simple string tokens can't trigger the preprocessor code

        private List<int> templateStringState = new List<int>();

        void PushTemplateString() => templateStringState.Add(0);

        bool InTemplateString() => templateStringState.Count > 0;

        void TemplateStringAddBracket()
        {
            if (InTemplateString()) {
                templateStringState[templateStringState.Count - 1]++;
            }
        }

        bool ReturnToTemplateString()
        {
            var c = templateStringState[templateStringState.Count - 1];
            if (c == 0) return true;
            templateStringState[templateStringState.Count - 1]--;
            return false;
        }

        void PopTemplateString() => templateStringState.RemoveAt(templateStringState.Count - 1);
        
        
        void SkipTemplateString(bool isStart)
        {
            for (char c = isStart ? cursor.Char() : cursor.CharNext(); cursor.NotEof(); c = cursor.CharNext())
            {
                if (outputChars) output.Append(c);
                switch (c)
                {
                    case '\\':
                        cursor.Next();
                        if (outputChars && cursor.NotEof()) output.Append(cursor.Char());
                        break;
                    case '{':
                        cursor.Next();
                        return;
                    case '`':
                        PopTemplateString();
                        cursor.Next();
                        return;
                }
            }
        }

        void SkipSimpleString()
        {
            var sep = cursor.Char();
            if (outputChars) output.Append(sep);
            for (char c = cursor.CharNext(); cursor.NotEof(); c = cursor.CharNext()) {
                if (outputChars) output.Append(c);
                if (c == '\\') {
                    cursor.Next();
                    if (outputChars && cursor.NotEof()) output.Append(cursor.Char());
                }
                //Technically newline in string is a syntax error, but we
                //handle this properly in the lexer
                if (c == sep || c == '\n') {
                    cursor.Next();
                    return;
                }
            }
        }
        
        Token CreateToken(TokenType tokenType, int fromLine, int fromCol, string text = null)
        {
            var ret = new Token(tokenType, sourceIndex, fromLine, fromCol, fromLine, fromCol + text?.Length ?? 0, 0, 0)
            {
                Text = text
            };
            return ret;
        }
        public string Process()
        {
            cursor.SkipWhiteSpace(output, outputChars);
            while (cursor.NotEof())
            {
                switch (cursor.Char())
                {
                    case '#' when cursor.StartOfLine:
                        cursor.Next();
                        ProcessDirective();
                        break;
                    case '}' when InTemplateString():
                        if (outputChars) output.Append(cursor.Char());
                        if (ReturnToTemplateString()) {
                            SkipTemplateString(false);
                        }
                        else {
                            cursor.Next();
                        }
                        break;
                    case '{':
                        TemplateStringAddBracket();
                        if (outputChars) output.Append(cursor.Char());
                        cursor.Next();
                        break;
                    case '`':
                        //Skip over template strings
                        if (outputChars) output.Append('`');
                        cursor.Next();
                        PushTemplateString();
                        SkipTemplateString(true);
                        break;
                    case '\'':
                    case '"':
                        //Avoids triggering comment detection within strings
                        SkipSimpleString();
                        break;
                    case '/':
                        //Don't parse within multiline comments
                        if (cursor.PeekNext() == '*')
                        {
                            cursor.Next();
                            cursor.Next();
                            if (outputChars) output.Append("/*");
                            while (cursor.NotEof() && 
                                   !(cursor.Char() == '*' && cursor.PeekNext() == '/'))
                            {
                                if (outputChars) output.Append(cursor.Char());
                                cursor.Next();
                            }
                            if (cursor.NotEof() && outputChars) output.Append("*/");
                            cursor.Next();
                            cursor.Next();
                        } 
                        else if (outputChars)
                        {
                            output.Append('/');
                            cursor.Next();
                        }
                        break;
                    default:
                        if (outputChars)
                            output.Append(cursor.Char());
                        cursor.Next();
                        break;
                }
                cursor.SkipWhiteSpace(output, outputChars);
            }
            if (ifs.Count > 0)
            {
                throw new SyntaxErrorException(CreateToken(TokenType.Eof, cursor.Line, cursor.Column),
                    "expected #endif, got <eof>");
            }
            if (regionCount > 0)
            {
                throw new SyntaxErrorException(CreateToken(TokenType.Eof, cursor.Line, cursor.Column),
                    "expected #endregion, got <eof>");
            }
            return output.ToString();
        }
    }
}