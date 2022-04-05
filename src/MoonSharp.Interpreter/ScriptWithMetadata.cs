using System.Collections.Generic;

namespace MoonSharp.Interpreter
{
    public class ScriptWithMetadata : Script
    {
        public List<string> Usings { get; set; } = new List<string>();

        public ScriptWithMetadata(CoreModules coreModules) : base(coreModules)
        {
        }
    }
}