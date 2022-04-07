using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests;

public class CLikeTestRunner
{
    private const string ROOT_FOLDER = "EndToEnd/CLike";

    static string[] GetTestCases()
    {
        string[] files = Directory.GetFiles(ROOT_FOLDER, "*.lua*", SearchOption.AllDirectories);

        return files;
    }

    [Test, TestCaseSource(nameof(GetTestCases))]
    public async Task Run(string path)
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
        
        if (path.Contains("SyntaxCLike"))
        {
            script.Options.Syntax = ScriptSyntax.CLike;
        }

        if (path.Contains("flaky"))
        {
            Assert.Inconclusive($"Test {path} marked as flaky");
            return;
        }

        try
        {
            await script.DoStringAsync(code);
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