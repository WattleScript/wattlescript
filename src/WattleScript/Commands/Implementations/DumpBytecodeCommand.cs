using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Commands.Implementations
{
    class DumpBytecodeCommand : ICommand
    {
        public string Name => "dumpbc";

        public void DisplayShortHelp(Script context)
        {
            Console.WriteLine("dumpbc <filename> - Dumps human-readable bytecode for the file");
        }

        public void DisplayLongHelp(Script context)
        {
            Console.WriteLine("printbc <filename> - Dumps human-readable bytecode for the file.\nThe destination filename will be appended with '-bytecode.txt'.");
        }

        public void Execute(Script context, string p)
        {
            string targetFileName = p + "-bytecode.txt";

            Script s = new Script(CoreModules.None);
            DynValue chunk = s.LoadFile(p);

            File.WriteAllText(targetFileName, s.DumpString(chunk));
        }
    }
}