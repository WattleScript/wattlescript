namespace WattleScript.Templating;

public class Tokenizer
{
    enum ImplicitExpressionTypes
    {
        Literal,
        AllowedKeyword,
        BannedKeyword
    }
    
    private string Source { get; set; }
    private List<Token> Tokens { get; set; } = new List<Token>();
    private List<string> Messages { get; set; } = new List<string>();
    private List<string> AllowedTransitionKeywords = new List<string>() {"if", "for", "do", "while", "require", "function"};
    private List<string> BannedTransitionKeywords = new List<string>() {"else", "elseif"};

    private Token LastToken => Tokens[^1];
    
    public List<Token> Tokenize(string source, bool includeNewlines = false)
    {
        int line = 1;
        int pos = 0;
        bool tokenize = true;
        char c;
        string currentLexeme = "";
        bool anyErrors = false;
        Source = source;

        ParseClient();

        if (IsAtEnd())
        {
            tokenize = false;
            AddToken(TokenTypes.Eof);
        }

        if (anyErrors)
        {
            tokenize = false;
        }
        

        bool IsAtEnd()
        {
            return pos >= Source.Length;
        }
        
    
        /* In client mode everything is a literal
         * until we encouter @
         * then we lookahead at next char and if it's not another @ (escape)
         * we enter server mode
         */
        void ParseClient()
        {
            while (!IsAtEnd())
            {
                if (Peek() == '@')
                {
                    if (Peek(2) != '@')
                    {
                        AddToken(TokenTypes.ClientText);
                        ParseTransition();
                        return;   
                    }
                }
                
                Step();
            }
            
            AddToken(TokenTypes.ClientText);
        }

        void ParseTransition()
        {
            /* Valid transition sequences are
            * @{ - block
            * @( - explicit expression
            * @: - line transition (back to client)
            * @TKeyword - if, for, while, do...
            * @TBannedKeyword - else, elseif
            * @ContrainedLiteral - eg. @myVar. First char has to be either alpha or underscore (eg. @8 is invalid)
            * ---------
            * If a valid transition has not been found
            * - if we found TBannedKeyword - we stash an errror, resume parsing client side. We should report: "@ can't be followed by a reserved keyword 'else'. Please remove the @ symbol at line X, char Y."
            * - else - we stash an error and consider the sequence to be client side literal. Eg. @8 should report: "@ must be followed by a valid code block"
            */

            Step(); // @
            DiscardCurrentLexeme();
            Step();
            
            if (c == '{')
            {
                DiscardCurrentLexeme();
                ParseCodeBlock();
            }
            else if (c == '(')
            {
                DiscardCurrentLexeme();
            }
            else if (c == ':')
            {
                DiscardCurrentLexeme();

            }
            else if (IsAlpha(c))
            {
                ParseImplicitExpression();
            }
            else
            {
                // [todo] report err, synchronise
            }
        }

        ImplicitExpressionTypes Str2ImplicitExprType(string str)
        {
            if (AllowedTransitionKeywords.Contains(str))
            {
                return ImplicitExpressionTypes.AllowedKeyword;
            }

            if (BannedTransitionKeywords.Contains(str))
            {
                return ImplicitExpressionTypes.BannedKeyword;
            }

            return ImplicitExpressionTypes.Literal;
        }

        void ParseImplicitExpression()
        {
            void ParseLiteral()
            {
                while (true)
                {
                    if (!IsAlphaNumeric(Peek()))
                    {
                        break;
                    }
                
                    Step();
                }   
            }
            void ParseLiteralStartsWithAlpha()
            {
                bool first = true;
                
                while (true)
                {
                    if (first)
                    {
                        if (!IsAlpha(Peek()))
                        {
                            break;
                        }

                        first = false;
                    }
                    else if (!IsAlphaNumeric(Peek()))
                    {
                        break;
                    }
                
                    Step();
                }   
            }
            void ParseUntilBalancedChar(char startBr, char endBr)
            {
                int inbalance = 0;
                while (!IsAtEnd())
                {
                    Step();
                    if (c == startBr)
                    {
                        inbalance++;
                    }
                    else if (c == endBr)
                    {
                        inbalance--;
                        if (inbalance <= 0)
                        {
                            break;
                        }
                    }
                }
            }

            while (!IsAtEnd())
            {
                ParseLiteralStartsWithAlpha();

                char bufC = Peek();
                if (bufC == '[')
                {
                    ParseUntilBalancedChar('[', ']');
                }
                else if (bufC == '(')
                {
                    ParseUntilBalancedChar('(', ')');
                }
                else if (bufC == '.')
                {
                    Step();
                    ParseLiteralStartsWithAlpha();
                }

                bufC = Peek();
                if (bufC is not ('.' or '(' or '['))
                {
                    break;
                }
            }


            string buffer = GetCurrentLexeme();
            ImplicitExpressionTypes bufferType = Str2ImplicitExprType(buffer);

            if (bufferType == ImplicitExpressionTypes.Literal)
            {
                AddToken(TokenTypes.ImplicitExpr);
                ParseClient();
            }
        }

        /* Here the goal is to find the matching end of transition expression
         * and recursively call ourselfs if we encounter a client transfer expression
         * We need to understand a subset of ws grammar for this
         * - comments (//, multiline)
         * - strings ('', "", ``)
         * ---------
         * We can enter server side in two situations
         * - looking for a } in case we've entered from a code block
         * - looking for a ) when entered from an explicit expression
         */
        void ParseCodeBlock()
        {
            c = Step();
            int missingBraces = 1;

            while (!IsAtEnd())
            {
                c = Step();

                if (c == '}')
                {
                    missingBraces--;
                    if (missingBraces <= 0)
                    {
                        currentLexeme = currentLexeme.Substring(0, currentLexeme.Length - 1);
                        AddToken(TokenTypes.BlockExpr);
                        ParseClient();
                        return;
                    }
                }
                else if (c == '{')
                {
                    missingBraces++;
                }
            }
        }

        string GetCurrentLexeme()
        {
            return currentLexeme;
        }

        bool IsAlphaNumeric(char ch)
        {
            return !IsAtEnd() && (IsDigit(ch) || IsAlpha(ch));
        }

        bool IsAlpha(char ch)
        {
            return !IsAtEnd() && (char.IsLetter(ch) || ch is '_');
        }

        bool IsDigit(char ch)
        {
            return !IsAtEnd() && (ch is >= '0' and <= '9');
        }

        char Step(int i = 1)
        {
            char cc = Source[pos];
            currentLexeme += cc;
            pos += i;
            c = cc;
            return cc;
        }

        char Peek(int i = 1)
        {
            if (IsAtEnd())
            {
                return '\n';
            }

            int peekedPos = pos + i - 1;

            if (peekedPos < 0)
            {
                pos = 0;
            }

            if (Source.Length <= peekedPos)
            {
                return Source[^1];
            }

            return Source[peekedPos];
        }

        bool Match(char expected)
        {
            if (IsAtEnd())
            {
                return false;
            }

            if (Source[pos] != expected)
            {
                return false;
            }

            Step();
            return true;
        }
        
        void AddToken(TokenTypes type)
        {
            if (currentLexeme.Length > 0)
            {
                Token token = new Token(type, currentLexeme, null, line);
                Tokens.Add(token);
                DiscardCurrentLexeme();   
            }
        }

        void DiscardCurrentLexeme()
        {
            currentLexeme = "";
        }

        void Error(int line, string message)
        {
            anyErrors = true;
            Messages.Add($"Error at line {line} - {message}");
        }
        
        return Tokens;
    }
}