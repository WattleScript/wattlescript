using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;
using WattleScript.Interpreter.Interop;
using WattleScript.Interpreter.Interop.BasicDescriptors;

namespace WattleScript.Hardwire.Generators
{
	class OverloadedMethodMemberDescriptorGenerator : IHardwireGenerator
	{
		public string ManagedType
		{
			get { return "WattleScript.Interpreter.Interop.OverloadedMethodMemberDescriptor"; }
		}

		public CodeExpression[] Generate(string parent, Table table, HardwireCodeGenerationContext generator,
			CodeTypeMemberCollection members)
		{
			List<CodeExpression> initializers = new List<CodeExpression>();

			generator.DispatchTablePairs(parent + (table["name"] as string), table.Get("overloads").Table, members, exp =>
			{
				initializers.Add(exp);
			});

			var name = new CodePrimitiveExpression((table["name"] as string));
			var type = new CodeTypeOfExpression(table["decltype"] as string);

			var array = new CodeArrayCreateExpression(typeof(IOverloadableMemberDescriptor), initializers.ToArray());

			return new CodeExpression[] {
					new CodeObjectCreateExpression(typeof(OverloadedMethodMemberDescriptor), name, type, array)
			};
		}
	}
}
