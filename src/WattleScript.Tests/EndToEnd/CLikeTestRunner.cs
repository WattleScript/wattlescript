using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.Tests;

public class CLikeTestRunner
{
    private const string ROOT_FOLDER = "EndToEnd/CLike";
    private const bool TEST_SOURCE_REFS_DUMP = true;
    
    StringBuilder stdOut = new StringBuilder();

    static string[] GetTestCases()
    {
        string[] files = Directory.GetFiles(ROOT_FOLDER, "*.lua*", SearchOption.AllDirectories);

        return files;
    }
    
    [Test, TestCaseSource(nameof(GetTestCases))]
    public async Task RunThrowErrors(string path)
    {
        await RunCore(path);
    }

    //[Test, TestCaseSource(nameof(GetTestCases))]
    public async Task RunReportErrors(string path)
    {
        await RunCore(path, true);
    }
    
    Script InitScript()
    {
        Script script = new Script(CoreModules.Preset_SoftSandboxWattle);
        script.Options.IndexTablesFrom = 0;
        script.Options.Syntax = ScriptSyntax.Wattle;
        script.Options.AnnotationPolicy.AnnotationParsingPolicy = AnnotationValueParsingPolicy.ForceTable;
        script.Options.InstructionLimit = 100_000_000;
        script.Options.DebugPrint = s => stdOut.AppendLine(s);
        script.Options.IndexTablesFrom = 0;
        script.Options.AnnotationPolicy = new CustomPolicy(AnnotationValueParsingPolicy.ForceTable);
        script.Globals["CurrentLine"] = (ScriptExecutionContext c, CallbackArguments a) => c.CallingLocation.FromLine;
        script.Globals["CurrentColumn"] = (ScriptExecutionContext c, CallbackArguments a) => c.CallingLocation.FromChar;

        return script;
    }
    
    public async Task RunCore(string path, bool reportErrors = false)
    {
        string outputPath = path.Replace(".lua", ".txt");

        if (!File.Exists(outputPath) && !path.Contains("-invalid"))
        {
            Assert.Inconclusive($"Missing output file for test {path}");
            return;
        }

        string code = await File.ReadAllTextAsync(path);
        string output = File.Exists(outputPath) ? await File.ReadAllTextAsync(outputPath) : "";
        stdOut = new StringBuilder();

        Script script = InitScript();
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
            await script.DoStringAsync(code);
            return;
        }

        try
        {
            DynValue dv = script.LoadString(code);
            IReadOnlyList<Annotation> annots = dv.Function.Annotations;
            await script.CallAsync(dv);
            
            Assert.AreEqual(output.Trim(), stdOut.ToString().Trim(), $"Test {path} did not pass.");

            if (TEST_SOURCE_REFS_DUMP)
            {
                Exception e = TestSourceRefsBinDump(code);
                if (e != null)
                {
                    Assert.Fail($"Dumped source refs not equal to original.\n{e.Message}\n{e.StackTrace}");
                }
            }
            
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

            if (e is ScriptRuntimeException se)
            {
                Assert.Fail($"Test {path} did not pass.\nMessage: {se.DecoratedMessage}\n{e.StackTrace}");
                return;
            }
            Assert.Fail($"Test {path} did not pass.\nMessage: {e.Message}\n{e.StackTrace}");
        }
    }
    
    public Exception TestSourceRefsBinDump(string code)
    {
        Script sc = InitScript();
        DynValue dv = sc.LoadString(code);
        IReadOnlyList<SourceRef> originalSourceRefs = dv.Function.Function.SourceRefs;

        using MemoryStream ms = new MemoryStream();
        sc.Dump(dv, ms);
        ms.Seek(0, SeekOrigin.Begin);
        sc = InitScript();
        dv = sc.LoadStream(ms);
        dv.Function.Call();

        IReadOnlyList<SourceRef> dumpedSourceRefs = dv.Function.Function.SourceRefs;
        Assert.AreEqual(originalSourceRefs.Count, dumpedSourceRefs.Count, "Dumped different number of source refs");

        for (int i = 0; i < originalSourceRefs.Count; i++)
        {
            SourceRef original = originalSourceRefs[i];
            SourceRef dumped = dumpedSourceRefs[i];

            if (original == null && dumped == null)
            {
                continue;
            }

            if (original == null != (dumped == null))
            {
                Assert.Fail($"Dumped source ref {i} isNull differs from original");
            }

            Assert.That(original.Equals(dumped), $"Dumped source ref {i} differs from original");
        }

        return null;
    }
}