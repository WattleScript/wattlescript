using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using WattleScript.Commands;
using WattleScript.Commands.Implementations;
using WattleScript.Interpreter;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Loaders;
using WattleScript.Interpreter.REPL;
using WattleScript.Interpreter.Serialization;

namespace WattleScript
{
	class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			CommandManager.Initialize();

			Script.DefaultOptions.ScriptLoader = new ReplInterpreterScriptLoader();
			
			bool langLua;
			if (CheckArgs(args, out langLua))
				return;

			Banner(langLua);
			
			Script script = new Script(langLua ? CoreModules.Preset_Complete : CoreModules.Preset_CompleteWattle);
			script.Options.Syntax = langLua ? ScriptSyntax.Lua : ScriptSyntax.WattleScript;
			script.Globals["makestatic"] = (Func<string, DynValue>)(MakeStatic);

			ReplInterpreter interpreter = new ReplInterpreter(script)
			{
				HandleDynamicExprs = true,
				HandleClassicExprsSyntax = true
			};


			while (true)
			{
				InterpreterLoop(interpreter, new ShellContext(script));
			}
		}

		private static DynValue MakeStatic(string type)
		{
			Type tt = Type.GetType(type);
			if (tt == null)
				Console.WriteLine("Type '{0}' not found.", type);
			else
				return UserData.CreateStatic(tt);

			return DynValue.Nil;
		}

		private static void InterpreterLoop(ReplInterpreter interpreter, ShellContext shellContext)
		{
			Console.Write(interpreter.ClassicPrompt + " ");

			string s = Console.ReadLine();

			if (!interpreter.HasPendingCommand && s.StartsWith("!"))
			{
				ExecuteCommand(shellContext, s.Substring(1));
				return;
			}

			try
			{
				DynValue result = interpreter.Evaluate(s);

				if (result.Type != DataType.Void)
					Console.WriteLine("{0}", result);
			}
			catch (InterpreterException ex)
			{
				Console.WriteLine("{0}", ex.DecoratedMessage ?? ex.Message);
			}
			catch (Exception ex)
			{
				Console.WriteLine("{0}", ex.Message);
			}
		}

		private static void Banner(bool lua)
		{
			Console.WriteLine(Script.GetBanner("Console"));
			Console.WriteLine();
			Console.WriteLine($"Type {(lua ? "Lua" : "WattleScript")} code to execute it or type !help to see help on commands.\n");
			Console.WriteLine("Welcome.\n");
		}

		private static void ShowUsage()
		{
			Console.WriteLine("usage: wattlescript [-L | --lua] [-h | --help | -X \"command\" | -W <dumpfile> <destfile> [--internals] [--vb] | <script>]");
		}
		
		private static bool CheckArgs(string[] args, out bool lang_lua)
		{
			lang_lua = false; //Default to Wattle
			
			//General options
			bool show_help = false;
			bool print_version = false;
			bool do_hardwire = false;
			bool do_exec = false;
			//Hardwire Options
			string classname = null;
			string namespacename = null;
			bool internals = false;
			bool useVb = false;
			bool lua = false;
			//
			var p = new OptionSet()
			{
				{"h|?|help", "show this message and exit", v => show_help = v != null},
				{"v|version", "print version and exit", v => print_version = v != null },
				{"X|exec", "runs the specified command", v => do_exec = v != null },
				{"L|lua", "run interpreter in lua mode", v => lua = v != null },
				{"W|wire", "generate code for hardwiring table", v=> do_hardwire = v != null },
				{"vb", "set hardwire generator to Visual Basic.NET", v => useVb = v != null },
				{"class", "hardwire class name", v => classname = v },
				{"namespace", "hardwire namespace", v => namespacename = v },
			};
			
			List<string> extra;
			try {
				extra = p.Parse (args);
			}
			catch (OptionException e) {
				Console.Write ("WattleScript: ");
				Console.WriteLine (e.Message);
				Console.WriteLine ("Try `WattleScript --help' for more information.");
				Environment.Exit(1);
				return false; //Exits
			}
			lang_lua = lua;

			if (print_version)
			{
				Console.WriteLine("WattleScript {0}", Script.VERSION);
				return true;
			}
			else if (show_help)
			{
				Console.WriteLine(Script.GetBanner("Console"));
				ShowUsage();
				p.WriteOptionDescriptions(Console.Out);
				return true;
			}
			else if (do_exec)
			{
				if (extra.Count > 0)
				{
					Script script = new Script(lua ? CoreModules.Preset_Complete : CoreModules.Preset_CompleteWattle);
					script.Options.Syntax = lua ? ScriptSyntax.Lua : ScriptSyntax.WattleScript;
					script.Globals["makestatic"] = (Func<string, DynValue>)(MakeStatic);	
					ExecuteCommand(new ShellContext(script), extra[0]);
				}
				else
				{
					Console.WriteLine("Incorrect syntax");
					ShowUsage();
					Environment.Exit(1);
				}
				return true;
			}
			else if (do_hardwire)
			{
				if (extra.Count >= 2)
				{
					string dumpfile = extra[0];
					string destfile = extra[1];
					HardWireCommand.Generate(useVb ? "vb" : "cs", dumpfile, destfile, internals, classname, namespacename);
				}
				else
				{
					Console.WriteLine("Incorrect syntax");
					ShowUsage();
					Environment.Exit(1);
				}
				return true;
			}
			else if(extra.Count > 0)
			{
				var script = new Script(lang_lua ? CoreModules.Preset_Default : CoreModules.Preset_DefaultWattle);
				script.Options.Syntax = lang_lua ? ScriptSyntax.Lua : ScriptSyntax.WattleScript;
				if (!File.Exists(extra[0]))
				{
					Console.Error.WriteLine("File not found: {0}", extra[0]);
					Environment.Exit(2);
				}
				script.DoFile(extra[0]);
				return true;
			}

			return false;
		}

	

		private static void ExecuteCommand(ShellContext shellContext, string cmdline)
		{
			StringBuilder cmd = new StringBuilder();
			StringBuilder args = new StringBuilder();
			StringBuilder dest = cmd;

			for (int i = 0; i < cmdline.Length; i++)
			{
				if (dest == cmd && cmdline[i] == ' ')
				{
					dest = args;
					continue;
				}

				dest.Append(cmdline[i]);
			}

			string scmd = cmd.ToString().Trim();
			string sargs = args.ToString().Trim();

			ICommand C = CommandManager.Find(scmd);

			if (C == null)
				Console.WriteLine("Invalid command '{0}'.", scmd);
			else
				C.Execute(shellContext, sargs);
		}







	}
}
