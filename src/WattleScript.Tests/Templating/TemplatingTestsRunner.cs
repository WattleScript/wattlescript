using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using WattleScript.Templating;

namespace WattleScript.Interpreter.Tests.Templating;

public class TemplatingTestsRunner
{
    private const string ROOT_FOLDER = "Templating/Tests";

    static string[] GetTestCases()
    {
        string[] files = Directory.GetFiles(ROOT_FOLDER, "*.wthtml*", SearchOption.AllDirectories);
        return files;
    }
    
    [Test, TestCaseSource(nameof(GetTestCases))]
    public async Task RunThrowErros(string path)
    {
        await RunCore(path);
    }
    
    public async Task RunCore(string path, bool reportErrors = false)
    {
        string outputPath = path.Replace(".wthtml", ".html");

        if (!File.Exists(outputPath))
        {
            Assert.Inconclusive($"Missing output file for test {path}");
            return;
        }

        string code = await File.ReadAllTextAsync(path);
        string output = await File.ReadAllTextAsync(outputPath);
        StringBuilder stdOut = new StringBuilder();

        Template tmp = new Template();
        string transpiled = tmp.Render(code, true);
        
        Script script = new Script(CoreModules.Preset_HardSandbox);
        script.Options.DebugPrint = s => stdOut.AppendLine(s);
        script.Options.IndexTablesFrom = 0;
        script.Options.AnnotationPolicy = new CustomPolicy(AnnotationValueParsingPolicy.ForceTable);
        script.Options.Syntax = ScriptSyntax.WattleScript;

        void PrintLine(Script script, CallbackArguments args)
        {
            stdOut.AppendLine(args[0].CastToString());
        }
        
        void Print(Script script, CallbackArguments args)
        {
            stdOut.Append(args[0].CastToString());
        }
        
        script.Globals["stdout_line"] = PrintLine;
        script.Globals["stdout"] = Print;

        if (path.Contains("flaky"))
        {
            Assert.Inconclusive($"Test {path} marked as flaky");
            return;
        }
        
        if (path.Contains("SyntaxCLike"))
        {
            script.Options.Syntax = ScriptSyntax.WattleScript;
        }

        if (reportErrors)
        {
            script.Options.ParserErrorMode = ScriptOptions.ParserErrorModes.Report;
            await script.DoStringAsync(transpiled);
            return;
        }

        try
        {
            DynValue dv = script.LoadString(transpiled);
            await script.CallAsync(dv);

            Assert.AreEqual(output, stdOut.ToString(), $"Test {path} did not pass.");

            if (path.Contains("invalid"))
            {
                Assert.Fail("Expected to crash but 'passed'");
            }
        }
        catch (Exception e)
        {
            if (e is AssertionException ae)
            {
                Assert.Fail($"Test {path} did not pass.\nMessage: {ae.Message}\n{ae.StackTrace}");
                return;
            }

            if (path.Contains("invalid"))
            {
                Assert.Pass($"Crashed as expected: {e.Message}");
                return;
            }

            Assert.Fail($"Test {path} did not pass.\nMessage: {e.Message}\n{e.StackTrace}");
        }
    }
}