﻿// Disable warnings about XML documentation
#pragma warning disable 1591


namespace WattleScript.Interpreter.CoreLib
{
	/// <summary>
	/// Class implementing dynamic expression evaluations at runtime (not a part of standard Lua lib).
	/// </summary>
	[WattleScriptModule(Namespace = "dynamic")]
	public class DynamicModule
	{
		private class DynamicExprWrapper
		{
			public DynamicExpression Expr;
		}

		public static void WattleScriptInit(Table globalTable, Table stringTable)
		{
			UserData.RegisterType<DynamicExprWrapper>(InteropAccessMode.HideMembers);
		}

		[WattleScriptModuleMethod]
		public static DynValue eval(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			try
			{
				if (args[0].Type == DataType.UserData)
				{
					UserData ud = args[0].UserData;
					if (ud.Object is DynamicExprWrapper)
					{
						return ((DynamicExprWrapper)ud.Object).Expr.Evaluate(executionContext);
					}
					else
					{
						throw ScriptRuntimeException.BadArgument(0, "dynamic.eval", "A userdata was passed, but was not a previously prepared expression.");
					}
				}
				else
				{
					DynValue vs = args.AsType(0, "dynamic.eval", DataType.String, false);
					DynamicExpression expr = executionContext.GetScript().CreateDynamicExpression(vs.String);
					return expr.Evaluate(executionContext);
				}
			}
			catch (SyntaxErrorException ex)
			{
				throw new ScriptRuntimeException(ex);
			}
		}

		[WattleScriptModuleMethod]
		public static DynValue prepare(ScriptExecutionContext executionContext, CallbackArguments args)
		{
			try
			{
				DynValue vs = args.AsType(0, "dynamic.prepare", DataType.String, false);
				DynamicExpression expr = executionContext.GetScript().CreateDynamicExpression(vs.String);
				return UserData.Create(new DynamicExprWrapper() { Expr = expr });
			}
			catch (SyntaxErrorException ex)
			{
				throw new ScriptRuntimeException(ex);
			}
		}


	}

}
