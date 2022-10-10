using System.Text;

namespace WattleScript.Interpreter.Tree
{
    //TODO: This class duplicates several large methods from Lexer,
    //find some way to do some code sharing between them?
    class DirectiveLexer
    {
        private TextCursor cur;
        private int line;
        private int sourceIndex;
        
        public DirectiveLexer(string text, int sourceIndex, int line, int column)
        {
            cur = new TextCursor(text);
            cur.Line = line;
            cur.Column = column;
            this.line = line;
            this.sourceIndex = sourceIndex;
            Current = GetToken();
        }

        public Token EofToken => CreateToken(TokenType.Eof, 0, 0);
        
        public Token Current { get; private set; }

        public Token Next()
        {
            var t = Current;
            Current = GetToken();
            return t;
        }

        string ReadNameToken()
        {
            StringBuilder name = new StringBuilder(32);

            for (char c = cur.Char(); cur.NotEof(); c = cur.CharNext())
            {
                if (char.IsLetterOrDigit(c) || c == '_')
                    name.Append(c);
                else
                    break;
            }

            return name.ToString();
        }
        
        private Token ReadNumberToken(int fromLine, int fromCol, bool leadingDot)
        {
            StringBuilder text = new StringBuilder(32);

            //INT : Digit+
            //HEX : '0' [xX] HexDigit+
            //FLOAT : Digit+ '.' Digit* ExponentPart?
            //		| '.' Digit+ ExponentPart?
            //		| Digit+ ExponentPart
            //HEX_FLOAT : '0' [xX] HexDigit+ '.' HexDigit* HexExponentPart?
            //			| '0' [xX] '.' HexDigit+ HexExponentPart?
            //			| '0' [xX] HexDigit+ HexExponentPart
            //
            // ExponentPart : [eE] [+-]? Digit+
            // HexExponentPart : [pP] [+-]? Digit+

            bool isHex = false;
            bool dotAdded = false;
            bool exponentPart = false;
            bool exponentSignAllowed = false;

            if (leadingDot)
            {
                text.Append("0.");
            }
            else if (cur.Char() == '0')
            {
                text.Append(cur.Char());
                char secondChar = cur.CharNext();

                if (secondChar == 'x' || secondChar == 'X')
                {
                    isHex = true;
                    text.Append(cur.Char());
                    cur.CharNext();
                }
            }

            for (char c = cur.Char(); cur.NotEof(); c = cur.CharNext())
            {
                if (exponentSignAllowed && (c == '+' || c == '-'))
                {
                    exponentSignAllowed = false;
                    text.Append(c);
                }
                else if (LexerUtils.CharIsDigit(c))
                {
                    text.Append(c);
                }
                else if (c == '.' && !dotAdded)
                {
                    if (cur.Char() == '.' && cur.PeekNext() == '.') break;
                    dotAdded = true;
                    text.Append(c);
                }
                else if (LexerUtils.CharIsHexDigit(c) && isHex && !exponentPart)
                {
                    text.Append(c);
                }
                else if (c == 'e' || c == 'E' || (isHex && (c == 'p' || c == 'P')))
                {
                    text.Append(c);
                    exponentPart = true;
                    exponentSignAllowed = true;
                    dotAdded = true;
                }
                else
                {
                    break;
                }
            }

            TokenType numberType = TokenType.Number;

            if (isHex && (dotAdded || exponentPart))
                numberType = TokenType.Number_HexFloat;
            else if (isHex)
                numberType = TokenType.Number_Hex;

            string tokenStr = text.ToString();
            return CreateToken(numberType, fromLine, fromCol, tokenStr);
        }
        
        private Token ReadSimpleStringToken(int fromLine, int fromCol)
        {
            StringBuilder text = new StringBuilder(32);
            char separator = cur.Char();

            for (char c = cur.CharNext(); cur.NotEof(); c = cur.CharNext())
            {
                redo_Loop:

                if (c == '\\')
                {
                    text.Append(c);
                    c = cur.CharNext();
                    text.Append(c);

                    if (c == '\r')
                    {
                        c = cur.CharNext();
                        if (c == '\n')
                            text.Append(c);
                        else
                            goto redo_Loop;
                    }
                    else if (c == 'z')
                    {
                        c = cur.CharNext();

                        if (char.IsWhiteSpace(c))
                            cur.SkipWhiteSpace();

                        c = cur.Char();

                        goto redo_Loop;
                    }
                }
                else if (c == '\n' || c == '\r')
                {
                    throw new SyntaxErrorException(
                        CreateToken(TokenType.Invalid, fromLine, fromCol),
                        "unfinished string near '{0}'", text.ToString());
                }
                else if (c == separator)
                {
                    cur.CharNext();
                    Token t = CreateToken(TokenType.String, fromLine, fromCol);
                    t.Text = LexerUtils.UnescapeLuaString(t, text.ToString());
                    return t;
                }
                else
                {
                    text.Append(c);
                }
            }

            throw new SyntaxErrorException(
                CreateToken(TokenType.Invalid, fromLine, fromCol),
                "unfinished string near '{0}'", text.ToString()) { IsPrematureStreamTermination = true };
        }

        Token GetToken()
        {
            cur.SkipWhiteSpace();
            if (!cur.NotEof()) return CreateToken(TokenType.Eof, line, cur.Column);
            int fromCol = cur.Column;
            switch (cur.Char())
            {
                case '(':
                    cur.Next();
                    return CreateToken(TokenType.Brk_Open_Round, line, fromCol, "(");
                case ')':
                    cur.Next();
                    return CreateToken(TokenType.Brk_Close_Round, line, fromCol, ")");
                case '>' when cur.PeekNext() == '=':
                    cur.Next();
                    cur.Next();
                    return CreateToken(TokenType.Op_GreaterThanEqual, line, fromCol, ">=");
                case '>':
                    cur.Next();
                    return CreateToken(TokenType.Op_GreaterThan, line, fromCol, ">");
                case '<' when cur.PeekNext() == '=':
                    cur.Next();
                    cur.Next();
                    return CreateToken(TokenType.Op_LessThanEqual, line, fromCol, "<=");
                case '<':
                    cur.Next();
                    return CreateToken(TokenType.Op_LessThan, line, fromCol, "<");
                case '!' when cur.PeekNext() == '=':
                    cur.Next();
                    cur.Next();
                    return CreateToken(TokenType.Op_NotEqual, line, fromCol, "!=");
                case '!':
                    cur.Next();
                    return CreateToken(TokenType.Not, line, fromCol, "!");
                case '&' when cur.PeekNext() == '&':
                    cur.Next();
                    cur.Next();
                    return CreateToken(TokenType.And, line, fromCol, "&&");
                case '|' when cur.PeekNext() == '|':
                    cur.Next();
                    cur.Next();
                    return CreateToken(TokenType.Or, line, fromCol, "||");
                case '=' when cur.PeekNext() == '=':
                    cur.Next();
                    cur.Next();
                    return CreateToken(TokenType.Op_Equal, line, fromCol, "==");
                case '=':
                    cur.Next();
                    return CreateToken(TokenType.Op_Assignment, line, fromCol, "=");
                case ',':
                    cur.Next();
                    return CreateToken(TokenType.Comma, line, fromCol, ",");
                case '"':
                case '\'':
                    return ReadSimpleStringToken(line, fromCol);
                case '.' when LexerUtils.CharIsDigit(cur.PeekNext()):
                    cur.Next();
                    return ReadNumberToken(line, fromCol, true);
                default:
                    if (char.IsLetter(cur.Char()) || cur.Char() == '_')
                    {
                        var name = ReadNameToken();
                        if (name == "defined")
                        {
                            return CreateToken(TokenType.Preprocessor_Defined, line, fromCol, name);
                        }
                        else
                        {
                            var type = Token.GetReservedTokenType(name, ScriptSyntax.Wattle);
                            return CreateToken(type ?? TokenType.Name, line, fromCol, name);
                        }
                    }
                    else if (LexerUtils.CharIsDigit(cur.Char()))
                    {
                        return ReadNumberToken(line, fromCol, false);
                    }
                    break;
            }
            throw new SyntaxErrorException(CreateToken(TokenType.Invalid, line, fromCol),
                "unexpected symbol near '{0}'", cur.Char());
        }
        
        Token CreateToken(TokenType tokenType, int fromLine, int fromCol, string text = null)
        {
            return new Token(tokenType, sourceIndex, fromLine, fromCol, fromLine, fromCol + text?.Length ?? 0, 0, 0, cur.Index - (text?.Length ?? 0), cur.Index, text);
        }
        
        public void CheckEndOfLine()
        {
            cur.SkipWhiteSpace();
            if(cur.NotEof())
                throw new SyntaxErrorException(CreateToken(TokenType.Invalid, line, cur.Column),
                    "unexpected symbol near '{0}'", cur.Char()); 
        }
    }
}