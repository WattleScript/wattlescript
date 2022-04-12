using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using WattleScript.Interpreter.CoreLib;
using WattleScript.Interpreter.Execution;
using WattleScript.Interpreter.Loaders;
using WattleScript.Interpreter.Platforms;
using NUnit.Framework;

namespace WattleScript.Interpreter.Tests
{
#if !EMBEDTEST
	class TestsScriptLoader : ScriptLoaderBase
	{
		public override bool ScriptFileExists(string name)
		{
			return File.Exists(name);
		}

		public override object LoadFile(string file, Table globalContext)
		{
			return new FileStream(file, FileMode.Open, FileAccess.Read);
		}
	}
#endif

	public class TapRunner
	{
		string m_File;

		private bool fail = false;
		private string firstFail = null;
		private StringBuilder output = new StringBuilder();
		public void Print(string str)
		{
			if (str.Trim().StartsWith("not ok"))
			{
				fail = true;
				firstFail = string.Format("TAP fail ({0}) : {1}", m_File, str);
			}
			output.AppendLine(str);
		}

		public TapRunner(string filename)
		{
			m_File = filename;
		}

		public void Run()
		{
			Script S = new Script();

			S.Options.DebugPrint = Print;

			S.Options.UseLuaErrorLocations = true;

#if PCL
	#if EMBEDTEST
			S.Options.ScriptLoader = new EmbeddedResourcesScriptLoader(Assembly.GetExecutingAssembly());
	#else
			S.Options.ScriptLoader = new TestsScriptLoader();
	#endif
#endif

			S.Globals.Set("arg", DynValue.NewTable(S));

			((ScriptLoaderBase)S.Options.ScriptLoader).ModulePaths = new string[] { "TestMore/Modules/?", "TestMore/Modules/?.lua" };

			S.DoFile(m_File);

			if (fail)
			{
				Assert.Fail(firstFail + "\n" + output.ToString());
			}
			
		}

		public static void Run(string filename)
		{
			TapRunner t = new TapRunner(filename);
			t.Run();
		}



	}
}
