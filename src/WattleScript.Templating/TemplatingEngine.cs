using System.Text;
using WattleScript.Interpreter;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Templating;

public class TemplatingEngine
{
    private readonly StringBuilder pooledSb = new StringBuilder();
    private readonly TemplatingEngineOptions options;
    private readonly Script script;
    private StringBuilder stdOut = new StringBuilder();
    internal StringBuilder stdOutTagHelper = new StringBuilder();
    public readonly List<TagHelper> tagHelpers;
    internal Dictionary<string, TagHelper> tagHelpersMap = new Dictionary<string, TagHelper>();
    internal readonly Script tagHelpersScript;

    public TemplatingEngine(Script script, TemplatingEngineOptions? options = null, List<TagHelper>? tagHelpers = null)
    {
        options ??= TemplatingEngineOptions.Default;
        this.options = options;
        this.script = script ?? throw new ArgumentNullException(nameof(script));
        this.tagHelpers = tagHelpers ?? new List<TagHelper>();

        foreach (TagHelper th in this.tagHelpers)
        {
            if (tagHelpersMap.ContainsKey(th.Name.ToLowerInvariant()))
            {
                // [todo] err, tag helper duplicate name
            }

            tagHelpersMap.TryAdd(th.Name.ToLowerInvariant(), th);
        }
        
        script.Globals["stdout_line"] = PrintLine;
        script.Globals["stdout"] = Print;

        tagHelpersScript = new Script(CoreModules.Preset_HardSandbox);
        tagHelpersScript.Options.IndexTablesFrom = 0;
        tagHelpersScript.Options.AnnotationPolicy = new CustomPolicy(AnnotationValueParsingPolicy.ForceTable);
        tagHelpersScript.Options.Syntax = ScriptSyntax.WattleScript;
        tagHelpersScript.Options.Directives.Add("using");
        
        tagHelpersScript.Globals["stdout_line"] = PrintTaghelper;
        tagHelpersScript.Globals["stdout"] = PrintTaghelper;
    }
    
    string EncodeJsString(string s)
    {
        pooledSb.Clear();
        pooledSb.Append('"');
        foreach (char c in s)
        {
            switch (c)
            {
                case '\"':
                    pooledSb.Append("\\\"");
                    break;
                case '\\':
                    pooledSb.Append("\\\\");
                    break;
                case '\b':
                    pooledSb.Append("\\b");
                    break;
                case '\f':
                    pooledSb.Append("\\f");
                    break;
                case '\n':
                    pooledSb.Append("\\n");
                    break;
                case '\r':
                    pooledSb.Append("\\r");
                    break;
                case '\t':
                    pooledSb.Append("\\t");
                    break;
                default:
                    int i = c;
                    if (i is < 32 or > 127)
                    {
                        pooledSb.Append($"\\u{i:X04}");
                    }
                    else
                    {
                        pooledSb.Append(c);
                    }
                    break;
            }
        }
        pooledSb.Append('"');
        return pooledSb.ToString();
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
                token.FromLine = Math.Min(token.FromLine, nextToken.FromLine);
                token.ToLine = Math.Max(token.ToLine, nextToken.ToLine);
                token.StartCol = Math.Min(token.StartCol, nextToken.StartCol);
                token.EndCol = Math.Max(token.EndCol, nextToken.EndCol);
                
                tokens.RemoveAt(i);
                i--;
                continue;
            }
            
            // move to next token
            token = tokens[i];
        }
        
        return tokens;
    }

    public string Debug(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }
        
        Parser parser = new Parser(this, script);
        List<Token> tokens = parser.Parse(code);
        pooledSb.Clear();
        
        if (options.Optimise)
        {
            tokens = Optimise(tokens);
        }

        foreach (Token tkn in tokens)
        {
            pooledSb.AppendLine(tkn.ToString());
        }
        
        string finalText = pooledSb.ToString();
        return finalText;
    }

    public string Transpile(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }
        
        Parser parser = new Parser(this, script);
        List<Token> tokens = parser.Parse(code);

        StringBuilder sb = new StringBuilder();
        bool firstClientPending = true;

        if (options.Optimise)
        {
            tokens = Optimise(tokens);
        }
        
        foreach (Token tkn in tokens)
        {
            switch (tkn.Type)
            {
                case TokenTypes.Text:
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
                case TokenTypes.Comment:
                    sb.AppendLine($"/*{tkn.Lexeme}*/");
                    break;
            }
        }

        string finalText = sb.ToString();
        return finalText;
    }

    public async Task ParseTagHelper(string code)
    {
        stdOut.Clear();

        string transpiledTemplate = Transpile(code);
        DynValue dv = script.LoadString(transpiledTemplate);

        FunctionProto? renderFn = dv.Function.Function.Functions.FirstOrDefault(x => x.Name == "Render");

        if (renderFn == null)
        {
            // [todo] err, mandatory Render() not found
            return;
        }

        IReadOnlyList<Annotation>? annots = dv.Function.Annotations;

        if (annots == null)
        {
            // [todo] err, mandatory annot "name" not found
            return;
        }
        
        Annotation? nameAnnot = annots.FirstOrDefault(x => x.Name == "name");

        if (nameAnnot == null)
        {
            // [todo] err, mandatory annot "name" not found
            return;
        }

        if (nameAnnot.Value.Type != DataType.Table)
        {
            // [todo] err, annot not valid, possibly wrong annot mode is used
            return;
        }
        
        Table tbl = nameAnnot.Value.Table;

        if (tbl.Length < 1)
        {
            // [todo] err, annot "name" is empty
            return;
        }

        DynValue nameDv = tbl.Values.First();

        if (nameDv.Type != DataType.String)
        {
            // [todo] err, annot "name" is something else than string
            return;
        }

        string name = nameDv.String;

        if (string.IsNullOrWhiteSpace(name))
        {
            // [todo] err, annot "name" is empty
            return;
        }
        
        tagHelpers.Add(new TagHelper(nameDv.String, transpiledTemplate));
    }

    public async Task<RenderResult> Render(string code, Table? globalContext = null, string? friendlyCodeName = null)
    {
        stdOut.Clear();

        string transpiledTemplate = Transpile(code);
        await script.DoStringAsync(transpiledTemplate, globalContext, friendlyCodeName);
        string htmlText = stdOut.ToString();
        
        return new RenderResult() {Output = htmlText, Transpiled = transpiledTemplate};
    }
    
    private void PrintLine(Script script, CallbackArguments args)
    {
        stdOut.AppendLine(args[0].CastToString());
    }
        
    private void Print(Script script, CallbackArguments args)
    {
        stdOut.Append(args[0].CastToString());
    }
    
    private void PrintTaghelper(Script script, CallbackArguments args)
    {
        stdOutTagHelper.Append(args[0].CastToString());
    }
    
    public class RenderResult
    {
        public string Output { get; init; }
        public string Transpiled { get; init; }
    }
}