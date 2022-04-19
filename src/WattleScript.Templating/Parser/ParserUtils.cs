namespace WattleScript.Templating;

internal partial class Parser
{
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
        return currentLexeme;
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
    
    char Step(int i = 1)
    {
        char cc = source[pos];

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
        if (pos >= source.Length)
        {
            pos = source.Length - 1;
        }
        
        storedPos = 0;
        c = source[pos];
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
        return IsSelfClosing(htmlTag.ToLowerInvariant());
    }

    bool AddToken(TokenTypes type)
    {
        if (currentLexeme.Length == 0)
        {
            return false;
        }

        Token token = new Token(type, currentLexeme, lastCommitedLine + 1, line + 1, lastCommitedPos + 1, pos + 1);
        Tokens.Add(token);
        DiscardCurrentLexeme();

        lastCommitedPos = pos;
        lastCommitedLine = line;
        return true;
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
}