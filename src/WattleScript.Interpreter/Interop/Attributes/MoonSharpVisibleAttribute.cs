using System;

namespace WattleScript.Interpreter.Interop
{
	/// <summary>
	/// Forces a class member visibility to scripts. Can be used to hide public members or to expose non-public ones.
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field
		| AttributeTargets.Constructor | AttributeTargets.Event, Inherited = true, AllowMultiple = false)]
	public sealed class WattleScriptVisibleAttribute : Attribute
	{
		/// <summary>
		/// Gets a value indicating whether this <see cref="WattleScriptVisibleAttribute"/> is set to "visible".
		/// </summary>
		public bool Visible { get; private set; }

		/// <summary>
		/// Initializes a new instance of the <see cref="WattleScriptVisibleAttribute"/> class.
		/// </summary>
		/// <param name="visible">if set to true the member will be exposed to scripts, if false the member will be hidden.</param>
		public WattleScriptVisibleAttribute(bool visible)
		{
			Visible = visible;
		}
	}
}
