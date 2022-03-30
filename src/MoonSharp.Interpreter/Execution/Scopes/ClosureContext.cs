using System;
using System.Collections.Generic;
using System.Linq;

namespace MoonSharp.Interpreter.Execution
{
	/// <summary>
	/// The scope of a closure (container of upvalues)
	/// </summary>
	internal class ClosureContext : List<Upvalue>
	{
		/// <summary>
		/// Gets the symbols.
		/// </summary>
		public string[] Symbols { get; private set; }

		internal ClosureContext(SymbolRef[] symbols, IEnumerable<Upvalue> values)
		{
			Symbols = symbols.Select(s => s.i_Name).ToArray();
			AddRange(values);
		}

		internal ClosureContext()
		{
			Symbols = Array.Empty<string>();
		}

	}
}
