using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Commands.Implementations
{
	class CompileCommand : ICommand
	{
		public string Name => "compile";

		public void DisplayShortHelp(Script context)
		{
			Console.WriteLine("compile <filename> - Compiles the file in a binary format");
		}

		public void DisplayLongHelp(Script context)
		{
			Console.WriteLine("compile <filename> - Compiles the file in a binary format.\nThe destination filename will be appended with '-compiled'.");
		}

		public void Execute(Script context, string p)
		{
			string targetFileName = p + "-compiled";

			Script s = new Script(CoreModules.None);
			DynValue chunk = s.LoadFile(p);

			using Stream stream = new FileStream(targetFileName, FileMode.Create, FileAccess.Write);
			s.Dump(chunk, stream);
		}
	}
}
