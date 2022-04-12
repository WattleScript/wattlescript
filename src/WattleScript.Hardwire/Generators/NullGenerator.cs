using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;

namespace WattleScript.Hardwire.Generators
{
	internal class NullGenerator : IHardwireGenerator
	{
		public NullGenerator()
		{
			ManagedType = "";
		}

		public NullGenerator(string type)
		{
			ManagedType = type;
		}

		public string ManagedType
		{
			get;
			private set;
		}

		public CodeExpression[] Generate(string parent, Table table, HardwireCodeGenerationContext generator, CodeTypeMemberCollection members)
		{
			generator.Error("Missing code generator for '{0}'.", ManagedType);

			return new CodeExpression[0];
		}
	}
}
