using System.ComponentModel;
namespace WattleScript.Templating;

internal enum TokenTypes
{
    [Description("BLOCK")]
    BlockExpr,
    [Description("EXPLICIT")]
    ExplicitExpr,
    [Description("IMPLICIT")]
    ImplicitExpr,
    [Description("TEXT")]
    Text,
    [Description("COMMENT")]
    Comment,
    [Description("EOF")]
    Eof,
    Length
}
    
internal class Token
{
    public TokenTypes Type { get; set; }
    public string Lexeme { get; set; }
    public int FromLine { get; set; }
    public int ToLine { get; set; }
    public int StartCol { get; set; }
    public int EndCol { get; set; }

    public Token(TokenTypes type, string lexeme, int fromLine, int toLine, int startCol, int endCol)
    {
        Type = type;
        Lexeme = lexeme;
        FromLine = fromLine;
        ToLine = toLine;
        StartCol = startCol;
        EndCol = endCol;
    }

    public override string ToString()
    {
        return $"{Type.ToDescriptionString()} [ln {FromLine}-{ToLine}, col {StartCol}-{EndCol}] - {Lexeme}";
    }
}