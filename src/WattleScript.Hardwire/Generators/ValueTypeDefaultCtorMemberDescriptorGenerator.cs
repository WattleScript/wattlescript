using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;
using WattleScript.Interpreter.Interop.BasicDescriptors;
using WattleScript.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors;

namespace WattleScript.Hardwire.Generators
{
	class ValueTypeDefaultCtorMemberDescriptorGenerator : IHardwireGenerator
	{
		public string ManagedType
		{
			get { return "WattleScript.Interpreter.Interop.ValueTypeDefaultCtorMemberDescriptor"; }
		}

		public CodeExpression[] Generate(string parent, Table table, HardwireCodeGenerationContext generator, CodeTypeMemberCollection members)
		{
			MethodMemberDescriptorGenerator mgen = new MethodMemberDescriptorGenerator("VTDC");

			Table mt = new Table(null);

			mt["params"] = new Table(null);
			mt["name"] = "__new";
			mt["type"] = table["type"];
			mt["ctor"] = true;
			mt["extension"] = false;
			mt["decltype"] = table["type"];
			mt["ret"] = table["type"];
			mt["special"] = false;


			return mgen.Generate(parent, mt, generator, members);
		}
	}
}
