using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using NUnit.Framework;
using WattleScript.Interpreter.Loaders;
using WattleScript.Templating;

namespace WattleScript.Interpreter.Tests.Templating;

class TemplatingTestsScriptLoader : ScriptLoaderBase
{
    public override bool ScriptFileExists(string name)
    {
        return true;
    }

    public override object LoadFile(string file, Table globalContext)
    {
        return "fn = () => 'Hello world'";
    }

    protected override string ResolveModuleName(string modname, string[] paths)
    {
        return modname;
    }
}

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TemplatingTestsRunner
{
    private const bool COMPILE_TAG_HELPERS = true;
    private const bool RUN_SLOW_TESTS = false;
    
    private const string ROOT_FOLDER = "Templating/Tests";
    private static Filter filter = Filter.Tests;
    private List<TagHelper> tagHelpers = new List<TagHelper>();
    
    public static string Snippet(string str, int pivot, int n)
    {
        int expectedStart = pivot - n;
        int realStart = Math.Max(0, str.Length > expectedStart ? expectedStart : str.Length);
        int expectedLen = 2 * n;
        int realLen = Math.Max(str.Length - realStart > expectedLen ? expectedLen : str.Length - realStart, 0);

        return str.Substring(realStart, realLen);
    }

    enum Filter
    {
        Tests,
        TagDefinitions
    }
    
    static int strDifIndex(string s1, string s2)
    {
        int index = 0;
        int min = Math.Min(s1.Length, s2.Length);
        
        while (index < min && s1[index] == s2[index])
        {
            index++;
        }

        return index == min && s1.Length == s2.Length ? -1 : index;
    }

    static string[] GetTestCases()
    {
        string[] files = Directory.GetFiles(ROOT_FOLDER, "*.wthtml*", SearchOption.AllDirectories);

        if (filter == Filter.TagDefinitions)
        {
            return files.Where(x => x.Contains("TagDefinitions")).ToArray();
        }

        if (filter == Filter.Tests)
        {
            return files.Where(x => !x.Contains("TagDefinitions")).ToArray();
        }
        
        return Array.Empty<string>();
    }
    
    [WattleScriptUserData]
    class HtmlModule
    {
        public static DynValue Encode(ScriptExecutionContext context, CallbackArguments args)
        {
            return DynValue.NewString(HttpUtility.HtmlEncode(args[0].String));
        }
        
        public static DynValue Raw(ScriptExecutionContext context, CallbackArguments args)
        {
            return DynValue.NewString(args[0].String);
        }
    }

    [OneTimeSetUp]
    public async Task Init()
    {
        UserData.RegisterAssembly(Assembly.GetAssembly(typeof(HtmlModule)));

        if (!COMPILE_TAG_HELPERS)
        {
            return;
        }
        
        filter = Filter.TagDefinitions;
        foreach (string path in GetTestCases())
        {
            string code = await File.ReadAllTextAsync(path);
            
            Script script = new Script(CoreModules.Preset_SoftSandboxWattle | CoreModules.LoadMethods);
            script.Options.IndexTablesFrom = 0;
            script.Options.AnnotationPolicy = new CustomPolicy(AnnotationValueParsingPolicy.ForceTable);
            script.Options.Syntax = ScriptSyntax.Wattle;
            script.Options.Directives.Add("using");

            HtmlModule htmlModule = new HtmlModule();
            script.Globals["Html"] = htmlModule;

            TemplatingEngine tmp = new TemplatingEngine(script);
            
            try
            {
                tmp.ParseTagHelper(code, script);
                tagHelpers.AddRange(tmp.tagHelpers);
            }
            catch (Exception e)
            {
                Assert.Fail($"Error parsing tag helper definition\nPath: {path}\nMessage: {e.Message}\nStacktrace: {e.StackTrace}");
            }
        }

        filter = Filter.Tests;
    }

    [Test, TestCaseSource(nameof(GetTestCases))]
    public async Task RunThrowErros(string path)
    {
        await RunCore(path);
    }
    
    public async Task RunCore(string path, bool reportErrors = false, bool runningFromDump = false)
    {
        if (path.Contains("TagHintsGenerated"))
        {
            return;
        }
        
        string outputPath = path.Replace(".wthtml", ".html");

        if (!File.Exists(outputPath))
        {
            Assert.Inconclusive($"Missing output file for test {path}");
            return;
        }
        
        string testName = Path.GetFileName(path);
        string currentDir = Directory.GetParent(Environment.CurrentDirectory)?.Parent?.Parent?.FullName;
        
        string code = await File.ReadAllTextAsync(path);
        string output = await File.ReadAllTextAsync(outputPath);

        Script script = new Script(CoreModules.Preset_SoftSandboxWattle | CoreModules.LoadMethods);
        script.Options.IndexTablesFrom = 0;
        script.Options.AnnotationPolicy = new CustomPolicy(AnnotationValueParsingPolicy.ForceTable);
        script.Options.Syntax = ScriptSyntax.Wattle;
        script.Options.Directives.Add("using");
        script.Options.ScriptLoader = new TemplatingTestsScriptLoader();
        
        HtmlModule htmlModule = new HtmlModule();
        script.Globals["Html"] = htmlModule;

        Dictionary<string, byte[]> hintsDict = new Dictionary<string, byte[]>();
        /*if (runningFromDump)
        {
            string[] hints = Directory.GetFiles($"{currentDir}\\Templating\\Tests\\TagHelpers\\TagHintsGenerated\\{testName}");
            foreach (string hint in hints)
            {
                if (!hint.Contains(".txt"))
                {
                    continue;
                }

                string hintText = await File.ReadAllTextAsync(hint);
                byte[] hintDump = await File.ReadAllBytesAsync(hint.Replace(".txt", ".bin"));

                if (!hintsDict.ContainsKey(hintText))
                {
                    hintsDict.Add(hintText, hintDump);
                }
            }
        }*/
        
        TemplatingEngine tmp = new TemplatingEngine(script, null, tagHelpers, hintsDict);
        TemplatingEngine.RenderResult rr = null;

        if (path.Contains("slow"))
        {
            if (!RUN_SLOW_TESTS)
            {
                Assert.Pass($"Test {path} skipped due to being marked as 'slow' and RUN_SLOW_TESTS set to 'false'");
                return;
            }
        }

        if (path.Contains("flaky"))
        {
            Assert.Inconclusive($"Test {path} marked as flaky");
            return;
        }
        
        if (path.Contains("SyntaxCLike"))
        {
            script.Options.Syntax = ScriptSyntax.Wattle;
        }

        if (reportErrors)
        {
            script.Options.ParserErrorMode = ScriptOptions.ParserErrorModes.Report;
            await tmp.Render(code);
            return;
        }

        try
        {
            if (!runningFromDump)
            {
                rr = await tmp.Render(code);
            }
            else
            {
                byte[] dumpedBc = tmp.Dump(code);
                rr = await tmp.Render(dumpedBc);

                if (!string.Equals(output, rr.Output))
                {
                    int difIndex = strDifIndex(output, rr.Output);
                    string diffSnippet = Snippet(rr.Output, difIndex, 50);
                    string expectedSnippet = Snippet(output, difIndex, 50);
                    
                    Assert.Fail($"Test failed from dumped BC, ok when running from string. Output and expected HTML are not equal.\nFirst difference at index: {difIndex}\nOutput near diff: {diffSnippet}\nExpected near diff: {expectedSnippet}\n---------------------- Expected ----------------------\n{output}\n---------------------- But was------------------------\n{rr.Output}\n------------------------------------------------------\n");
                }
            }

            if (string.Equals(output, rr.Output))
            {
                /*if (hints.Any() && !runningFromDump)
                {
                    if (!string.IsNullOrWhiteSpace(currentDir))
                    {
                        Directory.CreateDirectory( $"{currentDir}\\Templating\\Tests\\TagHelpers\\TagHintsGenerated\\{testName}");
                        int i = 0;
                        foreach (KeyValuePair<string, byte[]> hint in hints)
                        {
                            await File.WriteAllTextAsync($"{currentDir}\\Templating\\Tests\\TagHelpers\\TagHintsGenerated\\{testName}\\hint{i}.txt", hint.Key);
                            await File.WriteAllBytesAsync($"{currentDir}\\Templating\\Tests\\TagHelpers\\TagHintsGenerated\\{testName}\\hint{i}.bin", hint.Value);

                            i++;
                        }   
                    }

                    await RunCore(path, false, true);
                }*/

                if (!runningFromDump)
                {
                    await RunCore(path, false, true);
                }

                Assert.Pass();
            }
            else
            {
                int difIndex = strDifIndex(output, rr.Output);
                string diffSnippet = Snippet(rr.Output, difIndex, 50);
                string expectedSnippet = Snippet(output, difIndex, 50);
                
                Assert.Fail($"Test failed. Output and expected HTML are not equal.\nFirst difference at index: {difIndex}\nOutput near diff: {diffSnippet}\nExpected near diff: {expectedSnippet}\n---------------------- Expected ----------------------\n{output}\n---------------------- But was------------------------\n{rr.Output}\n------------------------------------------------------\n");
            }

            if (path.ToLowerInvariant().Contains("invalid"))
            {
                Assert.Fail("Expected to crash but 'passed'");
            }
            
        }
        catch (Exception e)
        {
            if (e is SuccessException)
            {
                return;
            }
            
            if (path.ToLowerInvariant().Contains("invalid"))
            {
                if (e is TemplatingEngineException tee)
                {
                    Assert.Pass($"Crashed as expected:\n{tee.FormatedMessage}");
                    return;
                }
                
                Assert.Pass($"Crashed as expected: {e.Message}");
                return;
            }
            
            if (e is AssertionException ae)
            {
                Assert.Fail($"---------------------- Parsed template ----------------------\n{rr?.Transpiled ?? ""}\n---------------------- Stacktrace ----------------------\n{ae.StackTrace}\n");
                return;
            }

            Assert.Fail($"Test {path} did not pass.\nMessage: {e.Message}\n{e.StackTrace}");
        }
    }
}