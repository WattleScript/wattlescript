using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Commands.Implementations
{
	class ExitCommand : ICommand
	{
		public string Name => "exit";

		public void DisplayShortHelp(Script context)
		{
			Console.WriteLine("exit - Exits the interpreter");
		}

		public void DisplayLongHelp(Script context)
		{
			Console.WriteLine("exit - Exits the interpreter");
		}

		public void Execute(Script context, string arguments)
		{
			Environment.Exit(0);
		}
	}
}
