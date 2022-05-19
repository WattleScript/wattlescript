﻿using System.Text;
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
    private int col;
    private int storedLine;
    private int storedCol;
    private char storedC;
    private StringBuilder currentLexeme = new StringBuilder();
    private int line = 1;
    private Script? script;
    private StepModes stepMode = StepModes.CurrentLexeme;
    private bool parsingTransitionCharactersEnabled = true;
    private Document document;
    private List<HtmlElement?> openElements = new List<HtmlElement?>();
    private bool parsingBlock = false;
    private List<Exception> fatalExceptions = new List<Exception>();
    private List<Exception> recoveredExceptions = new List<Exception>();
    private TemplatingEngine engine;
    private HtmlTagParsingModes tagParsingMode = HtmlTagParsingModes.Native;
    internal Table tagHelpersSharedTable;
    private string friendlyName;

    public Parser(TemplatingEngine engine, Script? script, Table? tagHelpersSharedTable)
    {
        this.engine = engine;
        this.script = script;
        this.tagHelpersSharedTable = tagHelpersSharedTable ?? new Table(this.script);
        document = new Document();

        KeywordsMap = new Dictionary<string, Func<bool>?>
        {
            { "if", ParseKeywordIf },
            { "for", ParseKeywordFor },
            { "while", ParseKeywordWhile },
            { "do", ParseKeywordDo },
            { "function", ParseKeywordFunction },
            { "switch", ParseKeywordSwitch },
            { "else", ParseKeywordInvalidElse },
            { "elseif", ParseKeywordInvalidElseIf },
        };
    }
    
    public List<Token> Parse(string templateSource, string friendlyName)
    {
        source = templateSource;
        this.friendlyName = friendlyName;
        ParseClient();

        if (IsAtEnd())
        {
            AddToken(TokenTypes.Eof);
        }

        HandleUnrecoverableErrors();
        return Tokens;
    }

    void HandleUnrecoverableErrors()
    {
        if (fatalExceptions.Count > 0)
        {
            throw fatalExceptions[0];
        }
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
                    continue;
                }
                
                if (c == '"')
                {
                    HandleStringSequence('"');
                    continue;
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
        
        if (Peek() == '@' && Peek(2) == ':')
        {
            AddToken(TokenTypes.BlockExpr);
            Step();
            Step();
            DiscardCurrentLexeme();
            ParseRestOfLineAsClient();
            return true;
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
    
    void ParseRestOfLineAsServer()
    {
        char chr = Step();
        while (!IsAtEnd() && chr != '\n' && chr != '\r')
        {
            chr = Step();
        }
        
        AddToken(TokenTypes.BlockExpr);
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

                Step();
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
            
            switch (c)
            {
                case '{':
                    Step();
                    ParseCodeBlock(false, false);
                    break;
                case '(':
                    Step();
                    DiscardCurrentLexeme();
                    ParseExplicitExpression();
                    break;
                case ':' when currentSide == Sides.Client:
                    Step();
                    DiscardCurrentLexeme();
                    ParseRestOfLineAsClient();
                    break;
                case '!' when currentSide == Sides.Client && NextNonWhiteSpaceCharMatches('{', 1):
                    Step();
                    DiscardCurrentLexeme();
                    SetParsingControlChars(false);
                    ParseCodeBlock(false, false);
                    SetParsingControlChars(true);
                    break;
                case '#':
                    Step();
                    ParseRestOfLineAsServer();
                    break;
                default:
                {
                    if (IsAlpha(c))
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

                    break;
                }
            }

            return false;
        }

        ImplicitExpressionTypes Str2ImplicitExprType(string str)
        {
            if (AllowedTransitionKeywords.Contains(str))
            {
                return ImplicitExpressionTypes.AllowedKeyword;
            }

            return BannedTransitionKeywords.Contains(str) ? ImplicitExpressionTypes.BannedKeyword : ImplicitExpressionTypes.Literal;
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
            parsingBlock = true;
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

            int lastPos = pos;
            
            while (!IsAtEnd())
            {
                ParseUntilHtmlOrClientTransition();
                StorePos();
                bool matchedClosingBrk = MatchNextNonWhiteSpaceNonNewlineChar('}');

                if (matchedClosingBrk)
                {
                    break;
                }
                
                RestorePos();

                // Safety measure. If something unexpected goes wrong we could deadlock here
                if (lastPos == pos)
                {
                    Throw("Internal parser error (infinite loop detected). Please open an issue with the template you are parsing here - https://github.com/WattleScript/wattlescript. We are sorry for the inconvenience.");
                }
                
                lastPos = pos;
            }
            
            if (!keepClosingBrk && currentLexeme.Length > 0)
            {
                RemoveLastCharFromCurrentLexeme();
            }
            
            AddToken(TokenTypes.BlockExpr);
            parsingBlock = false;
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

                if (LastStoredCharMatches(1, '\\')) // check that string symbol is not escaped
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
                        AddToken(TokenTypes.BlockExpr);
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
                        continue;
                    }
                    
                    if (Peek() == '"')
                    {
                        HandleStringSequence('"');
                        continue;
                    }
                    
                    if (Peek() == '`')
                    {
                        HandleStringSequence('`');
                        continue;
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
                    
                    if (Peek() == '<')
                    {
                        if (IsHtmlTagOpeningChar(Peek(2)))
                        {
                            if (allowHtml || LastStoredCharNotWhitespaceMatches('\n', '\r', ';'))
                            {
                                StepEol();
                                string ss = GetCurrentLexeme();
                                AddTokenSplitRightTrim(TokenTypes.BlockExpr, TokenTypes.Text);
                                ParseHtmlTag(null);
                            
                                allowHtml = true;
                                continue;
                            }   
                        }
                        else if (Peek(2) == '/')
                        {
                            // closing tag without opening is fatal
                            ParseHtmlClosingTag("", null, false);
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

                GetCurrentLexeme();
                Step();
                allowHtml = false;
            }
        }

        string ParseHtmlTagName(bool inBuffer, int offset = 1)
        {
            StringBuilder sb = new StringBuilder();
            
            if (inBuffer)
            {
                ClearBuffer(true);
                SetStepMode(StepModes.Buffer);   
            }
            else
            {
                SetAddTokenAction(TokenTypes.Text, () =>
                {
                    // Next text block to add will be tag's name
                    // If starting with ! and not doctype or comment (html/cdata), ommit the !
                    string str = GetCurrentLexeme();
                    if (str.TrimStart().StartsWith("<!"))
                    {
                        string subStr = str.TrimStart().Substring(2).ToLowerInvariant(); // skip <!, normalize

                        if (!subStr.StartsWith("doctype ") && !subStr.StartsWith("doctype>") && subStr != "-" && !subStr.StartsWith("--") && !subStr.StartsWith("["))
                        {
                            DiscardCurrentLexeme();
                            currentLexeme.Append(str.ReplaceFirst("!", ""));
                        }
                    }
                });
            }

            if (offset > 1 && inBuffer)
            {
                pos += offset - 1;
            }

            // Tag name can be provided via a server transition so we have to check for that
            LookaheadForTransitionClient(Sides.Client);
            
            // First char in a proper HTML tag (after opening <) can be [_, !, /, Alpha, ?]
            if (!(Peek() == '_' || Peek() == '!' || Peek() == '?' || Peek() == '/' || IsAlpha(Peek())))
            {
                Throw("First char after < in an  HTML tag must be _, !, / or alpha");
            }
            else
            {
                char chr = Step();
                sb.Append(chr);
            }

            // The next few chars represent element's name
            while (!IsAtEnd() && IsHtmlTagChar(Peek()))
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }
                
                char chr = Step();
                sb.Append(chr);
            }

            if (inBuffer)
            {
                string tagName = GetBuffer();

                AddBufferToCurrentLexeme();
                ClearBuffer(false);
                SetStepMode(StepModes.CurrentLexeme);

                return tagName;
            }
            
            return sb.ToString();
        }

        bool LookaheadForClosingTag()
        {
            return Peek() == '>' || (Peek() == '/' && Peek(2) == '>');
        }
        
        bool ParseHtmlTag(HtmlElement? parentElement)
        {
            ParseWhitespaceAndNewlines(Sides.Client);
            AddToken(TokenTypes.Text);
            
            if (Peek() != '<')
            {
                return false;
            }
            
            HtmlElement el = new HtmlElement() {CharFrom = pos, Line = line, Col = col};
            char chr = Step(); // has to be <
            string tagName = ParseHtmlTagName(false);
            el.Name = tagName;
            openElements.Push(el);

            if (tagParsingMode == HtmlTagParsingModes.Native && engine.tagHelpersMap.ContainsKey(tagName.ToLowerInvariant()))
            {
                return ParseTagHelper(el);
            }
            
            ParseWhitespaceAndNewlines(Sides.Client);

            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }
                
                bool shouldEnd = LookaheadForAttributeOrClose(tagName, false, el, true);
                if (shouldEnd)
                {
                    break;
                }
            }
            
            return true;
        }

        int ScanForElementClosingTag(HtmlElement el)
        {
            ParseHtmlOrPlaintextUntilClosingTag(el.Name, el);

            return el.ContentTo;
        }

        bool ParseTagHelper(HtmlElement el)
        {
            // parser is located after name in a tag
            // 0. the process starts the same as with native tag helpers
            DiscardCurrentLexeme();
            tagParsingMode = HtmlTagParsingModes.TagHelper;

            // 1. parse until end of opening tag-helper tag
            ParseWhitespaceAndNewlines(Sides.Client);
            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }
                
                bool shouldEnd = LookaheadForAttributeOrClose(el.Name, false, el, false);
                if (shouldEnd)
                {
                    break;
                }
            }

            // 2. if tag-helper is not self closing, scan for end of its content
            if (el.Closing != HtmlElement.ClosingType.SelfClosing)
            {
                el.ContentTo = ScanForElementClosingTag(el);   
            }

            DynValue tagHelpersDataDv = DynValue.NewTable(tagHelpersSharedTable);
            
            Table ctxTable = new Table(engine.script);
            ctxTable.Set("data", tagHelpersDataDv);

            TagHelper helper = engine.tagHelpersMap[el.Name.ToLowerInvariant()];
            int contentFrom = el.ContentFrom;
            int contentTo = el.ContentTo;
            string contentStr = contentTo > 0 ? source.Substring(contentFrom, contentTo - contentFrom) : "";
            
            engine.script.Globals.Set("__tagData", tagHelpersDataDv);
            ctxTable.Set("content", DynValue.NewString(contentStr));

            Table attrTable = new Table(engine.script);
            foreach (HtmlAttribute attr in el.Attributes)
            {
                attrTable.Set(attr.Name, DynValue.NewString(attr.Value));
            }
            
            DynValue fAttrTable = DynValue.NewTable(attrTable);
            ctxTable.Set("attributes", fAttrTable);
            
            // 3. before resolving tag helper, we need to resolve the part of template currently transpiled
            string pendingTemplate = engine.Transform(Tokens);
            
            DynValue pVal = engine.script.LoadString(pendingTemplate);
            engine.script.Call(pVal);
            
            Tokens.Clear();

            engine.script.DoString(helper.Template);
            engine.script.Globals.Get("Render").Function.Call(ctxTable);
            engine.script.Globals["stdout"] = engine.Print;
            
            // tag output is already in stdout
            tagParsingMode = HtmlTagParsingModes.Native;
            
            return true;
        }
        
        bool LookaheadForAttributeOrClose(string tagName, bool startsFromClosingTag, HtmlElement? el, bool parseContent)
        {
            if (LookaheadForClosingTag())
            {
                CloseTag(tagName, startsFromClosingTag, el, parseContent);
                return true;
            }
            
            return ParseAttribute(tagName, startsFromClosingTag, el, parseContent); 
        }
        
        string ParseAttributeName()
        {
            ParseWhitespaceAndNewlines(Sides.Client);
            
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

        Tuple<string, HtmlAttrEnclosingModes> ParseAttributeValue()
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
                    return new Tuple<string, HtmlAttrEnclosingModes>(sb.ToString(), HtmlAttrEnclosingModes.None);
                }

                if (Peek() == '\'' && closeMode == HtmlAttrEnclosingModes.SingleQuote)
                {
                    Step();
                    return new Tuple<string, HtmlAttrEnclosingModes>(sb.ToString(), HtmlAttrEnclosingModes.SingleQuote);
                }
                
                if (Peek() == '"' && closeMode == HtmlAttrEnclosingModes.DoubleQuote)
                {
                    Step();
                    return new Tuple<string, HtmlAttrEnclosingModes>(sb.ToString(), HtmlAttrEnclosingModes.DoubleQuote);
                }

                sb.Append(Step());
            }

            return new Tuple<string, HtmlAttrEnclosingModes>(sb.ToString(), HtmlAttrEnclosingModes.Unknown);
        }
           
        
        bool ParseAttribute(string tagName, bool startsFromClosingTag, HtmlElement el, bool parseTagContent)
        {
            string name = ParseAttributeName();

            if (Peek() == '=')
            {
                Step();
                Tuple<string, HtmlAttrEnclosingModes> val = ParseAttributeValue();
                el.Attributes.Add(new HtmlAttribute(name, val.Item1, val.Item2));
            }
            
            if (LookaheadForClosingTag())
            {
                CloseTag(tagName, startsFromClosingTag, el, parseTagContent);
                return true;
            }
            
            return false;
        }
        
        bool CloseTag(string tagName, bool startsFromClosingTag, HtmlElement? el, bool parseTagContent)
        {
            if (Peek() == '/' && Peek(2) == '>')
            {
                Step();
                Step();

                if (el != null)
                {
                    el.Closing = HtmlElement.ClosingType.SelfClosing;   
                }
            }
            else if (Peek() == '>')
            {
                Step();

                if (el != null)
                {
                    el.Closing = HtmlElement.ClosingType.EndTag;
                }
            }

            bool parseContent = false;
            string tagText = GetCurrentLexeme();

            if (el != null)
            {
                if (!startsFromClosingTag)
                {
                    el.ContentFrom = pos;
                }
            }
            
            if (tagParsingMode == HtmlTagParsingModes.TagHelper)
            {
                DiscardCurrentLexeme();
            }
            
            if (!parseTagContent)
            {
                return true;
            }

            if (!startsFromClosingTag)
            {
                bool isSelfClosing = IsSelfClosingHtmlTag(tagName);
                bool isSelfClosed = CurrentLexemeIsSelfClosedHtmlTag();
                bool startsWithSlash = tagName.StartsWith('/');
                
                parseContent = !isSelfClosed && !isSelfClosing && !startsWithSlash;

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
                        ParseHtmlClosingTag(tagName, el, false);
                    }
                    else
                    {
                        ParseHtmlOrPlaintextUntilClosingTag(tagName, el);
                    }
                }
                else
                {
                    HtmlElement? ell = openElements.Peek();
                    if (ell == el)
                    {
                        openElements.Pop();
                    } 
                }
            }

            string s = GetCurrentLexeme();
            
            if (startsFromClosingTag && tagName == "/text" && s.Trim() == "</text>" && el != null && source?.Substring(el.CharFrom, 6) == "<text>") // <text>
            {
                DiscardCurrentLexeme();
                return parseContent;
            }

            if (tagParsingMode == HtmlTagParsingModes.Native)
            {
                AddToken(TokenTypes.Text);   
            }
            else
            {
                DiscardCurrentLexeme();
            }
            
            return parseContent;   
        }

        // parser has to be positioned at opening < of the closing tag
        string ParseHtmlClosingTag(string openingTagName, HtmlElement? el, bool inBuffer)
        {
            ParseWhitespaceAndNewlines(Sides.Client);
            if (Peek() != '<')
            {
                return "";
            }

            char chr = Step(); // has to be <
            string closingTagName = ParseHtmlTagName(inBuffer);

            //ParseUntilBalancedChar('<', '>', true, true, true);
 
            while (!IsAtEnd())
            {
                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }
                
                bool shouldEnd = LookaheadForAttributeOrClose(closingTagName, true, el, true);
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
                
                if (Peek() == '<' && Peek(2) == '/')
                {
                    if (PeekRange(3, openingTagName.Length) == openingTagName)
                    {
                        break;   
                    }
                }
                
                Step();
            }
        }
        
        void ParseHtmlOrPlaintextUntilClosingTag(string openingTagName, HtmlElement el)
        {
            AddToken(TokenTypes.Text);

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

                if (el.Recovery)
                {
                    AddToken(TokenTypes.Text);
                    return;
                }
                
                if (Peek() == '<')
                {
                    if (Peek(2) == '/' && IsHtmlTagOpeningChar(Peek(3)))
                    {
                        AddToken(TokenTypes.Text);
                        
                        string closingNameLookahead = ParseHtmlTagName(true, 3); // skip </ and parse name in buffer
                        DiscardCurrentLexeme(); // parsed name is added to lexeme, discard
                        
                        HtmlElement? openEl = openElements.Peek();
                        
                        if (string.Equals(openingTagName, closingNameLookahead, StringComparison.InvariantCultureIgnoreCase))
                        {
                            el.ContentTo = pos;
                            if (tagParsingMode == HtmlTagParsingModes.TagHelper)
                            {
                                StorePos();
                            }

                            string closingNameParsed = ParseHtmlClosingTag(openingTagName, el, false);
                            AddToken(TokenTypes.Text);
                            openElements.Pop();
                            return;
                        }
                        
                        // recovery: close top item on stack
                        if (openElements.Count > 0)
                        {
                            // [todo]
                            // here two actions can take place based on the context
                            // 1) peeked element is superfluous and doesn't have an opening element -> <div></option></div>
                            // 2) peeked element encloses another element that is already in opened elements -> <div><a></a></div>
                            if (openElements.FirstOrDefault(x => string.Equals(x?.Name, closingNameLookahead, StringComparison.InvariantCultureIgnoreCase)) == null)
                            {
                                ParseHtmlTag(el);   
                            }
                            else
                            {
                                HtmlElement? rel = openElements.Pop();
                                if (rel != null)
                                {
                                    rel.Recovery = true;   
                                }
                            }
                        }
                        
                        AddToken(TokenTypes.Text);
                        return;
                    }
  
                    if (IsHtmlTagOpeningChar(Peek(2)))
                    {
                        ParseHtmlTag(el);
                        continue;
                    }
                }

                Step();
            }

            if (parsingBlock)
            {
                FatalIfInBlock($"Unclosed element {el.Name} at line {el.Line}, {el.Col}. Parser could not recover from this error.");
            }
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
            int startLine = line;
            int startCol = col;
            
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

            while (!IsAtEnd())
            {
                if (Peek() == '-' && Peek(2) == '-' && Peek(3) == '>') // -->
                {
                    StepN(3);
                    AddToken(TokenTypes.Text);
                    return;
                }

                if (openCommentMode == HtmlCommentModes.Cdata && Peek() == ']' && Peek(2) == ']' && Peek(3) == '>') // ]]>
                {
                    StepN(3);
                    AddToken(TokenTypes.Text);
                    return;
                }

                bool shouldContinue = LookaheadForTransitionClient(Sides.Client);
                if (shouldContinue)
                {
                    continue;
                }

                Step();
            }
            
            // [todo] error, unclosed html comment
            FatalIfInBlock($"Unclosed HTML comment at line {startLine}, {startCol}. Parser could not recover from this error.");
        }
}