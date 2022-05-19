using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Commands
{
	class HelpCommand : ICommand
	{
		public string Name => "help";

		public void DisplayShortHelp(Script context)
		{
			Console.WriteLine("help [command] - gets the list of possible commands or help about the specified command");
		}

		public void DisplayLongHelp(Script context)
		{
			DisplayShortHelp(context);
		}

		public void Execute(Script context, string arguments)
		{
			if (arguments.Length > 0)
			{
				var cmd = CommandManager.Find(arguments);
				if (cmd != null)
					cmd.DisplayLongHelp(context);
				else
					Console.WriteLine("Command '{0}' not found.", arguments);
			}
			else
			{
				Console.WriteLine($"Type {context.Options.Syntax} code to execute {context.Options.Syntax} code (multilines are accepted)");
				Console.WriteLine("or type one of the following commands to execute them.");
				Console.WriteLine("");
				Console.WriteLine("Commands:");
				Console.WriteLine("");

				foreach (var cmd in CommandManager.GetCommands())
				{
					Console.Write("  !");
					cmd.DisplayShortHelp(context);
				}

				Console.WriteLine("");
			}
		}
	}
}
