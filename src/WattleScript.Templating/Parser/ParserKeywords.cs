using WattleScript.Interpreter;

namespace WattleScript.Templating;

internal partial class Parser
{
    // @else {}
    // parser has to be positioned after else
    bool ParseKeywordInvalidElse()
    {
        return Throw("Found \"@else\". Please remove the leading @. Keywords else, elseif shouldn't be prefixed with @.");
    }
    
    // @elseif {}
    // parser has to be positioned after elseif
    bool ParseKeywordInvalidElseIf()
    {
        return Throw("Found \"@elseif\". Please remove the leading @. Keywords else, elseif shouldn't be prefixed with @.");
    }
    
    // if (expr) {}
    // parser has to be positioned after if, either at opening ( or at whitespace before it
    bool ParseKeywordIf()
    {
        ParseGenericBrkKeywordWithBlock("if");

        bool matchesElse = NextLiteralSkipEmptyCharsMatches("else", Sides.Server) || NextLiteralSkipEmptyCharsMatches("elseif", Sides.Server); // else handles "else if" but we have to check for "elseif" manually
        if (matchesElse)
        {
            ParseKeywordElseOrElseIf();
        }
        
        return false;
    }
    
    // @require "lib"
    // parser has to be positioned after require
    bool ParseKeywordRequire()
    {
        ParseWhitespaceAndNewlines(Sides.Server);
        if (!MatchNextNonWhiteSpaceChar('"'))
        {
            return Throw("Expected \" after @require");
        }

        ParseLiteral(Sides.Server);

        if (!MatchNextNonWhiteSpaceChar('"'))
        {
            return Throw("Expected \" after ident end at @require");
        }

        ParseWhitespaceAndNewlines(Sides.Server);
        
        AddToken(TokenTypes.BlockExpr);
        return true;
    }

    // enum Enum {}
    // parser has to be positioned after "enum", at name Ident
    bool ParseKeywordEnum()
    {
        return ParseGenericKeywordWithIdentBlock("enum");
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
    
    // while (i in a..b) {}
    // parser has to be positioned after "while", either at opening ( or at a whitespace preceding it
    bool ParseKeywordWhile()
    {
        return ParseGenericBrkKeywordWithBlock("while");
    }
    
    // switch (expr) {}
    // parser has to be positioned after "switch", either at opening ( or at a whitespace preceding it
    bool ParseKeywordSwitch()
    {
        return ParseGenericBrkKeywordWithBlock("switch");
    }

    // do {} while ()
    // parser has to be positioned after "do", either at opening { or at a whitespace preceding it
    bool ParseKeywordDo()
    {
        bool doParsed = ParseGenericKeywordWithBlock("do");
        bool matchesWhile = NextLiteralSkipEmptyCharsMatches("while", Sides.Server);
        
        if (matchesWhile)
        {
            ParseWhitespaceAndNewlines(Sides.Server);
            string whileStr = StepN(5);
            bool whileParsed = ParseGenericBrkKeywordWithoutBlock("while");

            if (whileParsed)
            {
                AddToken(TokenTypes.BlockExpr);
            }
        }
        
        return doParsed;
    }

    // function foo() {}
    // parser has to be positioned after "function", either at first ALPHA char of the function's name or whitespace preceding it
    bool ParseKeywordFunction()
    {
        ParseWhitespaceAndNewlines(Sides.Server);
        
        char chr = Peek();
        if (chr == '(')
        {
            return Throw("Missing function's name");
        }

        if (chr == '{')
        {
            return Throw("Missing function's name and signature");
        }
        
        if (!IsAlpha(chr))
        {
            return Throw("First char in function's name has to be an alpha character");
        }

        string fnName = ParseLiteral(Sides.Server);

        if (string.IsNullOrWhiteSpace(fnName))
        {
            return Throw("Missing function's name");
        }

        return ParseGenericBrkKeywordWithBlock("function");
    }

    // directive [a.b.c]?
    // parser has to be positioned after "directive", before optional right hand
    bool ParseDirective()
    {
        ParseWhitespaceAndNewlines(Sides.Server);

        // no right hand or invalid right hand
        if (!IsAlpha(Peek()))
        {
            AddToken(TokenTypes.BlockExpr);
            return true;
        }

        while (!IsAtEnd())
        {
            ParseLiteral(Sides.Server);

            if (Peek() == '.')
            {
                Step();
            }

            if (!IsAlpha(Peek()))
            {
                break;
            }
        }
        
        AddToken(TokenTypes.BlockExpr);
        return true;
    }

    // keyword () {}
    bool ParseGenericBrkKeywordWithBlock(string keyword)
    {
        bool openBrkMatched = MatchNextNonWhiteSpaceChar('(');
     
        if (!openBrkMatched)
        {
            Throw($"Expected ( after {keyword}");
        }
        
        bool endExprMatched = ParseUntilBalancedChar('(', ')', true, true, true);
  
        if (!endExprMatched)
        {
            return false;
        }
        
        ParseCodeBlock(true, true);
        return true;
    }
    
    // keyword IDENT {}
    bool ParseGenericKeywordWithIdentBlock(string keyword)
    {
        ParseWhitespaceAndNewlines(Sides.Client);
        string ident = ParseLiteralStartsWithAlpha(Sides.Client);

        if (string.IsNullOrWhiteSpace(ident)) // ident starts with non Alpha
        {
            return Throw($"Expected enum name starting with Alpha, got {Peek()}");
        }
        
        return ParseGenericKeywordWithBlock(keyword);
    }
    
    // keyword {}
    bool ParseGenericKeywordWithBlock(string keyword)
    {
        bool openBrkMatched = MatchNextNonWhiteSpaceChar('{');

        if (!openBrkMatched)
        {
            Throw($"Expected {{ after {keyword}");
        }
        
        ParseCodeBlock(true, true);
        return true;
    }

    // keyword ()
    bool ParseGenericBrkKeywordWithoutBlock(string keyword)
    {
        bool openBrkMatched = MatchNextNonWhiteSpaceChar('(');
   
        if (!openBrkMatched)
        {
            Throw($"Expected ( after {keyword}");
        }
        
        bool endExprMatched = ParseUntilBalancedChar('(', ')', true, true, true);
        return endExprMatched;
    }

    // else {}
    // or possibly else if () {}
    bool ParseKeywordElseOrElseIf()
    {
        StorePos();
        ParseWhitespaceAndNewlines(Sides.Server);
        string elseStr = StepN(4);
        ParseWhitespaceAndNewlines(Sides.Server);
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
        ParseWhitespaceAndNewlines(Sides.Server);
        string elseStr = StepN(4); // eat else
        ParseWhitespaceAndNewlines(Sides.Server);
        string elseIfStr = StepN(2); // ear if

        return ParseKeywordIf();
    }
    
    // else {}
    bool ParseKeywordElse()
    {
        ParseWhitespaceAndNewlines(Sides.Server);
        //DiscardCurrentLexeme();
        
        string elseStr = StepN(4);

        if (elseStr != "else")
        {
            return false;
        }
        
        string str = GetCurrentLexeme();
        
        ParseCodeBlock(true, true);
        
        return false;
    }
}