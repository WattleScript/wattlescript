using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Commands
{
	interface ICommand
	{
		string Name { get; }
		void DisplayShortHelp(Script context);
		void DisplayLongHelp(Script context);
		void Execute(Script context, string argument);
	}
}
