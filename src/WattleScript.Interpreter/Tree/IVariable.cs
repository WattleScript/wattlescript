
using WattleScript.Interpreter.Tree.Expressions;

namespace WattleScript.Interpreter.Tree
{
	interface IVariable
	{
		void CompileAssignment(Execution.VM.FunctionBuilder bc, Operator op,  int stackofs, int tupleidx);
		
		bool IsAssignment { get; }
	}
}
