using System.IO;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MoonSharp.Interpreter.Tests;

public class CLikeTestRunner
{
    private const string ROOT_FOLDER = "CLike";
    
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
        
        Script script = new Script();
        script.Options.DebugPrint = s => stdOut.AppendLine(s);
        script.Options.IndexTablesFrom = 0;
        
        await script.DoStringAsync(code);
        
        Assert.AreEqual(output.Trim(), stdOut.ToString().Trim(), $"Test {path} did not pass.");
    }
}