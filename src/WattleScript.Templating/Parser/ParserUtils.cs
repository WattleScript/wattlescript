using System.Runtime.InteropServices;

namespace WattleScript.Templating;

internal partial class Parser
{
    private Dictionary<TokenTypes, Action> AddTokenActions = new Dictionary<TokenTypes, Action>();

    internal enum HtmlAttrEnclosingModes
    {
        Unknown,
        None,
        SingleQuote,
        DoubleQuote
    }

    internal enum HtmlCommentModes
    {
        DoubleHyphen, // <!-- -->
        Cdata // <![ can end with ]]> or --> 
    }

    internal enum HtmlTagParsingModes
    {
        Native,
        TagHelper
    }
    
    void ClearPooledBuilder()
    {
        PooledStringBuilder.Clear();
    }
    
    bool IsAtEnd()
    {
        return pos >= source.Length;
    }
    
    string GetCurrentLexeme()
    {
        return currentLexeme.ToString();
    }

    bool IsAlphaNumeric(char ch)
    {
        return IsDigit(ch) || IsAlpha(ch);
    }

    bool IsHtmlTagChar(char ch)
    {
        return IsAlphaNumeric(ch) || ch == ':' || ch == '-';
    }

    bool IsAlpha(char ch)
    {
        return char.IsLetter(ch) || ch is '_';
    }

    bool IsDigit(char ch)
    {
        return ch is >= '0' and <= '9';
    }

    bool IsWhitespaceOrNewline(char ch)
    {
        return ch is ' ' or '\n' or '\r' or '\t' or '\f';
    }
    
    char Step(int i = 1)
    {
        if (source == null)
        {
            return ' ';
        }
        
        if (pos >= source.Length)
        {
            return ' ';
        }
        
        char cc = source[pos];

        if (stepMode == StepModes.CurrentLexeme)
        {
            currentLexeme.Append(cc);    
        }
        else
        {
            Buffer.Append(cc);
        }
        
        col += i;
        pos += i;
        c = cc;

        if (pos >= source.Length)
        {
            pos = source.Length;
        }

        if (cc == '\n')
        {
            col = 1;
            line++;
        }
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !IsAtEnd())
        {
            if (cc == '\r' && Peek() == '\n')
            {
                return Step();
            }   
        }
        
        return cc;
    }
    
    void SetAddTokenAction(TokenTypes tokenType, Action action)
    {
        if (AddTokenActions.ContainsKey(tokenType))
        {
            AddTokenActions[tokenType] = action;
        }
        else
        {
            AddTokenActions.Add(tokenType, action);
        }
    }

    private int storedPos;
    void StorePos()
    {
        storedPos = pos;
        storedCol = col;
        storedLine = line;
        storedLexeme = currentLexeme.ToString();
    }

    void RestorePos()
    {
        pos = storedPos;
        if (source != null && pos >= source.Length)
        {
            pos = source.Length - 1;
        }
        
        storedPos = 0;
        if (source != null)
        {
            c = source[pos];
        }
        
        col = storedCol;
        line = storedLine;
        DiscardCurrentLexeme();
        currentLexeme.Append(storedLexeme);
    }

    string? PeekRange(int from, int length)
    {
        return source?.Substring(pos + from - 1, length);
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

        if (source != null && source.Length <= peekedPos)
        {
            return source[^1];
        }

        return source?[peekedPos] ?? char.MaxValue;
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
        return IsSelfClosing(htmlTag.ToLowerInvariant().Replace("!", ""));
    }

    bool AddTokenSplitRightTrim(TokenTypes lhsType, TokenTypes rhsType)
    {
        string str = GetCurrentLexeme();
        string lhs = str.TrimEnd();
        
        int dif = str.Length - lhs.Length;
        bool any = false;
        
        if (lhs.Length > 0)
        {
            currentLexeme.Clear();
            currentLexeme.Append(lhs);
            any = AddToken(lhsType);   
        }

        if (dif > 0)
        {
            string rhs = str.Substring(str.Length - dif);
            currentLexeme.Clear();
            currentLexeme.Append(rhs);
            bool any2 = AddToken(rhsType);

            if (!any)
            {
                any = any2;
            }
        }

        return any;
    }
    
    bool AddToken(TokenTypes type)
    {
        if (tagParsingMode == HtmlTagParsingModes.TagHelper)
        {
            return false;
        }
        
        if (currentLexeme.Length == 0)
        {
            return false;
        }

        if (AddTokenActions.ContainsKey(type))
        {
            AddTokenActions[type].Invoke();
            AddTokenActions.Remove(type);
        }

        Token token = new Token(type, GetCurrentLexeme(), lastCommitedLine + 1, line + 1, lastCommitedPos + 1, pos + 1);
        Tokens.Add(token);
        DiscardCurrentLexeme();

        lastCommitedPos = pos;
        lastCommitedLine = line;
        return true;
    }

    void DiscardCurrentLexeme()
    {
        currentLexeme.Clear();
    }

    void AddBufferToCurrentLexeme()
    {
        currentLexeme.Append(Buffer);
    }

    void FatalIfInBlock(string message)
    {
        Exception e = new TemplatingEngineException(line, col, pos, message, source ?? "");
        
        if (parsingBlock)
        {
            fatalExceptions.Add(e);   
        }
        else
        {
            recoveredExceptions.Add(e);   
        }
    } 

    bool IsSelfClosing(string tagName)
    {
        return tagName is "area" 
            or "base" 
            or "br" 
            or "col" 
            or "embed" 
            or "hr" 
            or "img" 
            or "input" 
            or "keygen" 
            or "link" 
            or "menuitem" 
            or "meta" 
            or "param" 
            or "source" 
            or "track" 
            or "wbr"
            or "doctype";
    }
    
    void ClearBuffer(bool start)
    {
        Buffer.Clear();

        if (start)
        {
            storedCol = col;
            storedLine = line;
            storedC = c;
            storedPos = pos;
        }
        else
        {
            col = storedCol;
            line = storedLine;
            c = storedC;
            pos = storedPos;
        }
    }

    void SetStepMode(StepModes mode)
    {
        stepMode = mode;
    }

    bool Throw(string message)
    {
        throw new TemplatingEngineException(line, col, pos, message, source ?? "");
    }

    void SetParsingControlChars(bool enabled)
    {
        parsingTransitionCharactersEnabled = enabled;
    }

    bool ParsingControlChars()
    {
        return parsingTransitionCharactersEnabled;
    }

    bool StepEol()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (Peek() == '\n' && LastStoredCharMatches('\r'))
            {
                Step();
                return true;
            }   
        }

        return false;
    }

    void DiscardTokensAfter(int n)
    {
        Tokens = Tokens.GetRange(0, n);
    }
}