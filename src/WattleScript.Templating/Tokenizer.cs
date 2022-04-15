﻿namespace WattleScript.Templating;

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
    private Dictionary<string, Func<bool>?> KeywordsMap;
    private Token LastToken => Tokens[^1];
    int pos = 0;
    char c;
    string currentLexeme = "";
    int line = 1;
    
    public Tokenizer()
    {
        KeywordsMap = new Dictionary<string, Func<bool>?>
        {
            { "if", ParseKeywordIf }
        };
    }

    bool IsAtEnd()
    {
        return pos >= Source.Length;
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
    
    bool ParseUntilBalancedChar(char startBr, char endBr, bool startsInbalanced)
    {
        int inbalance = startsInbalanced ? 1 : 0;
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
                    return true;
                }
            }
        }

        return false;
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

    bool MatchNextNonWhiteSpaceChar(char ch)
    {
        while (!IsAtEnd())
        {
            if (Peek() == ' ')
            {
                Step();
            }
            else if (Peek() == ch)
            {
                Step();
                return true;
            }
            else
            {
                return false;
            }
        }

        return false;
    }
    
    // if (expr) {}
    bool ParseKeywordIf()
    {
        bool openBrkMatched = MatchNextNonWhiteSpaceChar('(');
        string l = GetCurrentLexeme();
        
        if (!openBrkMatched)
        {
            return false;
        }
        
        bool endExprMatched = ParseUntilBalancedChar('(', ')', true);
        l = GetCurrentLexeme();

        if (!endExprMatched)
        {
            return false;
        }
        
        //bool openBlockBrkMatched = MatchNextNonWhiteSpaceChar('{');
        ParseCodeBlock();
        
        return false;
    }
    
    public List<Token> Tokenize(string source, bool includeNewlines = false)
    {
        bool tokenize = true;
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
        
        void Error(int line, string message)
        {
            anyErrors = true;
            Messages.Add($"Error at line {line} - {message}");
        }
        
        return Tokens;
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

            while (!IsAtEnd())
            {
                ParseLiteralStartsWithAlpha();
                string firstPart = GetCurrentLexeme();
                ImplicitExpressionTypes firstPartType = Str2ImplicitExprType(firstPart);

                if (firstPartType is ImplicitExpressionTypes.AllowedKeyword or ImplicitExpressionTypes.BannedKeyword)
                {
                    ParseKeyword();
                    return;
                }

                char bufC = Peek();
                if (bufC == '[')
                {
                    ParseUntilBalancedChar('[', ']', false);
                }
                else if (bufC == '(')
                {
                    ParseUntilBalancedChar('(', ')', false);
                }
                else if (bufC == '.')
                {
                    if (!IsAlpha(Peek(2))) // next char after . has to be alpha else the dot itself is client side
                    {
                        break;
                    }
                    
                    Step();
                    ParseLiteralStartsWithAlpha();
                }

                bufC = Peek();
                if (bufC is not ('.' or '(' or '['))
                {
                    break;
                }
            }
            
            AddToken(TokenTypes.ImplicitExpr);
            ParseClient();
        }

        void ParseKeyword()
        {
            string keyword = GetCurrentLexeme();

            if (KeywordsMap.TryGetValue(keyword, out Func<bool>? resolver))
            {
                resolver?.Invoke();
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
            bool matchedOpenBrk = MatchNextNonWhiteSpaceChar('{');
            string l = GetCurrentLexeme();
            AddToken(TokenTypes.BlockExpr);

            ParseUntilHtmlOrClientTransition();
        }

        bool LastStoredCharNotWhitespaceMatches(params char[] chars)
        {
            string str = GetCurrentLexeme();
            for (int i = str.Length; i > 0; i--)
            {
                char cc = str[i - 1];
                if (cc == ' ')
                {
                    continue;
                }

                return chars.Contains(cc);
            }
            
            return false;
        }

        /* A point of transition can be
         * <Alpha where char preceding < is either semicolon or newline
         */
        void ParseUntilHtmlOrClientTransition()
        {
            while (!IsAtEnd())
            {
                if (Peek() == '<')
                {
                    if (LastStoredCharNotWhitespaceMatches('\n', '\r', ';'))
                    {
                        AddToken(TokenTypes.BlockExpr);
                        ParseHtmlTag();
                    }
                }

                string str = GetCurrentLexeme();
                char chr = Step();
            }
        }

        void ParseHtmlTag()
        {
            char chr = Step();
            
            // First char in a proper HTML tag (after opening <) can be [_, !, /, Alpha]
            // If we have something else we can consider it a malformated tag
            // Razor renders first char = NUM as an error but compiles
            // If it is some other char, Razor throws
            
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

}