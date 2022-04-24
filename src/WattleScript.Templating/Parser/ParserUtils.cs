using System.Runtime.InteropServices;

namespace WattleScript.Templating;

internal partial class Parser
{
    internal enum HtmlAttrEnclosingModes
    {
        None,
        SingleQuote,
        DoubleQuote
    }

    internal enum HtmlCommentModes
    {
        DoubleHyphen, // <!-- -->
        Cdata // <![ can end with ]]> or --> 
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
        
        pos += i;
        c = cc;

        if (pos >= source.Length)
        {
            pos = source.Length;
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

    private int storedPos;
    void StorePos()
    {
        storedPos = pos;
    }

    void RestorePos()
    {
        pos = storedPos;
        if (pos >= source.Length)
        {
            pos = source.Length - 1;
        }
        
        storedPos = 0;
        c = source[pos];
        DiscardCurrentLexeme();
    }

    string PeekRange(int from, int length)
    {
        return source.Substring(pos + from - 1, length);
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

        if (source.Length <= peekedPos)
        {
            return source[^1];
        }

        return source[peekedPos];
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

    bool AddToken(TokenTypes type)
    {
        if (currentLexeme.Length == 0)
        {
            return false;
        }

        if (currentLexeme.ToString() == "\r\n</html>")
        {
            string str = "";
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
    
    void ClearBuffer()
    {
        Buffer.Clear();
    }

    void SetStepMode(StepModes mode)
    {
        stepMode = mode;
    }

    bool Throw(string message)
    {
        throw new Exception(message);
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
                return false;
            }   
        }

        return false;
    }
}