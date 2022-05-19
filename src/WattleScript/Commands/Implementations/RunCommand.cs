using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Commands.Implementations
{
	class RunCommand : ICommand
	{
		public string Name => "run";

		public void DisplayShortHelp(Script context)
		{
			Console.WriteLine($"run <filename> - Executes the specified {context.Options.Syntax} script");
		}

		public void DisplayLongHelp(Script context)
		{
			Console.WriteLine($"run <filename> - Executes the specified {context.Options.Syntax} script.");
		}

		public void Execute(Script context, string arguments)
		{
			if (arguments.Length == 0)
			{
				Console.WriteLine("Syntax : !run <file>");
			}
			else
			{
				context.DoFile(arguments);
			}
		}
	}
}
