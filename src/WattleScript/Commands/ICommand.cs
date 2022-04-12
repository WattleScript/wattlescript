using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace WattleScript.Commands
{
	interface ICommand
	{
		string Name { get; }
		void DisplayShortHelp();
		void DisplayLongHelp();
		void Execute(ShellContext context, string argument);
	}
}
