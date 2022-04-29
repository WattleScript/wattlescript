﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using WattleScript.Templating;

namespace WattleScript.Interpreter.Tests.Templating;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class TemplatingTestsRunner
{
    private const string ROOT_FOLDER = "Templating/Tests";

    static string[] GetTestCases()
    {
        string[] files = Directory.GetFiles(ROOT_FOLDER, "*.wthtml*", SearchOption.AllDirectories);
        return files;
    }

    [OneTimeSetUp]
    public async Task Init()
    {
        foreach (string path in GetTestCases().Where(x => x.Contains("TagDefinitions")))
        {
            string code = await File.ReadAllTextAsync(path);
            
            Script script = new Script(CoreModules.Preset_HardSandbox);
            script.Options.IndexTablesFrom = 0;
            script.Options.AnnotationPolicy = new CustomPolicy(AnnotationValueParsingPolicy.ForceTable);
            script.Options.Syntax = ScriptSyntax.WattleScript;
            script.Options.Directives.Add("using");

            TemplatingEngine tmp = new TemplatingEngine(script);

            try
            {
                await tmp.ParseTagHelper(code);
            }
            catch (Exception e)
            {
                Assert.Fail($"Error parsing tag helper definition\nPath: {path}\nMessage: {e.Message}\nStacktrace: {e.StackTrace}");
            }
        }
    }
    
    [Test, TestCaseSource(nameof(GetTestCases))]
    public async Task RunThrowErros(string path)
    {
        await RunCore(path);
    }
    
    public async Task RunCore(string path, bool reportErrors = false)
    {
        string outputPath = path.Replace(".wthtml", ".html");

        if (outputPath.Contains("TagDefinitions"))
        {
            Assert.Pass($"Tag definition (test skipped)");
            return;
        }
        
        if (!File.Exists(outputPath))
        {
            Assert.Inconclusive($"Missing output file for test {path}");
            return;
        }

        string code = await File.ReadAllTextAsync(path);
        string output = await File.ReadAllTextAsync(outputPath);

        Script script = new Script(CoreModules.Preset_HardSandbox);
        script.Options.IndexTablesFrom = 0;
        script.Options.AnnotationPolicy = new CustomPolicy(AnnotationValueParsingPolicy.ForceTable);
        script.Options.Syntax = ScriptSyntax.WattleScript;
        script.Options.Directives.Add("using");

        TemplatingEngine tmp = new TemplatingEngine(script);
        TemplatingEngine.RenderResult rr = null;
        
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
            rr = await tmp.Render(code);
            return;
        }

        try
        {
            rr = await tmp.Render(code);
            Assert.AreEqual(output, rr.Output, $"Test {path} did not pass.");

            if (path.ToLowerInvariant().Contains("invalid"))
            {
                Assert.Fail("Expected to crash but 'passed'");
            }
            
            string debugStr = tmp.Debug(code);
        }
        catch (Exception e)
        {
            if (path.ToLowerInvariant().Contains("invalid"))
            {
                Assert.Pass($"Crashed as expected: {e.Message}");
                return;
            }
            
            if (e is AssertionException ae)
            {
                Assert.Fail($"Test {path} did not pass.\nMessage: {ae.Message}\n{ae.StackTrace}\nParsed template:\n{rr?.Transpiled ?? ""}");
                return;
            }

            Assert.Fail($"Test {path} did not pass.\nMessage: {e.Message}\n{e.StackTrace}");
        }
    }
}