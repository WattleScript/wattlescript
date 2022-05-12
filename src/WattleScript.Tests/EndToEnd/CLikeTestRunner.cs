using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace WattleScript.Interpreter.Tests;

public class CLikeTestRunner
{
    private const string ROOT_FOLDER = "EndToEnd/CLike";

    static string[] GetTestCases()
    {
        string[] files = Directory.GetFiles(ROOT_FOLDER, "*.lua*", SearchOption.AllDirectories);

        return files;
    }
    
    [Test, TestCaseSource(nameof(GetTestCases))]
    public async Task RunThrowErros(string path)
    {
        await RunCore(path);
    }

    //[Test, TestCaseSource(nameof(GetTestCases))]
    public async Task RunReportErrors(string path)
    {
        await RunCore(path, true);
    }
    
    public async Task RunCore(string path, bool reportErrors = false)
    {
        string outputPath = path.Replace(".lua", ".txt");

        if (!File.Exists(outputPath))
        {
            Assert.Inconclusive($"Missing output file for test {path}");
            return;
        }

        string code = await File.ReadAllTextAsync(path);
        string output = await File.ReadAllTextAsync(outputPath);
        StringBuilder stdOut = new StringBuilder();

        Script script = new Script(CoreModules.Preset_HardSandbox);
        script.Options.DebugPrint = s => stdOut.AppendLine(s);
        script.Options.IndexTablesFrom = 0;
        script.Options.AnnotationPolicy = new CustomPolicy(AnnotationValueParsingPolicy.ForceTable);
        script.Globals["CurrentLine"] = (ScriptExecutionContext c, CallbackArguments a) => {
            return c.CallingLocation.FromLine;
        };
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
            await script.DoStringAsync(code);
            return;
        }

        try
        {
            DynValue dv = script.LoadString(code);
            IReadOnlyList<Annotation> annots = dv.Function.Annotations;
            await script.CallAsync(dv);

            DynValue dv2 = script.Globals.Get("f");
            
            Assert.AreEqual(output.Trim(), stdOut.ToString().Trim(), $"Test {path} did not pass.");

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