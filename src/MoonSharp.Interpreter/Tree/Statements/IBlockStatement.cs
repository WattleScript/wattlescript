using MoonSharp.Interpreter.Debugging;

namespace MoonSharp.Interpreter.Tree.Statements
{
    interface IBlockStatement
    {
        SourceRef End { get; }
    }
}