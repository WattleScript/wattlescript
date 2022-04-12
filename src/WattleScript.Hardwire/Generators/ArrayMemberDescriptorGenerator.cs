using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Hardwire.Utils;
using WattleScript.Interpreter;
using WattleScript.Interpreter.Interop;
using WattleScript.Interpreter.Interop.BasicDescriptors;
using WattleScript.Interpreter.Serialization;

namespace WattleScript.Hardwire.Generators
{
	public class ArrayMemberDescriptorGenerator : IHardwireGenerator
	{
		public string ManagedType
		{
			get { return "WattleScript.Interpreter.Interop.ArrayMemberDescriptor"; }
		}

		public CodeExpression[] Generate(string parent, Table table, HardwireCodeGenerationContext generatorContext, CodeTypeMemberCollection members)
		{
			string name = table.Get("name").String;
			string className = "AIDX_" + IdGen.Create($"{parent ?? "null"}:array:{name}");
			bool setter = table.Get("setter").Boolean;

			CodeTypeDeclaration classCode = new CodeTypeDeclaration(className);

			classCode.TypeAttributes = System.Reflection.TypeAttributes.NestedPrivate | System.Reflection.TypeAttributes.Sealed;

			classCode.BaseTypes.Add(typeof(ArrayMemberDescriptor));

			CodeConstructor ctor = new CodeConstructor();
			ctor.Attributes = MemberAttributes.Assembly;
			classCode.Members.Add(ctor);

			ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(name));
			ctor.BaseConstructorArgs.Add(new CodePrimitiveExpression(setter));
			
			DynValue vparams = table.Get("params");

			if (vparams.Type == DataType.Table)
			{
				List<HardwireParameterDescriptor> paramDescs = HardwireParameterDescriptor.LoadDescriptorsFromTable(vparams.Table);

				ctor.BaseConstructorArgs.Add(new CodeArrayCreateExpression(typeof(ParameterDescriptor), paramDescs.Select(e => e.Expression).ToArray()));
			}

			members.Add(classCode);
			return new CodeExpression[] { new CodeObjectCreateExpression(className) };
		}
	}
}
