
namespace WattleScript.Interpreter.Execution
{
	internal interface IClosureBuilder
	{
		SymbolRef CreateUpvalue(BuildTimeScope scope, SymbolRef symbol);

	}
}
