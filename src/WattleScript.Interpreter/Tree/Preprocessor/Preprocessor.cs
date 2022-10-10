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
    class Preprocessor
    {
        private StringBuilder output;
        private TextCursor cursor;
        private Script script;
        private int sourceIndex;
        
        private Dictionary<string, PreprocessorDefine> defines = new Dictionary<string, PreprocessorDefine>();
        private Dictionary<string, DefineNode> nodes = new Dictionary<string, DefineNode>();
        public Preprocessor(Script script, int sourceIndex, string text)
        {
            this.script = script;
            this.sourceIndex = sourceIndex;
            cursor = new TextCursor(text);
            // remove unicode BOM if any
            if (text.Length > 0 && text[0] == 0xFEFF)
                cursor.Input = text.Substring(1);
            output = new StringBuilder();
            //Read Hashbang line
            if (cursor.Input.Length > 2 && cursor.Input[0] == '#' && cursor.Input[1] == '!') {
                while (cursor.NotEof() && cursor.Char() != '\n')
                {
                    output.Append(cursor.Char());
                    cursor.Next();
                }
            }
            SetDefine("LANGVER", new PreprocessorDefine("LANGVER", Script.VERSION_NUMBER));
            foreach (var def in script.Options.Defines)
            {
                if (string.IsNullOrEmpty(def.Name))
                    throw new ArgumentException("Preprocessor definition name can't be empty");
                if (Token.GetReservedTokenType(def.Name, ScriptSyntax.Wattle) != null)
                    throw new ArgumentException($"Preprocessor definition name can't be keyword '{def.Name}'");
                //overwrite previous entries as we are still at line 1
                SetDefine(def.Name, def, true);
            }
        }

        //preprocessor execution state
        
        private bool outputChars = true;
        private int regionCount = 0;

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
                return true;
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

        bool OptionalToken(DirectiveLexer lex, TokenType type)
        {
            if (lex.Current.Type == type)
            {
                lex.Next();
                return true;
            }
            return false;
        }

        void SetDefine(string name, PreprocessorDefine value, bool overwrite = false)
        {
            if (value == null)
                defines.Remove(name);
            else
                defines[name] = value;
            var newNode = new DefineNode()
            {
                StartLine = cursor.DefaultLine,
                EndLine = int.MaxValue,
                Define = value
            };
            if (!overwrite && nodes.TryGetValue(name, out var n))
            {
                n.EndLine = cursor.DefaultLine;
                n.Next = newNode;
            }
            else
            {
                nodes[name] = newNode;
            }
        }
        
        void ProcessDirective()
        {
            //Skip beginning spaces
            while (cursor.NotEof() && cursor.Char() != '\n' && char.IsWhiteSpace(cursor.Char()))
                cursor.Next();
            //Read up to end of line
            var currentLine = cursor.Line;
            int startCol = cursor.Column;
            var builder = new StringBuilder();
            bool read = true;
            while (cursor.NotEof() && cursor.Char() != '\n' && read)
            {
                switch (cursor.Char())
                {
                    case '\'':
                    case '"':
                    {
                        //Read whole literal so '//' is valid
                        var sep = cursor.Char();
                        builder.Append(sep);
                        for (var c = cursor.CharNext(); cursor.NotEof() && cursor.Char() != '\n'; c = cursor.CharNext())
                        {
                            builder.Append(c);
                            if (c == '\\') {
                                cursor.Next();
                                if (cursor.NotEof()) builder.Append(cursor.Char());
                            }
                            if (c == sep) {
                                cursor.Next();
                                break;
                            }
                        }
                        break;
                    }
                    //Don't include comments in directive
                    case '/' when (cursor.PeekNext() == '/' || cursor.PeekNext() == '*'):
                        read = false;
                        break;
                    default:
                        builder.Append(cursor.Char());
                        cursor.Next();
                        break;
                }
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
                    if(lexer.Current.Type == TokenType.Comma)
                        throw new SyntaxErrorException(nameToken, "unexpected symbol '{0}'", lexer.Current.Text);
                    do
                    {
                        //skip comma
                        on_comma:
                        OptionalToken(lexer, TokenType.Comma);
                        var definedName = lexer.Next();
                        CheckTokenType(nameToken, TokenType.Name);
                        Token definedValue;
                        //#define optionally has =
                        OptionalToken(lexer, TokenType.Op_Assignment);
                        //#define value optionally can be wrapped in ()
                        if (OptionalToken(lexer, TokenType.Brk_Open_Round))
                        {
                            definedValue = lexer.Next();
                            CheckTokenType(lexer.Next(), TokenType.Brk_Close_Round);
                        }
                        else
                        {
                            definedValue = lexer.Next();
                        }
                        
                        switch (definedValue.Type)
                        {
                            case TokenType.Eof: //eof tokens = empty define
                                SetDefine(definedName.Text, new PreprocessorDefine(definedName.Text));
                                break;
                            case TokenType.Comma: //empty - jump to next
                                SetDefine(definedName.Text, new PreprocessorDefine(definedName.Text));
                                goto on_comma; //we've already consumed the comma, check again
                            case TokenType.True:
                                SetDefine(definedName.Text, new PreprocessorDefine(definedName.Text, true));
                                break;
                            case TokenType.False:
                                SetDefine(definedName.Text, new PreprocessorDefine(definedName.Text, false));
                                break;
                            case TokenType.Number:
                            case TokenType.Number_Hex:
                            case TokenType.Number_HexFloat:
                                SetDefine(definedName.Text, new PreprocessorDefine(definedName.Text, definedValue.GetNumberValue()));
                                break;
                            case TokenType.String:
                                SetDefine(definedName.Text, new PreprocessorDefine(definedName.Text, definedValue.Text));
                                break;
                            default:
                                throw new SyntaxErrorException(nameToken, "unexpected symbol '{0}'", definedValue.Text);
                        }
                    } while (lexer.Current.Type == TokenType.Comma);
                    lexer.CheckEndOfLine();
                    //For now do null
                    break;
                }
                case "undef":
                {
                    if (!outputChars) break; //We're not executing now
                    if(lexer.Current.Type == TokenType.Comma)
                        throw new SyntaxErrorException(nameToken, "unexpected symbol '{0}'", lexer.Current.Text);
                    do
                    {
                        OptionalToken(lexer, TokenType.Comma); //skip comma
                        var definedName = lexer.Next();
                        CheckTokenType(definedName, TokenType.Name);
                        SetDefine(definedName.Text, null);
                    } while (lexer.Current.Type == TokenType.Comma);
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
                case "line":
                {
                    //Parse for preprocessor state
                    var line = lexer.Next();
                    if (line.Text == "default")
                    {
                        cursor.Line = cursor.DefaultLine;
                    }
                    else
                    {
                        CheckTokenType(line, TokenType.Number);
                        cursor.Line = (int) (line.GetNumberValue() - 1); //Next line = value
                    }
                    //column offset, not really relevant to preprocessor. check for correctness
                    if (lexer.Current.Type == TokenType.Comma)
                    {
                        lexer.Next();
                        var colOffset = lexer.Next();
                        if (colOffset.Type != TokenType.Number)
                            throw new SyntaxErrorException(colOffset, "unexpected symbol near '{0}'", colOffset.Text);
                    }
                    lexer.CheckEndOfLine();
                    //Pass through to lexer
                    output.Append("#");
                    output.Append(builder);
                    break;
                }
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
                    if (!outputChars) break; //don't execute
                    var msg = lexer.Next();
                    var message = string.IsNullOrWhiteSpace(msg.Text) ? "#error" : "#error: " + msg.Text;
                    throw new SyntaxErrorException(nameToken, message);
                }
                default:
                    throw new SyntaxErrorException(nameToken, "unexpected preprocessor directive '{0}'", nameToken.Text);
            }
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
            return new Token(tokenType, sourceIndex, fromLine, fromCol, fromLine, fromCol + text?.Length ?? 0, 0, 0, cursor.Index - (text?.Length ?? 0), cursor.Index, text);
        }

        public string ProcessedSource => output.ToString();
        public Dictionary<string, DefineNode> Defines => nodes;
        
        public void Process()
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
        }
    }
}