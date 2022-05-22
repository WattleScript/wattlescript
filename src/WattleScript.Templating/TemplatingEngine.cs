using System.Text;
using WattleScript.Interpreter;
using WattleScript.Interpreter.Execution.VM;

namespace WattleScript.Templating;

public class TemplatingEngine
{
    private readonly StringBuilder pooledSb = new StringBuilder();
    private readonly TemplatingEngineOptions options;
    internal readonly Script script;
    private StringBuilder stdOut = new StringBuilder();
    private StringBuilder stdOutTagHelper = new StringBuilder();
    public readonly List<TagHelper> tagHelpers;
    internal Dictionary<string, TagHelper> tagHelpersMap = new Dictionary<string, TagHelper>();
    private StringBuilder stdOutTagHelperTmp = new StringBuilder();
    private Parser? parser;
    private Table? tagHelpersSharedTbl;

    public TemplatingEngine(TemplatingEngine parent, Table? tbl)
    {
        options = parent.options;
        script = parent.script;
        tagHelpers = parent.tagHelpers;

        stdOut = parent.stdOut;
        stdOutTagHelper = parent.stdOutTagHelper;
        stdOutTagHelperTmp = parent.stdOutTagHelperTmp;

        tagHelpersSharedTbl = tbl;
        
        SharedSetup();
    }

    public TemplatingEngine(Script script, TemplatingEngineOptions? options = null, List<TagHelper>? tagHelpers = null)
    {
        options ??= TemplatingEngineOptions.Default;
        this.options = options;
        this.script = script ?? throw new ArgumentNullException(nameof(script));
        this.tagHelpers = tagHelpers ?? new List<TagHelper>();

        SharedSetup();
    }

    void SharedSetup()
    {
        MapTagHelpers();
        SetSpecials();
    }

    void SetSpecials()
    {
        script.Globals["stdout_line"] = PrintLine;
        script.Globals["stdout"] = Print;
        script.Globals["render_tag_content"] = RenderTagContent;
    }

    void MapTagHelpers()
    {
        foreach (TagHelper th in tagHelpers)
        {
            if (tagHelpersMap.ContainsKey(th.Name.ToLowerInvariant()))
            {
                // [todo] err, tag helper duplicate name
            }

            tagHelpersMap.TryAdd(th.Name.ToLowerInvariant(), th);
        }
    }

    DynValue RenderTagContent(Script s, CallbackArguments args)
    {
        if (args.Count > 0)
        {
            Table? tbl= null;
            
            if (parser != null)
            {
                tbl = script.Globals.Get("__tagData").Table;
                parser.tagHelpersSharedTable = tbl;
            }
            
            DynValue arg = args[0];
            string str = arg.String;
            string transpiled = new TemplatingEngine(this, tbl).Transpile(str);

            stdOutTagHelperTmp.Clear();
            script.Globals["stdout"] = PrintTaghelperTmp;
            script.DoString(transpiled);

            string output = stdOutTagHelperTmp.ToString();
            stdOutTagHelper.Append(output);
            
            script.Globals["stdout"] = Print;
            return DynValue.NewString(output);
        }
        
        return DynValue.Nil;
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
                token.Lexeme.Append(nextToken.Lexeme);
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
        
        parser = new Parser(this, script, tagHelpersSharedTbl);
        List<Token> tokens = parser.Parse(code, "");
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

    public string Transpile(string code, string friendlyName = "")
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }
        
        parser = new Parser(this, script, tagHelpersSharedTbl);
        List<Token> tokens = parser.Parse(code, friendlyName);

        string str = Transform(tokens);
        return str;
    }

    internal string Transform(List<Token> tokens)
    {
        StringBuilder sb = new StringBuilder();
        bool firstClientPending = true;

        if (options.Optimise)
        {
            tokens = Optimise(tokens);
        }
        
        foreach (Token tkn in tokens)
        {
            if (options.RunMode == TemplatingEngineOptions.RunModes.Debug)
            {
                sb.AppendLine($"#line {tkn.FromLine}, {tkn.StartCol}");
            }

            switch (tkn.Type)
            {
                case TokenTypes.Text:
                {
                    string lexeme = tkn.Lexeme.ToString();
                    if (firstClientPending)
                    {
                        lexeme = lexeme.TrimStart();
                        firstClientPending = false;
                    }
                
                    sb.AppendLine($"stdout({EncodeJsString(lexeme)})");
                    break;
                }
                case TokenTypes.BlockExpr:
                    string str = tkn.Lexeme.ToString();
                    if (string.IsNullOrWhiteSpace(str))
                    {
                        continue;
                    }
                    sb.AppendLine(str);
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

    public async Task<DynValue> ParseTagHelper(string code)
    {
        stdOut.Clear();

        string transpiledTemplate = Transpile(code, "tagHelperDefinition");
        DynValue dv = script.LoadString(transpiledTemplate);

        FunctionProto? renderFn = dv.Function.Function.Functions.FirstOrDefault(x => x.Name == "Render");

        if (renderFn == null)
        {
            // [todo] err, mandatory Render() not found
            return dv;
        }

        IReadOnlyList<Annotation>? annots = dv.Function.Annotations;

        if (annots == null)
        {
            // [todo] err, mandatory annot "name" not found
            return dv;
        }
        
        Annotation? nameAnnot = annots.FirstOrDefault(x => x.Name == "name");

        if (nameAnnot == null)
        {
            // [todo] err, mandatory annot "name" not found
            return dv;
        }

        if (nameAnnot.Value.Type != DataType.Table)
        {
            // [todo] err, annot not valid, possibly wrong annot mode is used
            return dv;
        }
        
        Table tbl = nameAnnot.Value.Table;

        if (tbl.Length < 1)
        {
            // [todo] err, annot "name" is empty
            return dv;
        }

        DynValue nameDv = tbl.Values.First();

        if (nameDv.Type != DataType.String)
        {
            // [todo] err, annot "name" is something else than string
            return dv;
        }

        string name = nameDv.String;

        if (string.IsNullOrWhiteSpace(name))
        {
            // [todo] err, annot "name" is empty
            return dv;
        }
        
        tagHelpers.Add(new TagHelper(nameDv.String, transpiledTemplate));

        return dv;
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
        
    public void Print(Script script, CallbackArguments args)
    {
        stdOut.Append(args[0].CastToString());
    }
    
    public void PrintTaghelper(Script script, CallbackArguments args)
    {
        string str = args[0].CastToString();
        stdOutTagHelper.Append(str);
    }
    
    public void PrintTaghelperTmp(Script script, CallbackArguments args)
    {
        string str = args[0].CastToString();
        stdOutTagHelperTmp.Append(str);
    }

    public class RenderResult
    {
        public string Output { get; init; }
        public string Transpiled { get; init; }
    }
}