using System.Text;

namespace WattleScript.Templating;

public sealed class TemplatingEngineException : Exception
{
    private int Line { get; init; }
    private int Col { get; init; }
    private string Snippet { get; init; }
    private string Decor { get; init; }
    
    public TemplatingEngineException(int line, int col, int pos, string message, string rawSource) : base(message)
    {
        Tuple<string, bool> snippet = rawSource.Snippet(pos, 40); // show full text for now, might be more useful
        
        Line = line;
        Col = col;
        Snippet = rawSource;

        int decorCol = col;

        int insertPos = Snippet.IndexOf('\n', pos);
        if (insertPos > -1 && Snippet.Length > insertPos + 1)
        {
            if (Snippet[insertPos + 1] == '\r')
            {
                insertPos++;
            }
        }
        if (insertPos == -1)
        {
            insertPos = Snippet.Length;
            decorCol = 0;
        }

        if (pos >= rawSource.Length - 1)
        {
            decorCol = 0;
            insertPos = Snippet.Length;
        }
        
        StringBuilder decorBuilder = new StringBuilder();
        for (int i = 0; i < decorCol; i++)
        {
            decorBuilder.Append('-');
        }

        decorBuilder.Append('^');
        Decor = decorBuilder.ToString();

        Snippet = Snippet.Insert(insertPos, $"\n{Decor}");
    }

    public string FormatedMessage => $"{Snippet}\nLine {Line}, col {Col}: {Message}";
}