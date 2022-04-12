using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;
using WattleScript.Interpreter.Interop;
using WattleScript.Interpreter.Interop.BasicDescriptors;
using WattleScript.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors;

namespace WattleScript.Hardwire.Generators
{
	class PropertyMemberDescriptorGenerator : AssignableMemberDescriptorGeneratorBase
	{
		public override string ManagedType
		{
			get { return "WattleScript.Interpreter.Interop.PropertyMemberDescriptor"; }
		}

		protected override CodeExpression GetMemberAccessExpression(CodeExpression thisObj, string name)
		{
			return new CodePropertyReferenceExpression(thisObj, name);
		}

		protected override string GetPrefix()
		{
			return "PROP";
		}
	}
}
