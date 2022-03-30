using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using MoonSharp.Interpreter;

namespace Moonsharp.Bench;

[MemoryDiagnoser(false)]
public class Program
{
    [Benchmark]
    public void Scimark()
    {
        Script script = new Script();
        script.DoString(File.ReadAllText("Programs/scimark.lua"));
    }
    
    //[Benchmark]
    public void Heapsort()
    {
        Script script = new Script();
        script.DoString(File.ReadAllText("Programs/heapsort.lua"));
    }
    
    public static void Main(string[] args)
    {
        Summary[]? summary = BenchmarkRunner.Run(typeof(Program).Assembly);
    }
}