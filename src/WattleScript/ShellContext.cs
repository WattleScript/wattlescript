using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript
{
	public class ShellContext
	{
		public Script Script { get; private set; }
		public bool Lua;

		public ShellContext(Script script)
		{
			this.Script = new Script();
		}
	}
}
