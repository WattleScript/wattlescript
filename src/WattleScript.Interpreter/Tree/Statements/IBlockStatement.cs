using WattleScript.Interpreter.Debugging;

namespace WattleScript.Interpreter.Tree.Statements
{
    interface IBlockStatement
    {
        SourceRef End { get; }
    }
}