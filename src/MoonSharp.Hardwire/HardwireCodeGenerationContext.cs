﻿using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using MoonSharp.Hardwire.Languages;
using MoonSharp.Interpreter;

namespace MoonSharp.Hardwire
{
	/// <summary>
	/// The context under which code is generated.
	/// </summary>
	public sealed class HardwireCodeGenerationContext
	{
		/// <summary>
		/// Gets the compile unit.
		/// </summary>
		internal CodeCompileUnit CompileUnit { get; private set; }
		
		CodeStatementCollection m_InitStatements;
		CodeTypeDeclaration m_KickstarterClass;
		CodeNamespace m_Namespace;
		ICodeGenerationLogger m_Logger;

		Stack<string> m_NestStack = new Stack<string>();

		public HardwireCodeGenerationLanguage TargetLanguage { get; private set; }

		public bool AllowInternals { get; internal set; }


		internal HardwireCodeGenerationContext(string namespaceName, string entryClassName, ICodeGenerationLogger logger,
			HardwireCodeGenerationLanguage language)
		{
			TargetLanguage = language;

			m_Logger = logger;

			CompileUnit = new CodeCompileUnit();


			m_Namespace = new CodeNamespace(namespaceName);
			CompileUnit.Namespaces.Add(m_Namespace);

			Comment("----------------------------------------------------------");
			Comment("Compatible with MoonSharp v.{0} or equivalent", Script.VERSION);
			Comment("----------------------------------------------------------");

			string[] extraComments = language.GetInitialComment();

			if (extraComments != null)
			{
				foreach(string str in extraComments)
					Comment("{0}", str);
			
				Comment("----------------------------------------------------------");
			}
			
			Comment("----------------------------------------------------------");

			GenerateKickstarter(entryClassName);
		}

		/// <summary>
		/// Generates the code from the specified dump table
		/// </summary>
		/// <param name="table">The table.</param>
		internal void GenerateCode(Table table)
		{
			try
			{
				DispatchTablePairs(null, table, m_KickstarterClass.Members,
					exp => m_InitStatements.Add(new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(typeof(UserData)), "RegisterType", exp)));
			}
			catch (Exception ex)
			{
				m_Logger.LogError(string.Format("Internal error, code generation aborted : {0}", ex));
			}
		}

		/// <summary>
		/// Used by generators to dispatch a table of types
		/// </summary>
		/// <param name="parent">Parent name (can be null)</param>
		/// <param name="table">The table.</param>
		/// <param name="members">The members.</param>
		/// <param name="action">The action to be performed, or null.</param>
		public void DispatchTablePairs(string parent, Table table, CodeTypeMemberCollection members, Action<string, CodeExpression> action = null)
		{
			foreach (var pair in table.Pairs.OrderBy(x => x.Key.ToString(), StringComparer.Ordinal))
			{
				var key = pair.Key;
				var value = pair.Value;

				if (value.Type == DataType.Table && value.Table.Get("error").IsNotNil())
				{
					value = value.Table.Get("error");
				}


				if (value.Type == DataType.Table)
				{
					if (value.Table.Get("skip").IsNotNil())
						continue;

					if (!IsVisibilityAccepted(value.Table))
					{
						Warning("Type/Member '{0}' cannot be hardwired because its visibility is '{1}' (stack = {2}).", key.String ?? "(null)", value.Table.Get("visibility").String, GetStackTrace());

						continue;
					}

					var exp = DispatchTable(parent, key.String, value.Table, members);

					if (action != null && exp != null)
						foreach (var e in exp)
							action(key.String, e);
				}
				else
				{
					if (value.Type == DataType.String)
					{
						Error("Type/Member '{0}' cannot be hardwired, error = '{1}' (stack = {2}).", key.String ?? "(null)", value.String ?? "(null)", GetStackTrace());
					}
					else
					{
						Error("Type/Member '{0}' cannot be hardwired (stack = {1}).", key.String ?? "(null)", GetStackTrace());
					}
				}
			}
		}

		public string GetStackTrace()
		{
			return string.Join(" - ", m_NestStack.ToArray());
		}

		/// <summary>
		/// Used by generators to dispatch a table of types
		/// </summary>
		/// <param name="table">The table.</param>
		/// <param name="members">The members.</param>
		/// <param name="action">The action to be performed, or null.</param>
		public void DispatchTablePairs(string parent, Table table, CodeTypeMemberCollection members, Action<CodeExpression> action)
		{
			DispatchTablePairs(parent, table, members, (_, e) => action(e));
		}


		/// <summary>
		/// Used by generators to dispatch a single table
		/// </summary>
		/// <param name="key">The key.</param>
		/// <param name="table">The table.</param>
		/// <param name="members">The members.</param>
		/// <returns></returns>
		/// <exception cref="System.ArgumentException">table cannot be dispatched as it has no class or class of invalid type.</exception>
		public CodeExpression[] DispatchTable(string parent, string key, Table table, CodeTypeMemberCollection members)
		{
			DynValue d = table.Get("class");
			if (d.Type != DataType.String)
				throw new ArgumentException("table cannot be dispatched as it has no class or class of invalid type.");

			//m_NestStack.Push(string.Format("{0}[{1}]", key, d.String));

			m_NestStack.Push((key ?? d.String) ?? "(null)");

			table.Set("$key", DynValue.NewString(key));

			var gen = HardwireGeneratorRegistry.GetGenerator(d.String);
			var result = gen.Generate(parent, table, this, members);

			m_NestStack.Pop();

			return result;
		}

		/// <summary>
		/// Adds a comment to the source code head
		/// </summary>
		/// <param name="format">The format.</param>
		/// <param name="args">The arguments.</param>
		public void Comment(string format, params object[] args)
		{
			string str = string.Format(format, args);
			m_Namespace.Comments.Add(new CodeCommentStatement(str));
		}

		/// <summary>
		/// Reports a code generation error message
		/// </summary>
		/// <param name="format">The format.</param>
		/// <param name="args">The arguments.</param>
		public void Error(string format, params object[] args)
		{
			string str = string.Format(format, args);
			m_Namespace.Comments.Add(new CodeCommentStatement("ERROR : " + str));
			m_Logger.LogError(str);
		}

		/// <summary>
		/// Reports a code generation warning message
		/// </summary>
		/// <param name="format">The format.</param>
		/// <param name="args">The arguments.</param>
		public void Warning(string format, params object[] args)
		{
			string str = string.Format(format, args);
			m_Namespace.Comments.Add(new CodeCommentStatement("WARNING : " + str));
			m_Logger.LogWarning(str);
		}

		/// <summary>
		/// Reports a code generation warning message
		/// </summary>
		/// <param name="format">The format.</param>
		/// <param name="args">The arguments.</param>
		public void Minor(string format, params object[] args)
		{
			string str = string.Format(format, args);
			m_Namespace.Comments.Add(new CodeCommentStatement("Minor : " + str));
			m_Logger.LogMinor(str);
		}

		public bool IsVisibilityAccepted(Table t)
		{
			DynValue dv = t.Get("visibility");

			if (dv.Type != DataType.String)
				return true;

			if (dv.String == "public")
				return true;

			if (dv.String == "internal" || dv.String == "protected-internal")
				return AllowInternals;

			return false;
		}




		private void GenerateKickstarter(string className)
		{
			var cl = new CodeTypeDeclaration(className);
			cl.TypeAttributes = System.Reflection.TypeAttributes.Public |
				System.Reflection.TypeAttributes.Abstract;

			m_Namespace.Types.Add(cl);

			CodeConstructor ctor = new CodeConstructor();
			ctor.Attributes = MemberAttributes.Private;
			cl.Members.Add(ctor);

			CodeMemberMethod m = new CodeMemberMethod();
			m.Name = "Initialize";
			m.Attributes = MemberAttributes.Static | MemberAttributes.Public;

			cl.Members.Add(m);

			this.m_InitStatements = m.Statements;
			this.m_KickstarterClass = cl;
		}




	}
}
