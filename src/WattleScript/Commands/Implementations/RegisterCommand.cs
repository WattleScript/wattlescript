﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Commands.Implementations
{
	class RegisterCommand : ICommand
	{
		public string Name => "register";

		public void DisplayShortHelp(Script context)
		{
			Console.WriteLine("register [type] - register a CLR type or prints a list of registered types");
		}

		public void DisplayLongHelp(Script context)
		{
			Console.WriteLine("register [type] - register a CLR type or prints a list of registered types. Use makestatic('type') to make a static instance.");
		}

		public void Execute(Script context, string argument)
		{
			if (argument.Length > 0)
			{
				Type t = Type.GetType(argument);
				if (t == null)
					Console.WriteLine("Type {0} not found.", argument);
				else
					UserData.RegisterType(t);
			}
			else
			{
				foreach (var type in UserData.GetRegisteredTypes())
				{
					Console.WriteLine(type.FullName);
				}
			}
		}
	}
}
