using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using WattleScript.Commands;
using WattleScript.Commands.Implementations;
using WattleScript.Interpreter;
using WattleScript.Interpreter.REPL;

namespace WattleScript
{
	class Program
	{
		[STAThread]
		static void Main(string[] args)
		{
			CommandManager.Initialize();

			Script.DefaultOptions.ScriptLoader = new ReplInterpreterScriptLoader();

			if (CheckArgs(args, out ScriptSyntax syntax))
				return;

			Banner(syntax);
			
			Script script = new Script(syntax == ScriptSyntax.Lua ? CoreModules.Preset_Complete : CoreModules.Preset_CompleteWattle)
			{
				Options = { Syntax = syntax },
				Globals =
				{
					["makestatic"] = (Func<string, DynValue>) MakeStatic
				}
			};

			ReplInterpreter interpreter = new ReplInterpreter(script)
			{
				HandleDynamicExprs = true,
				HandleClassicExprsSyntax = true
			};

			while (true)
			{
				InterpreterLoop(interpreter, script);
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

		private static void InterpreterLoop(ReplInterpreter interpreter, Script script)
		{
			Console.Write(interpreter.ClassicPrompt + " ");

			string s = Console.ReadLine();

			if (s != null && !interpreter.HasPendingCommand && s.StartsWith("!"))
			{
				ExecuteCommand(script, s[1..]);
				return;
			}

			try
			{
				DynValue result = interpreter.Evaluate(s);

				if (result.IsNotVoid())
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

		private static void Banner(ScriptSyntax syntax)
		{
			Console.WriteLine(Script.GetBanner("Console"));
			Console.WriteLine();
			Console.WriteLine($"Type {syntax} code to execute it or type !help to see help on commands.\n");
			Console.WriteLine("Welcome.\n");
		}

		private static void ShowUsage()
		{
			Console.WriteLine("usage: wattlescript [-L | --lua] [-h | --help | -X \"command\" | -W <dumpfile> <destfile> [--class=name] [--namespace=name] [--internals] [--vb] | <script>]");
		}
		
		private static bool CheckArgs(string[] args, out ScriptSyntax syntax)
		{
			syntax = ScriptSyntax.Wattle;
			bool internals = false;
			
			// General options
			bool show_help = false;
			bool print_version = false;
			bool do_hardwire = false;
			bool do_exec = false;
			
			// Hardwire Options
			string classname = null;
			string namespacename = null;
			bool useVb = false;
			bool lua = false;

			var p = new OptionSet()
			{
				{"h|?|help", "show this message and exit", v => show_help = v != null},
				{"v|version", "print version and exit", v => print_version = v != null },
				{"X|exec", "runs the specified command", v => do_exec = v != null },
				{"L|lua", "run interpreter in lua mode", v => lua = v != null },
				{"W|wire", "generate code for hardwiring table", v=> do_hardwire = v != null },
				{"vb", "set hardwire generator to Visual Basic.NET", v => useVb = v != null },
				{"internals", "enable internals for hardwire generator", v => internals = v != null },
				{"class=", "hardwire class name", v => classname = v },
				{"namespace=", "hardwire namespace", v => namespacename = v },
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
				return false;
			}

			if (lua)
			{
				syntax = ScriptSyntax.Lua;
			}

			if (print_version)
			{
				Console.WriteLine("WattleScript {0}", Script.VERSION);
				return true;
			}
			
			if (show_help)
			{
				Console.WriteLine(Script.GetBanner("Console"));
				ShowUsage();
				p.WriteOptionDescriptions(Console.Out);
				return true;
			}
			
			if (do_exec)
			{
				if (extra.Count > 0)
				{
					Script script = new Script(lua ? CoreModules.Preset_Complete : CoreModules.Preset_CompleteWattle)
					{
						Options = { Syntax = lua ? ScriptSyntax.Lua : ScriptSyntax.Wattle },
						Globals =
						{
							["makestatic"] = (Func<string, DynValue>) MakeStatic
						}
					};
					ExecuteCommand(script, extra[0]);
				}
				else
				{
					Console.WriteLine("Incorrect syntax");
					ShowUsage();
					Environment.Exit(1);
				}
				return true;
			}
			
			if (do_hardwire)
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
			
			if (extra.Count > 0)
			{
				var script = new Script(syntax == ScriptSyntax.Lua ? CoreModules.Preset_Default : CoreModules.Preset_DefaultWattle);
				script.Options.Syntax = syntax;
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

	

		private static void ExecuteCommand(Script script, string cmdline)
		{
			StringBuilder cmd = new StringBuilder();
			StringBuilder args = new StringBuilder();
			StringBuilder dest = cmd;

			foreach (char t in cmdline)
			{
				if (dest == cmd && t == ' ')
				{
					dest = args;
					continue;
				}

				dest.Append(t);
			}

			string scmd = cmd.ToString().Trim();
			string sargs = args.ToString().Trim();

			ICommand c = CommandManager.Find(scmd);

			if (c == null)
				Console.WriteLine("Invalid command '{0}'.", scmd);
			else
				c.Execute(script, sargs);
		}
	}
}
