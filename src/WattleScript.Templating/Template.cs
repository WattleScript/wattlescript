﻿using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Templating;

public class Template
{
    string EncodeJsString(string s)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append("\"");
        foreach (char c in s)
        {
            switch (c)
            {
                case '\"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                default:
                    int i = (int)c;
                    if (i < 32 || i > 127)
                    {
                        sb.AppendFormat("\\u{0:X04}", i);
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }
        sb.Append("\"");

        return sb.ToString();
    }

    List<Token> Optimise(List<Token>? tokens)
    {
        if (tokens == null) // if we have no tokens or only one we can't merge
        {
            return new List<Token>();
        }

        if (tokens.Count <= 1)
        {
            return tokens;
        }
        
        int i = 0;
        Token token = tokens[i];

        while (true)
        {
            i++;
            if (i > tokens.Count - 1)
            {
                break;
            } 
            
            Token nextToken = tokens[i];
            if (token.Type == nextToken.Type)
            {
                token.Lexeme += nextToken.Lexeme;
                tokens.RemoveAt(i);
                i--;
                continue;
            }
            
            // move to next token
            token = tokens[i];
        }
        
        return tokens;
    }
    
    public string Render(Script script, string code, bool optimise)
    {
        Tokenizer tk = new Tokenizer(script);
        List<Token> tokens = tk.Tokenize(code);

        StringBuilder sb = new StringBuilder();
        bool firstClientPending = true;

        if (optimise)
        {
            tokens = Optimise(tokens);
        }
        
        foreach (Token tkn in tokens)
        {
            switch (tkn.Type)
            {
                case TokenTypes.ClientText:
                {
                    string lexeme = tkn.Lexeme;
                    if (firstClientPending)
                    {
                        lexeme = lexeme.TrimStart();
                        firstClientPending = false;
                    }
                
                    sb.AppendLine($"stdout({EncodeJsString(lexeme)})");
                    break;
                }
                case TokenTypes.BlockExpr:
                    sb.AppendLine(tkn.Lexeme);
                    break;
                case TokenTypes.ImplicitExpr:
                case TokenTypes.ExplicitExpr:
                    sb.AppendLine($"stdout({tkn.Lexeme})");
                    break;
                case TokenTypes.ServerComment:
                    sb.AppendLine($"/*{tkn.Lexeme}*/");
                    break;
            }
        }

        string finalText = sb.ToString();
        return finalText;
    }
}