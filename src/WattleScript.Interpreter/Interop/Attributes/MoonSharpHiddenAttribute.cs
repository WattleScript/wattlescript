using System;

namespace WattleScript.Interpreter
{
	/// <summary>
	/// Forces a class member visibility to scripts. Can be used to hide public members. Equivalent to WattleScriptVisible(false).
	/// </summary>
	[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property | AttributeTargets.Field
		| AttributeTargets.Constructor | AttributeTargets.Event, Inherited = true, AllowMultiple = false)]
	public sealed class WattleScriptHiddenAttribute : Attribute
	{
	}
}
