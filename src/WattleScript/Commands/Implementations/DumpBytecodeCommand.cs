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
        public string Name
        {
            get { return "dumpbc"; }
        }

        public void DisplayShortHelp()
        {
            Console.WriteLine("dumpbc <filename> - Dumps human-readable bytecode for the file");
        }

        public void DisplayLongHelp()
        {
            Console.WriteLine("printbc <filename> - Dumps human-readable bytecode for the file.\nThe destination filename will be appended with '-bytecode.txt'.");
        }

        public void Execute(ShellContext context, string p)
        {
            string targetFileName = p + "-bytecode.txt";

            Script S = new Script(CoreModules.None);

            DynValue chunk = S.LoadFile(p);

            File.WriteAllText(targetFileName, S.DumpString(chunk));

        }
    }
}