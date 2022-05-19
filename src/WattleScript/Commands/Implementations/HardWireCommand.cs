using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WattleScript.Hardwire;
using WattleScript.Hardwire.Languages;
using WattleScript.Interpreter;

namespace WattleScript.Commands.Implementations
{
	class HardWireCommand : ICommand
	{
		class ConsoleLogger : ICodeGenerationLogger
		{
			public int Errors;
			public int Warnings;

			public void LogError(string message)
			{
				Console.WriteLine("[EE] - " + message);
				++Errors;
			}

			public void LogWarning(string message)
			{
				Console.WriteLine("[ww] - " + message);
				++Warnings;
			}

			public void LogMinor(string message)
			{
				Console.WriteLine("[ii] - " + message);
			}
		}

		public string Name => "hardwire";

		public void DisplayShortHelp(Script context)
		{
			Console.WriteLine("hardwire - Creates hardwire descriptors from a dump table (interactive). ");
		}

		public void DisplayLongHelp(Script context)
		{
			Console.WriteLine("hardwire - Creates hardwire descriptors from a dump table (interactive). ");
			Console.WriteLine();
		}

		public void Execute(Script context, string argument)
		{
			Console.WriteLine("At any question, type #quit to abort.");
			Console.WriteLine();

			string language = AskQuestion("Language, cs or vb ? [cs] : ",
				"cs", s => s == "cs" || s == "vb", "Must be 'cs' or 'vb'.");

			if (language == null)
				return;

			string luafile = AskQuestion("Lua dump table file: ",
				"", File.Exists, "File does not exists.");

			if (luafile == null)
				return;

			string destfile = AskQuestion("Destination file: ",
				"", s => true, "");

			if (destfile == null)
				return;

			string allowinternals = AskQuestion("Allow internals y/n ? [y]: ",
				"y", s => s == "y" || s == "n", "");

			if (allowinternals == null)
				return;

			string namespaceName = AskQuestion("Namespace ? [HardwiredClasses]: ",
				"HardwiredClasses", IsValidIdentifier, "Not a valid identifier.");

			if (namespaceName == null)
				return;

			string className = AskQuestion("Class ? [HardwireTypes]: ",
				"HardwireTypes", IsValidIdentifier, "Not a valid identifier.");

			if (className == null)
				return;

			Generate(language, luafile, destfile, allowinternals == "y", className, namespaceName);
		}

		private static bool IsValidIdentifier(string s)
		{
			if (string.IsNullOrEmpty(s))
				return false;

			foreach (char c in s)
			{
				if (c != '_' && !char.IsLetterOrDigit(c))
					return false;
			}

			return !char.IsDigit(s[0]);
		}

		public static void Generate(string language, string luafile, string destfile, bool allowInternals, string classname, string namespacename)
		{
			var logger = new ConsoleLogger();
			try
			{
				Script s = new Script(CoreModules.None);
				var eee = s.CreateDynamicExpression(File.ReadAllText(luafile));

				Table t = eee.Evaluate(null).Table;

				HardwireGeneratorRegistry.RegisterPredefined();

				HardwireGenerator hcg = new HardwireGenerator(namespacename ?? "HardwiredClasses", classname ?? "HardwireTypes", logger,
					language == "vb" ? HardwireCodeGenerationLanguage.VB : HardwireCodeGenerationLanguage.CSharp)
				{
					AllowInternals = allowInternals
				};

				hcg.BuildCodeModel(t);

				string code = hcg.GenerateSourceCode();

				File.WriteAllText(destfile, code);
			}
			catch (Exception ex)
			{
				Console.WriteLine("Internal error : {0}", ex.Message);
			}

			Console.WriteLine();
			Console.WriteLine("done: {0} errors, {1} warnings.", logger.Errors, logger.Warnings);
		}

		string AskQuestion(string prompt, string defval, Func<string, bool> validator, string errormsg)
		{
			while (true)
			{
				Console.Write(prompt);
				string inp = Console.ReadLine();

				switch (inp)
				{
					case "#quit":
						return null;
					case "":
						inp = defval;
						break;
				}

				if (validator(inp))
					return inp;

				Console.WriteLine(errormsg);
			}
		}
	}
}
