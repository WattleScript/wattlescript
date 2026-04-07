using WattleScript.Interpreter.Interop.BasicDescriptors;

namespace WattleScript.Interpreter.Interop
{
	/// <summary>
	/// Class providing a simple descriptor for constant DynValues in userdata
	/// </summary>
	public class DynValueMemberDescriptor : IMemberDescriptor
	{
		private DynValue m_Value;

		/// <summary>
		/// Initializes a new instance of the <see cref="DynValueMemberDescriptor" /> class.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="serializedTableValue">A string containing a table whose first member is the dynvalue to be deserialized (convoluted...).</param>
		protected DynValueMemberDescriptor(string name, string serializedTableValue)
		{
			Script s = new Script();
			var exp = s.CreateDynamicExpression(serializedTableValue);
			DynValue val = exp.Evaluate(null);

			m_Value = val.Table.Get(1);
			Name = name;
			MemberAccess = MemberDescriptorAccess.CanRead;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DynValueMemberDescriptor" /> class.
		/// </summary>
		/// <param name="name">The name.</param>
		protected DynValueMemberDescriptor(string name)
		{
			MemberAccess = MemberDescriptorAccess.CanRead;
			m_Value = DynValue.Nil;
			Name = name;
		}


		/// <summary>
		/// Initializes a new instance of the <see cref="DynValueMemberDescriptor"/> class.
		/// </summary>
		/// <param name="name">The name.</param>
		/// <param name="value">The value.</param>
		public DynValueMemberDescriptor(string name, DynValue value)
		{
			m_Value = value;
			Name = name;

			if (value.Type == DataType.ClrFunction)
				MemberAccess = MemberDescriptorAccess.CanRead | MemberDescriptorAccess.CanExecute;
			else
				MemberAccess = MemberDescriptorAccess.CanRead;
		}

		/// <summary>
		/// Gets a value indicating whether the described member is static.
		/// </summary>
		public bool IsStatic { get { return true; } }
		/// <summary>
		/// Gets the name of the member
		/// </summary>
		public string Name { get; private set;  }
		/// <summary>
		/// Gets the types of access supported by this member
		/// </summary>
		public MemberDescriptorAccess MemberAccess { get; private set;  }


		/// <summary>
		/// Gets the value wrapped by this descriptor
		/// </summary>
		public virtual DynValue Value 
		{
			get
			{
				return m_Value;
			}
		}

		/// <summary>
		/// Gets the value of this member as a <see cref="DynValue" /> to be exposed to scripts.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="obj">The object owning this member, or null if static.</param>
		/// <returns>
		/// The value of this member as a <see cref="DynValue" />.
		/// </returns>
		public DynValue GetValue(Script script, object obj)
		{
			return Value;
		}

		/// <summary>
		/// Sets the value of this member from a <see cref="DynValue" />.
		/// </summary>
		/// <param name="script">The script.</param>
		/// <param name="obj">The object owning this member, or null if static.</param>
		/// <param name="value">The value to be set.</param>
		/// <exception cref="ScriptRuntimeException">userdata '{0}' cannot be written to.</exception>
		public void SetValue(Script script, object obj, DynValue value)
		{
			throw new ScriptRuntimeException("userdata '{0}' cannot be written to.", this.Name);
		}
	}
}
