using System;

namespace WattleScript.Interpreter
{

	/// <summary>
	/// Marks a property as a configruation property
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, Inherited = true, AllowMultiple = true)]
	public sealed class WattleScriptPropertyAttribute : Attribute
	{
		/// <summary>
		/// The metamethod name (like '__div', '__ipairs', etc.)
		/// </summary>
		public string Name { get; private set; }


		/// <summary>
		/// Initializes a new instance of the <see cref="WattleScriptPropertyAttribute"/> class.
		/// </summary>
		public WattleScriptPropertyAttribute()
		{

		}

		/// <summary>
		/// Initializes a new instance of the <see cref="WattleScriptPropertyAttribute"/> class.
		/// </summary>
		/// <param name="name">The name for this property</param>
		public WattleScriptPropertyAttribute(string name)
		{
			Name = name;
		}
	}

}
