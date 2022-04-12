using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WattleScript.Interpreter;
using WattleScript.Interpreter.Interop;
using WattleScript.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors;

namespace WattleScript.Hardwire.Generators
{
	public class StandardUserDataDescriptorGenerator : IHardwireGenerator
	{
		public string ManagedType
		{
			get { return "WattleScript.Interpreter.Interop.StandardUserDataDescriptor"; }
		}

		public CodeExpression[] Generate(string parent, Table table, HardwireCodeGenerationContext generator,
			CodeTypeMemberCollection members)
		{
			string type = (string)table["$key"];
			string className = "TYPE_" + IdGen.Create($"TYPE${type}");

			CodeTypeDeclaration classCode = new CodeTypeDeclaration(className);

			classCode.Comments.Add(new CodeCommentStatement("Descriptor of " + type));


			classCode.StartDirectives.Add(new CodeRegionDirective(CodeRegionMode.Start, "Descriptor of " + type));
			
			classCode.EndDirectives.Add(new CodeRegionDirective(CodeRegionMode.End, string.Empty));


			classCode.TypeAttributes = System.Reflection.TypeAttributes.NestedPrivate | System.Reflection.TypeAttributes.Sealed;
			
			classCode.BaseTypes.Add(typeof(HardwiredUserDataDescriptor));

			CodeConstructor ctor = new CodeConstructor();
			ctor.Attributes = MemberAttributes.Assembly;
			ctor.BaseConstructorArgs.Add(new CodeTypeOfExpression(type));

			classCode.Members.Add(ctor);

			generator.DispatchTablePairs(type, table.Get("members").Table,
				classCode.Members, (key, exp) =>
				{
					var mname = new CodePrimitiveExpression(key);

					ctor.Statements.Add(new CodeMethodInvokeExpression(
						new CodeThisReferenceExpression(), "AddMember", mname, exp));
				});

			generator.DispatchTablePairs(type, table.Get("metamembers").Table,
				classCode.Members, (key, exp) =>
				{
					var mname = new CodePrimitiveExpression(key);
					
					ctor.Statements.Add(new CodeMethodInvokeExpression(
						new CodeThisReferenceExpression(), "AddMetaMember", mname, exp));
				});

			members.Add(classCode);

			return new CodeExpression[] {
					new CodeObjectCreateExpression(className)
			};
		}



	}
}
