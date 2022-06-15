using System.Text;
using System.Text.RegularExpressions;
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
    private Dictionary<string, byte[]>? tagHelperHints;
    internal TranspileModes transpileMode = TranspileModes.Run;

    public enum TranspileModes
    {
        Run,
        Dump
    }

    internal enum TranspileModesExt
    {
        None,
        DumpRecursive
    }
    
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

    public TemplatingEngine(Script script, TemplatingEngineOptions? options = null, List<TagHelper>? tagHelpers = null, Dictionary<string, byte[]>? tagHelperHints = null)
    {
        options ??= TemplatingEngineOptions.Default;
        this.options = options;
        this.script = script ?? throw new ArgumentNullException(nameof(script));
        this.tagHelpers = tagHelpers ?? new List<TagHelper>();
        this.tagHelperHints = tagHelperHints;
        
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

    DynValue RenderTagContentDumped(Script s, CallbackArguments args)
    {
        if (args.Count > 0)
        {
            DynValue ctx = args[0];
            if (ctx.IsNotNil() && ctx.Type == DataType.Function)
            {
                ctx.Function.Call();
            }
        }
        
        return DynValue.Void;
    }
    
    DynValue RenderTagContent(Script s, CallbackArguments args)
    {
        if (args.Count > 0)
        {
            Table? tbl = null;

            if (parser != null)
            {
                tbl = script.Globals.Get("__tagData").Table;
                parser.tagHelpersSharedTable = tbl;
            }

            DynValue arg = args[0];
            string str = arg.String;
            string transpiled = new TemplatingEngine(this, tbl).Transpile(str, transpileMode);

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

        parser = new Parser(this, script, tagHelpersSharedTbl, null);
        List<Token> tokens = parser.Parse(code, transpileMode, "");
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

    internal string TranspileRecursive(string code, Dictionary<TagHelper, bool> loadedTagHelpers)
    {
        Parser locParser = new Parser(this, script, tagHelpersSharedTbl, tagHelperHints);
        List<Token> tokens = locParser.Parse(code, TranspileModes.Dump, "", TranspileModesExt.DumpRecursive, loadedTagHelpers);

        string str = Transform(tokens);
        return str;
    }
    
    public string Transpile(string code, TranspileModes mode = TranspileModes.Run, string friendlyName = "")
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return "";
        }

        parser = new Parser(this, script, tagHelpersSharedTbl, tagHelperHints);
        List<Token> tokens = parser.Parse(code, mode, friendlyName);

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

    DynValue ParseTagHelperAsFunc(string code, Script sc)
    {
        string transpiledTemplate = Transpile(code, transpileMode, "tagHelperDefinition");
        DynValue dv = script.LoadString(transpiledTemplate);

        foreach (FunctionProto? fn in dv.Function.Function.Functions)
        {
            var annot = fn.Annotations?.FirstOrDefault(x => x.Name == "tagHelper");
            
            if (annot == null)
            {
                continue;
            }
            
            string snip = fn.GetSourceCode(transpiledTemplate);
            string[] snipLines = snip.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            List<string> outLines = new List<string>();
            foreach (string snipLine in snipLines)
            {
                if (!snipLine.StartsWith("#line"))
                {
                    outLines.Add(snipLine);
                }
            }

            snip = string.Join("\n", outLines); // get rid of generated #line

            string proxyName = $"__tagHelper_{Guid.NewGuid().ToString().Replace("-", "")}";

            tagHelpers.Add(new TagHelper(annot.Value.Table.Values.First().String, snip, fn.Name));
        }
        
        return dv;
    }

    public void ParseTagHelper(string code, Script sc)
    {
        stdOut.Clear();
        ParseTagHelperAsFunc(code, sc);
    }

    public async Task<Dictionary<string, byte[]>> GetAfterRenderHints()
    {
        Dictionary<string, byte[]> dict = new Dictionary<string, byte[]>();

        if (parser != null)
        {
            if (parser.pendingTemplateParts.Count > 0)
            {
                foreach (KeyValuePair<string, DynValue> pair in parser.pendingTemplateParts)
                {
                    await using MemoryStream ms = new MemoryStream();
                    script.Dump(pair.Value, ms);
                    byte[] arr = ms.ToArray();
                    
                    dict.Add(pair.Key, arr);
                }
            }
        }

        return dict;
    }

    public byte[] Dump(string code, Table? globalContext = null, string? friendlyCodeName = null, bool writeSourceRefs = true)
    {
        transpileMode = TranspileModes.Dump;
        stdOut.Clear();

        string transpiledTemplate = Transpile(code, transpileMode);

        DynValue dv = script.LoadString(transpiledTemplate, globalContext, friendlyCodeName);

        using MemoryStream ms = new MemoryStream();
        script.Dump(dv, ms, writeSourceRefs);
        return ms.ToArray();
    }
    
    public async Task<RenderResult> Render(byte[] bytecode, Table? globalContext = null, string? friendlyCodeName = null)
    {
        transpileMode = TranspileModes.Run;
        stdOut.Clear();
        
        script.Globals["render_tag_content"] = RenderTagContentDumped;

        using MemoryStream ms = new MemoryStream(bytecode);
        await script.DoStreamAsync(ms, globalContext, friendlyCodeName);
        string htmlText = stdOut.ToString();

        return new RenderResult() {Output = htmlText, Transpiled = ""};
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