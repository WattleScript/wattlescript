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
    private List<string> AllowedTransitionKeywords = new List<string>() {"if", "for", "do", "while", "require", "function", "switch"};
    private List<string> BannedTransitionKeywords = new List<string>() {"else", "elseif"};
    private Dictionary<string, Func<bool>?> KeywordsMap;
    private StringBuilder Buffer = new StringBuilder();
    private StringBuilder PooledStringBuilder = new StringBuilder();
    private int pos;
    private int lastCommitedPos;
    private int lastCommitedLine;
    private char c;
    private StringBuilder currentLexeme = new StringBuilder();
    private int line = 1;
    private Script? script;
    private StepModes stepMode = StepModes.CurrentLexeme;
    private bool parsingTransitionCharactersEnabled = true;
    private Document document;
    private List<TagHelper>? tagHelpers;
    
    public Parser(Script? script, List<TagHelper>? tagHelpers)
    {
        this.script = script;
        this.tagHelpers = tagHelpers;
        document = new Document();

        KeywordsMap = new Dictionary<string, Func<bool>?>
        {
            { "if", ParseKeywordIf },
            { "for", ParseKeywordFor },
            { "while", ParseKeywordWhile },
            { "do", ParseKeywordDo },
            { "function", ParseKeywordFunction },
            { "switch", ParseKeywordSwitch },
        };
    }
    
    public List<Token> Parse(string templateSource)
    {
        source = templateSource;
        ParseClient();
        //Lookahead();

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
    
    bool NextNonWhiteSpaceCharMatches(char ch, int skip = 0)
    {
        StorePos();

        for (int i = 0; i < skip; i++)
        {
            Step();
        }
        
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
            
            if (IsWhitespaceOrNewline(Peek()))
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
                    continue;
                }

                string str = GetCurrentLexeme();
                DiscardCurrentLexeme();
                started = true;
                continue;
            }

            if (Peek() == ' ' || Peek() == '\n' || Peek() == '\r')
            {
                break;
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

                shouldContinue = LookaheadForHtmlComment(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }

                if (Peek() == '<' && IsHtmlTagOpeningChar(Peek(2)))
                {
                    ParseHtmlTag(null);
                }

                char c = Step();
                string str = GetCurrentLexeme();
            }

            AddToken(TokenTypes.Text);
        }

        bool IsHtmlTagOpeningChar(char chr)
        {
            return IsAlpha(chr) || chr == '!';
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
                string str = currentLexeme.ToString()[..^1];
                currentLexeme.Clear();
                currentLexeme.Append(str);
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
            c = Peek();
            
            if (c == '{')
            {
                Step();
                ParseCodeBlock(false, false);
            }
            else if (c == '(')
            {
                Step();
                DiscardCurrentLexeme();
                ParseExplicitExpression();
            }
            else if (c == ':' && currentSide == Sides.Client)
            {
                Step();
                DiscardCurrentLexeme();
                ParseRestOfLineAsClient();
            }
            else if (c == '!' && currentSide == Sides.Client && NextNonWhiteSpaceCharMatches('{', 1))
            {
                Step();
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
                    currentLexeme.Insert(0, "@");
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

            currentLexeme.Clear();
            currentLexeme.Append(str);
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
            if (!keepClosingBrk && currentLexeme.Length > 0)
            {
                RemoveLastCharFromCurrentLexeme();
            }
            AddToken(TokenTypes.BlockExpr);
        }
        
        bool LastStoredCharMatches(params char[] chars)
        {
            if (GetCurrentLexeme().Length < 1)
            {
                return false;
            }
            
            char chr = currentLexeme.ToString()[..^1][0];
            return chars.Contains(chr);
        }
        
        bool LastStoredCharMatches(int n = 1, params char[] chars)
        {
            if (GetCurrentLexeme().Length < n)
            {
                return false;
            }
            
            char chr = currentLexeme.ToString().Substring(currentLexeme.Length - 1 - n, 1)[0];
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
                Step();
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

            bool allowHtml = false;
            while (!IsAtEnd())
            {
                if (!inMultilineComment)
                {
                    if (Peek() == '\'')
                    {
                        HandleStringSequence('\'');
                    }
                    else if (Peek() == '"')
                    {
                        HandleStringSequence('"');
                    }
                    else if (Peek() == '`')
                    {
                        HandleStringSequence('`');
                    }
                }
                
                if (!inString)
                {
                    if (Peek() == '/' && Peek() == '*')
                    {
                        if (!inMultilineComment)
                        {
                            inMultilineComment = true;   
                        }
                    }
                    else if (Peek() == '*' && Peek() == '/')
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

                    cnt = LookaheadForHtmlComment(Sides.Server);
                    if (cnt)
                    {
                        continue;
                    }
                    
                    if (Peek() == '<' && IsHtmlTagOpeningChar(Peek(2)))
                    {
                        if (allowHtml || LastStoredCharNotWhitespaceMatches('\n', '\r', ';'))
                        {
                            StepEol();
                            AddToken(TokenTypes.BlockExpr);
                            ParseHtmlTag(null);
                            
                            allowHtml = true;
                            continue;
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
                allowHtml = false;
            }
            
            string str = GetCurrentLexeme();
        }

        string ParseHtmlTagName(bool inBuffer)
        {
            StringBuilder sb = new StringBuilder();
            
            if (inBuffer)
            {
                ClearBuffer();
                SetStepMode(StepModes.Buffer);   
            }

            // Tag name can be provided via a server transition so we have to check for that
            LookaheadForTransitionClient(Sides.Client);
            
            // First char in a proper HTML tag (after opening <) can be [_, !, /, Alpha, ?]
            if (!(Peek() == '_' || Peek() == '!' || Peek() == '?' || Peek() == '/' || IsAlpha(Peek())))
            {
                char chr = Peek();
                string lexeme = GetCurrentLexeme();
                
                Throw("First char after < in an opening HTML tag must be _, ! or alpha");
            }
            else
            {
                char chr = Step();
                sb.Append(chr);
            }

            // The next few chars represent element's name
            while (!IsAtEnd() && (IsAlphaNumeric(Peek())))
            {
                bool shouldContinue = LookaheadForTransitionServerSide();
                if (shouldContinue)
                {
                    continue;
                }
                
                char chr = Step();
                sb.Append(chr);
            }

            if (IsAtEnd())
            {
                //Throw("Unclosed HTML tag at the end of file");
            }

            if (inBuffer)
            {
                string tagName = GetBuffer();
            
                AddBufferToCurrentLexeme();
                ClearBuffer();
                SetStepMode(StepModes.CurrentLexeme);
            
                return tagName;   
            }

            return sb.ToString();
        }

        bool LookaheadForClosingTag()
        {
            return Peek() == '>' || (Peek() == '/' && Peek(2) == '>');
        }
        
        bool ParseHtmlTag(string? parentTagName)
        {
            ParseWhitespaceAndNewlines(Sides.Client);

            if (Peek() != '<')
            {
                return false;
            }
            
            HtmlElement el = new HtmlElement() {CharFrom = pos};
            char chr = Step(); // has to be <
            string tagName = ParseHtmlTagName(false);

            ParseWhitespaceAndNewlines(Sides.Client);
            
            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }
                
                bool shouldEnd = LookaheadForAttributeOrClose(tagName, false, el);
                if (shouldEnd)
                {
                    break;
                }
            }
            
            return true;
        }
        
        bool LookaheadForAttributeOrClose(string tagName, bool startsFromClosingTag, HtmlElement el)
        {
            if (LookaheadForClosingTag())
            {
                CloseTag(tagName, startsFromClosingTag, el);
                return true;
            }
            
            return ParseAttribute(tagName, startsFromClosingTag, el); 
        }
        
        string ParseAttributeName()
        {
            StringBuilder sb = new StringBuilder();
            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }

                if (Peek() == '=' || LookaheadForClosingTag())
                {
                    return sb.ToString();
                }

                sb.Append(Step());
            }

            return sb.ToString();
        }

        string ParseAttributeValue()
        {
            HtmlAttrEnclosingModes closeMode = HtmlAttrEnclosingModes.None;
            StringBuilder sb = new StringBuilder();
            
            if (Peek() == '\'')
            {
                closeMode = HtmlAttrEnclosingModes.SingleQuote;
                Step();
            }
            else if (Peek() == '\"')
            {
                closeMode = HtmlAttrEnclosingModes.DoubleQuote;
                Step();
            }

            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }
                
                if (IsWhitespaceOrNewline(Peek()) && closeMode == HtmlAttrEnclosingModes.None)
                {
                    return sb.ToString();
                }

                if (Peek() == '\'' && closeMode == HtmlAttrEnclosingModes.SingleQuote)
                {
                    Step();
                    return sb.ToString();
                }
                
                if (Peek() == '"' && closeMode == HtmlAttrEnclosingModes.DoubleQuote)
                {
                    Step();
                    return sb.ToString();
                }

                sb.Append(Step());
            }

            return sb.ToString();
        }
           
        
        bool ParseAttribute(string tagName, bool startsFromClosingTag, HtmlElement el)
        {
            string name = ParseAttributeName();

            if (Peek() == '=')
            {
                Step();
                string val = ParseAttributeValue();
            }
            
            if (LookaheadForClosingTag())
            {
                CloseTag(tagName, startsFromClosingTag, el);
                return true;
            }
            
            return false;
        }
        
        bool CloseTag(string tagName, bool startsFromClosingTag, HtmlElement el)
        {
            if (tagName == "html")
            {
                string g = "";
            }
            
            if (Peek() == '/' && Peek(2) == '>')
            {
                Step();
                Step();
            }
            else if (Peek() == '>')
            {
                Step();
            }

            bool parseContent = false;
            string tagText = GetCurrentLexeme();
            
            if (!startsFromClosingTag)
            {
                bool isSelfClosing = IsSelfClosingHtmlTag(tagName);
                bool isSelfClosed = CurrentLexemeIsSelfClosedHtmlTag();
                parseContent = !isSelfClosed && !isSelfClosing;
        
                if (parseContent)
                {
                    if (tagText == "<text>") // "<text>" has a special meaning only when exactly matched. Any modification like "<text >" will be rendered as a normal tag
                    {
                        DiscardCurrentLexeme();
                        ParseHtmlOrPlaintextUntilClosingTag(tagName, el);
                    }
                    else if (tagName is "script" or "style") // raw text elements
                    {
                        ParsePlaintextUntilClosingTag(tagName);
                        ParseHtmlClosingTag(tagName, el);
                    }
                    else
                    {
                        ParseHtmlOrPlaintextUntilClosingTag(tagName, el);
                    }
                }
            }

            string s = GetCurrentLexeme();

            if (s.Contains("</body>"))
            {
                string h = "";
            }
            
            if (startsFromClosingTag && tagName == "/text" && s.Trim() == "</text>" && source?.Substring(el.CharFrom, 6) == "<text>") // <text>
            {
                DiscardCurrentLexeme();
                return parseContent;
            }
            
            AddToken(TokenTypes.Text);
            return parseContent;   
        }

            // parser has to be positioned at opening < of the closing tag
        string ParseHtmlClosingTag(string openingTagName, HtmlElement el)
        {
            ParseWhitespaceAndNewlines(Sides.Client);
            if (Peek() != '<')
            {
                return "";
            }

            char chr = Step(); // has to be <
            string closingTagName = ParseHtmlTagName(false);

            //ParseUntilBalancedChar('<', '>', true, true, true);
 
            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }
                
                bool shouldEnd = LookaheadForAttributeOrClose(closingTagName, true, el);
                if (shouldEnd)
                {
                    break;
                }
            }
            
            if ($"/{openingTagName}" != closingTagName)
            {
                // [todo] end tag does not match opening tag
                // we will be graceful but a warning can be emmited
            }

            return closingTagName;
        }

        void ParsePlaintextUntilClosingTag(string openingTagName)
        {
            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }
                
                if (Peek() == '<' && Peek(2) == '/' && PeekRange(3, openingTagName.Length) == openingTagName) // && IsWhitespaceOrNewline(Peek(4 + openingTagName.Length))
                {
                    break;
                }
                
                Step();
            }
        }
        
        void ParseHtmlOrPlaintextUntilClosingTag(string openingTagName, HtmlElement el)
        {
            AddToken(TokenTypes.Text);
            string s = GetCurrentLexeme();
            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }

                shouldContinue = LookaheadForHtmlComment(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }
                
                if (Peek() == '<')
                {
                    if (Peek(2) == '/' && IsHtmlTagOpeningChar(Peek(3)))
                    {
                        string closingName = ParseHtmlClosingTag(openingTagName, el);
                        if (string.Equals($"/{openingTagName}", closingName, StringComparison.InvariantCultureIgnoreCase))
                        {
                            AddToken(TokenTypes.Text);
                            return;
                        }
                    }
  
                    if (IsHtmlTagOpeningChar(Peek(2)))
                    {
                        ParseHtmlTag(openingTagName);
                        continue;
                    }
                }

                Step();
            }

            s = GetCurrentLexeme();
        }

        bool LookaheadForHtmlComment(Sides currentSide)
        {
            if (Peek() == '<' && Peek(2) == '!')
            {
                if (Peek(3) == '-' && Peek(4) == '-')
                {
                    ParseHtmlComment(HtmlCommentModes.DoubleHyphen, currentSide);
                    return true;
                }

                if (Peek(3) == '[')
                {
                    ParseHtmlComment(HtmlCommentModes.Cdata, currentSide);
                    return true;
                }
            }

            return false;
        }

        void ParseHtmlComment(HtmlCommentModes openCommentMode, Sides currentSide)
        {
            if (currentSide == Sides.Server)
            {
                AddToken(TokenTypes.BlockExpr);
            }
            
            if (openCommentMode == HtmlCommentModes.DoubleHyphen)
            {
                StepN(4); // <!--
            }
            else if (openCommentMode == HtmlCommentModes.Cdata) // <![
            {
                StepN(3);
            }

            if (Peek() == '-' && Peek(2) == '-' && Peek(3) == '>') // -->
            {
                StepN(3);
                return;
            }

            if (openCommentMode == HtmlCommentModes.Cdata && Peek() == ']' && Peek(2) == ']' && Peek(3) == '>') // ]]>
            {
                StepN(3);
                return;
            } 

            Step();
        }
}