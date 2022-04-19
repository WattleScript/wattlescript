using System.Text;

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
    private Dictionary<string, Func<bool>?> KeywordsMap;
    private StringBuilder Buffer = new StringBuilder();
    private Token LastToken => Tokens[^1];
    int pos = 0;
    char c;
    string currentLexeme = "";
    int line = 1;
    
    public Tokenizer()
    {
        KeywordsMap = new Dictionary<string, Func<bool>?>
        {
            { "if", ParseKeywordIf },
            { "for", ParseKeywordFor }
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

        if (stepMode == StepModes.CurrentLexeme)
        {
            currentLexeme += cc;    
        }
        else
        {
            Buffer.Append(cc);
        }
        
        pos += i;
        c = cc;
        return cc;
    }

    private int storedPos;
    void StorePos()
    {
        storedPos = pos;
    }

    void RestorePos()
    {
        pos = storedPos;
        if (pos >= Source.Length)
        {
            pos = Source.Length - 1;
        }
        
        storedPos = 0;
        c = Source[pos];
        DiscardCurrentLexeme();
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
    
    bool ParseUntilBalancedChar(char startBr, char endBr, bool startsInbalanced, bool handleStrings, bool handleServerComments)
    {
        bool inString = false;
        char stringChar = ' ';
        bool inMultilineComment = false;

        bool InSpecialSequence()
        {
           return inString || inMultilineComment;
        }

        int inbalance = startsInbalanced ? 1 : 0;
        while (!IsAtEnd())
        {
            Step();
            if (handleStrings && !inMultilineComment)
            {
                if (c == '\'')
                {
                    if (!InSpecialSequence())
                    {
                        inString = true;
                        stringChar = '\'';
                    }
                    else
                    {
                        if (stringChar == '\'')
                        {
                            inString = false;   
                        }
                    }
                }
                else if (c == '"')
                {
                    if (!InSpecialSequence())
                    {
                        inString = true;
                        stringChar = '"';
                    }
                    else
                    {
                        if (stringChar == '"')
                        {
                            inString = false;   
                        }
                    }
                }
            }

            if (handleServerComments && !inString)
            {
                if (c == '/' && Peek() == '*')
                {
                    if (!inMultilineComment)
                    {
                        inMultilineComment = true;   
                    }
                }
                else if (c == '*' && Peek() == '/')
                {
                    if (inMultilineComment)
                    {
                        inMultilineComment = false;
                    }
                }
            }
            
            if (c == startBr)
            {
                if (!InSpecialSequence())
                {
                    inbalance++;    
                }
            }
            else if (c == endBr)
            {
                if (!InSpecialSequence())
                {
                    inbalance--;
                    if (inbalance <= 0)
                    {
                        return true;
                    }   
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
    
    bool MatchNextNonWhiteSpaceNonNewlineChar(char ch)
    {
        while (!IsAtEnd())
        {
            if (Peek() == ' ' || Peek() == '\n' || Peek() == '\r')
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
    // parser has to be positioned after if, either at opening ( or at whitespace before it
    bool ParseKeywordIf()
    {
        ParseGenericBrkKeywordWithBlock("if");

        bool matchesElse = NextLiteralSkipEmptyCharsMatches("else") || NextLiteralSkipEmptyCharsMatches("elseif"); // else handles "else if" but we have to check for "elseif" manually
        if (matchesElse)
        {
            ParseKeywordElseOrElseIf();
        }
        
        return false;
    }

    // for (i in a..b)
    // for (i = 0; i < x; i++) 
    // for (;;)
    // we always have () around expr/s
    // parser has to be positioned after "for", either at opening ( or at a whitespace preceding it
    bool ParseKeywordFor()
    {
        return ParseGenericBrkKeywordWithBlock("for");
    }

    // keyword () {}
    bool ParseGenericBrkKeywordWithBlock(string keyword)
    {
        bool openBrkMatched = MatchNextNonWhiteSpaceChar('(');
        string l = GetCurrentLexeme();

        if (!openBrkMatched)
        {
            Throw($"Expected ( after {keyword}");
        }
        
        bool endExprMatched = ParseUntilBalancedChar('(', ')', true, true, true);
        l = GetCurrentLexeme();

        if (!endExprMatched)
        {
            return false;
        }
        
        ParseCodeBlock(true);

        return true;
    }

    // else {}
    // or possibly else if () {}
    bool ParseKeywordElseOrElseIf()
    {
        StorePos();
        ParseWhitespaceAndNewlines();
        string elseStr = StepN(4);
        ParseWhitespaceAndNewlines();
        string elseIfStr = StepN(2);
        RestorePos();

        if (elseStr == "else" && elseIfStr == "if")
        {
            ParseKeywordElseIf();
        }
        else if (elseStr == "else")
        {
            ParseKeywordElse();
        }
        
        return true;
    }

    // else if () {}
    bool ParseKeywordElseIf()
    {
        ParseWhitespaceAndNewlines();
        string elseStr = StepN(4); // eat else
        ParseWhitespaceAndNewlines();
        string elseIfStr = StepN(2); // ear if

        return ParseKeywordIf();
    }
    
    // else {}
    bool ParseKeywordElse()
    {
        ParseWhitespaceAndNewlines();
        //DiscardCurrentLexeme();
        
        string elseStr = StepN(4);

        if (elseStr != "else")
        {
            return false;
        }
        
        string str = GetCurrentLexeme();
        
        ParseCodeBlock(true);
        
        return false;
    }

    string StepN(int steps)
    {
        string str = "";
        while (!IsAtEnd() && steps > 0)
        {
            char chr = Step();
            str += chr;
            steps--;
        }

        return str;
    }

    bool ParseWhitespaceAndNewlines()
    {
        while (!IsAtEnd())
        {
            if (Peek() == ' ' || Peek() == '\n' || Peek() == '\r')
            {
                Step();
                continue;
            }

            break;
        }

        return true;
    }

    bool NextLiteralSkipEmptyCharsMatches(string literal)
    {
        if (IsAtEnd())
        {
            return false;
        }
        
        StorePos();

        bool started = false;
        while (!IsAtEnd())
        {
            if (!started)
            {
                if (Peek() == ' ' || Peek() == '\n' || Peek() == '\r')
                {
                    Step();
                }
                else
                {
                    string str = GetCurrentLexeme();
                    DiscardCurrentLexeme();
                    started = true;
                }
            }
            else
            {
                if (Peek() == ' ' || Peek() == '\n' || Peek() == '\r')
                {
                    break;
                }   
            }

            Step();
        }

        bool match = literal == GetCurrentLexeme();
        RestorePos();
        
        return match;
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

    /// <summary>
    /// 
    /// </summary>
    /// <returns>true if we should end current iteration</returns>
    bool LookaheadForTransition(bool calledFromClientSide)
    {
        if (Peek() == '@')
        {
            if (Peek(2) == '@') // @@ -> @
            {
                Step();
                Step();
                RemoveLastCharFromCurrentLexeme();
                return true;
            }

            if (Peek(2) == '*') // @* -> comment
            {
                if (calledFromClientSide)
                {
                    AddToken(TokenTypes.ClientText);
                }
                
                Step();
                Step();
                DiscardCurrentLexeme();
                ParseServerComment();
                return true;
            }
                    

            AddToken(TokenTypes.ClientText);
            ParseTransition();
            return true;
        }

        return false;
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
                bool shouldContinue = LookaheadForTransition(true);
                if (shouldContinue)
                {
                    continue;
                }

                Step();
            }
            
            AddToken(TokenTypes.ClientText);
        }

        // @* *@
        // parser must be positioned directly after opening @*
        void ParseServerComment()
        {
            while (!IsAtEnd())
            {
                if (Peek() == '*' && Peek(2) == '@')
                {
                    Step();
                    Step();
                    RemoveLastCharFromCurrentLexeme();
                    RemoveLastCharFromCurrentLexeme(); // eat and discard closing *@
                    AddToken(TokenTypes.ServerComment);
                    break;
                }
                
                Step();
            }
        }
        
        void RemoveLastCharFromCurrentLexeme()
        {
            if (GetCurrentLexeme().Length > 0)
            {
                currentLexeme = currentLexeme[..^1];
            }
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
                ParseCodeBlock(false);
            }
            else if (c == '(')
            {
                DiscardCurrentLexeme();
                ParseExplicitExpression();
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

        void ParseExplicitExpression()
        {
            // parser is positioned after opening (
            ParseUntilBalancedChar('(', ')', true, true, true);
            string str = GetCurrentLexeme();

            // get rid of closing )
            if (str.EndsWith(')'))
            {
                str = str[..^1];
            }

            currentLexeme = str;
            AddToken(TokenTypes.ExplicitExpr);
        }

        void ParseImplicitExpression()
        {
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
                    ParseUntilBalancedChar('[', ']', false, true, true);
                }
                else if (bufC == '(')
                {
                    ParseUntilBalancedChar('(', ')', false, true, true);
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
        void ParseCodeBlock(bool keepClosingBrk)
        {
            bool matchedOpenBrk = MatchNextNonWhiteSpaceChar('{');
            string l = GetCurrentLexeme();
            AddToken(TokenTypes.BlockExpr);
            
            while (!IsAtEnd())
            {
                ParseUntilHtmlOrClientTransition();
                string str = GetCurrentLexeme();
                StorePos();
                bool matchedClosingBrk = MatchNextNonWhiteSpaceNonNewlineChar('}');

                if (matchedClosingBrk)
                {
                    break;
                }
                
                RestorePos();
            }
            
            l = GetCurrentLexeme();
            if (!keepClosingBrk)
            {
                currentLexeme = currentLexeme.Substring(0, currentLexeme.Length - 1);
            }
            AddToken(TokenTypes.BlockExpr);
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
            int missingBrks = 1;
            
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
                else if (Peek() == '{')
                {
                    missingBrks++;
                }
                else if (Peek() == '}')
                {
                    missingBrks--;
                    if (missingBrks <= 0)
                    {
                        break;
                    }
                }

                string str2 = GetCurrentLexeme();
                char chr = Step();
            }
            
            string str = GetCurrentLexeme();
        }

        void ClearBuffer()
        {
            Buffer.Clear();
        }

        private StepModes stepMode = StepModes.CurrentLexeme;
        enum StepModes
        {
            CurrentLexeme,
            Buffer
        }

        void SetStepMode(StepModes mode)
        {
            stepMode = mode;
        }

        bool Throw(string message)
        {
            throw new Exception(message);
        }

        bool ParseHtmlTag()
        {
            char chr = Step(); // has to be <
            
            // First char in a proper HTML tag (after opening <) can be [_, !, /, Alpha]
            if (!(Peek() == '_' || Peek() == '!' || IsAlpha(Peek())))
            {
                Throw("First char after < in an opening HTML tag must be _, ! or alpha");
            }
            
            // The next few chars represent element's name
            ClearBuffer();
            SetStepMode(StepModes.Buffer);
            
            while (!IsAtEnd() && IsAlphaNumeric(Peek()))
            {
                Step();
            }

            if (IsAtEnd())
            {
                Throw("Unclosed HTML tag at the end of file");
            }

            string tagName = GetBuffer();
            /*char nextMeaningful = GetNextCharNotWhitespace();

            if (nextMeaningful == '>') // self closing without / or end of start tag
            {
                if (IsSelfClosing(tagName))
                {
                    
                }
            }
            else if (nextMeaningful == '/') // self closing with /
            {
                
            }
            else if (IsAlpha(nextMeaningful)) // start of an attribute
            {
                
            }
            else
            {
                return Throw("Unexpected char");
            }*/
            
            AddBufferToCurrentLexeme();
            ClearBuffer();
            SetStepMode(StepModes.CurrentLexeme);
            
            ParseUntilBalancedChar('<', '>', true, true, true);
            string s = GetCurrentLexeme();

            bool isSelfClosing = IsSelfClosingHtmlTag(tagName);
            bool isSelfClosed = CurrentLexemeIsSelfClosedHtmlTag();
            bool parseContent = !isSelfClosed && !isSelfClosing;
            
            if (parseContent)
            {
                ParseHtmlOrPlaintextUntilClosingTag(tagName);
                ParseHtmlClosingTag();
            }
            
            s = GetCurrentLexeme();
            AddToken(TokenTypes.ClientText);

            return true;
        }

        void ParseHtmlClosingTag()
        {
            ParseUntilBalancedChar('<', '>', false, true, true);
            string str = GetCurrentLexeme();
        }

        void ParseHtmlOrPlaintextUntilClosingTag(string openingTagName)
        {
            int missingBrks = 1;
            
            string s = GetCurrentLexeme();
            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransition(true);
                if (shouldContinue)
                {
                    continue;
                }
                
                if (Peek() == '<')
                {
                    if (IsAlpha(Peek(2))) // we enter new tag
                    {
                        ParseHtmlTag();
                    }
                    else if (Peek(2) == '/' && IsAlpha(Peek(3)))
                    {
                        missingBrks--;
                    }
                   
                    if (missingBrks <= 0)
                    {
                        break;
                    }
                }
                else if (Peek() == '>')
                {
                    if (IsAlpha(Peek(2)))
                    {
                        missingBrks++;   
                    }
                }

                Step();
            }

            s = GetCurrentLexeme();
        }

        char GetNextCharNotWhitespace()
        {
            StorePos();
            char ch = '\0';
            
            while (!IsAtEnd())
            {
                ch = Step();
                if (Peek() != ' ')
                {
                    break;
                }
            }
            RestorePos();

            return ch;
        }

        string GetBuffer()
        {
            return Buffer.ToString();
        }

        bool CurrentLexemeIsSelfClosedHtmlTag()
        {
            return GetCurrentLexeme().EndsWith("/>");
        }

        bool IsSelfClosingHtmlTag(string htmlTag)
        {
            return IsSelfClosing(htmlTag.ToLowerInvariant());
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

        void AddBufferToCurrentLexeme()
        {
            currentLexeme += Buffer.ToString();
        }

        bool IsSelfClosing(string tagName)
        {
            return tagName == "area" || tagName == "base" || tagName == "br" || tagName == "col" || tagName == "embed" || tagName == "hr" || tagName == "img" || tagName == "input" || tagName == "keygen" || tagName == "link" || tagName == "menuitem" || tagName == "meta" || tagName == "param" || tagName == "source" || tagName == "track" || tagName == "wbr";
        }
}