using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Templating;

internal partial class Parser
{
    enum ImplicitExpressionTypes
    {
        Literal,
        AllowedKeyword,
        BannedKeyword
    }

    enum Sides
    {
        Client,
        Server
    }
    
    enum StepModes
    {
        CurrentLexeme,
        Buffer
    }

    private string? source;
    private List<Token> Tokens { get; set; } = new List<Token>();
    private List<string> Messages { get; set; } = new List<string>();
    private List<string> AllowedTransitionKeywords = new List<string>() {"if", "for", "do", "while", "require", "function"};
    private List<string> BannedTransitionKeywords = new List<string>() {"else", "elseif"};
    private Dictionary<string, Func<bool>?> KeywordsMap;
    private StringBuilder Buffer = new StringBuilder();
    private StringBuilder PooledStringBuilder = new StringBuilder();
    private int pos;
    private int lastCommitedPos;
    private int lastCommitedLine;
    private char c;
    private string currentLexeme = "";
    private int line = 1;
    private Script? script;
    private StepModes stepMode = StepModes.CurrentLexeme;
    private bool parsingTransitionCharactersEnabled = true;
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="script">can be null but templating engine won't resolve custom directives</param>
    public Parser(Script? script)
    {
        this.script = script;

        KeywordsMap = new Dictionary<string, Func<bool>?>
        {
            { "if", ParseKeywordIf },
            { "for", ParseKeywordFor },
            { "while", ParseKeywordWhile },
            { "do", ParseKeywordDo },
            { "function", ParseKeywordFunction }
        };
    }
    
    public List<Token> Parse(string templateSource, bool includeNewlines = false)
    {
        source = templateSource;
        ParseClient();

        if (IsAtEnd())
        {
            AddToken(TokenTypes.Eof);
        }

        return Tokens;
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
        
        void HandleStringSequence(char chr)
        {
            string str = GetCurrentLexeme();
            if (LastStoredCharMatches(2, '\\')) // check that string symbol is not escaped
            {
                return;
            }
                 
            if (!InSpecialSequence())
            {
                inString = true;
                stringChar = chr;
            }
            else
            {
                if (stringChar == chr)
                {
                    inString = false;   
                }
            }
        }

        while (!IsAtEnd())
        {
            Step();
            if (handleStrings && !inMultilineComment)
            {
                if (c == '\'')
                {
                    HandleStringSequence('\'');
                }
                else if (c == '"')
                {
                    HandleStringSequence('"');
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
    
    bool NextNonWhiteSpaceCharMatches(char ch)
    {
        StorePos();
        while (!IsAtEnd())
        {
            if (Peek() == ' ')
            {
                Step();
            }
            else if (Peek() == ch)
            {
                Step();
                RestorePos();
                return true;
            }
            else
            {
                RestorePos();
                return false;
            }
        }

        RestorePos();
        return false;
    }

    bool ParseWhitespaceAndNewlines(Sides currentSide)
    {
        while (!IsAtEnd())
        {
            bool shouldContinue = LookaheadForTransitionClient(currentSide);
            if (shouldContinue)
            {
                continue;
            }
            
            if (Peek() == ' ' || Peek() == '\n' || Peek() == '\r')
            {
                Step();
                continue;
            }

            break;
        }

        return true;
    }

    bool NextLiteralSkipEmptyCharsMatches(string literal, Sides currentSide)
    {
        if (IsAtEnd())
        {
            return false;
        }
        
        StorePos();

        bool started = false;
        while (!IsAtEnd())
        {
            bool shouldContinue = LookaheadForTransitionClient(currentSide);
            if (shouldContinue)
            {
                continue;
            }
            
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
    
    /// <summary>
    /// 
    /// </summary>
    /// <returns>true if we should end current iteration</returns>
    bool LookaheadForTransitionClient(Sides currentSide)
    {
        if (!ParsingControlChars())
        {
            return false;
        }
        
        TokenTypes transitionType = currentSide == Sides.Client ? TokenTypes.Text : TokenTypes.BlockExpr;
        
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
                AddToken(transitionType);

                Step();
                Step();
                DiscardCurrentLexeme();
                ParseServerComment();
                return true;
            }
            
            AddToken(transitionType);
            ParseTransition(currentSide);
            return true;
        }

        return false;
    }

    bool LookaheadForTransitionServerSide()
    {
        if (!ParsingControlChars())
        {
            return false;
        }
        
        if (Peek() == '@')
        {
            if (Peek(2) == ':')
            {
                AddToken(TokenTypes.BlockExpr);
                Step();
                Step();
                string str = GetCurrentLexeme();
                DiscardCurrentLexeme();
                ParseRestOfLineAsClient();
                return true;
            }
        }

        return false;
    }

    void ParseRestOfLineAsClient()
    {
        char chr = Step();
        while (!IsAtEnd() && chr != '\n' && chr != '\r')
        {
            bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
            if (shouldContinue)
            {
                continue;
            }

            chr = Step();
        }

        // if we ended on \r check for \n and consume if matches
        if (chr == '\r')
        {
            chr = Peek();
            if (chr == '\n')
            {
                Step();
            }   
        }

        AddToken(TokenTypes.Text);
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
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }

                Step();
            }
            
            AddToken(TokenTypes.Text);
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
                    AddToken(TokenTypes.Comment);
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

        bool ParseTransition(Sides currentSide)
        {
            /* Valid transition sequences are
            * @{ - block
            * @( - explicit expression
            * @: - line transition (back to client)
            * @! - explicit escape expression 
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
                ParseCodeBlock(false, false);
            }
            else if (c == '(')
            {
                DiscardCurrentLexeme();
                ParseExplicitExpression();
            }
            else if (c == ':' && currentSide == Sides.Client)
            {
                DiscardCurrentLexeme();
                ParseRestOfLineAsClient();
            }
            else if (c == '!' && currentSide == Sides.Client && NextNonWhiteSpaceCharMatches('{'))
            {
                DiscardCurrentLexeme();
                SetParsingControlChars(false);
                ParseCodeBlock(false, false);
                SetParsingControlChars(true);
            }
            else if (IsAlpha(c))
            {
                ParseImplicitExpression(currentSide);
            }
            else
            {
                // [todo] either an invalid transition or an annotation
                if (currentSide == Sides.Server)
                {
                    // we don't know enough to decide so we treat it as an annotation in server mode for now
                    currentLexeme = $"@{GetCurrentLexeme()}";
                    return true;
                }

                return Throw("Invalid character after @");
            }

            return false;
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

        string ParseLiteral(Sides currentSide)
        {
            ClearPooledBuilder();
            
            while (!IsAtEnd())
            {
                bool shouldSkip = LookaheadForTransitionClient(currentSide);
                if (shouldSkip)
                {
                    continue;
                }
                
                if (!IsAlphaNumeric(Peek()))
                {
                    break;
                }
                
                char chr = Step();
                PooledStringBuilder.Append(chr);
            }   
            
            string str = PooledStringBuilder.ToString();
            ClearPooledBuilder();
            return str;
        }
            
        string ParseLiteralStartsWithAlpha(Sides currentSide)
        {
            ClearPooledBuilder();
            bool first = true;
                
            while (!IsAtEnd())
            {
                bool shouldSkip = LookaheadForTransitionClient(currentSide);
                if (shouldSkip)
                {
                    continue;
                }
                
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
                
                char chr = Step();
                PooledStringBuilder.Append(chr);
            }

            string str = PooledStringBuilder.ToString();
            ClearPooledBuilder();
            return str;
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

        void ParseImplicitExpression(Sides currentSide)
        {
            while (!IsAtEnd())
            {
                ParseLiteralStartsWithAlpha(currentSide);
                string firstPart = GetCurrentLexeme();
                ImplicitExpressionTypes firstPartType = Str2ImplicitExprType(firstPart);

                // 1. we check for known keywords
                if (firstPartType is ImplicitExpressionTypes.AllowedKeyword or ImplicitExpressionTypes.BannedKeyword)
                {
                    ParseKeyword();
                    return;
                }
                
                // 2. scan custom directives, if the feature is available
                // all directives are parsed as a sequence of oscillating [ALPHA, .] tokens
                if (script?.Options.Directives.TryGetValue(firstPart, out _) ?? false)
                {
                    ParseDirective();
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
                    ParseLiteralStartsWithAlpha(currentSide);
                }

                bufC = Peek();
                if (bufC is not ('.' or '(' or '['))
                {
                    break;
                }
            }
            
            AddToken(TokenTypes.ImplicitExpr);
        }

        bool ParseKeyword()
        {
            string keyword = GetCurrentLexeme();

            // if keyword is from a know list of keywords we invoke a handler method of that keyword
            if (KeywordsMap.TryGetValue(keyword, out Func<bool>? resolver))
            {
                resolver?.Invoke();
                return true;
            }

            return false;
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
        void ParseCodeBlock(bool keepOpeningBrk, bool keepClosingBrk)
        {
            bool matchedOpenBrk = MatchNextNonWhiteSpaceChar('{');
            string l = GetCurrentLexeme();
            if (l == "{")
            {
                matchedOpenBrk = true;
            }
            
            if (matchedOpenBrk && !keepOpeningBrk)
            {
                RemoveLastCharFromCurrentLexeme();
            }
            
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

        bool LastStoredCharMatches(params char[] chars)
        {
            if (GetCurrentLexeme().Length < 1)
            {
                return false;
            }
            
            char chr = currentLexeme[..^1][0];
            return chars.Contains(chr);
        }
        
        bool LastStoredCharMatches(int n = 1, params char[] chars)
        {
            if (GetCurrentLexeme().Length < n)
            {
                return false;
            }
            
            char chr = currentLexeme.Substring(currentLexeme.Length - 1 - n, 1)[0];
            return chars.Contains(chr);
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
            bool inString = false;
            char stringChar = ' ';
            bool inMultilineComment = false;
            int missingBrks = 1;
            
            bool InSpecialSequence()
            {
                return inString || inMultilineComment;
            }

            void HandleStringSequence(char chr)
            {
                string str = GetCurrentLexeme();
                if (LastStoredCharMatches(2, '\\')) // check that string symbol is not escaped
                {
                    return;
                }
                 
                if (!InSpecialSequence())
                {
                    inString = true;
                    stringChar = chr;
                }
                else
                {
                    if (stringChar == chr)
                    {
                        inString = false;   
                    }
                }
            }
            
            while (!IsAtEnd())
            {
                if (!inMultilineComment)
                {
                    if (c == '\'')
                    {
                        HandleStringSequence('\'');
                    }
                    else if (c == '"')
                    {
                        HandleStringSequence('"');
                    }
                    else if (c == '`')
                    {
                        HandleStringSequence('`');
                    }
                }
                
                if (!inString)
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

                if (!InSpecialSequence())
                {
                    bool cnt = LookaheadForTransitionServerSide();
                    if (cnt)
                    {
                        continue;
                    }

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
                }

                string str2 = GetCurrentLexeme();
                char chr = Step();
            }
            
            string str = GetCurrentLexeme();
        }

        string ParseHtmlTagNameInBuffer()
        {
            // First char in a proper HTML tag (after opening <) can be [_, !, /, Alpha, ?]
            if (!(Peek() == '_' || Peek() == '!' || Peek() == '?' || Peek() == '/' || IsAlpha(Peek())))
            {
                Throw("First char after < in an opening HTML tag must be _, ! or alpha");
            }
            
            // The next few chars represent element's name
            ClearBuffer();
            SetStepMode(StepModes.Buffer);
            
            while (!IsAtEnd() && (IsAlphaNumeric(Peek()) || Peek() == '/' || Peek() == '!' || Peek() == '?'))
            {
                Step();
            }

            if (IsAtEnd())
            {
                Throw("Unclosed HTML tag at the end of file");
            }

            string tagName = GetBuffer();
            
            AddBufferToCurrentLexeme();
            ClearBuffer();
            SetStepMode(StepModes.CurrentLexeme);
            
            return tagName;
        }

        bool ParseHtmlTag()
        {
            char chr = Step(); // has to be <
            string tagName = ParseHtmlTagNameInBuffer();

            ParseUntilBalancedChar('<', '>', true, true, true);
            string tagText = GetCurrentLexeme();

            bool isSelfClosing = IsSelfClosingHtmlTag(tagName);
            bool isSelfClosed = CurrentLexemeIsSelfClosedHtmlTag();
            bool parseContent = !isSelfClosed && !isSelfClosing;
            
            if (parseContent)
            {
                if (tagText == "<text>") // "<text>" has a special meaning only when exactly matched. Any modification like "<text >" will be rendered as a normal tag
                {
                    DiscardCurrentLexeme();
                }
                
                ParseHtmlOrPlaintextUntilClosingTag(tagName);
                ParseHtmlClosingTag(tagName);
            }
            
            string s = GetCurrentLexeme();
            AddToken(TokenTypes.Text);

            return true;
        }

        // parser has to be positioned at opening < of the closing tag
        void ParseHtmlClosingTag(string openingTagName)
        {
            char chr = Step(); // has to be <
            
            string str = GetCurrentLexeme();
            string closingTagName = ParseHtmlTagNameInBuffer();

            ParseUntilBalancedChar('<', '>', true, true, true);
            str = GetCurrentLexeme();

            if ($"/{openingTagName}" != closingTagName)
            {
                // [todo] end tag does not match opening tag
                // we will be graceful but a warning can be emmited
            }
            
            if (openingTagName == "text" && closingTagName == "/text" && str.Trim() == "</text>")
            {
                DiscardCurrentLexeme();
            }
        }

        void ParseHtmlOrPlaintextUntilClosingTag(string openingTagName)
        {
            int missingBrks = 1;
            
            string s = GetCurrentLexeme();
            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
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
}