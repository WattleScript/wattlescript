﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript
{
	public class ShellContext
	{
		public Script Script { get; private set; }

		public ShellContext(Script script)
		{
			this.Script = script;
		}
	}
}