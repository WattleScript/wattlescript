namespace WattleScript.Templating;


public enum TokenTypes
{
    BlockExpr,
    ExplicitExpr,
    ImplicitExpr,
    ClientText,
    ServerComment,
    Eof,
    Length
}
    
public class Token
{
    public TokenTypes Type { get; set; }
    public string Lexeme { get; set; }
    public object Literal { get; set; }
    public int Line { get; set; }

    public Token(TokenTypes type, string lexeme, object literal, int line)
    {
        Type = type;
        Lexeme = lexeme;
        Literal = literal;
        Line = line;
    }

    public override string ToString()
    {
        return $"{Type} - {Lexeme} - {Literal}";
    }
}