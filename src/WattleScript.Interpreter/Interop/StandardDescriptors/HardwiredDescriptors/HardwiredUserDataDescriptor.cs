using System;
using WattleScript.Interpreter.Interop.BasicDescriptors;

namespace WattleScript.Interpreter.Interop.StandardDescriptors.HardwiredDescriptors
{
	public abstract class HardwiredUserDataDescriptor : DispatchingUserDataDescriptor
	{
		protected HardwiredUserDataDescriptor(Type T) :
			base(T, "::hardwired::" + T.Name)
		{

		}

	}
}
